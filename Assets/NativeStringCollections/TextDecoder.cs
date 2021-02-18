using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections.Impl
{
    using NativeStringCollections.Utility;

    internal struct TextDecoder : IDisposable
    {
        private NativeHeadRemovableList<char> _charBuff;
        private NativeList<char> _continueBuff;

        private NativeList<byte> _preamble;
        private GCHandle<Decoder> _decoder;

        private struct Info
        {
            public Boolean checkPreamble;
            public Boolean allocHandle;
            public Boolean check_CR;
        }
        private PtrHandle<Info> _info;

        public unsafe TextDecoder(Allocator alloc)
        {
            _charBuff = new NativeHeadRemovableList<char>(alloc);
            _continueBuff = new NativeList<char>(alloc);

            _preamble = new NativeList<byte>(alloc);
            _info = new PtrHandle<Info>(alloc);
        }
        public void Dispose()
        {
            this.ReleaseDecoder();

            _charBuff.Dispose();
            _continueBuff.Dispose();

            _preamble.Dispose();
            _info.Dispose();
        }
        public unsafe void ReleaseDecoder()
        {
            if (_info.Target->allocHandle)
            {
                _decoder.Dispose();
                _info.Target->allocHandle = false;
            }
        }

        public unsafe void Clear()
        {
            _charBuff.Clear();
            _continueBuff.Clear();

            _decoder.Target.Reset();

            _info.Target->checkPreamble = true;
            _info.Target->check_CR = false;
        }

        public unsafe void SetEncoding(Encoding encoding)
        {
            _decoder.Create(encoding.GetDecoder());

            _preamble.Clear();
            foreach (byte b in encoding.GetPreamble()) _preamble.Add(b);

            _info.Target->allocHandle = true;
            this.Clear();
        }

        public bool IsEmpty
        {
            get
            {
                return (_charBuff.Length == 0 && _continueBuff.Length == 0);
            }
        }

        public unsafe int GetLines(NativeStringList lines, byte* ptr, int len)
        {
            // get new chars from byte buffer
            this.GetCharsImpl(ptr, len);

            return this.GetLinesImpl(lines);
        }
        public unsafe void GetChars(NativeList<char> chars, byte* ptr, int len)
        {
            this.GetInternalBuffer(chars);

            this.GetCharsImpl(ptr, len);

            chars.AddRange(_charBuff.GetUnsafePtr(), _charBuff.Length);
            _charBuff.Clear();
        }
        public unsafe void GetInternalBuffer(NativeList<char> chars)
        {
            if (_continueBuff.Length > 0)
            {
                chars.AddRange(_continueBuff.GetUnsafePtr(), _continueBuff.Length);
                _continueBuff.Clear();
            }

            if (_charBuff.Length > 0)
            {
                chars.AddRange(_charBuff.GetUnsafePtr(), _charBuff.Length);
                _charBuff.Clear();
            }
        }
        private unsafe int GetLinesImpl(NativeStringList lines)
        {
            // move continue buffer data into head of new charBuff
            if (_continueBuff.Length > 0)
            {
                _charBuff.InsertHead((char*)_continueBuff.GetUnsafePtr(), _continueBuff.Length);
                _continueBuff.Clear();
            }

            int line_count = 0;
            while (true)
            {
                // read charBuff by line
                bool detect_line_factor = this.ParseLineImpl(ref lines);
                if (detect_line_factor)
                {
                    line_count++;
                }
                else
                {
                    break;
                }
            }

            // move left charBuff data into continue buffer
            if (_charBuff.Length > 0)
            {
                _continueBuff.Clear();
                _continueBuff.AddRange(_charBuff.GetUnsafePtr(), _charBuff.Length);

                /*
                var sb = new StringBuilder();
                sb.Append("TextDecoder >> continueBuff:\n");
                for (int m = 0; m < _continueBuff.Length; m++) sb.Append(_continueBuff[m]);
                Debug.Log(sb.ToString());
                */
            }
            _charBuff.Clear();
            return line_count;
        }
        private unsafe bool ParseLineImpl(ref NativeStringList lines)
        {
            // check '\r\n' is overlap between previous buffer and current buffer
            if (_info.Target->check_CR && _charBuff.Length > 0)
            {
                if (_charBuff[0] == '\n') _charBuff.RemoveHead();
            }

            int len_chars = _charBuff.Length;
            char* ptr_chars = (char*)_charBuff.GetUnsafePtr();

            if (len_chars == 0) return false;

            for (int i = 0; i < len_chars; i++)
            {
                char ch = ptr_chars[i];
                // detect ch = '\n' (unix), '\r\n' (DOS), or '\r' (Mac)
                if (ch == '\n' || ch == '\r')
                {
                    /*
                    //Debug.Log("  ** found LF = " + ((int)ch).ToString() + ", i=" + i.ToString() + "/" + _charBuff.Length.ToString());
                    if (_charBuff[i] == '\n' && i > 0)
                    {
                        //Debug.Log("  ** before LF = " + ((int)_charBuff[i-1]).ToString());
                    }
                    //*/

                    lines.Add(ptr_chars, i);

                    if (ch == '\r')
                    {
                        if (i + 1 < len_chars)
                        {
                            if (ptr_chars[i + 1] == '\n')
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
            return false;
        }
        private unsafe void GetCharsImpl(byte* ptr, int len)
        {
            if (!_info.Target->allocHandle) throw new InvalidOperationException("internal error: null decoder.");

            int byte_offset = 0;
            if (this.IsPreamble(ptr, len)) byte_offset = _preamble.Length;  // skip BOM

            byte* byte_ptr = ptr + byte_offset;
            int byte_len = len - byte_offset;
            if (byte_len < 0) throw new ArgumentException("Internal error: invalid buffer size.");

            int char_len = _decoder.Target.GetCharCount(byte_ptr, byte_len, false);

            _charBuff.Clear();
            if (char_len > _charBuff.Capacity) _charBuff.Capacity = char_len;
            _charBuff.ResizeUninitialized(char_len);

            _decoder.Target.GetChars(byte_ptr, byte_len, (char*)_charBuff.GetUnsafePtr(), char_len, false);
        }
        private unsafe bool IsPreamble(byte* ptr, int len)
        {
            if (!_info.Target->checkPreamble) return false;
            if (_preamble.Length > len) return false;
            _info.Target->checkPreamble = false; // check at first only

            for (int i = 0; i < _preamble.Length; i++)
            {
                if (_preamble[i] != ptr[i]) return false;
            }

            return true;
        }
    }
}