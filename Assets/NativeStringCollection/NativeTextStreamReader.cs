using System;
using System.Text;

using Unity.Collections;


namespace NativeStringCollections
{
    using NativeStringCollections.Utility;
    using NativeStringCollections.Impl;

    /// <summary>
    ///  the System.IO.StreamReader like wrapper for Unity.IO.LowLevel.Unsafe.AsyncReadManager
    /// </summary>
    public struct NativeTextStreamReader : IDisposable
    {
        private const int DefaultBufferSize = 4096;

        private GCHandle<Encoding> _encoding;
        private GCHandle<Decoder> _decoder;
        private bool _disposeEncodingHandle;

        private NativeList<byte> _preamble;
        private bool _checkPreamble;

        private NativeByteStreamReader _byteStream;

        private NativeHeadRemovableList<char> _charBuff;
        private bool _check_CR;

        private bool _allocated;



        public NativeTextStreamReader(Allocator alloc)
        {
            _encoding = new GCHandle<Encoding>();
            _decoder = new GCHandle<Decoder>();
            _disposeEncodingHandle = false;

            _preamble = new NativeList<byte>(alloc);
            _checkPreamble = false;

            _byteStream = new NativeByteStreamReader(alloc);

            _charBuff = new NativeHeadRemovableList<char>(128, alloc);
            _check_CR = false;

            _allocated = true;
        }
        /// <summary>
        /// initializer of NativeTextStreamReader.
        /// this function must be called by main thread. use Init(Utility.GCHandle<string>, long fileSize, int buffersize) for calling in JobSystem.
        /// </summary>
        /// <param name="path">file path.</param>
        /// <param name="encoding">encoding object.</param>
        /// <param name="bufferSize">internal buffer size (if < 0, buffering entire the file).</param>
        public void Init(string path, Encoding encoding, int bufferSize = DefaultBufferSize)
        {
            this.SetEncoding(encoding);
            this.Init(path, bufferSize);
        }
        /// <summary>
        /// initializer of NativeTextStreamReader.
        /// this function must be called by main thread.
        /// use Init(Impl.GCHandle<string>, long fileSize, int buffersize) for calling in JobSystem.
        /// </summary>
        /// <param name="path">file path.</param>
        /// <param name="bufferSize">internal buffer size (if < 0, buffering entire the file).</param>
        public void Init(string path, int bufferSize = DefaultBufferSize)
        {
            var fileInfo = new System.IO.FileInfo(path);
            if (!fileInfo.Exists) throw new ArgumentException("file is not exists.");

            var pathHandle = new GCHandle<string>();
            pathHandle.Create(path);

            long fileSize = fileInfo.Length;

            this.Init(pathHandle, true, fileSize, bufferSize);
        }
        /// <summary>
        /// initializer of NativeTextStreamReader.
        /// this function is callable from workerthread.
        /// </summary>
        /// <param name="pathHandle">GCHandle of file path string.</param>
        /// <param name="disposePathHandle">dispose pathHandle when this struct will be disposed.</param>
        /// <param name="fileSize">file size.</param>
        /// <param name="bufferSize">internal buffer size (if < 0, buffering entire the file).</param>
        public void Init(GCHandle<string> pathHandle, bool disposePathHandle, long fileSize, int bufferSize)
        {
            _byteStream.Init(pathHandle, disposePathHandle, fileSize, bufferSize);

            _charBuff.Clear();
            _check_CR = false;
        }

        public Encoding CurrentEncoding { get { return _encoding.Target; } }
        /// <summary>
        /// setter for Encoding.
        /// this function must be called by main thread.
        /// use SetEncoding(Utility.GCHandle<Encoding> encodingHandle, Utility.GCHandle<Decoder> decoderHandle, NativeList<byte> preamble)
        /// for calling in JobSystem.
        /// </summary>
        /// <param name="encoding">Encoding object.</param>
        public void SetEncoding(Encoding encoding)
        {
            var encodingHandle = new GCHandle<Encoding>();
            var decoderHandle = new GCHandle<Decoder>();
            encodingHandle.Create(encoding);
            decoderHandle.Create(encoding.GetDecoder());
            var preamble = new NativeList<byte>(Allocator.Temp);
            foreach (byte b in encoding.GetPreamble())
            {
                preamble.Add(b);
            }
            this.SetEncoding(encodingHandle, decoderHandle, preamble, true);
            preamble.Dispose();
        }
        /// <summary>
        /// setter for Encoding.
        /// this function is callable from workerthread.
        /// </summary>
        /// <param name="encoding">GCHandle of Encoding object.</param>
        /// <param name="decoder">GCHandle of Decoder object.</param>
        /// <param name="preamble">byte sequence of preamble.</param>
        public void SetEncoding(GCHandle<Encoding> encodingHandle, GCHandle<Decoder> decoderHandle, NativeList<byte> preamble, bool disposeEncodingHandle)
        {
            if (_disposeEncodingHandle)
            {
                _encoding.Dispose();
                _decoder.Dispose();
            }
            _disposeEncodingHandle = disposeEncodingHandle;

            _preamble.Clear();

            _encoding = encodingHandle;
            _decoder = decoderHandle;
            _preamble.AddRange(preamble);

            _checkPreamble = (_preamble.Length > 0);
        }
        public bool EndOfStream { get { return (_byteStream.EndOfStream && _charBuff.Length == 0); } }

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
                if (_disposeEncodingHandle)
                {
                    _encoding.Dispose();
                    _decoder.Dispose();
                }

                _preamble.Dispose();

                _byteStream.Dispose();
                _charBuff.Dispose();

                _allocated = false;
            }
        }

        private unsafe bool IsPreamble()
        {
            _checkPreamble = false; // check at first only

            byte* ptr = (byte*)_byteStream.GetUnsafePtr();
            for (int i=0; i<_preamble.Length; i++)
            {
                if (_preamble[i] != *(ptr + i)) return false;
            }

            return true;
        }
        private unsafe int DecodeBuffer()
        {
            int byte_offset = 0;
            if (_checkPreamble && this.IsPreamble()) byte_offset = _preamble.Length;  // skip BOM

            int byte_len = _byteStream.BufferSize - byte_offset;
            if (byte_len < 0) throw new InvalidOperationException("internal error");

            byte* byte_ptr = (byte*)_byteStream.GetUnsafePtr() + byte_offset;
            int char_len = _decoder.Target.GetCharCount(byte_ptr, byte_len, false);

            _charBuff.Clear();
            if (char_len > _charBuff.Capacity) _charBuff.Capacity = char_len;
            _charBuff.ResizeUninitialized(char_len);

            _decoder.Target.GetChars(byte_ptr, byte_len, (char*)_charBuff.GetUnsafePtr(), char_len, false);
            return char_len;
        }
        private unsafe bool ReadLineFromCharBuffer(NativeList<char> buff)
        {
            // check '\r\n' is overlap between previous buffer and current buffer
            if (_check_CR && _charBuff.Length > 0)
            {
                if (_charBuff[0] == '\n') _charBuff.RemoveHead();

                //if (_charBuff[0] == '\n') Debug.LogWarning("  >> detect overlap \\r\\n");
            }

            if (_charBuff.Length == 0) return false;

            for(int i=0; i<_charBuff.Length; i++)
            {
                char ch = _charBuff[i];
                // detect ch = '\n' (unix), '\r\n' (DOS), or '\r' (Mac)
                if (ch == '\n' || ch == '\r')
                {
                    //Debug.Log("  ** found LF = " + ((int)ch).ToString() + ", i=" + i.ToString() + "/" + _charBuff.Length.ToString());
                    if(_charBuff[i] == '\n' && i > 0)
                    {
                        //Debug.Log("  ** before LF = " + ((int)_charBuff[i-1]).ToString());
                    }

                    if (i > 0) buff.AddRange(_charBuff.GetUnsafePtr(), i);

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
                            _check_CR = true;
                        }
                    }
                    else
                    {
                        _check_CR = false;
                    }
                    _charBuff.RemoveHead(i + 1);
                    return true;
                }
            }

            // line factor was not detect in charBuffer
            if(_charBuff.Length > 0) buff.AddRange(_charBuff.GetUnsafePtr(), _charBuff.Length);
            _charBuff.Clear();
            return false;
        }
        unsafe public void ReadLine(NativeList<char> buff, bool append = false)
        {
            if (!_allocated) throw new InvalidOperationException("the reader is not initialized.");
            if (this.EndOfStream) return;

            if (!append) buff.Clear();

            bool detect_line_factor = false;
            do
            {
                if (_charBuff.Length == 0 && !_byteStream.EndOfStream)
                {
                    int len = _byteStream.Read();
                    if (len < 0) return; // EOF
                    this.DecodeBuffer();
                }
                if (_charBuff.Length > 0)
                {
                    detect_line_factor = this.ReadLineFromCharBuffer(buff);
                }

                /*
                var sb = new StringBuilder();
                sb.Append("  -- Pos / Length = [" + _byteStream.Pos.ToString() + "/" + _byteStream.Length.ToString() + "]\n");
                sb.Append("  -- byteBuff size   = " + _byteStream.BufferSize.ToString() + "\n");
                sb.Append("  -- charBuff.Length = " + _charBuff.Length.ToString() + "\n");
                sb.Append("  -- detect LF = " + detect_line_factor.ToString() + "\n");
                sb.Append("  -- outBuff.Length  = " + buff.Length.ToString() + "\n");
                Debug.Log(sb.ToString());
                */

                if (detect_line_factor) return;
            } while (!EndOfStream);
        }
        public NativeList<char> ReadLine(Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            this.ReadLine(tmp);
            return tmp;
        }
        private unsafe void ReadBufferImpl(NativeList<char> buff, bool append = false)
        {
            if (!append) buff.Clear();

            if (_charBuff.Length == 0 && !_byteStream.EndOfStream)
            {
                int len = _byteStream.Read();
                if (len < 0) return; // EOF
                this.DecodeBuffer();
            }
            if (_charBuff.Length > 0)
            {
                // copy all _charBuff data
                buff.AddRange(_charBuff.GetUnsafePtr(), _charBuff.Length);
                _charBuff.Clear();
            }
        }
        public void ReadBuffer(NativeList<char> buff, bool append = false)
        {
            if (!_allocated) throw new InvalidOperationException("the reader is not initialized.");
            if (this.EndOfStream) return;

            this.ReadBufferImpl(buff, append);
        }
        public NativeList<char> ReadBuffer(Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            this.ReadBuffer(tmp);
            return tmp;
        }
        public void ReadToEnd(NativeList<char> buff)
        {
            if (!_allocated) throw new InvalidOperationException("the reader is not initialized.");
            if (this.EndOfStream) return;

            buff.Clear();
            do
            {
                this.ReadBufferImpl(buff, true);
            } while (!EndOfStream);
        }
        public NativeList<char> ReadToEnd(Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            this.ReadToEnd(tmp);
            return tmp;
        }
    }
}
