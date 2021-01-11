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
        void Init();
        void Clear();
        bool ParseLine(ReadOnlyStringEntity line);
        void PostReadProc();
        void UnLoad();
    }

    public enum ReadJobState
    {
        // not in process
        UnLoaded,
        Completed,

        // in process
        ReadAsync,
        ParseText,
        PostProc,
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
        where T : ITextFileParser, IDisposable, new()
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

        public void Init(Allocator alloc)
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

            // disposing managed resource
            if (disposing)
            {

            }
            // disposing unmanaged resource
            if (true)
            {
                foreach (var p in _parserPool) p.Value.Dispose();
                _runningJob.Dispose();

                _state.Dispose();

                _requestQueue.Dispose();

                _updateQueueTmp.Dispose();
                _updateLoadTgtTmp.Dispose();
            }
        }
        public void Clear()
        {
            _fileList.Clear();

            foreach (var d in _data) d.Dispose();
            _data.Clear();
            foreach (var s in _state) s.Dispose();
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
                    int n_add = value - _maxJobCount;
                    _maxJobCount = value;

                    if(n_add > 0)
                    {
                        for (int i = 0; i < n_add; i++) this.GenerateParser();
                    }
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
                if (!_state[fileIndex].Target->IsCompleted) throw new InvalidOperationException("loading file is not completed.");
                return _data[fileIndex];
            }
        }
        public unsafe ReadState GetState(int index)
        {
            return _state[index].Target->GetState();
        }
        public unsafe JobHandle GetJobHandle(int index)
        {
            return _state[index].Target->JobHandle;
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
                if (read_state.Target->DelayPostProc >= 0.0)
                {
                    _parserPool[job_info.ParserID].Complete();
                    read_state.Target->State = ReadJobState.Completed;

                    this.ReleaseParser(job_info.ParserID);
                    _runningJob.RemoveAt(i);
                }
            }

            if (_parserAvail.Count <= 0) return;

            // preprocess requests
            _updateQueueTmp.Clear();
            _updateLoadTgtTmp.Clear();

            for(int i=0; i<_requestQueue.Count; i++)
            {
                _updateQueueTmp.Add(_requestQueue.Dequeue());
            }
            _requestQueue.Clear();

            //--- set load action
            foreach(var act in _updateQueueTmp)
            {
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
            foreach(var act in _updateQueueTmp)
            {
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
                        }
                        else
                        {
                            // remove from loaded data
                            if (_state[act.fileIndex].Target->IsCompleted)
                            {
                                _data[act.fileIndex].UnLoad();
                                _state[act.fileIndex].Target->State = ReadJobState.UnLoaded;
                            }
                            else
                            {
                                // now loading. unload request will try in next update.
                                _requestQueue.Enqueue(act);
                            }
                        }
                    }
                    if (tgt_state.Target->RefCount < 0) throw new InvalidOperationException("invalid UnLoading.");
                }
            }
            _updateQueueTmp.Clear();

            // schedule jobs
            //--- supply parsers for flushing all jobs.
            if (flush_all_jobs)
            {
                int n_add_parser = _updateLoadTgtTmp.Length - _parserAvail.Count;
                for (int i = 0; i < n_add_parser; i++) this.GenerateParser();
            }
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
            }
            //--- write back excessive queue
            for(int i=n_job; i<_updateLoadTgtTmp.Length; i++)
            {
                this.LoadFile(_updateLoadTgtTmp[i]);
            }
            _updateLoadTgtTmp.Clear();
        }

        private void GenerateParser()
        {
            if (_parserPool.Count >= _maxJobCount) return;

            for(int i=0; i<Define.NumParserLimit; i++)
            {
                _gen++;
                if (_gen >= Define.NumParserLimit) _gen = 0;

                if (!_parserPool.ContainsKey(_gen))
                {
                    var p_tmp = new ParseJob<T>(_alloc);
                    _parserPool.Add(_gen, p_tmp);
                    _parserAvail.Enqueue(_gen);
                    break;
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
            }
            else
            {
                // dispose excessive parser
                _parserPool[parser_id].Dispose();
                _parserPool.Remove(parser_id);
            }
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
            public JobHandle JobHandle;

            public void Clear()
            {
                State = ReadJobState.UnLoaded;
                Length = 0;
                Read = 0;
                RefCount = 0;

                ParserID = -1;
                JobHandle = new JobHandle();

                DelayReadAsync = -1;
                DelayParseText = -1;
                DelayPostProc = -1;
            }
            public bool IsCompleted { get { return (State == ReadJobState.Completed); } }
            public ReadState GetState()
            {
                return new ReadState(State, Length, Read, RefCount, DelayReadAsync, DelayParseText, DelayPostProc);
            }
        }

        internal struct ParseJobInfo
        {
            public int decodeBlock;
            public int blockNum;
            public int blockPos;

            public Boolean check_CR;

            public Boolean allocated;
            public Boolean checkPreamble;
            public Boolean disposeHandle;

            public JobHandle jobHandle;
        }
        internal unsafe struct ParseJob<Tdata> : IJob, IDisposable
            where Tdata : ITextFileParser, IDisposable
        {
            private AsyncByteReader _byteReader;

            private GCHandle<Decoder> _decoder;
            private NativeList<byte> _preamble;

            private NativeHeadRemovableList<char> _charBuff;
            private NativeList<char> _continueBuff;

            private NativeStringList _lines;

            GCHandle<Tdata> _data;
            PtrHandle<ReadStateImpl> _state_ptr;

            private PtrHandle<ParseJobInfo> _info;
            private GCHandle<System.Diagnostics.Stopwatch> _timer;
            private double _timer_ms_coef;

            public ParseJob(Allocator alloc)
            {
                _byteReader = new AsyncByteReader(alloc);

                _decoder = new GCHandle<Decoder>();
                _preamble = new NativeList<byte>(alloc);

                _charBuff = new NativeHeadRemovableList<char>(alloc);
                _continueBuff = new NativeList<char>(alloc);
                _lines = new NativeStringList(alloc);

                _data = new GCHandle<Tdata>();
                _state_ptr = new PtrHandle<ReadStateImpl>();  // do not allocate (this will be assigned). use as reference.

                _info = new PtrHandle<ParseJobInfo>(alloc);

                _info.Target->decodeBlock = Define.DefaultDecodeBlock;
                _info.Target->blockNum = 0;
                _info.Target->blockPos = 0;

                _info.Target->allocated = true;
                _info.Target->checkPreamble = false;
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

                    if (_info.Target->allocated)
                    {
                        _byteReader.Dispose();
                        _preamble.Dispose();

                        _charBuff.Dispose();
                        _continueBuff.Dispose();
                        _lines.Dispose();

                        _info.Target->allocated = false;
                    }
                    this.DisposeHandle();
                    _info.Dispose();
                }
            }
            private void DisposeHandle()
            {
                if (_info.Target->disposeHandle)
                {
                    _decoder.Dispose();
                    _data.Dispose();
                    _timer.Dispose();

                    _info.Target->disposeHandle = false;
                }
            }
            public int BlockSize
            {
                get { return _info.Target->decodeBlock; }
                set { if (value > Define.DefaultDecodeBlock) _info.Target->decodeBlock = value; }
            }
            public JobHandle ReadFileAsync(string path, Encoding encoding, Tdata data, PtrHandle<ReadStateImpl> state_ptr)
            {
                this.DisposeHandle();

                _decoder.Create(encoding.GetDecoder());
                _preamble.Clear();
                foreach (byte b in encoding.GetPreamble()) _preamble.Add(b);

                _data.Create(data);
                _state_ptr = state_ptr;
                _state_ptr.Target->State = ReadJobState.ReadAsync;
                _state_ptr.Target->DelayReadAsync = -1;
                _state_ptr.Target->DelayParseText = -1;
                _state_ptr.Target->DelayPostProc = -1;   // Loader will check 'DelayPostProc' value >= 0 or not to calling 'Complete()'.

                _info.Target->checkPreamble = true;
                _info.Target->disposeHandle = true;

                _timer.Create(new System.Diagnostics.Stopwatch());
                _timer.Target.Start();
                _timer_ms_coef = 1000000.0 / System.Diagnostics.Stopwatch.Frequency;

                var job_byteReader = _byteReader.ReadFileAsync(path);
                _info.Target->jobHandle = this.Schedule(job_byteReader);

                _state_ptr.Target->JobHandle = _info.Target->jobHandle;
                return _info.Target->jobHandle;
            }
            public void Complete()
            {
                if (!_info.Target->jobHandle.IsCompleted) _info.Target->jobHandle.Complete();
                this.DisposeHandle();
            }
            public bool IsCreated { get { return _info.Target->allocated; } }



            public void Execute()
            {
                // read async is completed
                _state_ptr.Target->DelayReadAsync = this.TimerElapsedMicroSeconds();
                _timer.Target.Restart();

                // initialize
                if (_info.Target->decodeBlock > _byteReader.Length)
                {
                    _info.Target->blockNum = 1;
                }
                else
                {
                    _info.Target->blockNum = (int)(_byteReader.Length / _info.Target->decodeBlock) + 1;
                }
                _info.Target->blockPos = 0;
                _state_ptr.Target->Length = _info.Target->blockNum;
                _state_ptr.Target->Read = 0;
                _state_ptr.Target->State = ReadJobState.ParseText;

                _charBuff.Clear();
                _continueBuff.Clear();

                // parse text
                this.ParseText();
                _state_ptr.Target->DelayParseText = this.TimerElapsedMicroSeconds();
                _timer.Target.Restart();

                // post proc
                _data.Target.PostReadProc();
                _timer.Target.Stop();

                _state_ptr.Target->DelayPostProc = this.TimerElapsedMicroSeconds();
                this.DisposeHandle();
            }
            private void ParseText()
            {
                _data.Target.Clear();
                Boolean continue_flag = true;
                for (int pos = 0; pos < _info.Target->blockNum; pos++)
                {
                    _info.Target->blockPos = pos;

                    this.DecodeBuffer();
                    _lines.Clear();
                    this.ParseLinesFromBuffer();
                    foreach (var str in _lines)
                    {
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

            private unsafe bool IsPreamble()
            {
                if (!_info.Target->checkPreamble) return false;
                if (_preamble.Length > _byteReader.Length) return false;
                _info.Target->checkPreamble = false; // check at first only

                for (int i = 0; i < _preamble.Length; i++)
                {
                    if (_preamble[i] != _byteReader[i]) return false;
                }

                return true;
            }
            private unsafe void DecodeBuffer()
            {
                int byte_offset = 0;
                if (this.IsPreamble()) byte_offset = _preamble.Length;  // skip BOM

                long byte_pos = _info.Target->blockPos * _info.Target->decodeBlock;
                int byte_len = Math.Min(_info.Target->decodeBlock, (int)(_byteReader.Length - byte_pos));
                byte_pos += byte_offset;
                byte_len -= byte_offset;
                if (byte_len < 0) throw new InvalidOperationException("Internal error: invalid buffer size.");

                byte* byte_ptr = (byte*)_byteReader.GetUnsafePtr() + byte_pos;
                int char_len = _decoder.Target.GetCharCount(byte_ptr, byte_len, false);

                _charBuff.Clear();
                if (char_len > _charBuff.Capacity) _charBuff.Capacity = char_len;
                _charBuff.ResizeUninitialized(char_len);

                _decoder.Target.GetChars(byte_ptr, byte_len, (char*)_charBuff.GetUnsafePtr(), char_len, false);
            }
            private unsafe bool ParseLineImpl()
            {
                // check '\r\n' is overlap between previous buffer and current buffer
                if (_info.Target->check_CR && _charBuff.Length > 0)
                {
                    if (_charBuff[0] == '\n') _charBuff.RemoveHead();

                    //if (_charBuff[0] == '\n') Debug.LogWarning("  >> detect overlap \\r\\n");
                }

                if (_charBuff.Length == 0) return false;

                for (int i = 0; i < _charBuff.Length; i++)
                {
                    char ch = _charBuff[i];
                    // detect ch = '\n' (unix), '\r\n' (DOS), or '\r' (Mac)
                    if (ch == '\n' || ch == '\r')
                    {
                        //Debug.Log("  ** found LF = " + ((int)ch).ToString() + ", i=" + i.ToString() + "/" + _charBuff.Length.ToString());
                        if (_charBuff[i] == '\n' && i > 0)
                        {
                            //Debug.Log("  ** before LF = " + ((int)_charBuff[i-1]).ToString());
                        }

                        if (i > 0) _lines.Add((char*)_charBuff.GetUnsafePtr(), i);

                        if (ch == '\r')
                        {
                            if (i + 1 < _charBuff.Length)
                            {
                                if (_charBuff[i + 1] == '\n')
                                {
                                    i++;
                                    //Debug.Log("  ** found CRLF");
                                }
                            }
                            else
                            {
                                // check '\r\n' or not on the head of next buffer
                                //Debug.LogWarning("  >> checking overlap CRLF");
                                _info.Target->check_CR = true;
                            }
                        }
                        else
                        {
                            _info.Target->check_CR = false;
                        }
                        _charBuff.RemoveHead(i + 1);
                        return true;
                    }
                }
                return false;
            }
            private unsafe int ParseLinesFromBuffer()
            {
                // move continue buffer data into head of new charBuff
                if (_continueBuff.Length > 0)
                {
                    _charBuff.InsertHead(_continueBuff.GetUnsafePtr(), _continueBuff.Length);
                    _continueBuff.Clear();
                }

                bool detect_line_factor = false;
                int line_count = 0;
                for (int i_line = 0; i_line < _info.Target->decodeBlock; i_line++)   // decodeBlock must be >> # of lines in buffer.
                {
                    // read charBuff by line
                    detect_line_factor = this.ParseLineImpl();
                    if (detect_line_factor)
                    {
                        line_count++;
                    }
                    else
                    {
                        break;
                    }
                }

                // LF was not found in charBuff
                if (!detect_line_factor)
                {
                    // move left charBuff data into continue buffer
                    if (_charBuff.Length > 0)
                    {
                        _continueBuff.Clear();
                        _continueBuff.AddRange(_charBuff.GetUnsafePtr(), _charBuff.Length);
                    }
                    _charBuff.Clear();
                }
                return line_count;
            }
            private double TimerElapsedMicroSeconds()
            {
                return _timer.Target.ElapsedTicks * _timer_ms_coef;
            }
        }
    }
}
