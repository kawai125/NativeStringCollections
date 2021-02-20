using System;
using System.IO;
using System.Text;

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
        private TextDecoder _decoder;

        private NativeStringList _lines;

        private long _fileSize;
        private int _blockNum;
        private int _blockPos;
        private int _byteLength;

        private bool _allocated = false;
        private bool _disposeStream = false;

        public NativeTextStreamReader(Allocator alloc)
        {
            _byteBuffer = new byte[Define.DefaultBufferSize];

            _decoder = new TextDecoder(alloc);
            _lines = new NativeStringList(alloc);

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
                _decoder.Dispose();
                _lines.Dispose();
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

            _decoder.Clear();
        }

        public Encoding Encoding
        {
            get { return _encoding; }
            set
            {
                _encoding = value;
                _decoder.SetEncoding(_encoding);
            }
        }

        public int Length { get { return _blockNum; } }
        public int Pos { get { return _blockPos; } }
        public bool EndOfStream { get { return (_blockPos == _blockNum && _decoder.IsEmpty && _lines.Length == 0); } }


        private void ReadStream()
        {
            if (_blockPos >= _blockNum) return;

            _byteLength = _fileStream.Read(_byteBuffer, 0, _byteBuffer.Length);

            _blockPos++;
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

        private unsafe void ReadLinesImpl(IAddResult result, int max_lines)
        {
            int line_count = 0;

            line_count += this.PickLinesImpl(result, max_lines);
            if (line_count == max_lines) return;

            _lines.Clear();
            while (line_count < max_lines)
            {
                if (_blockPos < _blockNum && _decoder.IsEmpty)
                {
                    this.ReadStream();
                    fixed(byte* byte_ptr = _byteBuffer)
                    {
                        _decoder.GetLines(ref _lines, byte_ptr, _byteLength);
                    }
                }

                line_count += this.PickLinesImpl(result, max_lines - line_count);
                if (line_count == max_lines) return;

                if (_blockPos == _blockNum)
                {
                    // if final part do not have LF.
                    if (!_decoder.IsEmpty)
                    {
                        var buff = new NativeList<char>(Allocator.Temp);
                        _decoder.GetInternalBuffer(buff);
                        result.AddResult((char*)buff.GetUnsafePtr(), buff.Length);
                        buff.Dispose();
                    }

                    // reach EOF
                    break;
                }
            }
        }
        private unsafe int PickLinesImpl(IAddResult result, int max_lines)
        {
            int line_count = 0;
            if (_lines.Length > 0)
            {
                int n_lines = Math.Min(_lines.Length, max_lines);
                for (int i = 0; i < n_lines; i++)
                {
                    var se = _lines[i];
                    result.AddResult((char*)se.GetUnsafePtr(), se.Length);
                    line_count++;
                }
                _lines.RemoveRange(0, n_lines);
            }
            return line_count;
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

            if (_blockPos < _blockNum)
            {
                this.ReadStream();
                fixed (byte* byte_ptr = _byteBuffer)
                {
                    _decoder.GetLines(ref _lines, byte_ptr, _byteLength);
                }
            }

            // read all lines in buffer
            this.PickLinesImpl(ret, int.MaxValue);
            _lines.Clear();

            if (_blockPos == _blockNum)
            {
                // if final part do not have LF.
                if (!_decoder.IsEmpty)
                {
                    using (var buff = new NativeList<char>(Allocator.Temp))
                    {
                        _decoder.GetInternalBuffer(buff);
                        result.Add((char*)buff.GetUnsafePtr(), buff.Length);
                    }
                }
            }
        }
        public unsafe void ReadBuffer(NativeList<char> result)
        {
            if (_blockPos < _blockNum)
            {
                this.ReadStream();
                fixed (byte* byte_ptr = _byteBuffer)
                {
                    _decoder.GetChars(result, byte_ptr, _byteLength);
                }
            }

            if (!_decoder.IsEmpty)
            {
                _decoder.GetInternalBuffer(result);
            }
        }
        public unsafe void ReadToEnd(NativeStringList result)
        {
            while (_blockPos <= _blockNum)
            {
                this.ReadLinesFromBuffer(result);
            }
        }
        public unsafe void ReadToEnd(NativeList<char> result)
        {
            if (!_decoder.IsEmpty)
            {
                _decoder.GetInternalBuffer(result);
            }
            while (_blockPos < _blockNum)
            {
                this.ReadBuffer(result);
            }
        }
    }
}
