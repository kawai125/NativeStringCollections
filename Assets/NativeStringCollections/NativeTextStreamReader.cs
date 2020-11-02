using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections
{
    using NativeStringCollections.Utility;
    using NativeStringCollections.Impl;

    /// <summary>
    ///  the System.IO.StreamReader like wrapper using NativeContainer.
    /// </summary>
    public class NativeTextStreamReader : IDisposable
    {
        private byte[] _byteBuffer;

        private FileStream _fileStream;
        private string _path;

        private Encoding _encoding;
        private Decoder _decoder;

        private NativeList<byte> _preamble;
        private NativeHeadRemovableList<char> _charBuff;
        private NativeList<char> _continueBuff;

        private long _fileSize;
        private int _blockNum;
        private int _blockPos;
        private int _byteLength;

        private bool _checkPreamble;
        private bool _check_CR;
        private bool _allocated = false;
        private bool _disposeStream = false;

        public NativeTextStreamReader(Allocator alloc)
        {
            _byteBuffer = new byte[Define.DefaultBufferSize];

            _preamble = new NativeList<byte>(alloc);
            _charBuff = new NativeHeadRemovableList<char>(alloc);
            _continueBuff = new NativeList<char>(alloc);
            _allocated = true;

            this.Encoding = Encoding.Default;
        }

        public void Dispose()
        {
            if (_disposeStream)
            {
                _fileStream.Dispose();
            }
            if (_allocated)
            {
                _preamble.Dispose();
                _charBuff.Dispose();
                _continueBuff.Dispose();
            }
        }
        public void Init(string path, Encoding encoding, int bufferSize = Define.DefaultBufferSize)
        {
            this.Encoding = encoding;
            this.Init(path, bufferSize);
        }
        public void Init(string path, int bufferSize = Define.DefaultBufferSize)
        {
            var fileInfo = new System.IO.FileInfo(path);
            if (!fileInfo.Exists) throw new ArgumentException("file is not exists.");

            _path = path;
            _fileSize = fileInfo.Length;

            bufferSize = Math.Max(bufferSize, Define.MinBufferSize);
            Array.Resize<byte>(ref _byteBuffer, bufferSize);

            if(_fileSize <= _byteBuffer.Length)
            {
                _blockNum = 1;
            }
            else
            {
                _blockNum = (int)(_fileSize / _byteBuffer.Length) + 1;
            }
            _blockPos = 0;
            _byteLength = 0;

            if (_disposeStream) _fileStream.Dispose();
            _fileStream = new FileStream(_path, FileMode.Open, FileAccess.Read);
            _disposeStream = true;

            _decoder.Reset();
            _check_CR = false;

            _continueBuff.Clear();
            _charBuff.Clear();
        }

        public Encoding Encoding
        {
            get { return _encoding; }
            set
            {
                _encoding = value;
                _decoder = _encoding.GetDecoder();

                var tmp = _encoding.GetPreamble();
                _preamble.Clear();
                foreach (byte b in tmp) _preamble.Add(b);

                if (_preamble.Length > 0) _checkPreamble = true;
            }
        }

        public int Length { get { return _blockNum; } }
        public int Pos { get { return _blockPos; } }
        public bool EndOfStream { get { return (_blockPos == _blockNum && _charBuff.Length == 0); } }


        private void ReadStream()
        {
            if (_blockPos >= _blockNum) return;

            _byteLength = _fileStream.Read(_byteBuffer, 0, _byteBuffer.Length);

            _blockPos++;
        }
        private unsafe bool IsPreamble()
        {
            if (!_checkPreamble) return false;
            if (_preamble.Length > _byteLength) return false;
            _checkPreamble = false; // check at first only

            for (int i = 0; i < _preamble.Length; i++)
            {
                if (_preamble[i] != _byteBuffer[i]) return false;
            }

            return true;
        }
        private unsafe void DecodeBuffer()
        {
            int byte_offset = 0;
            if (this.IsPreamble()) byte_offset = _preamble.Length;  // skip BOM

            int byte_len = _byteLength - byte_offset;
            if (byte_len < 0) throw new InvalidOperationException("internal error");

            fixed(byte* byte_ptr = _byteBuffer)
            {
                byte* data_ptr = byte_ptr + byte_offset;
                int char_len = _decoder.GetCharCount(data_ptr, byte_len, false);

                _charBuff.Clear();
                if (char_len > _charBuff.Capacity) _charBuff.Capacity = char_len;
                _charBuff.ResizeUninitialized(char_len);

                _decoder.GetChars(data_ptr, byte_len, (char*)_charBuff.GetUnsafePtr(), char_len, false);
            }
        }

        private unsafe interface IAddResult
        {
            void AddResult(char* ptr, int len);
        }

        // result container
        private struct Result_NSL : IAddResult
        {
            public NativeStringList result;

            public unsafe void AddResult(char* ptr, int len)
            {
                result.Add(ptr, len);
            }
        }
        private struct Result_NL_Char : IAddResult
        {
            public NativeList<char> result;

            public unsafe void AddResult(char* ptr, int len)
            {
                result.AddRange((void*)ptr, len);
            }
        }
        private unsafe bool ParseLineFromCharBuffer(IAddResult result)
        {
            // check '\r\n' is overlap between previous buffer and current buffer
            if (_check_CR && _charBuff.Length > 0)
            {
                if (_charBuff[0] == '\n') _charBuff.RemoveHead();

                //if (_charBuff[0] == '\n') Debug.LogWarning("  >> detect overlap \\r\\n");
            }

            if (_charBuff.Length == 0) return false;

            for (int i = 0; i < _charBuff.Length; i++)
            {
                char ch = _charBuff[i];
                // detect ch = '\n' (unix), '\r\n' (DOS), or '\r' (Mac)
                if (ch == '\n' || ch == '\r')
                {
                    //Debug.Log("  ** found LF = " + ((int)ch).ToString() + ", i=" + i.ToString() + "/" + _charBuff.Length.ToString());
                    if (_charBuff[i] == '\n' && i > 0)
                    {
                        //Debug.Log("  ** before LF = " + ((int)_charBuff[i-1]).ToString());
                    }

                    if (i > 0) result.AddResult((char*)_charBuff.GetUnsafePtr(), i);

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
            return false;
        }
        private unsafe int ReadLinesFromCharBuffer(IAddResult result, int max_lines)
        {
            // move continue buffer data into head of new charBuff
            if (_continueBuff.Length > 0)
            {
                _charBuff.InsertHead(_continueBuff.GetUnsafePtr(), _continueBuff.Length);
                _continueBuff.Clear();
            }

            bool detect_line_factor = false;
            int line_count = 0;
            for (int i_line = 0; i_line < max_lines; i_line++)
            {
                // read charBuff by line
                detect_line_factor = this.ParseLineFromCharBuffer(result);
                if (detect_line_factor)
                {
                    line_count++;
                }
                else
                {
                    break;
                }
            }

            // LF was not found in charBuff
            if (!detect_line_factor)
            {
                // move left charBuff data into continue buffer
                if (_charBuff.Length > 0)
                {
                    _continueBuff.Clear();
                    _continueBuff.AddRange(_charBuff.GetUnsafePtr(), _charBuff.Length);
                }
                _charBuff.Clear();
            }
            return line_count;
        }
        private unsafe void ReadLinesImpl(IAddResult result, int max_lines)
        {
            int line_count = 0;
            while (line_count < max_lines)
            {
                if(_blockPos < _blockNum && _charBuff.Length == 0)
                {
                    this.ReadStream();
                    this.DecodeBuffer();
                }

                line_count += this.ReadLinesFromCharBuffer(result, max_lines);

                if (_blockPos == _blockNum)
                {
                    // if final part do not have LF.
                    if(_continueBuff.Length > 0)
                    {
                        result.AddResult((char*)_continueBuff.GetUnsafePtr(), _continueBuff.Length);
                    }

                    // reach EOF
                    break;
                }
            }
        }

        public void ReadLine(NativeList<char> result)
        {
            var ret = new Result_NL_Char();
            ret.result = result;
            this.ReadLinesImpl(ret, 1);
        }
        public void ReadLine(NativeStringList result)
        {
            var ret = new Result_NSL();
            ret.result = result;
            this.ReadLinesImpl(ret, 1);
        }
        public unsafe void ReadLinesFromBuffer(NativeStringList result)
        {
            var ret = new Result_NSL();
            ret.result = result;

            if (_blockPos < _blockNum && _charBuff.Length == 0)
            {
                this.ReadStream();
                this.DecodeBuffer();
            }

            this.ReadLinesFromCharBuffer(ret, _charBuff.Length);  // read all lines in buffer

            if (_blockPos == _blockNum)
            {
                // if final part do not have LF.
                if (_continueBuff.Length > 0)
                {
                    result.Add((char*)_continueBuff.GetUnsafePtr(), _continueBuff.Length);
                }
            }
        }
        public unsafe void ReadBuffer(NativeList<char> result)
        {
            if (_blockPos < _blockNum && _charBuff.Length == 0)
            {
                this.ReadStream();
                this.DecodeBuffer();
            }

            if(_continueBuff.Length > 0)
            {
                result.AddRange(_continueBuff.GetUnsafePtr(), _continueBuff.Length);
                _continueBuff.Clear();
            }

            result.AddRange(_charBuff.GetUnsafePtr(), _charBuff.Length);
            _charBuff.Clear();
        }
        public unsafe void ReadToEnd(NativeStringList result)
        {
            while (_blockPos < _blockNum)
            {
                this.ReadLinesFromBuffer(result);
            }
            if(_continueBuff.Length > 0)
            {
                result.Add((char*)_continueBuff.GetUnsafePtr(), _continueBuff.Length);
                _continueBuff.Clear();
            }
        }
        public unsafe void ReadToEnd(NativeList<char> result)
        {
            if (_continueBuff.Length > 0)
            {
                result.AddRange((char*)_continueBuff.GetUnsafePtr(), _continueBuff.Length);
                _continueBuff.Clear();
            }
            while (_blockPos < _blockNum)
            {
                this.ReadBuffer(result);
            }
        }
    }
}
