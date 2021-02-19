using System;

using Unity.Collections;
using Unity.IO.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections.Impl
{
    using NativeStringCollections.Utility;


    internal struct ByteStreamReaderInfo
    {
        public long fileSize;
        public Boolean disposePathHandle;

        public int blockSize;
        public int blockNum;
        public int blockPos;

        public int dataLength;

        public Boolean allocated;
    }


    public unsafe struct NativeByteStreamReader : IDisposable
    {
        private GCHandle<string> _path;
        private PtrHandle<ByteStreamReaderInfo> _info;
        private NativeArray<byte> _byteBuffer;
        private NativeArray<ReadCommand> _readCommands;

        private Allocator _alloc;


        public string Path { get { return _path.Target; } }
        public int BufferSize { get { return _info.Target->dataLength; } }
        public int Length { get { return _info.Target->blockNum; } }
        public int Pos
        {
            get { return _info.Target->blockPos; }
            set
            {
                if (value < 0 || _info.Target->blockNum <= value) throw new ArgumentOutOfRangeException("input Pos is out of range.");
                _info.Target->blockPos = value;
            }
        }
        public bool EndOfStream { get { return _info.Target->blockPos == _info.Target->blockNum; } }

        /// <summary>
        /// the constructor must be called by main thread only.
        /// </summary>
        /// <param name="path"></param>
        public NativeByteStreamReader(Allocator alloc)
        {
            _info = new PtrHandle<ByteStreamReaderInfo>(alloc);
            _byteBuffer = new NativeArray<byte>(Define.MinBufferSize, alloc);
            _readCommands = new NativeArray<ReadCommand>(1, alloc);

            _path.Create("");
            _info.Target->disposePathHandle = true;

            _info.Target->fileSize = 0;

            _alloc = alloc;

            _info.Target->dataLength = 0;

             _info.Target->blockSize = 0;
             _info.Target->blockNum = 0;
            _info.Target->blockPos = 0;

            _info.Target->allocated = true;
        }
        /// <summary>
        /// initializer of NativeByteStreamReader.
        /// this function must be called by main thread.
        /// use Init(Utility.GCHandle<string>, buffersize) for calling in JobSystem.
        /// </summary>
        /// <param name="path">file path.</param>
        /// <param name="bufferSize">internal buffer size (if < 0, buffering entire the file).</param>
        public void Init(string path, int bufferSize = Define.DefaultBufferSize)
        {
            var fileInfo = new System.IO.FileInfo(path);
            if (!fileInfo.Exists) throw new ArgumentException("file is not exists.");
            long fileSize = fileInfo.Length;

            var pathHandle = new GCHandle<string>();
            pathHandle.Create(path);

            this.InitState(pathHandle, true, fileSize, bufferSize);
        }
        /// <summary>
        /// initializer of NativeByteStreamReader.
        /// this function is callable from workerthread.
        /// </summary>
        /// <param name="pathHandle">GCHandle of file path string.</param>
        /// <param name="disposePathHandle">dispose pathHandle when this struct will be disposed.</param>
        /// <param name="fileSize">file size.</param>
        /// <param name="bufferSize">internal buffer size (if < 0, buffering entire the file).</param>
        public void Init(GCHandle<string> pathHandle, bool disposePathHandle, long fileSize, int bufferSize = Define.DefaultBufferSize)
        {
            this.InitState(pathHandle,disposePathHandle, fileSize, bufferSize);
        }
        private void InitState(GCHandle<string> pathHandle, bool disposePathHandle, long fileSize, int bufferSize)
        {
            if (fileSize < 0) throw new ArgumentOutOfRangeException("file size must be > 0.");

            if (_info.Target->disposePathHandle) _path.Dispose();
            _path = pathHandle;
            _info.Target->disposePathHandle = disposePathHandle;
            _info.Target->fileSize = fileSize;
            ;
            long tgtBufferSize = bufferSize;
            if (tgtBufferSize < Define.MinBufferSize) tgtBufferSize = Define.MinBufferSize;
            if (tgtBufferSize > Int32.MaxValue) tgtBufferSize = Int32.MaxValue;

            // for debug
            //tgtBufferSize = 64;

            if (bufferSize < 0 || _info.Target->fileSize < tgtBufferSize)
            {
                // buffering entire the file
                _info.Target->blockSize = (int)_info.Target->fileSize;
                _info.Target->blockNum = 1;
            }
            else
            {
                // buffering as blocking
                _info.Target->blockSize = (int)tgtBufferSize;
                _info.Target->blockNum = (int)(_info.Target->fileSize / _info.Target->blockSize) + 1;
            }
            _info.Target->blockPos = 0;

            this.Reallocate();
        }
        private void Reallocate()
        {
            // reallocation buffer
            if (_info.Target->blockSize > _byteBuffer.Length)
            {
                _byteBuffer.Dispose();
                _byteBuffer = new NativeArray<byte>(_info.Target->blockSize, _alloc);
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
                if (_info.Target->disposePathHandle) _path.Dispose();

                _info.Dispose();
                _byteBuffer.Dispose();
                _readCommands.Dispose();
            }
        }
        public ReadHandle ReadAsync()
        {
            if (EndOfStream) return new ReadHandle();  // returns void handle

            // check file termination
            _info.Target->dataLength = Math.Min(_info.Target->blockSize, (int)(_info.Target->fileSize - ((long)_info.Target->blockSize) * _info.Target->blockPos));

            _readCommands[0] = new ReadCommand
            {
                Offset = _info.Target->blockPos * _info.Target->blockSize,
                Size = _info.Target->dataLength,
                Buffer = _byteBuffer.GetUnsafePtr(),
            };

            ReadHandle read_handle = AsyncReadManager.Read(_path.Target, (ReadCommand*)(_readCommands.GetUnsafePtr()), 1);
            if (!read_handle.IsValid()) throw new InvalidOperationException("failure to open file.");

            _info.Target->blockPos++;
            return read_handle;
        }

        public int Read()
        {
            if (EndOfStream) return -1; // EOF

            var read_handle = this.ReadAsync();
            read_handle.JobHandle.Complete();
            read_handle.Dispose();

            return _info.Target->dataLength;
        }
        public int Read(NativeList<byte> buff, bool append = false)
        {
            if (EndOfStream) return -1; // EOF

            if (!append) buff.Clear();

            int len = this.Read();

            if (_info.Target->dataLength > 0) buff.AddRange(_byteBuffer.GetUnsafePtr(), _info.Target->dataLength);

            return len;
        }

        public void* GetUnsafePtr() { return _byteBuffer.GetUnsafePtr(); }
        public void* GetUnsafeReadOnlyPtr() { return _byteBuffer.GetUnsafeReadOnlyPtr(); }
        public byte this[int index]
        {
            get { return _byteBuffer[index]; }
        }
    }
}
