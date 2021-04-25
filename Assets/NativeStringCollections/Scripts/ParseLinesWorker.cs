using System;
using System.Text;
using System.Runtime.InteropServices;

using UnityEngine;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;


namespace NativeStringCollections.Impl
{
    using NativeStringCollections.Utility;

    [BurstCompile]
    internal unsafe static class LineParserBurst
    {
        private delegate void ParseLinesDelegate(ParseLinesWorker.Info* info,
                                                 ref UnsafeRefToNativeList<Char16> continueBuff,
                                                 ref UnsafeRefToNativeHeadRemovableList<Char16> charBuff,
                                                 ref UnsafeRefToNativeStringList lines,
                                                 out int line_count);
        private static ParseLinesDelegate _parseLinesDelegate;

        [RuntimeInitializeOnLoadMethod]
        static void Initialize()
        {
            _parseLinesDelegate = BurstCompiler.CompileFunctionPointer<ParseLinesDelegate>(GetLinesBurst).Invoke;
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(ParseLinesDelegate))]
        private static unsafe void GetLinesBurst(ParseLinesWorker.Info* info,
                                                 ref UnsafeRefToNativeList<Char16> continueBuff,
                                                 ref UnsafeRefToNativeHeadRemovableList<Char16> charBuff,
                                                 ref UnsafeRefToNativeStringList lines,
                                                 out int line_count)
        {
            GetLinesImpl(info, continueBuff, charBuff, lines, out line_count);
        }

        internal static unsafe void GetLinesImpl(ParseLinesWorker.Info* info,
                                                 UnsafeRefToNativeList<Char16> continueBuff,
                                                 UnsafeRefToNativeHeadRemovableList<Char16> charBuff,
                                                 UnsafeRefToNativeStringList lines,
                                                 out int line_count)
        {
            // move continue buffer data into head of new charBuff
            if (continueBuff.Length > 0)
            {
                charBuff.InsertHead((Char16*)continueBuff.GetUnsafePtr(), continueBuff.Length);
                continueBuff.Clear();
            }

            line_count = 0;
            while (true)
            {
                // read charBuff by line
                bool detect_line_factor = ParseLineImpl(info, charBuff, lines);
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
            if (charBuff.Length > 0)
            {
                continueBuff.Clear();
                continueBuff.AddRange(charBuff.GetUnsafePtr(), charBuff.Length);

                /*
                var sb = new StringBuilder();
                sb.Append("TextDecoder >> continueBuff:\n");
                for (int m = 0; m < _continueBuff.Length; m++) sb.Append(_continueBuff[m]);
                Debug.Log(sb.ToString());
                */
            }
            charBuff.Clear();
        }
        private static unsafe bool ParseLineImpl(ParseLinesWorker.Info* info,
                                                 UnsafeRefToNativeHeadRemovableList<Char16> charBuff,
                                                 UnsafeRefToNativeStringList lines)
        {
            // check '\r\n' is overlap between previous buffer and current buffer
            if (info->check_CR && charBuff.Length > 0)
            {
                if (charBuff[0] == UTF16CodeSet.code_LF) charBuff.RemoveHead();
            }

            int len_chars = charBuff.Length;
            Char16* ptr_chars = (Char16*)charBuff.GetUnsafePtr();

            if (len_chars == 0) return false;

            for (int i = 0; i < len_chars; i++)
            {
                Char16 ch = ptr_chars[i];
                // detect ch = '\n' (unix), '\r\n' (DOS), or '\r' (Mac)
                if (ch == UTF16CodeSet.code_LF || ch == UTF16CodeSet.code_CR)
                {
                    /*
                    //Debug.Log("  ** found LF = " + ((int)ch).ToString() + ", i=" + i.ToString() + "/" + charBuff.Length.ToString());
                    if (_charBuff[i] == '\n' && i > 0)
                    {
                        //Debug.Log("  ** before LF = " + ((int)charBuff[i-1]).ToString());
                    }
                    //*/

                    lines.Add(ptr_chars, i);

                    if (ch == UTF16CodeSet.code_CR)
                    {
                        if (i + 1 < len_chars)
                        {
                            if (ptr_chars[i + 1] == UTF16CodeSet.code_LF)
                            {
                                i++;
                                //Debug.Log("  ** found CRLF");
                            }
                        }
                        else
                        {
                            // check '\r\n' or not on the head of next buffer
                            //Debug.LogWarning("  >> checking overlap CRLF");
                            info->check_CR = true;
                        }
                    }
                    else
                    {
                        info->check_CR = false;
                    }
                    charBuff.RemoveHead(i + 1);
                    return true;
                }
            }
            return false;
        }

        public static int GetLines(ParseLinesWorker worker, NativeStringList lines)
        {
            var ref_continueBuff = worker._continueBuff.GetUnsafeRef();
            var ref_charBuff = worker._charBuff.GetUnsafeRef();
            var ref_lines = lines.GetUnsafeRef();
            _parseLinesDelegate(worker._info.Target,
                                ref ref_continueBuff,
                                ref ref_charBuff,
                                ref ref_lines,
                                out int line_count);
            return line_count;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ParseLinesWorker : IDisposable
    {
        internal NativeHeadRemovableList<Char16> _charBuff;
        internal NativeList<Char16> _continueBuff;

        internal NativeList<byte> _preamble;

        internal struct Info
        {
            public Boolean checkPreamble;
            public Boolean check_CR;
        }
        internal PtrHandle<Info> _info;

        public unsafe ParseLinesWorker(Allocator alloc)
        {
            _charBuff = new NativeHeadRemovableList<Char16>(alloc);
            _continueBuff = new NativeList<Char16>(alloc);

            _preamble = new NativeList<byte>(alloc);
            _info = new PtrHandle<Info>(alloc);
        }
        public void Dispose()
        {
            _charBuff.Dispose();
            _continueBuff.Dispose();

            _preamble.Dispose();
            _info.Dispose();
        }

        public unsafe void Clear()
        {
            _charBuff.Clear();
            _continueBuff.Clear();

            _info.Target->checkPreamble = true;
            _info.Target->check_CR = false;
        }

        public unsafe void SetPreamble(byte[] preamble)
        {
            _preamble.Clear();
            foreach (byte b in preamble) _preamble.Add(b);

            this.Clear();
        }

        public bool IsEmpty
        {
            get
            {
                return (_charBuff.Length == 0 && _continueBuff.Length == 0);
            }
        }

        public unsafe void DecodeTextIntoBuffer(byte* ptr_source, int len, GCHandle<Decoder> decoder)
        {
            this.GetCharsImpl(ptr_source, len, decoder);
        }
        public unsafe int GetLines(NativeStringList lines)
        {
            return this.GetLinesImpl(lines);
        }
        public unsafe void GetChars(NativeList<Char16> chars)
        {
            this.GetInternalBuffer(chars);
        }
        public unsafe void GetInternalBuffer(NativeList<Char16> chars)
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
            LineParserBurst.GetLinesImpl(_info.Target,
                                         _continueBuff.GetUnsafeRef(),
                                         _charBuff.GetUnsafeRef(),
                                         lines.GetUnsafeRef(),
                                         out int line_count);
            return line_count;
        }
        private unsafe void GetCharsImpl(byte* ptr, int len, GCHandle<Decoder> decoder)
        {
            int byte_offset = 0;
            if (this.IsPreamble(ptr, len)) byte_offset = _preamble.Length;  // skip BOM

            byte* byte_ptr = ptr + byte_offset;
            int byte_len = len - byte_offset;
            if (byte_len < 0) throw new ArgumentException("Internal error: invalid buffer size.");

            int char_len = decoder.Target.GetCharCount(byte_ptr, byte_len, false);

            _charBuff.Shrink();
            int left_data_len = _charBuff.Length;
            int new_buff_len = char_len + left_data_len;
            if (new_buff_len > _charBuff.Capacity) _charBuff.Capacity = new_buff_len;
            _charBuff.ResizeUninitialized(new_buff_len);

            char* ptr_write_st = (char*)_charBuff.GetUnsafePtr() + left_data_len;
            decoder.Target.GetChars(byte_ptr, byte_len, ptr_write_st, char_len, false);
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