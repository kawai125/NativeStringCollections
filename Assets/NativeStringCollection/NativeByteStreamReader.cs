using System;

using Unity.Collections;
using Unity.IO.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections.Impl
{
    using NativeStringCollections.Utility;

    public unsafe struct NativeByteStreamReader : IDisposable
    {
        private const int DefaultBufferSize = 4096;
        private const int MinBufferSize = 1024;

        private GCHandle<string> _path;
        private long _fileSize;
        private bool _disposePathHandle;

        private int _blockSize;
        private int _blockNum;
        private int _blockPos;

        private Allocator _alloc;

        private NativeArray<byte> _byteBuffer;
        private int _dataLength;

        private NativeArray<ReadCommand> _readCommands;

        private bool _allocated;

        public string Path { get { return _path.Target; } }
        public int BufferSize { get { return _dataLength; } }
        public int Length { get { return _blockNum; } }
        public int Pos { get { return _blockPos; } }
        public bool EndOfStream { get { return _blockPos == _blockNum; } }

        /// <summary>
        /// the constructor must be called by main thread only.
        /// </summary>
        /// <param name="path"></param>
        public NativeByteStreamReader(Allocator alloc)
        {
            _path.Create("");
            _disposePathHandle = true;

            _fileSize = 0;

            _alloc = alloc;
            _byteBuffer = new NativeArray<byte>(MinBufferSize, alloc);
            _dataLength = 0;
            _readCommands = new NativeArray<ReadCommand>(1, alloc);

            _blockSize = 0;
            _blockNum = 0;
            _blockPos = 0;

            _allocated = true;
        }
        /// <summary>
        /// initializer of NativeByteStreamReader.
        /// this function must be called by main thread.
        /// use Init(Utility.GCHandle<string>, buffersize) for calling in JobSystem.
        /// </summary>
        /// <param name="path">file path.</param>
        /// <param name="bufferSize">internal buffer size (if < 0, buffering entire the file).</param>
        public void Init(string path, int bufferSize = DefaultBufferSize)
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
        public void Init(GCHandle<string> pathHandle, bool disposePathHandle, long fileSize, int bufferSize = DefaultBufferSize)
        {
            this.InitState(pathHandle,disposePathHandle, fileSize, bufferSize);
        }
        private void InitState(GCHandle<string> pathHandle, bool disposePathHandle, long fileSize, int bufferSize)
        {
            if (fileSize < 0) throw new ArgumentOutOfRangeException("file size must be > 0.");

            if (_disposePathHandle) _path.Dispose();
            _path = pathHandle;
            _disposePathHandle = disposePathHandle;
            _fileSize = fileSize;
            ;
            int tgtBufferSize = Math.Max(bufferSize, MinBufferSize);

            // for debug
            //tgtBufferSize = 64;

            if (bufferSize < 0 || _fileSize < tgtBufferSize)
            {
                // buffering entire the file
                _blockSize = (int)_fileSize;
                _blockNum = 1;
            }
            else
            {
                // buffering as blocking
                _blockSize = tgtBufferSize;
                _blockNum = (int)(_fileSize / _blockSize) + 1;
            }
            _blockPos = 0;

            this.Reallocate();
        }
        private void Reallocate()
        {
            // reallocation buffer
            if(_blockSize > _byteBuffer.Length)
            {
                _byteBuffer.Dispose();
                _byteBuffer = new NativeArray<byte>(_blockSize, _alloc);
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
            if (_allocated)
            {
                if(_disposePathHandle) _path.Dispose();

                _byteBuffer.Dispose();
                _readCommands.Dispose();

                _allocated = false;
            }
        }

        public int Read()
        {
            if (_blockPos == _blockNum) return -1; // EOF

            // check file termination
            _dataLength = Math.Min(_blockSize, (int)(_fileSize - ((long)_blockSize) * _blockPos));

            _readCommands[0] = new ReadCommand
            {
                Offset = _blockPos * _blockSize,
                Size = _dataLength,
                Buffer = _byteBuffer.GetUnsafePtr(),
            };

            ReadHandle read_handle = AsyncReadManager.Read(_path.Target, (ReadCommand*)(_readCommands.GetUnsafePtr()), 1);
            if (!read_handle.IsValid()) throw new InvalidOperationException("failure to open file.");
            read_handle.JobHandle.Complete();
            read_handle.Dispose();

            _blockPos++;
            return _dataLength;
        }
        public int Read(NativeList<byte> buff, bool append = false)
        {
            if (_blockPos == _blockNum) return -1; // EOF

            if (!append) buff.Clear();

            this.Read();

            if (_dataLength > 0) buff.AddRange(_byteBuffer.GetUnsafePtr(), _dataLength);

            return _dataLength;
        }
        public void* GetUnsafePtr() { return _byteBuffer.GetUnsafePtr(); }
    }
}
