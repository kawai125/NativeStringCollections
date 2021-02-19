﻿using System;
using System.Text;

using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections
{
    using NativeStringCollections.Utility;

    namespace Impl
    {
        internal struct ReadStateImpl
        {
            public ReadJobState State;
            public int Length;
            public int Read;
            public int RefCount;

            public float DelayReadAsync;
            public float DelayParseText;
            public float DelayPostProc;

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
            public bool IsStandby
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
            where Tdata : class, ITextFileParser
        {
            private AsyncByteReader _byteReader;
            private TextDecoder _decoder;

            private NativeStringList _lines;

            private GCHandle<Tdata> _data;
            private PtrHandle<ReadStateImpl> _state_ptr;    // monitoring state from main thread

            private PtrHandle<ParseJobInfo> _info;  // internal use
            private GCHandle<System.Diagnostics.Stopwatch> _timer;
            private float _timer_ms_coef;

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
                _state_ptr.Target->State = ReadJobState.ParseText;

                // parse text
                this.ParseText();
                _state_ptr.Target->DelayParseText = this.TimerElapsedMilliSeconds();
                _timer.Target.Restart();

                // post proc
                _data.Target.PostReadProc();
                _timer.Target.Stop();

                _state_ptr.Target->DelayPostProc = this.TimerElapsedMilliSeconds();

                // wait for Complete()
                _state_ptr.Target->State = ReadJobState.WaitForCallingComplete;
            }
            private void ParseText()
            {
                _decoder.Clear();
                _data.Target.Clear();

                Boolean continue_flag = true;
                for (int pos = 0; pos < _info.Target->blockNum; pos++)
                {
                    _lines.Clear();
                    this.ParseLinesFromBuffer();
                    for (int i = 0; i < _lines.Length; i++)
                    {
                        var str = _lines[i];
                        continue_flag = _data.Target.ParseLine(str.GetReadOnly());

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

                if (_info.Target->blockPos == _info.Target->blockNum)
                {
                    if (!_decoder.IsEmpty)
                    {
                        using (var buff = new NativeList<char>(Allocator.Temp))
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
            private float TimerElapsedMilliSeconds()
            {
                return (float)(_timer.Target.ElapsedTicks * _timer_ms_coef);
            }
        }

        internal struct UnLoadJobTarget<Tdata>
            where Tdata : class, ITextFileParser
        {
            internal GCHandle<Tdata> data;
            internal PtrHandle<ReadStateImpl> state_ptr;
            internal int file_index;

            public UnLoadJobTarget(int file_index, Tdata data, PtrHandle<ReadStateImpl> state_ptr)
            {
                this.data = new GCHandle<Tdata>();

                this.data.Create(data);
                this.state_ptr = state_ptr;
                this.file_index = file_index;
            }
            public unsafe void UnLoad()
            {
                this.state_ptr.Target->State = ReadJobState.UnLoaded;
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

            public unsafe void AddUnLoadTarget(int file_index, Tdata data, PtrHandle<ReadStateImpl> state_ptr)
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

            public unsafe ReadJobState State { get { return _info.Target->job_state; } }

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