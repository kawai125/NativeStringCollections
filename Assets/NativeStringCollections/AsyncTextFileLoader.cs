﻿//#define LOG_ASYNC_TEXT_FILE_LOADER_UPDATE

using System;
using System.Text;
using System.Collections.Generic;

using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections
{
    using NativeStringCollections.Utility;
    using NativeStringCollections.Impl;

    public interface ITextFileParser
    {
        /// <summary>
        /// called once at the first in main thread (you can use managed object in this function).
        /// </summary>
        void Init();

        /// <summary>
        /// called every time at first on reading file.
        /// </summary>
        void Clear();

        /// <summary>
        /// when you returned 'false', the AsyncTextFileLoader discontinue calling the 'ParseLine()' and jump to calling 'PostReadProc()'.
        /// </summary>
        /// <param name="line">the string of a line.</param>
        /// <returns>continue reading lines or not.</returns>
        bool ParseLine(ReadOnlyStringEntity line);

        /// <summary>
        /// called every time at last on reading file.
        /// </summary>
        void PostReadProc();

        /// <summary>
        /// called when the AsyncTextFileLoader.UnLoadFile(index) function was called.
        /// </summary>
        void UnLoad();
    }

    
    public readonly struct ReadState
    {
        public ReadJobState State { get; }
        public int Length { get; }
        public int Read { get; }
        public int RefCount { get; }
        /// <summary>
        /// elapsed milliseconds for AsyncReadManager.Read()
        /// </summary>
        public float DelayReadAsync { get; }
        /// <summary>
        /// elapsed milliseconds for ITextFileParser.ParseLine()
        /// </summary>
        public float DelayParseText { get; }
        /// <summary>
        /// elapsed milliseconds for ITextFileParser.PostReadProc()
        /// </summary>
        public float DelayPostProc { get; }

        /// <summary>
        /// elapsed milliseconds to parse the file.
        /// </summary>
        public double Delay { get { return DelayReadAsync + DelayParseText + DelayPostProc; } }
        public bool IsCompleted { get { return (State == ReadJobState.Completed); } }
        public bool IsStandby
        {
            get { return (State == ReadJobState.Completed || State == ReadJobState.UnLoaded); }
        }

        public ReadState(ReadJobState state, int len, int read, int ref_count,
            float delay_read_async,
            float delay_parse_text,
            float delay_post_proc)
        {
            State = state;
            Length = len;
            Read = read;
            RefCount = ref_count;
            DelayReadAsync = delay_read_async;
            DelayParseText = delay_parse_text;
            DelayPostProc = delay_post_proc;
        }
    }

    public class AsyncTextFileReader<T> : IDisposable
        where T : class, ITextFileParser, new()
    {
        public T Data;
        public string Path;
        public int BlockSize
        {
            get { return BlockSize; }
            set { if (value >= Define.MinDecodeBlock) BlockSize = value; }
        }
        public Encoding Encoding;

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
        private void Init(string path, Allocator alloc, Encoding encoding)
        {
            Data = new T();
            Path = path;
            BlockSize = Define.DefaultDecodeBlock;
            Encoding = encoding;

            _parser = new ParseJob<T>(alloc);
            _state = new PtrHandle<ReadStateImpl>(alloc);

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

                _allocated = true;
            }
        }

        public unsafe ReadState GetState() { return _state.Target->GetState(); }

        public JobHandle LoadFile() { return this.LoadFile(Path); }
        public unsafe JobHandle LoadFile(string path)
        {
            if (path.Length == 0)
                throw new ArgumentException("path string is empty.");

            if(_state.Target->RefCount == 0)
            {
                _parser.BlockSize = BlockSize;
                _job_handle = _parser.ReadFileAsync(path, Encoding, Data, _state);

                _state.Target->RefCount = 1;
            }

            return _job_handle;
        }
        public unsafe void Complete()
        {
            if(_state.Target->State == ReadJobState.WaitForCallingComplete)
            {
                _job_handle.Complete();
                _state.Target->State = ReadJobState.Completed;
            }
        }
        public unsafe void UnLoadFile()
        {
            if(_state.Target->RefCount == 1)
            {
                Data.UnLoad();
                _state.Target->RefCount = 0;
            }
        }
        public void LoadFileInMainThread() { this.LoadFileInMainThread(Path); }
        public unsafe void LoadFileInMainThread(string path)
        {
            if (path.Length == 0)
                throw new ArgumentException("path string is empty.");

            if (_state.Target->RefCount == 0)
            {
                _parser.BlockSize = BlockSize;
                _parser.ReadFileInMainThread(path, Encoding, Data, _state);
                _state.Target->State = ReadJobState.Completed;

                _state.Target->RefCount = 1;
            }
        }
    }

    public class AsyncTextFileLoader<T> :
        IDisposable
        where T : class, ITextFileParser, new()
    {
        private List<string> _pathList;
        private Encoding _encoding;

        private Allocator _alloc;
        private Dictionary<int, ParseJob<T>> _parserPool;
        private int _gen;

        private int _blockSize;
        private int _maxJobCount;
        private NativeList<JobInfo> _runningJob;

        private NativeList<PtrHandle<ReadStateImpl>> _state;
        private List<T> _data;


        private struct JobInfo
        {
            public int FileIndex { get; }
            public int ParserID { get; }
            public JobInfo(int file_index, int parser_index)
            {
                FileIndex = file_index;
                ParserID = parser_index;
            }
        }
        private enum FileAction
        {
            Store = 1,
            UnLoad = -1,
        }
        private struct QueueRequest
        {
            public int fileIndex { get; }
            public FileAction action { get; }

            public QueueRequest(int index, FileAction action)
            {
                fileIndex = index;
                this.action = action;
            }
        }

        private NativeQueue<int> _parserAvail;
        private NativeQueue<QueueRequest> _requestQueue;

        private NativeList<QueueRequest> _updateQueueTmp;
        private NativeList<int> _updateLoadTgtTmp;

        private UnLoadJob<T> _unLoadJob;


        private AsyncTextFileLoader() { }
        public AsyncTextFileLoader(Allocator alloc)
        {
            _alloc = alloc;
            _pathList = new List<string>();

            _blockSize = Define.DefaultDecodeBlock;
            _runningJob = new NativeList<JobInfo>(_alloc);

            _parserPool = new Dictionary<int, ParseJob<T>>();
            _parserAvail = new NativeQueue<int>(_alloc);
            _gen = 0;

            this.MaxJobCount = Define.DefaultNumParser;

            this.Encoding = Encoding.Default;

            _state = new NativeList<PtrHandle<ReadStateImpl>>(_alloc);
            _data = new List<T>();

            _requestQueue = new NativeQueue<QueueRequest>(_alloc);

            _updateQueueTmp = new NativeList<QueueRequest>(_alloc);
            _updateLoadTgtTmp = new NativeList<int>(_alloc);

            _unLoadJob = new UnLoadJob<T>(alloc);
        }
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        void Dispose(bool disposing)
        {
            this.Clear();
            
            // disposing unmanaged resource
            if (true)
            {
                foreach (var p in _parserPool) p.Value.Dispose();
                _parserAvail.Dispose();
                _runningJob.Dispose();

                _state.Dispose();

                _requestQueue.Dispose();

                _updateQueueTmp.Dispose();
                _updateLoadTgtTmp.Dispose();

                _unLoadJob.Dispose();
            }
            // disposing managed resource
            if (disposing)
            {
                GC.SuppressFinalize(_data);
                GC.SuppressFinalize(_parserPool);
                GC.SuppressFinalize(_encoding);
            }
        }
        public void Clear()
        {
            _pathList.Clear();

            _data.Clear();
            for(int i=0; i<_state.Length; i++)
            {
                _state[i].Dispose();
            }
            _state.Clear();
        }
        ~AsyncTextFileLoader() { this.Dispose(); }

        public int Length { get { return _pathList.Count; } }
        public Encoding Encoding
        {
            get { return _encoding; }
            set { _encoding = value; }
        }
        public int BlockSize
        {
            get { return _blockSize; }
            set { if (value >= Define.MinDecodeBlock) _blockSize = value; }
        }
        public int MaxJobCount
        {
            get { return _maxJobCount; }
            set
            {
                if(value > 0)
                {
                    _maxJobCount = value;
                }
            }
        }

        public unsafe void AddFile(string str)
        {
            _pathList.Add(str);

            _data.Add(new T());
            _data[_data.Count - 1].Init();

            var s_tmp = new PtrHandle<ReadStateImpl>(_alloc);
            s_tmp.Target->Clear();
            _state.Add(s_tmp);
        }
        public void AddFile(IEnumerable<string> str_list)
        {
            foreach (var str in str_list) this.AddFile(str);
        }
        public List<string> FileList
        {
            get
            {
                var list = new List<string>();
                foreach (var s in _pathList) list.Add(s);
                return list;
            }
        }
        public string GetFile(int index)
        {
            return _pathList[index];
        }
        public unsafe List<T> DataList
        {
            get
            {
                for(int i=0; i<_state.Length; i++)
                {
                    if (!_state[i].Target->IsStandby)
                        throw new InvalidOperationException($"the job running now for fileIndex = {i}.");
                }
                return _data;
            }
        }
        public unsafe T this[int fileIndex]
        {
            get
            {
                if (!_state[fileIndex].Target->IsStandby)
                    throw new InvalidOperationException($"the job running now for fileIndex = {fileIndex}.");
                return _data[fileIndex];
            }
        }
        public unsafe ReadState GetState(int index)
        {
            return _state[index].Target->GetState();
        }

        public void LoadFile(int index)
        {
            _requestQueue.Enqueue(new QueueRequest(index, FileAction.Store));
        }
        public void UnLoadFile(int index)
        {
            _requestQueue.Enqueue(new QueueRequest(index, FileAction.UnLoad));
        }

        public void Update()
        {
            this.UpdateImpl(false);
        }
        public void RunAllJobs()
        {
            this.UpdateImpl(true);
        }


        private unsafe void UpdateImpl(bool flush_all_jobs = false)
        {
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
            var sb = new StringBuilder();
#endif

            // check job completed or not
            for (int i= _runningJob.Length-1; i>=0; i--)
            {
                var job_info = _runningJob[i];
                var read_state = _state[job_info.FileIndex];
                if (read_state.Target->State == ReadJobState.WaitForCallingComplete)
                {
                    _parserPool[job_info.ParserID].Complete();
                    read_state.Target->State = ReadJobState.Completed;

                    this.ReleaseParser(job_info.ParserID);
                    _runningJob.RemoveAt(i);
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                    sb.Append($"  -- Loading file: index = {job_info.FileIndex} was completed.\n");
#endif
                }
            }
            if(_unLoadJob.State == ReadJobState.WaitForCallingComplete)
            {
                _unLoadJob.Complete();
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                for (int i=0; i<_unLoadJob._target.Length; i++)
                {
                    sb.Append($"  -- UnLoading file: index = {_unLoadJob._target[i].file_index} was completed.\n");
                }
#endif
                _unLoadJob.Clear();
            }

#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
            if (_requestQueue.Count > 0)
            {
                sb.Append($"   _requestQueue.Count = {_requestQueue.Count}\n");
                sb.Append($"   _runningJob.Length  = {_runningJob.Length}\n");
                sb.Append($"   _maxJobCount        = {_maxJobCount}\n");
            }
#endif
            // no requests. or all available parser were running. retry in next Update().
            if (_requestQueue.Count == 0 || (_maxJobCount - _runningJob.Length <= 0 && !flush_all_jobs))
            {
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                if(sb.Length > 0)
                {
                    Debug.Log(" >> AsyncTextFileLoader.Update() >> \n" + sb.ToString());
                    sb.Clear();
                }
#endif
                return;
            }

            // preprocess requests
            _updateQueueTmp.Clear();
            _updateLoadTgtTmp.Clear();

            for(int i=0; i<_requestQueue.Count; i++)
            {
                _updateQueueTmp.Add(_requestQueue.Dequeue());
            }
            _requestQueue.Clear();

            //--- set load action
            for(int i=0; i<_updateQueueTmp.Length; i++)
            {
                var act = _updateQueueTmp[i];
                if(act.action == FileAction.Store)
                {
                    var tgt_state = _state[act.fileIndex];
                    if(tgt_state.Target->RefCount == 0)
                    {
                        _updateLoadTgtTmp.Add(act.fileIndex);
                    }
                    tgt_state.Target->RefCount++;
                }
            }

            //--- set unload action
            for(int i=0; i<_updateQueueTmp.Length; i++)
            {
                var act = _updateQueueTmp[i];
                if(act.action == FileAction.UnLoad)
                {
                    var tgt_state = _state[act.fileIndex];
                    tgt_state.Target->RefCount--;

                    if(tgt_state.Target->RefCount == 0)
                    {
                        int found_index = _updateLoadTgtTmp.IndexOf(act.fileIndex);
                        if(found_index >= 0)
                        {
                            // remove from loading order (file loading is not performed)
                            _updateLoadTgtTmp.RemoveAtSwapBack(found_index);
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                            sb.Append($"  -- loading index = {act.fileIndex} was cancelled.\n");
#endif
                        }
                        else
                        {
                            // remove from loaded data
                            if (_unLoadJob.State == ReadJobState.Completed && _state[act.fileIndex].Target->IsStandby)
                            {
                                //--- unload in main thread
                                //_data[act.fileIndex].UnLoad();
                                //_state[act.fileIndex].Target->State = ReadJobState.UnLoaded;
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                                //sb.Append($"  -- index = {act.fileIndex} was unloaded.\n");
#endif

                                //--- unload in job (workaround for LargeAllocation.Free() cost in T.UnLoad().)
                                int file_index = act.fileIndex;
                                _unLoadJob.AddUnLoadTarget(file_index, _data[file_index], _state[file_index]);
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                                sb.Append($"   run the UnLoadJob: file index = {file_index}");
#endif
                            }
                            else
                            {
                                // now loading. unload request will try in next update.
                                _requestQueue.Enqueue(act);
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                                sb.Append($"  -- index = {act.fileIndex} is loading in progress.");
                                sb.Append(" retry unload in next Update().\n");
#endif
                            }
                        }
                    }
                    if (tgt_state.Target->RefCount < 0)
                    {
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                        var sb_e = new StringBuilder();
                        sb_e.Append(" >> AsyncTextFileLoader.Update() >> \n");
                        sb_e.Append($"  invalid unloading for index = {act.fileIndex}.\n");
                        Debug.LogError(sb_e.ToString());
#endif
                        throw new InvalidOperationException($"invalid UnLoading for index = {act.fileIndex}.");
                    }
                }
            }
            _updateQueueTmp.Clear();

            // schedule jobs
            //--- unload job
            _unLoadJob.UnLoadAsync();

            //--- supply parsers for load job
            int n_add_parser = Math.Max(_updateLoadTgtTmp.Length - _parserAvail.Count, 0);
            if (!flush_all_jobs)
            {
                n_add_parser = Math.Min(this.MaxJobCount - _parserPool.Count, n_add_parser);
            }
            for (int i = 0; i < n_add_parser; i++) this.GenerateParser();
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
            if(n_add_parser > 0)
            {
                sb.Append($"   {n_add_parser} parsers were generated.\n");
            }
#endif

            //--- run jobs
            int n_job = Math.Min(_parserAvail.Count, _updateLoadTgtTmp.Length);
            for(int i=0; i<n_job; i++)
            {
                int file_index = _updateLoadTgtTmp[i];
                int p_id = _parserAvail.Dequeue();
                var p_tmp = _parserPool[p_id];
                var p_state = _state[file_index];
                p_tmp.BlockSize = _blockSize;
                p_tmp.ReadFileAsync(_pathList[file_index], _encoding, _data[file_index], p_state);
                _runningJob.Add(new JobInfo(file_index, p_id));
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                sb.Append($"   run the ParseJob: file index = {file_index}");
                sb.Append($", parser_id = {p_id}\n");
#endif
            }

            //--- write back excessive queue
            for (int i=n_job; i<_updateLoadTgtTmp.Length; i++)
            {
                this.LoadFile(_updateLoadTgtTmp[i]);
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                sb.Append($"   loadning queue: {_updateLoadTgtTmp[i]} is pending.\n");
#endif
            }
            _updateLoadTgtTmp.Clear();

#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
            if (sb.Length > 0)
            {
                Debug.Log(" >> AsyncTextFileLoader.Update() >> \n" + sb.ToString());
                sb.Clear();
            }
#endif
        }

        private void GenerateParser()
        {
            for(int i=0; i<Define.NumParserLimit; i++)
            {
                _gen++;
                if (_gen >= Define.NumParserLimit) _gen = 0;

                if (!_parserPool.ContainsKey(_gen))
                {
                    var p_tmp = new ParseJob<T>(_alloc);
                    _parserPool.Add(_gen, p_tmp);
                    _parserAvail.Enqueue(_gen);
                    return;
                }
            }

            throw new InvalidOperationException("Internal error: key '_gen' was spent.");
        }
        private void ReleaseParser(int parser_id)
        {
            if (!_parserPool.ContainsKey(parser_id)) throw new InvalidOperationException("Internal error: release invalid parser.");

            if (_parserPool.Count < _maxJobCount)
            {
                // return into queue
                _parserAvail.Enqueue(parser_id);
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                var sb = new StringBuilder();
                sb.Append(" >> AsyncTextFileLoader >> \n");
                sb.Append($"  parser id = {parser_id} was returned into pool.\n");
                Debug.Log(sb.ToString());
#endif
            }
            else
            {
                // dispose excessive parser
                _parserPool[parser_id].Dispose();
                _parserPool.Remove(parser_id);
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                var sb = new StringBuilder();
                sb.Append(" >> AsyncTextFileLoader >> \n");
                sb.Append($"  parser id = {parser_id} was disposed.\n");
                sb.Append($"  total parser = {_parserPool.Count}\n");
                Debug.Log(sb.ToString());
#endif
            }
        }

        /// <summary>
        /// force the job to be processed in the main thread for debuging user defined parser.
        /// </summary>
        /// <param name="index"></param>
        public unsafe void LoadFileInMainThread(int file_index)
        {
            var p_state = _state[file_index];

            if(p_state.Target->RefCount == 0)
            {
                var p_tmp = new ParseJob<T>(_alloc);
                p_tmp.BlockSize = _blockSize;
                p_tmp.ReadFileInMainThread(_pathList[file_index], _encoding, _data[file_index], p_state);
                p_tmp.Dispose();
                p_state.Target->State = ReadJobState.Completed;
            }

            p_state.Target->RefCount++;
        }
    }
}
