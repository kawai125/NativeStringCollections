//#define LOG_ASYNC_TEXT_FILE_LOADER_UPDATE

using System;
using System.Diagnostics;
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

    /// <summary>
    /// async file reader for multi file and multi user
    /// </summary>
    /// <typeparam name="T">parser class</typeparam>
    public class AsyncTextFileLoader<T> :
        IDisposable
        where T : class, ITextFileParser, new()
    {
        private List<string> _pathList;
        private Encoding _encoding;

        private Allocator _alloc;
        private bool _enableBurst;
        private Dictionary<int, ParseJob<T>> _parserPool;
        private int _gen;

        private int _blockSize;
        private int _maxJobCount;
        private NativeList<RunningJobInfo> _runningJob;

        private List<PtrHandle<ReadStateImpl>> _state;
        private List<T> _data;


        private struct RunningJobInfo
        {
            public int FileIndex { get; }
            public int ParserID { get; }
            public RunningJobInfo(int file_index, int parser_index)
            {
                FileIndex = file_index;
                ParserID = parser_index;
            }
            public override string ToString()
            {
                return $"file index = {FileIndex} => parser: {ParserID}";
            }
        }
        private enum FileAction
        {
            Store = 1,
            UnLoad = -1,
        }
        private struct Request
        {
            public int fileIndex { get; }
            public FileAction action { get; }

            public Request(int index, FileAction action)
            {
                fileIndex = index;
                this.action = action;
            }
            public override string ToString()
            {
                return $"file index = {fileIndex} @ {action}";
            }
        }

        private int _loadWaitingQueueNum;
        private NativeQueue<int> _parserAvail;
        private NativeList<Request> _requestList;

        private NativeList<int> _updateLoadTgtList;
        private NativeList<int> _updateUnLoadTgtList;

        private UnLoadJob<T> _unLoadJob;


        private AsyncTextFileLoader() { }
        public AsyncTextFileLoader(Allocator alloc)
        {
            _alloc = alloc;
            _enableBurst = true;

            _pathList = new List<string>();

            _blockSize = Define.DefaultDecodeBlock;
            _runningJob = new NativeList<RunningJobInfo>(_alloc);

            _parserPool = new Dictionary<int, ParseJob<T>>();
            _parserAvail = new NativeQueue<int>(_alloc);
            _gen = 0;

            this.MaxJobCount = Define.DefaultNumParser;

            this.Encoding = Encoding.Default;

            _state = new List<PtrHandle<ReadStateImpl>>();
            _data = new List<T>();

            _requestList = new NativeList<Request>(_alloc);

            _updateLoadTgtList = new NativeList<int>(_alloc);
            _updateUnLoadTgtList = new NativeList<int>(_alloc);

            _updateLoadTgtList.Clear();
            _updateUnLoadTgtList.Clear();

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

                _requestList.Dispose();

                _updateLoadTgtList.Dispose();
                _updateUnLoadTgtList.Dispose();

                _unLoadJob.Dispose();
            }
            // disposing managed resource
            if (disposing)
            {
                GC.SuppressFinalize(_data);
                GC.SuppressFinalize(_state);
                GC.SuppressFinalize(_parserPool);
                GC.SuppressFinalize(_encoding);
            }
        }
        public void Clear()
        {
            CheckAreAllParsersStandby();

            _pathList.Clear();

            _data.Clear();
            for(int i=0; i<_state.Count; i++)
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
        public int LoadWaitingQueue { get { return _loadWaitingQueueNum; } }
        public bool FlushLoadJobs { get; set; }

        /// <summary>
        /// Use or not BurstCompile for parsing lines internally (default = true).
        /// </summary>
        public bool EnableBurst
        {
            get { return _enableBurst; }
            set { _enableBurst = value; }
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
        public string GetFilePath(int index)
        {
            CheckIndex(index, Length);
            return _pathList[index];
        }
        /// <summary>
        /// returns all parser list. all parsers must be standby state.
        /// </summary>
        public unsafe List<T> DataList
        {
            get
            {
                CheckAreAllParsersStandby();
                return _data;
            }
        }
        /// <summary>
        /// returns the parser. the parser must be standby state.
        /// </summary>
        public unsafe T this[int fileIndex]
        {
            get
            {
                CheckIndex(fileIndex, Length);

                if (!_state[fileIndex].Target->IsStandby)
                    throw new InvalidOperationException($"the job running now for fileIndex = {fileIndex}.");
                return _data[fileIndex];
            }
        }
        public unsafe ReadState GetState(int index)
        {
            CheckIndex(index, Length);
            return _state[index].Target->GetState();
        }
        public unsafe bool IsCompleted(int index)
        {
            CheckIndex(index, Length);
            return _state[index].Target->IsCompleted;
        }
        public unsafe bool IsStandby(int index)
        {
            CheckIndex(index, Length);
            return _state[index].Target->IsStandby;
        }

        public void LoadFile(int index)
        {
            CheckIndex(index, Length);
            _loadWaitingQueueNum++;
            _requestList.Add(new Request(index, FileAction.Store));
        }
        public void UnLoadFile(int index)
        {
            CheckIndex(index, Length);
            _requestList.Add(new Request(index, FileAction.UnLoad));
        }

        public void Update()
        {
            this.UpdateImpl(this.FlushLoadJobs);
            this.FlushLoadJobs = false;
        }
        /// <summary>
        /// complete all running jobs.
        /// </summary>
        public unsafe void Complete()
        {
            for (int i = _runningJob.Length - 1; i >= 0; i--)
            {
                var job_info = _runningJob[i];

                _parserPool[job_info.ParserID].Complete();
                _state[job_info.FileIndex].Target->JobState = ReadJobState.Completed;

                this.ReleaseParser(job_info.ParserID);
                _runningJob.RemoveAt(i);
            }

            if (!_unLoadJob.IsCompleted)
            {
                _unLoadJob.Complete();
                _unLoadJob.Clear();
            }
        }
        /// <summary>
        /// complete if the job running for file[index].
        /// </summary>
        /// <param name="index"></param>
        public unsafe void Complete(int index)
        {
            CheckIndex(index, Length);

            for(int i=0; i<_runningJob.Length; i++)
            {
                var job_info = _runningJob[i];
                if (job_info.FileIndex == index)
                {
                    _parserPool[job_info.ParserID].Complete();
                    _state[index].Target->JobState = ReadJobState.Completed;

                    this.ReleaseParser(job_info.ParserID);
                    _runningJob.RemoveAt(i);

                    return;
                }
            }

            if (!_unLoadJob.IsCompleted)
            {
                _unLoadJob.Complete();
                _unLoadJob.Clear();
            }
        }


        private unsafe void CheckAreAllParsersStandby()
        {
            bool standby_all = true;
            for (int i = 0; i < _state.Count; i++)
            {
                if (!_state[i].Target->IsStandby) standby_all = false;
            }
            if (standby_all) return;

            var running_list = new List<int>();
            for (int i = 0; i < _state.Count; i++)
            {
                if (!_state[i].Target->IsStandby) running_list.Add(i);
            }

            var sb = new StringBuilder();
            sb.Append("the job running now for fileIndex = [");

            bool first = true;
            foreach (int i in running_list)
            {
                if (first) first = false;
                else
                {
                    sb.Append(", ");
                }
                sb.Append($"{i}");
            }
            sb.Append("] .\n all parsers must be standby.\n");

            throw new InvalidOperationException(sb.ToString());
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
                if (read_state.Target->JobState == ReadJobState.WaitForCallingComplete)
                {
                    _parserPool[job_info.ParserID].Complete();
                    read_state.Target->JobState = ReadJobState.Completed;

                    this.ReleaseParser(job_info.ParserID);
                    _runningJob.RemoveAt(i);
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                    sb.Append($"  -- Loading file: index = {job_info.FileIndex} was completed.\n");
#endif
                }
            }
            if(_unLoadJob.JobState == ReadJobState.WaitForCallingComplete)
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
            if (_requestList.Length > 0)
            {
                sb.Append("   _requestList = {\n");
                for(int j = 0; j<_requestList.Length; j++)
                {
                    var request = _requestList[j];
                    sb.Append($"    {request} : current ref = {_state[request.fileIndex].Target->RefCount}\n");
                }
                sb.Append("  }\n");
                sb.Append("   _runningJob = {\n");
                for (int j = 0; j < _runningJob.Length; j++)
                {
                    sb.Append($"    {_runningJob[j]}\n");
                }
                sb.Append("  }\n");
                sb.Append($"   _maxJobCount = {_maxJobCount}\n");
            }
#endif
            // no requests. or all available parser were running. retry in next Update().
            if ((_requestList.Length == 0 && _updateLoadTgtList.Length == 0 && _updateUnLoadTgtList.Length == 0) ||
                (_maxJobCount - _runningJob.Length <= 0 && !flush_all_jobs))
            {
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                if(sb.Length > 0)
                {
                //    Debug.Log($" >> AsyncTextFileLoader.Update() >> \n    --- waiting for running jobs.\n{sb}");
                    sb.Clear();
                }
#endif
                return;
            }

#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
            {
                sb.Append(" @@ pended Load/UnLoad list:\n");
                sb.Append("   _updateLoadTgtList = [");
                bool first_sb = true;
                for (int j = 0; j < _updateLoadTgtList.Length; j++)
                {
                    if (first_sb)
                    {
                        first_sb = false;
                    }
                    else
                    {
                        sb.Append(", ");
                    }
                    sb.Append(_updateLoadTgtList[j].ToString());
                }
                sb.Append("]\n");
                sb.Append("   _updateUnLoadTgtList = [");
                first_sb = true;
                for(int j=0; j<_updateUnLoadTgtList.Length; j++)
                {
                    if (first_sb)
                    {
                        first_sb = false;
                    }
                    else
                    {
                        sb.Append(", ");
                    }
                    sb.Append(_updateUnLoadTgtList[j].ToString());
                }
                sb.Append("]\n");
            }
#endif

            //--- extract action
            for (int i=0; i<_requestList.Length; i++)
            {
                var act = _requestList[i];
                if (act.action == FileAction.Store)
                {
                    var tgt_state = _state[act.fileIndex];
                    if (tgt_state.Target->RefCount == 0)
                    {
                        _updateLoadTgtList.Add(act.fileIndex);
                    }
                    tgt_state.Target->RefCount++;
                }
                else
                {
                    _updateUnLoadTgtList.Add(act.fileIndex);
                }
            }
            _requestList.Clear();

            //--- preprocess unload action
            for (int i=_updateUnLoadTgtList.Length-1; i>=0; i--)
            {
                int id = _updateUnLoadTgtList[i];
                var tgt_state = _state[id];
                tgt_state.Target->RefCount--;
                _updateUnLoadTgtList.RemoveAt(i);

#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                sb.Append($"  -- index = {id}, RefCount = {tgt_state.Target->RefCount}");
#endif

                if (tgt_state.Target->RefCount == 0)
                {
                    int found_index = _updateLoadTgtList.IndexOf(id);
                    if (found_index >= 0)
                    {
                        // remove from loading order (file loading is not performed)
                        _updateLoadTgtList.RemoveAtSwapBack(found_index);
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                        sb.Append($"  -- loading index = {id} was cancelled.\n");
#endif
                    }
                    else
                    {
                        // remove from loaded data
                        if (_unLoadJob.IsCompleted &&
                            tgt_state.Target->JobState == ReadJobState.Completed)
                        {
                            //--- unload in main thread
                            //_data[act.fileIndex].UnLoad();
                            //_state[act.fileIndex].Target->State = ReadJobState.UnLoaded;
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                            //sb.Append($"  -- index = {act.fileIndex} was unloaded.\n");
#endif

                            //--- unload in job (workaround for LargeAllocation.Free() cost in T.UnLoad().)
                            _unLoadJob.AddUnLoadTarget(id, _data[id], _state[id].Target);
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                            sb.Append($"   schedule UnLoadJob: file index = {id}\n");
#endif
                        }
                        else
                        {
                            // now loading. unload request will try in next update.
                            tgt_state.Target->RefCount++;
                            _updateUnLoadTgtList.Add(id);
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                            sb.Append($"  -- index = {id} is loading in progress.");
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
                    sb_e.Append($"  invalid unloading for index = {id}.\n");
                    Debug.LogError(sb_e.ToString());
                    throw new InvalidOperationException($"invalid UnLoading for index = {id}.");
#else
                    tgt_state.Target->RefCount = 0;   // reset ref count
#endif
                }
            }

            // schedule jobs
            //--- unload job
            if(_unLoadJob.IsCompleted)_unLoadJob.UnLoadAsync();

            //--- supply parsers for load job
            int n_add_parser = Math.Max(_updateLoadTgtList.Length - _parserAvail.Count, 0);
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
            int n_job = Math.Min(_parserAvail.Count, _updateLoadTgtList.Length);
            for(int i=0; i<n_job; i++)
            {
                int file_index = _updateLoadTgtList[i];
                int p_id = _parserAvail.Dequeue();
                var p_tmp = _parserPool[p_id];
                var p_state = _state[file_index];

                // update parser settings
                p_tmp.BlockSize = _blockSize;
                p_tmp.EnableBurst = _enableBurst;

                p_tmp.ReadFileAsync(_pathList[file_index], _encoding, _data[file_index], p_state);
                _runningJob.Add(new RunningJobInfo(file_index, p_id));
#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
                sb.Append($"   run the ParseJob: file index = {file_index}");
                sb.Append($", parser_id = {p_id}\n");
#endif
            }
#if PATCH_COLLECTIONS_REMOVE_RANGE
            _updateLoadTgtList.RemoveRangeWithBeginEnd(0, n_job);
#else
            _updateLoadTgtList.RemoveRange(0, n_job);
#endif
            _loadWaitingQueueNum = _updateLoadTgtList.Length;

#if LOG_ASYNC_TEXT_FILE_LOADER_UPDATE
            //--- report excessive queue
            sb.Append("\n");
            for (int i=n_job; i<_updateLoadTgtList.Length; i++)
            {
                int id = _updateLoadTgtList[i];
                sb.Append($"   Loadning queue: {id} is pending.\n");
            }
            for(int i=0; i<_updateUnLoadTgtList.Length; i++)
            {
                int id = _updateUnLoadTgtList[i];
                sb.Append($"   UnLoadning queue: {id} is pending.\n");
            }
            if (sb.Length > 0)
            {
                Debug.Log(" >> AsyncTextFileLoader.Update() >> \n"
                        + sb.ToString()
                        + $"_loadWaitingQueueNum: {_loadWaitingQueueNum}\n");
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
                    var p_tmp = new ParseJob<T>(_alloc, _enableBurst);
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

            if (_parserPool.Count <= _maxJobCount)
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
            CheckIndex(file_index, Length);

            var p_state = _state[file_index];

            if(p_state.Target->RefCount == 0)
            {
                var p_tmp = new ParseJob<T>(_alloc, _enableBurst);
                p_tmp.BlockSize = _blockSize;
                p_tmp.ReadFileInMainThread(_pathList[file_index], _encoding, _data[file_index], p_state);
                p_tmp.Dispose();
                p_state.Target->JobState = ReadJobState.Completed;
            }

            p_state.Target->RefCount++;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndex(int index, int length)
        {
            if (index < 0 || length <= index)
                throw new ArgumentOutOfRangeException($"the index={index} is out of range=[0, {length - 1}].");
        }
    }
}
