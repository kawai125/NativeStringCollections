using System;
using System.Text;
using System.Runtime.InteropServices;

using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.IO.LowLevel.Unsafe;


namespace NativeStringCollections.Deprected
{
    using NativeStringCollections.Utility;
    using NativeStringCollections.Impl;
    using NativeStringCollections.Deprected.Impl;

    namespace Impl
    {
        internal struct TextStreamReaderInfo
        {
            public Boolean checkPreamble;
            public Boolean check_CR;
            public Boolean allocated;

            public ReadHandle readHandle;
            public Boolean disposeEncodingHandle;
            public Boolean disposeReadHandle;

            public JobHandle jobHandle;
        }
    }

    /// <summary>
    ///  the System.IO.StreamReader like wrapper for Unity.IO.LowLevel.Unsafe.AsyncReadManager
    /// </summary>
    public unsafe struct NativeTextStreamReader : IDisposable
    {
        private GCHandle<Encoding> _encoding;
        private GCHandle<Decoder> _decoder;

        private NativeList<byte> _preamble;
        private NativeByteStreamReader _byteStream;
        private NativeHeadRemovableList<char> _charBuff;
        private NativeList<char> _continueBuff;

        private PtrHandle<TextStreamReaderInfo> _info;


        public NativeTextStreamReader(Allocator alloc)
        {
            _encoding = new GCHandle<Encoding>();
            _decoder = new GCHandle<Decoder>();

            _info = new PtrHandle<TextStreamReaderInfo>(alloc);

            _preamble = new NativeList<byte>(alloc);
            _byteStream = new NativeByteStreamReader(alloc);
            _charBuff = new NativeHeadRemovableList<char>(128, alloc);
            _continueBuff = new NativeList<char>(alloc);

            _info.Target->checkPreamble = false;
            _info.Target->allocated = true;

            _info.Target->disposeEncodingHandle = false;
            _info.Target->disposeReadHandle = false;
        }
        /// <summary>
        /// initializer of NativeTextStreamReader.
        /// this function must be called by main thread. use Init(Utility.GCHandle<string>, long fileSize, int buffersize) for calling in JobSystem.
        /// </summary>
        /// <param name="path">file path.</param>
        /// <param name="encoding">encoding object.</param>
        /// <param name="bufferSize">internal buffer size (if < 0, buffering entire the file).</param>
        public void Init(string path, Encoding encoding, int bufferSize = Define.DefaultBufferSize)
        {
            this.Init(path, bufferSize);
            this.SetEncoding(encoding);
        }
        /// <summary>
        /// initializer of NativeTextStreamReader.
        /// this function must be called by main thread.
        /// use Init(Impl.GCHandle<string>, long fileSize, int buffersize) for calling in JobSystem.
        /// </summary>
        /// <param name="path">file path.</param>
        /// <param name="bufferSize">internal buffer size (if < 0, buffering entire the file).</param>
        public void Init(string path, int bufferSize = Define.DefaultBufferSize)
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
            this.CheckReadHandle();

            _byteStream.Init(pathHandle, disposePathHandle, fileSize, bufferSize);

            _charBuff.Clear();
            _info.Target->check_CR = false;
        }
        private void CheckReadHandle()
        {
            if (_info.Target->disposeReadHandle)
            {
                if (!_info.Target->readHandle.JobHandle.IsCompleted)
                {
                    throw new InvalidOperationException("previous read job is not completed. call Complete() before re-initialize reader.");
                }
                _info.Target->readHandle.Dispose();
                _info.Target->disposeReadHandle = false;
            }
        }

        public int Length { get { return _byteStream.Length; } }
        public int Pos { get { return _byteStream.Pos; } }

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
            if (_info.Target->disposeEncodingHandle)
            {
                _encoding.Dispose();
                _decoder.Dispose();
            }
            _info.Target->disposeEncodingHandle = disposeEncodingHandle;

            _preamble.Clear();

            _encoding = encodingHandle;
            _decoder = decoderHandle;
            _preamble.AddRange(preamble);

            _info.Target->checkPreamble = (_preamble.Length > 0);
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
            if (_info.Target->allocated)
            {
                if (_info.Target->disposeEncodingHandle)
                {
                    _encoding.Dispose();
                    _decoder.Dispose();
                }

                // finalize internal jobHandle & readHandle if needed.
                if (!_info.Target->jobHandle.IsCompleted)
                {
                    _info.Target->jobHandle.Complete();
                }
                if (_info.Target->disposeReadHandle)
                {
                    _info.Target->readHandle.Dispose();
                }

                _preamble.Dispose();

                _byteStream.Dispose();
                _charBuff.Dispose();
                _continueBuff.Dispose();

                _info.Dispose();
            }
            _info.Target->allocated = false;
        }

        private struct DecodeJob : IJob
        {
            [ReadOnly] public NativeList<byte> preamble;
            public PtrHandle<TextStreamReaderInfo> info;

            public GCHandle<Decoder> decoder;
            [ReadOnly] public NativeByteStreamReader byteStream;

            public NativeHeadRemovableList<char> charBuff;

            private unsafe bool IsPreamble()
            {
                if (!info.Target->checkPreamble) return false;
                info.Target->checkPreamble = false;  // check at head of file only.

                if (byteStream.BufferSize < preamble.Length) return false;

                byte* ptr = (byte*)byteStream.GetUnsafeReadOnlyPtr();
                for(int i=0; i<preamble.Length; i++)
                {
                    if (ptr[i] != preamble[i]) return false;
                }

                return true;
            }

            public unsafe void Execute()
            {
                int byte_offset = 0;
                if (this.IsPreamble()) byte_offset += preamble.Length;   // skip BOM

                int byte_len = byteStream.BufferSize - byte_offset;
                if (byte_len < 0) throw new InvalidOperationException("decoding error. file length is smaller than preamble(BOM).");

                byte* byte_ptr = (byte*)byteStream.GetUnsafeReadOnlyPtr() + byte_offset;
                int char_len = decoder.Target.GetCharCount(byte_ptr, byte_len, false);

                charBuff.Clear();
                if (char_len > charBuff.Capacity) charBuff.Capacity = char_len;
                charBuff.ResizeUninitialized(char_len);

                decoder.Target.GetChars(byte_ptr, byte_len, (char*)charBuff.GetUnsafePtr(), char_len, false);

                decoder.Dispose();  // release decoder handle
            }
        }
        private struct ReadLinesJob : IJob
        {
            public PtrHandle<TextStreamReaderInfo> info;
            [ReadOnly] public int maxLines;

            public NativeHeadRemovableList<char> charBuff;
            public NativeList<char> continueBuff;

            [WriteOnly] public NativeStringList result;

            private unsafe bool ReadLineFromCharBuffer()
            {
                // check '\r\n' is overlap between previous buffer and current buffer
                if (info.Target->check_CR && charBuff.Length > 0)
                {
                    if (charBuff[0] == '\n') charBuff.RemoveHead();

                    //if (charBuff[0] == '\n') Debug.LogWarning("  >> detect overlap \\r\\n");
                }

                if (charBuff.Length == 0) return false;

                for (int i = 0; i < charBuff.Length; i++)
                {
                    char ch = charBuff[i];
                    // detect ch = '\n' (unix), '\r\n' (DOS), or '\r' (Mac)
                    if (ch == '\n' || ch == '\r')
                    {
                        //Debug.Log("  ** found LF = " + ((int)ch).ToString() + ", i=" + i.ToString() + "/" + _charBuff.Length.ToString());
                        if (charBuff[i] == '\n' && i > 0)
                        {
                            //Debug.Log("  ** before LF = " + ((int)_charBuff[i-1]).ToString());
                        }

                        if (i > 0) result.Add((char*)charBuff.GetUnsafePtr(), i);

                        if (ch == '\r')
                        {
                            if (i + 1 < charBuff.Length)
                            {
                                if (charBuff[i + 1] == '\n')
                                {
                                    i++;
                                    //Debug.Log("  ** found CRLF");
                                }
                            }
                            else
                            {
                                // check '\r\n' or not on the head of next buffer
                                //Debug.LogWarning("  >> checking overlap CRLF");
                                info.Target->check_CR = true;
                            }
                        }
                        else
                        {
                            info.Target->check_CR = false;
                        }
                        charBuff.RemoveHead(i + 1);
                        return true;
                    }
                }

                // line factor was not detect in charBuffer
                return false;
            }

            public void Execute()
            {
                // move continue buffer data into head of new charBuff
                if (continueBuff.Length > 0)
                {
                    charBuff.InsertHead(continueBuff.GetUnsafePtr(), continueBuff.Length);
                    continueBuff.Clear();
                }

                bool detect_line_factor = true;
                for(int i_line=0; i_line<maxLines; i_line++)
                {
                    // read charBuff by line
                    detect_line_factor = this.ReadLineFromCharBuffer();
                    if (!detect_line_factor) break;
                }

                // LF was not found in charBuff
                if (!detect_line_factor)
                {
                    // move left charBuff data into continue buffer
                    if (charBuff.Length > 0)
                    {
                        continueBuff.Clear();
                        continueBuff.AddRange(charBuff.GetUnsafePtr(), charBuff.Length);
                    }
                    charBuff.Clear();
                }
            }
        }

        private struct CopyCharBuffJob : IJob
        {
            public NativeHeadRemovableList<char> charBuff;
            public NativeList<char> result;

            public unsafe void Execute()
            {
                result.AddRange(charBuff.GetUnsafePtr(), charBuff.Length);
                charBuff.Clear();
            }
        }
        private JobHandle GenerateReadJob()
        {
            var ret = new JobHandle();
            if (_info.Target->disposeReadHandle)
            {
                _info.Target->readHandle.Dispose();
                _info.Target->disposeReadHandle = false;
            }

            if (!_byteStream.EndOfStream && _charBuff.Length == 0)
            {
                _info.Target->readHandle = _byteStream.ReadAsync();
                _info.Target->disposeReadHandle = true;

                ret = _info.Target->readHandle.JobHandle;
            }
            return ret;
        }
        private JobHandle GenerateDecodeJob(JobHandle job_handle)
        {
            // decode job
            var decode_job = new DecodeJob();
            decode_job.preamble = _preamble;
            decode_job.info = _info;
            decode_job.decoder.Create(_decoder.Target);
            decode_job.byteStream = _byteStream;
            decode_job.charBuff = _charBuff;
            return decode_job.Schedule(job_handle);
        }
        public JobHandle ReadLineAsync(NativeStringList result, bool append = false)
        {

            return this.ReadLinesFromBufferAsync(result, append, 1);
        }
        public JobHandle ReadLinesFromBufferAsync(NativeStringList result, bool append = false, int max_lines = Int32.MaxValue)
        {
            UnityEngine.Debug.Log("ReadLinesFromBufferAsync() was called.");
            if (!append) result.Clear();

            // read byteStream job
            var read_job_handle = this.GenerateReadJob();

            // decode job
            var decode_job_handle = this.GenerateDecodeJob(read_job_handle);

            //return decode_job_handle;

            //++++
            //  この ReadLinesJob を Schedule() すると ReadLinesJob.Execute() に到達する前に
            //  decode_job_handle 内の decoder の参照に失敗する (NullReferenceExeption)
            //  この ReadLinesJob のみをコメントアウトするとエラーなく実行される。
            //  原因の見当がつかないので API としては ReadBufferAsync() のみを公開する
            //++++
            // read lines job
            var read_lines_job = new ReadLinesJob();
            read_lines_job.info = _info;
            read_lines_job.maxLines = max_lines;
            read_lines_job.charBuff = _charBuff;
            read_lines_job.continueBuff = _continueBuff;
            read_lines_job.result = result;
            var job = read_lines_job.Schedule(decode_job_handle);
            //++++

            _info.Target->jobHandle = job;
            return job;
        }
        public JobHandle ReadBufferAsync(NativeList<char> result, bool append = false)
        {
            if (!append) result.Clear();

            // read byteStream job
            var read_job_handle = this.GenerateReadJob();

            // decode job
            var decode_job_handle = this.GenerateDecodeJob(read_job_handle);

            // read lines job
            var copy_charBuff_job = new CopyCharBuffJob();
            copy_charBuff_job.charBuff = _charBuff;
            copy_charBuff_job.result = result;
            var job = copy_charBuff_job.Schedule(decode_job_handle);

            _info.Target->jobHandle = job;
            return job;
        }

        private unsafe bool IsPreamble()
        {
            _info.Target->checkPreamble = false; // check at first only

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
            if (_info.Target->checkPreamble && this.IsPreamble()) byte_offset = _preamble.Length;  // skip BOM

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
        private unsafe bool ReadLineFromCharBuffer(NativeList<char> result)
        {
            // check '\r\n' is overlap between previous buffer and current buffer
            if (_info.Target->check_CR && _charBuff.Length > 0)
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

                    if (i > 0) result.AddRange(_charBuff.GetUnsafePtr(), i);

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

            // line factor was not detect in charBuffer
            if(_charBuff.Length > 0) result.AddRange(_charBuff.GetUnsafePtr(), _charBuff.Length);
            _charBuff.Clear();
            return false;
        }
        unsafe public void ReadLine(NativeList<char> result, bool append = false)
        {
            if (!_info.Target->allocated) throw new InvalidOperationException("the reader is not initialized.");
            if (this.EndOfStream) return;

            if (!append) result.Clear();

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
                    detect_line_factor = this.ReadLineFromCharBuffer(result);
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
            var ret = new NativeList<char>(alloc);
            this.ReadLine(ret);
            return ret;
        }
        private unsafe void ReadBufferImpl(NativeList<char> result, bool append = false)
        {
            if (!append) result.Clear();

            if (_charBuff.Length == 0 && !_byteStream.EndOfStream)
            {
                int len = _byteStream.Read();
                if (len < 0) return; // EOF
                this.DecodeBuffer();
            }
            if (_charBuff.Length > 0)
            {
                // copy all _charBuff data
                result.AddRange(_charBuff.GetUnsafePtr(), _charBuff.Length);
                _charBuff.Clear();
            }
        }
        public void ReadBuffer(NativeList<char> result, bool append = false)
        {
            if (!_info.Target->allocated) throw new InvalidOperationException("the reader is not initialized.");
            if (this.EndOfStream) return;

            this.ReadBufferImpl(result, append);
        }
        public NativeList<char> ReadBuffer(Allocator alloc)
        {
            var ret = new NativeList<char>(alloc);
            this.ReadBuffer(ret);
            return ret;
        }
        public void ReadToEnd(NativeList<char> result)
        {
            if (!_info.Target->allocated) throw new InvalidOperationException("the reader is not initialized.");
            if (this.EndOfStream) return;

            result.Clear();
            do
            {
                this.ReadBufferImpl(result, true);
            } while (!EndOfStream);
        }
        public NativeList<char> ReadToEnd(Allocator alloc)
        {
            var ret = new NativeList<char>(alloc);
            this.ReadToEnd(ret);
            return ret;
        }
    }
}
