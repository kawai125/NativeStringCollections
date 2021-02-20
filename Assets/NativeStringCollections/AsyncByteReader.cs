using System;

using Unity.Jobs;
using Unity.Collections;
using Unity.IO.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections.Impl
{
    using NativeStringCollections.Utility;


    internal struct AsyncByteReaderInfo
    {
        public int bufferSize;
        public int dataSize;
        
        public Boolean allocated;

        private Boolean _haveReadHandle;
        private ReadHandle _readHandle;

        public ReadHandle ReadHandle
        {
            get { return _readHandle; }
            set
            {
                this.DisposeReadHandle();
                _readHandle = value;
                _haveReadHandle = true;
            }
        }
        public void DisposeReadHandle()
        {
            if (_haveReadHandle)
            {
                _readHandle.Dispose();
                _haveReadHandle = false;
            }
        }
        public bool HaveReadHandle { get { return _haveReadHandle; } }
    }

    public unsafe struct AsyncByteReader : IDisposable
    {
        private NativeList<byte> _byteBuffer;
        private PtrHandle<AsyncByteReaderInfo> _info;

        private PtrHandle<ReadCommand> _readCmd;


        public int BufferSize { get { return _info.Target->bufferSize; } }
        public int Length { get { return _info.Target->dataSize; } }

        /// <summary>
        /// the constructor must be called by main thread only.
        /// </summary>
        /// <param name="path"></param>
        public AsyncByteReader(Allocator alloc)
        {
            _byteBuffer = new NativeList<byte>(Define.MinByteBufferSize, alloc);
            _info = new PtrHandle<AsyncByteReaderInfo>(alloc);

            _readCmd = new PtrHandle<ReadCommand>(alloc);

            _info.Target->bufferSize = Define.MinByteBufferSize;
            _info.Target->dataSize = 0;

            _info.Target->allocated = true;
        }

        private void Reallocate(long buffSize)
        {
            // reallocation buffer
            if (_info.Target->bufferSize < buffSize)
            {
                long new_size = _info.Target->bufferSize * ((buffSize / _info.Target->bufferSize) + 1);

                if (new_size > int.MaxValue)
                    throw new InvalidOperationException("too large file size. The AsyncByteReader can buffering < int.MaxValue bytes.");

                _byteBuffer.ResizeUninitialized((int)new_size);
                _info.Target->bufferSize = (int)new_size;
            }
        }

        public void Dispose()
        {
            _byteBuffer.Dispose();

            _info.Target->DisposeReadHandle();
            _info.Dispose();

            _readCmd.Dispose();
        }

        public JobHandle ReadFileAsync(string path)
        {
            this.CheckPreviousJob();

            var fileInfo = new System.IO.FileInfo(path);
            if (!fileInfo.Exists) throw new ArgumentException("the file '" + path + "'is not found.");

            this.Reallocate(fileInfo.Length);

            *_readCmd.Target = new ReadCommand
            {
                Offset = 0,
                Size = fileInfo.Length,
                Buffer = _byteBuffer.GetUnsafePtr(),
            };

            _info.Target->dataSize = (int)fileInfo.Length;
            _info.Target->ReadHandle = AsyncReadManager.Read(path, _readCmd.Target, 1);
            return _info.Target->ReadHandle.JobHandle;
        }
        public JobHandle ReadFileAsync(string path, int i_block)
        {
            this.CheckPreviousJob();
            
            var fileInfo = new System.IO.FileInfo(path);
            if (!fileInfo.Exists)
                throw new ArgumentException("the file '" + path + "'is not found.");

            long bufferSize = _info.Target->bufferSize;
            long offset = i_block * bufferSize;
            long size = Math.Min(bufferSize, fileInfo.Length - offset);
            if (offset >= fileInfo.Length || i_block < 0)
            {
                int max_index = (int)(fileInfo.Length / bufferSize);
                throw new ArgumentOutOfRangeException($"invalid i_block. i_block must be in range of [0, {max_index}].");
            }

            *_readCmd.Target = new ReadCommand
            {
                Offset = offset,
                Size = size,
                Buffer = _byteBuffer.GetUnsafePtr(),
            };

            _info.Target->dataSize = (int)size;
            _info.Target->ReadHandle = AsyncReadManager.Read(path, _readCmd.Target, 1);
            return _info.Target->ReadHandle.JobHandle;
        }
        private void CheckPreviousJob()
        {
            if (_info.Target->HaveReadHandle)
            {
                if (!_info.Target->ReadHandle.JobHandle.IsCompleted)
                {
                    throw new InvalidOperationException("previous read job is still running. call Complete().");
                }
                else
                {
                    _info.Target->DisposeReadHandle();
                }
            }
        }

        public void Complete()
        {
            _info.Target->ReadHandle.JobHandle.Complete();
        }

        public void* GetUnsafePtr() { return _byteBuffer.GetUnsafePtr(); }
        public byte this[int index]
        {
            get { return _byteBuffer[index]; }
        }
    }
}
