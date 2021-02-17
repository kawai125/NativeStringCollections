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

    
    public struct ReadState
    {
        public ReadJobState State { get; }
        public int Length { get; }
        public int Read { get; }
        public int RefCount { get; }
        public double DelayReadAsync { get; }
        public double DelayParseText { get; }
        public double DelayPostProc { get; }


        public double Delay { get { return DelayReadAsync + DelayParseText + DelayPostProc; } }
        public bool IsCompleted { get { return (State == ReadJobState.Completed); } }
        public bool IsStandby
        {
            get { return (State == ReadJobState.Completed || State == ReadJobState.UnLoaded); }
        }

        public ReadState(ReadJobState state, int len, int read, int ref_count,
            double delay_read_async,
            double delay_parse_text,
            double delay_post_proc)
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

    public class AsyncTextFileLoader<T>
        where T : class, ITextFileParser, IDisposable, new()
    {
        private List<string> _fileList;
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
            Dispose = -1,
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


        private AsyncTextFileLoader() { }
        public AsyncTextFileLoader(Allocator alloc)
        {
            _alloc = alloc;
            _fileList = new List<string>();

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
            _fileList.Clear();

            for(int i=0; i<_data.Count; i++)
            {
                _data[i].Dispose();
            }
            _data.Clear();
            for(int i=0; i<_state.Length; i++)
            {
                _state[i].Dispose();
            }
            _state.Clear();
        }
        ~AsyncTextFileLoader() { this.Dispose(); }

        public Encoding Encoding
        {
            get { return _encoding; }
            set { _encoding = value; }
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
            _fileList.Add(str);

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
                foreach (var s in _fileList) list.Add(s);
                return list;
            }
        }
        public string GetFile(int index)
        {
            return _fileList[index];
        }
        public unsafe T this[int fileIndex]
        {
            get
            {
                if (!_state[fileIndex].Target->IsCompleted)
                    throw new InvalidOperationException("the read job running now.");
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
            _requestQueue.Enqueue(new QueueRequest(index, FileAction.Dispose));
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
            // check job completed or not
            for(int i= _runningJob.Length-1; i>=0; i--)
            {
                var job_info = _runningJob[i];
                var read_state = _state[job_info.FileIndex];
                if (read_state.Target->State == ReadJobState.WaitForCallingComplete)
                {
                    _parserPool[job_info.ParserID].Complete();
                    read_state.Target->State = ReadJobState.Completed;

                    this.ReleaseParser(job_info.ParserID);
                    _runningJob.RemoveAt(i);
                }
            }

#if UNITY_EDITOR
            var sb = new StringBuilder();
            if (_requestQueue.Count > 0)
            {
                sb.Append(" >> AsyncTextFileLoader.Update() >> \n");
                sb.Append("   _requestQueue.Count = " + _requestQueue.Count.ToString() + '\n');
                sb.Append("   _runningJob.Length  = " + _runningJob.Length.ToString() + '\n');
                sb.Append("   _maxJobCount        = " + _maxJobCount.ToString() + '\n');
            }
#endif
            // no requests. or all available parser were running. retry in next Update().
            if (_requestQueue.Count == 0 || (_maxJobCount - _runningJob.Length <= 0 && !flush_all_jobs))
            {
#if UNITY_EDITOR
                if(sb.Length > 0)
                {
                    Debug.Log(sb.ToString());
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
                if(act.action == FileAction.Dispose)
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
#if UNITY_EDITOR
                            sb.Append("  -- loading index = " + found_index.ToString() + " was cancelled.\n");
#endif
                        }
                        else
                        {
                            // remove from loaded data
                            if (_state[act.fileIndex].Target->IsCompleted)
                            {
                                _data[act.fileIndex].UnLoad();
                                _state[act.fileIndex].Target->State = ReadJobState.UnLoaded;
#if UNITY_EDITOR
                                sb.Append("  -- index = " + found_index.ToString() + " was unloaded.\n");
#endif
                            }
                            else
                            {
                                // now loading. unload request will try in next update.
                                _requestQueue.Enqueue(act);
#if UNITY_EDITOR
                                sb.Append("  -- index = " + found_index.ToString() + " is loading in progress.");
                                sb.Append(" retry unload in next Update().\n");
#endif
                            }
                        }
                    }
                    if (tgt_state.Target->RefCount < 0)
                    {
#if UNITY_EDITOR
                        var sb_e = new StringBuilder();
                        sb_e.Append(" >> AsyncTextFileLoader.Update() >> \n");
                        sb_e.Append("  invalid unloading for index = " + act.fileIndex.ToString() + ".\n");
                        Debug.LogError(sb_e.ToString());
#endif
                        throw new InvalidOperationException("invalid UnLoading.");
                    }
                }
            }
            _updateQueueTmp.Clear();

            // schedule jobs
            //--- supply parsers
            int n_add_parser = Math.Max(_updateLoadTgtTmp.Length - _parserAvail.Count, 0);
            if (!flush_all_jobs)
            {
                n_add_parser = Math.Min(this.MaxJobCount - _parserPool.Count, n_add_parser);
            }
            for (int i = 0; i < n_add_parser; i++) this.GenerateParser();
#if UNITY_EDITOR
            if(n_add_parser > 0)
            {
                sb.Append("   " + n_add_parser.ToString() + " parsers were generated.\n");
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
                p_tmp.ReadFileAsync(_fileList[file_index], _encoding, _data[file_index], p_state);
                _runningJob.Add(new JobInfo(file_index, p_id));
#if UNITY_EDITOR
                sb.Append("   run the ParseJob: file index = " + file_index.ToString());
                sb.Append(", parser_id = " + p_id.ToString() + '\n');
#endif
            }

            //--- write back excessive queue
            for (int i=n_job; i<_updateLoadTgtTmp.Length; i++)
            {
                this.LoadFile(_updateLoadTgtTmp[i]);
#if UNITY_EDITOR
                sb.Append($"   loadning queue: {_updateLoadTgtTmp[i]} is pending.\n");
#endif
            }
            _updateLoadTgtTmp.Clear();

#if UNITY_EDITOR
            if (sb.Length > 0)
            {
                Debug.Log(sb.ToString());
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
#if UNITY_EDITOR
                var sb = new StringBuilder();
                sb.Append(" >> AsyncTextFileLoader >> \n");
                sb.Append("  parser id = " + parser_id.ToString() + " was returned into pool.\n");
                Debug.Log(sb.ToString());
#endif
            }
            else
            {
                // dispose excessive parser
                _parserPool[parser_id].Dispose();
                _parserPool.Remove(parser_id);
#if UNITY_EDITOR
                var sb = new StringBuilder();
                sb.Append(" >> AsyncTextFileLoader >> \n");
                sb.Append("  parser id = " + parser_id.ToString() + " was disposed.\n");
                sb.Append("  total parser = " + _parserPool.Count.ToString() + "\n");
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
#if UNITY_EDITOR
            var p_state = _state[file_index];

            if(p_state.Target->RefCount == 0)
            {
                var p_tmp = new ParseJob<T>(_alloc);
                p_tmp.BlockSize = _blockSize;
                p_tmp.ReadFileInMainThread(_fileList[file_index], _encoding, _data[file_index], p_state);
                p_tmp.Dispose();
                p_state.Target->State = ReadJobState.Completed;
            }

            p_state.Target->RefCount++;
#else
            throw new InvalidOperationException("this function is use in UnityEditer only. use LoadFile(int index).");
#endif
        }
    }

    namespace Impl
    {
        internal struct ReadStateImpl
        {
            public ReadJobState State;
            public int Length;
            public int Read;
            public int RefCount;

            public double DelayReadAsync;
            public double DelayParseText;
            public double DelayPostProc;

            public int ParserID;

            public void Clear()
            {
                State = ReadJobState.UnLoaded;
                Length = 0;
                Read = 0;
                RefCount = 0;

                ParserID = -1;

                DelayReadAsync = -1;
                DelayParseText = -1;
                DelayPostProc = -1;
            }
            public bool IsCompleted
            {
                get
                {
                    return (State == ReadJobState.Completed) || (State == ReadJobState.UnLoaded);
                }
            }
            public ReadState GetState()
            {
                return new ReadState(State, Length, Read, RefCount, DelayReadAsync, DelayParseText, DelayPostProc);
            }
        }

        internal struct ParseJobInfo
        {
            public int decodeBlockSize;
            public int blockNum;
            public int blockPos;

            public Boolean allocated;
            public Boolean disposeHandle;

            public JobHandle jobHandle;
        }
        internal unsafe struct ParseJob<Tdata> : IJob, IDisposable
            where Tdata : ITextFileParser, IDisposable
        {
            private AsyncByteReader _byteReader;
            private TextDecoder _decoder;

            private NativeStringList _lines;

            GCHandle<Tdata> _data;
            PtrHandle<ReadStateImpl> _state_ptr;    // monitoring state from main thread

            private PtrHandle<ParseJobInfo> _info;  // internal use
            private GCHandle<System.Diagnostics.Stopwatch> _timer;
            private double _timer_ms_coef;

            public ParseJob(Allocator alloc)
            {
                _byteReader = new AsyncByteReader(alloc);
                _decoder = new TextDecoder(alloc);

                _lines = new NativeStringList(alloc);

                _data = new GCHandle<Tdata>();
                _state_ptr = new PtrHandle<ReadStateImpl>();  // do not allocate (this will be assigned). used as reference.

                _info = new PtrHandle<ParseJobInfo>(alloc);

                _info.Target->decodeBlockSize = Define.DefaultDecodeBlock;
                _info.Target->blockNum = 0;
                _info.Target->blockPos = 0;

                _info.Target->allocated = true;
                _info.Target->disposeHandle = false;

                _info.Target->jobHandle = new JobHandle();

                _timer = new GCHandle<System.Diagnostics.Stopwatch>();
                _timer_ms_coef = 1.0;
            }
            public void Dispose()
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }
            public void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.Complete();

                    this.DisposeHandle();

                    if (_info.Target->allocated)
                    {
                        _byteReader.Dispose();
                        _decoder.Dispose();

                        _lines.Dispose();

                        _info.Target->allocated = false;
                    }
                    _info.Dispose();
                }
            }
            private void DisposeHandle()
            {
                if (_info.Target->disposeHandle)
                {
                    _decoder.ReleaseDecoder();

                    _data.Dispose();
                    _timer.Dispose();

                    _info.Target->disposeHandle = false;
                }
            }
            public int BlockSize
            {
                get { return _info.Target->decodeBlockSize; }
                set { if (value > Define.DefaultDecodeBlock) _info.Target->decodeBlockSize = value; }
            }
            public JobHandle ReadFileAsync(string path, Encoding encoding, Tdata data, PtrHandle<ReadStateImpl> state_ptr)
            {
                this.InitializeReadJob(path, encoding, data, state_ptr);

                var job_byteReader = _byteReader.ReadFileAsync(path);
                _info.Target->jobHandle = this.Schedule(job_byteReader);

                return _info.Target->jobHandle;
            }
            public void ReadFileInMainThread(string path, Encoding encoding, Tdata data, PtrHandle<ReadStateImpl> state_ptr)
            {
                this.InitializeReadJob(path, encoding, data, state_ptr);

                var job_byteReader = _byteReader.ReadFileAsync(path);
                job_byteReader.Complete();
                this.Run();
            }
            private void InitializeReadJob(string path, Encoding encoding, Tdata data, PtrHandle<ReadStateImpl> state_ptr)
            {
                this.DisposeHandle();

                _decoder.SetEncoding(encoding);

                _data.Create(data);
                _state_ptr = state_ptr;
                _state_ptr.Target->State = ReadJobState.ReadAsync;
                _state_ptr.Target->DelayReadAsync = -1;
                _state_ptr.Target->DelayParseText = -1;
                _state_ptr.Target->DelayPostProc = -1;

                _info.Target->disposeHandle = true;

                _timer.Create(new System.Diagnostics.Stopwatch());
                _timer.Target.Start();
                _timer_ms_coef = 1000000.0 / System.Diagnostics.Stopwatch.Frequency;
            }
            public void Complete()
            {
                _info.Target->jobHandle.Complete();
                this.DisposeHandle();
            }
            public bool IsCreated { get { return _info.Target->allocated; } }



            public void Execute()
            {
                // read async is completed
                _state_ptr.Target->DelayReadAsync = this.TimerElapsedMicroSeconds();
                _timer.Target.Restart();

                // initialize
                if (_info.Target->decodeBlockSize >= _byteReader.Length)
                {
                    _info.Target->blockNum = 1;
                }
                else
                {
                    _info.Target->blockNum = (_byteReader.Length / _info.Target->decodeBlockSize) + 1;
                }
                _info.Target->blockPos = 0;
                _state_ptr.Target->Length = _info.Target->blockNum;
                _state_ptr.Target->Read = 0;
                _state_ptr.Target->State = ReadJobState.ParseText;

                _decoder.Clear();

                // parse text
                this.ParseText();
                _state_ptr.Target->DelayParseText = this.TimerElapsedMicroSeconds();
                _timer.Target.Restart();

                // post proc
                _data.Target.PostReadProc();
                _timer.Target.Stop();

                _state_ptr.Target->DelayPostProc = this.TimerElapsedMicroSeconds();

                // wait for Complete()
                _state_ptr.Target->State = ReadJobState.WaitForCallingComplete;
            }
            private void ParseText()
            {
                _data.Target.Clear();

                Boolean continue_flag = true;
                for (int pos = 0; pos < _info.Target->blockNum; pos++)
                {
                    _lines.Clear();
                    this.ParseLinesFromBuffer();
                    for (int i=0; i<_lines.Length; i++)
                    {
                        var str = _lines[i];
                        continue_flag = _data.Target.ParseLine(str.GetReadOnlyEntity());

                        // abort
                        if (!continue_flag)
                        {
                            return;
                        }
                    }

                    _state_ptr.Target->Read = pos;
                }
            }

            private unsafe void ParseLinesFromBuffer()
            {
                // decode byte buffer
                long byte_pos = _info.Target->blockPos * _info.Target->decodeBlockSize;
                int decode_len = (int)(_byteReader.Length - byte_pos);
                if (decode_len <= 0) return;
                int byte_len = Math.Min(_info.Target->decodeBlockSize, decode_len);

                byte* byte_ptr = (byte*)_byteReader.GetUnsafePtr() + byte_pos;
                _decoder.GetLines(_lines, byte_ptr, byte_len);

                _info.Target->blockPos++;

                if(_info.Target->blockPos == _info.Target->blockNum)
                {
                    if (!_decoder.IsEmpty)
                    {
                        using(var buff = new NativeList<char>(Allocator.Temp))
                        {
                            _decoder.GetInternalBuffer(buff);
                            _lines.Add((char*)buff.GetUnsafePtr(), buff.Length);
                        }
                    }
                }

                /*
                var sb = new StringBuilder();
                sb.Append("decoded lines:\n");
                foreach(var se in _lines)
                {
                    sb.Append(se.ToString());
                    sb.Append('\n');
                }
                Debug.Log(sb.ToString());
                */
            }
            private double TimerElapsedMicroSeconds()
            {
                return _timer.Target.ElapsedTicks * _timer_ms_coef;
            }
        }
    }
}
