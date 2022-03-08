using System;
using System.Text;

using UnityEngine;

using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;


namespace NativeStringCollections
{
    using NativeStringCollections.Utility;

    namespace Impl
    {
        internal struct ReadStateImpl
        {
            public ReadJobState JobState;
            public int Length;
            public int Read;
            public int RefCount;

            public float DelayReadAsync;
            public float DelayParseText;
            public float DelayPostProc;

            public int ParserID;

            public void Clear()
            {
                JobState = ReadJobState.UnLoaded;
                Length = 0;
                Read = 0;
                RefCount = 0;

                ParserID = -1;

                DelayReadAsync = 0f;
                DelayParseText = 0f;
                DelayPostProc = 0f;
            }
            public bool IsStandby
            {
                get
                {
                    return (JobState == ReadJobState.Completed) || (JobState == ReadJobState.UnLoaded);
                }
            }
            public bool IsCompleted
            {
                get
                {
                    return JobState == ReadJobState.Completed;
                }
            }
            public ReadState GetState()
            {
                return new ReadState(JobState, Length, Read, RefCount, DelayReadAsync, DelayParseText, DelayPostProc);
            }
        }

        internal struct ParseJobInfo
        {
            public int decodeBlockSize;
            public int blockNum;
            public int blockPos;

            public Boolean allocated;
            public Boolean disposeHandle;
            public Boolean enableBurst;

            public JobHandle jobHandle;
        }

        internal unsafe struct ParseJob<Tdata> : IJob, IDisposable
            where Tdata : class, ITextFileParser
        {
            private AsyncByteReader _byteReader;
            private GCHandle<Decoder> _decoder;
            private ParseLinesWorker _worker;

            private NativeStringList _lines;

            private GCHandle<Tdata> _data;
            private PtrHandle<ReadStateImpl> _state_ptr;    // monitoring state from main thread

            private PtrHandle<ParseJobInfo> _info;  // internal use
            private GCHandle<System.Diagnostics.Stopwatch> _timer;
            private float _timer_ms_coef;

            public ParseJob(Allocator alloc, bool enableBurst = true)
            {
                _byteReader = new AsyncByteReader(alloc);
                _worker = new ParseLinesWorker(alloc);

                _lines = new NativeStringList(alloc);

                _data = new GCHandle<Tdata>();
                _state_ptr = new PtrHandle<ReadStateImpl>();  // do not allocate (this will be assigned). used as reference.

                _info = new PtrHandle<ParseJobInfo>(alloc);

                _info.Target->decodeBlockSize = Define.DefaultDecodeBlock;
                _info.Target->blockNum = 0;
                _info.Target->blockPos = 0;

                _info.Target->allocated = true;
                _info.Target->disposeHandle = false;
                _info.Target->enableBurst = enableBurst;

                _info.Target->jobHandle = new JobHandle();

                _timer = new GCHandle<System.Diagnostics.Stopwatch>();
                _timer_ms_coef = 1.0f;
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
                        _worker.Dispose();

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
                    _decoder.Dispose();

                    _data.Dispose();
                    _timer.Dispose();

                    _info.Target->disposeHandle = false;
                }
            }
            public int BlockSize
            {
                get { return _info.Target->decodeBlockSize; }
                set { if (value >= Define.MinDecodeBlock) _info.Target->decodeBlockSize = value; }
            }
            public bool EnableBurst
            {
                get { return _info.Target->enableBurst; }
                set { _info.Target->enableBurst = value; }
            }
            public JobHandle ReadFileAsync(string path, Encoding encoding, Tdata data, PtrHandle<ReadStateImpl> state_ptr)
            {
                this.InitializeReadJob(encoding, data, state_ptr);

                var job_byteReader = _byteReader.ReadFileAsync(path);
                _info.Target->jobHandle = this.Schedule(job_byteReader);

                return _info.Target->jobHandle;
            }
            public void ReadFileInMainThread(string path, Encoding encoding, Tdata data, PtrHandle<ReadStateImpl> state_ptr)
            {
                this.InitializeReadJob(encoding, data, state_ptr);

                var job_byteReader = _byteReader.ReadFileAsync(path);
                job_byteReader.Complete();
                this.Run();
            }
            private void InitializeReadJob(Encoding encoding, Tdata data, PtrHandle<ReadStateImpl> state_ptr)
            {
                this.DisposeHandle();

                _decoder.Create(encoding.GetDecoder());
                _worker.SetPreamble(encoding.GetPreamble());

                _data.Create(data);
                _state_ptr = state_ptr;
                _state_ptr.Target->JobState = ReadJobState.ReadAsync;
                _state_ptr.Target->DelayReadAsync = -1;
                _state_ptr.Target->DelayParseText = -1;
                _state_ptr.Target->DelayPostProc = -1;

                _info.Target->disposeHandle = true;

                _timer.Create(new System.Diagnostics.Stopwatch());
                _timer.Target.Start();
                _timer_ms_coef = 1000.0f / System.Diagnostics.Stopwatch.Frequency;
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
                _state_ptr.Target->DelayReadAsync = this.TimerElapsedMilliSeconds();
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
                _state_ptr.Target->JobState = ReadJobState.ParseText;

                // parse text
                this.ParseText();
                _state_ptr.Target->DelayParseText = this.TimerElapsedMilliSeconds();
                _timer.Target.Restart();

                // post proc
                _data.Target.PostReadProc();
                _timer.Target.Stop();

                _state_ptr.Target->DelayPostProc = this.TimerElapsedMilliSeconds();

                // wait for Complete()
                _state_ptr.Target->JobState = ReadJobState.WaitForCallingComplete;
            }
            private void ParseText()
            {
                _decoder.Target.Reset();
                _worker.Clear();
                _data.Target.Clear();

                Boolean continue_flag = true;
                for (int pos = 0; pos < _info.Target->blockNum; pos++)
                {
                    _lines.Clear();
                    this.ParseLinesFromBuffer();

                    continue_flag = _data.Target.ParseLines(_lines);
                    if (!continue_flag) return;

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
                _worker.DecodeTextIntoBuffer(byte_ptr, byte_len, _decoder);

                // parse lines from buffer
                if (_info.Target->enableBurst)
                {
                    LineParserBurst.GetLines(_worker, _lines); // with Burst version
                }
                else
                {
                    _worker.GetLines(_lines);  // without Burst version
                }

                _info.Target->blockPos++;

                // termination of file
                if (_info.Target->blockPos == _info.Target->blockNum)
                {
                    if (!_worker.IsEmpty)
                    {
                        using (var buff = new NativeList<Char16>(Allocator.Temp))
                        {
                            _worker.GetInternalBuffer(buff);
                            _lines.Add((Char16*)buff.GetUnsafePtr(), buff.Length);
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
            private float TimerElapsedMilliSeconds()
            {
                return (float)(_timer.Target.ElapsedTicks * _timer_ms_coef);
            }
        }

        internal unsafe struct UnLoadJobTarget<Tdata>
            where Tdata : class, ITextFileParser
        {
            internal GCHandle<Tdata> data;
            internal ReadStateImpl* state_ptr;
            internal int file_index;

            public UnLoadJobTarget(int file_index, Tdata data, ReadStateImpl* state_ptr)
            {
                this.data = new GCHandle<Tdata>();

                this.data.Create(data);
                this.state_ptr = state_ptr;
                this.file_index = file_index;
            }
            public unsafe void UnLoad()
            {
                this.state_ptr->JobState = ReadJobState.UnLoaded;
                this.data.Target.UnLoad();
            }
        }
        internal struct UnLoadJobInfo
        {
            internal ReadJobState job_state;
            internal JobHandle job_handle;
            internal Boolean alloc_handle;
        }
        internal struct UnLoadJob<Tdata> : IJob, IDisposable
            where Tdata : class, ITextFileParser
        {
            internal NativeList<UnLoadJobTarget<Tdata>> _target;
            internal PtrHandle<UnLoadJobInfo> _info;

            public unsafe UnLoadJob(Allocator alloc)
            {
                _target = new NativeList<UnLoadJobTarget<Tdata>>(alloc);
                _info = new PtrHandle<UnLoadJobInfo>(alloc);

                _info.Target->job_state = ReadJobState.Completed;
            }
            public unsafe void Dispose()
            {
                this.DisposeHandle();
                _target.Dispose();
                _info.Dispose();
            }
            private unsafe void DisposeHandle()
            {
                if (_info.Target->alloc_handle)
                {
                    for (int i = 0; i < _target.Length; i++) _target[i].data.Dispose();
                    _info.Target->alloc_handle = false;
                }
            }

            public void Clear()
            {
                this.DisposeHandle();
                _target.Clear();
            }

            public unsafe void AddUnLoadTarget(int file_index, Tdata data, ReadStateImpl* state_ptr)
            {
                _target.Add( new UnLoadJobTarget<Tdata>(file_index, data, state_ptr) );
                _info.Target->alloc_handle = true;
            }
            public unsafe JobHandle UnLoadAsync()
            {
                if(_target.Length > 0)
                {
                    _info.Target->job_state = ReadJobState.UnLoadJob;
                    _info.Target->job_handle = this.Schedule();
                    return _info.Target->job_handle;
                }
                else
                {
                    // no action
                    return new JobHandle();
                }
            }

            public bool IsCompleted { get { return JobState == ReadJobState.Completed; } }
            public unsafe ReadJobState JobState { get { return _info.Target->job_state; } }

            public unsafe void Execute()
            {
                for (int i = 0; i < _target.Length; i++) _target[i].UnLoad();

                _info.Target->job_state = ReadJobState.WaitForCallingComplete;
            }

            public unsafe void Complete()
            {
                _info.Target->job_handle.Complete();
                _info.Target->job_state = ReadJobState.Completed;
            }
        }
    }
}
