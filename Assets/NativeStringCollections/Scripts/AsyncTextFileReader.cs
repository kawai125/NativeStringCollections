using System;
using System.Text;

using Unity.Jobs;
using Unity.Collections;

using NativeStringCollections.Utility;
using NativeStringCollections.Impl;

namespace NativeStringCollections
{
    /// <summary>
    /// async file reader for single file and single user
    /// </summary>
    /// <typeparam name="T">parser class</typeparam>
    public class AsyncTextFileReader<T> : IDisposable
        where T : class, ITextFileParser, new()
    {
        public T Data;
        public string Path;
        public Encoding Encoding;
        public int BlockSize
        {
            get { return _blockSize; }
            set { if (value >= Define.MinDecodeBlock) _blockSize = value; }
        }
        public unsafe ReadJobState JobState { get { return _state.Target->JobState; } }

        private int _blockSize;
        private ParseJob<T> _parser;
        private PtrHandle<ReadStateImpl> _state;
        private JobHandle _job_handle;

        private bool _allocated;

        private AsyncTextFileReader() { }
        public AsyncTextFileReader(Allocator alloc)
        {
            this.Init("", alloc, System.Text.Encoding.UTF8);
        }
        public AsyncTextFileReader(Allocator alloc, Encoding encoding)
        {
            this.Init("", alloc, encoding);
        }
        public AsyncTextFileReader(string path, Allocator alloc)
        {
            this.Init(path, alloc, System.Text.Encoding.UTF8);
        }
        public AsyncTextFileReader(string path, Allocator alloc, Encoding encoding)
        {
            this.Init(path, alloc, encoding);
        }
        private unsafe void Init(string path, Allocator alloc, Encoding encoding)
        {
            Data = new T();
            Data.Init();

            Path = path;
            _blockSize = Define.DefaultDecodeBlock;
            Encoding = encoding;

            _parser = new ParseJob<T>(alloc);
            _state = new PtrHandle<ReadStateImpl>(alloc);
            _state.Target->Clear();

            _allocated = true;
        }
        ~AsyncTextFileReader()
        {
            this.Dispose();

            GC.SuppressFinalize(Path);
            GC.SuppressFinalize(Encoding);
            GC.SuppressFinalize(this);
        }
        public void Dispose()
        {
            if (_allocated)
            {
                _parser.Dispose();
                _state.Dispose();

                _allocated = false;
            }
        }

        /// <summary>
        /// Use or not BurstCompile for parsing lines internally (default = true).
        /// </summary>
        public bool EnableBurst
        {
            get { return _parser.EnableBurst; }
            set { _parser.EnableBurst = value; }
        }

        public unsafe ReadState GetState { get { return _state.Target->GetState(); } }
        public bool IsCompleted
        {
            get { return GetState.JobState == ReadJobState.Completed; }
        }
        public bool IsStandby
        {
            get
            {
                var job_stat = GetState.JobState;
                return (job_stat == ReadJobState.Completed || job_stat == ReadJobState.UnLoaded);
            }
        }

        public JobHandle LoadFile() { return this.LoadFile(Path); }
        public unsafe JobHandle LoadFile(string path)
        {
            Path = path;

            if (path.Length == 0)
                throw new ArgumentException("path string is empty.");

            if (_state.Target->RefCount == 0)
            {
                _parser.BlockSize = _blockSize;
                _job_handle = _parser.ReadFileAsync(path, Encoding, Data, _state);

                _state.Target->RefCount = 1;
            }

            return _job_handle;
        }
        public unsafe void Update()
        {
            if (_state.Target->JobState == ReadJobState.WaitForCallingComplete)
            {
                Complete();
            }
        }
        public unsafe void Complete()
        {
            _job_handle.Complete();
            _state.Target->JobState = ReadJobState.Completed;
        }
        public unsafe void UnLoadFile()
        {
            if (_state.Target->RefCount == 1)
            {
                Data.UnLoad();
                _state.Target->RefCount = 0;
                _state.Target->JobState = ReadJobState.UnLoaded;
            }
        }
        public void LoadFileInMainThread() { this.LoadFileInMainThread(Path); }
        public unsafe void LoadFileInMainThread(string path)
        {
            Path = path;

            if (path.Length == 0)
                throw new ArgumentException("path string is empty.");

            if (_state.Target->RefCount == 0)
            {
                _parser.BlockSize = _blockSize;
                _parser.ReadFileInMainThread(path, Encoding, Data, _state);
                _state.Target->JobState = ReadJobState.Completed;

                _state.Target->RefCount = 1;
            }
        }
    }
}
