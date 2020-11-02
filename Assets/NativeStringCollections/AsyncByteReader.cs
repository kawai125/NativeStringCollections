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
        private PtrHandle<AsyncByteReaderInfo> _info;
        private byte* _byteBuffer;

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

            _info = new PtrHandle<AsyncByteReaderInfo>(alloc);
            _byteBuffer = (byte*)UnsafeUtility.Malloc(Define.MinByteBufferSize, Define.ByteAlign, alloc);

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
                UnsafeUtility.Free(_byteBuffer, _alloc);
                long new_size = _info.Target->bufferSize * ((fileSize / _info.Target->bufferSize) + 1);

                _byteBuffer = (byte*)UnsafeUtility.Malloc(Define.MinByteBufferSize, Define.ByteAlign, _alloc);
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
                _info.Target->readHandle.Dispose();
                _info.Dispose();

                UnsafeUtility.Free(_byteBuffer, _alloc);

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
                Buffer = _byteBuffer,
            };

            _info.Target->readHandle = AsyncReadManager.Read(path, _readCmd.Target, 1);
            return _info.Target->readHandle.JobHandle;
        }
        public void Complete()
        {
            if (!_info.Target->readHandle.JobHandle.IsCompleted) _info.Target->readHandle.JobHandle.Complete();
        }

        public void* GetUnsafePtr() { return (void*)_byteBuffer; }
        public byte this[int index]
        {
            get { return _byteBuffer[index]; }
        }
    }
}
