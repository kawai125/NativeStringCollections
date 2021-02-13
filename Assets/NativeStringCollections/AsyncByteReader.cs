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
        public long bufferSize;
        public long fileSize;

        public ReadHandle readHandle;

        public Boolean allocated;
    }


    public unsafe struct AsyncByteReader : IDisposable
    {
        private NativeArray<byte> _byteBuffer;
        private PtrHandle<AsyncByteReaderInfo> _info;

        private PtrHandle<ReadCommand> _readCmd;

        private Allocator _alloc;


        public long BufferSize { get { return _info.Target->bufferSize; } }
        public long Length { get { return _info.Target->fileSize; } }

        /// <summary>
        /// the constructor must be called by main thread only.
        /// </summary>
        /// <param name="path"></param>
        public AsyncByteReader(Allocator alloc)
        {
            _alloc = alloc;

            _byteBuffer = new NativeArray<byte>(Define.MinBufferSize, alloc);
            _info = new PtrHandle<AsyncByteReaderInfo>(alloc);

            _readCmd = new PtrHandle<ReadCommand>(alloc);

            _info.Target->bufferSize = Define.MinByteBufferSize;
            _info.Target->fileSize = 0;

            _info.Target->allocated = true;
        }

        private void Reallocate(long fileSize)
        {
            // reallocation buffer
            if (_info.Target->bufferSize < fileSize)
            {
                _byteBuffer.Dispose();
                long new_size = _info.Target->bufferSize * ((fileSize / _info.Target->bufferSize) + 1);

                if (new_size > int.MaxValue)
                    throw new InvalidOperationException("too large file size. The AsyncByteReader can buffering < int.MaxValue bytes.");

                _byteBuffer = new NativeArray<byte>((int)new_size, _alloc);
                _info.Target->bufferSize = new_size;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        void Dispose(bool disposing)
        {
            // disposing managed resource
            if (disposing)
            {

            }
            // disposing unmanaged resource
            if (_info.Target->allocated)
            {
                _byteBuffer.Dispose();

                _info.Target->readHandle.Dispose();
                _info.Dispose();

                _readCmd.Dispose();
            }
        }

        public JobHandle ReadFileAsync(string path)
        {
            if (!_info.Target->readHandle.JobHandle.IsCompleted)
            {
                throw new InvalidOperationException("previous read job is still running. call Complete().");
            }
            else
            {
                _info.Target->readHandle.Dispose();
            }

            var fileInfo = new System.IO.FileInfo(path);
            if (!fileInfo.Exists) throw new ArgumentException("file is not exists.");

            this.Reallocate(fileInfo.Length);

            *_readCmd.Target = new ReadCommand
            {
                Offset = 0,
                Size = fileInfo.Length,
                Buffer = _byteBuffer.GetUnsafePtr(),
            };

            _info.Target->readHandle = AsyncReadManager.Read(path, _readCmd.Target, 1);
            return _info.Target->readHandle.JobHandle;
        }
        public void Complete()
        {
            if (!_info.Target->readHandle.JobHandle.IsCompleted) _info.Target->readHandle.JobHandle.Complete();
        }

        public void* GetUnsafePtr() { return _byteBuffer.GetUnsafePtr(); }
        public byte this[int index]
        {
            get { return _byteBuffer[index]; }
        }

        private void CheckAllocator()
        {
            if (!UnsafeUtility.IsValidAllocator(_alloc))
                throw new InvalidOperationException("The buffer can not be Disposed because it was not allocated with a valid allocator.");
        }
    }
}
