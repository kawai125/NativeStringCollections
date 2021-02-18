// enable the below macro to enable reallocation trace for debug.
//#define NATIVE_STRING_COLLECTION_TRACE_REALLOCATION

using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections
{
    using NativeStringCollections.Impl;
    using NativeStringCollections.Utility;


    public enum Endian
    {
        Big,
        Little,
    }

    public unsafe interface IParseExt
    {
        int Length { get; }
        void* GetUnsafePtr();
    }

    public static class StringParserExt
    {
        /// <summary>
        /// Try to parse StringEntity to bool. Cannot accept whitespaces (this is differ from official C# bool.TryParse()).
        /// </summary>
        public unsafe static bool TryParse(this IParseExt source, out bool result)
        {
            int len_source = source.Length;
            char* ptr_source = (char*)source.GetUnsafePtr();
            if (len_source == 5)
            {
                // match "False", "false", or "FALSE"
                if(ptr_source[0] == 'F')
                {
                    if (ptr_source[1] == 'a' &&
                        ptr_source[2] == 'l' &&
                        ptr_source[3] == 's' &&
                        ptr_source[4] == 'e')
                    {
                        result = false;
                        return true;
                    }
                    else if (ptr_source[1] == 'A' &&
                        ptr_source[2] == 'L' &&
                        ptr_source[3] == 'S' &&
                        ptr_source[4] == 'E')
                    {
                        result = false;
                        return true;
                    }
                }
                else if (ptr_source[0] == 'f' &&
                    ptr_source[1] == 'a' &&
                    ptr_source[2] == 'l' &&
                    ptr_source[3] == 's' &&
                    ptr_source[4] == 'e')
                {
                    result = false;
                    return true;
                }
            }
            else if (len_source == 4)
            {
                // match "True", "true", or "TRUE"
                if(ptr_source[0] == 'T')
                {
                    if (ptr_source[1] == 'r' &&
                        ptr_source[2] == 'u' &&
                        ptr_source[3] == 'e')
                    {
                        result = true;
                        return true;
                    }
                    else if (ptr_source[1] == 'R' &&
                        ptr_source[2] == 'U' &&
                        ptr_source[3] == 'E')
                    {
                        result = true;
                        return true;
                    }
                }
                else if (ptr_source[0] == 't' &&
                    ptr_source[1] == 'r' &&
                    ptr_source[2] == 'u' &&
                    ptr_source[3] == 'e')
                {
                    result = true;
                    return true;
                }
            }
            result = false;
            return false;
        }
        /// <summary>
        /// Try to parse StringEntity to Int32. Cannot accept whitespaces & hex format (this is differ from official C# int.TryParse()). Use TryParseHex(out T) for hex data.
        /// </summary>
        public unsafe static bool TryParse(this IParseExt source, out int result)
        {
            const int max_len = 10;

            int len_source = source.Length;
            char* ptr_source = (char*)source.GetUnsafePtr();

            result = 0;
            if (len_source <= 0) return false;

            int i_start = 0;
            if (ptr_source[0].IsSign(out int sign)) i_start = 1;

            int digit_count = 0;
            int MSD = 0;

            int tmp = 0;
            for (int i = i_start; i <len_source; i++)
            {
                if (ptr_source[i].IsDigit(out int d))
                {
                    if (digit_count > 0) digit_count++;

                    if (MSD == 0 && d > 0)
                    {
                        MSD = d;
                        digit_count = 1;
                    }

                    tmp = tmp * 10 + d;

                    if (digit_count == max_len)
                    {
                        if (MSD > 2) return false;
                        if (sign == 1 && tmp < 0) return false;
                        if (sign == -1 && (tmp - 1) < 0) return false;
                    }
                    if (digit_count > max_len) return false;
                }
                else
                {
                    return false;
                }
            }

            result = sign * tmp;
            return true;
        }
        /// <summary>
        /// Try to parse StringEntity to Int64. Cannot accept whitespaces & hex format (this is differ from official C# long.TryParse()). Use TryParseHex(out T) for hex data.
        /// </summary>
        public unsafe static bool TryParse(this IParseExt source, out long result)
        {
            const int max_len = 19;

            int len_source = source.Length;
            char* ptr_source = (char*)source.GetUnsafePtr();

            result = 0;
            if (len_source <= 0) return false;

            int i_start = 0;
            if (ptr_source[0].IsSign(out int sign)) i_start = 1;

            int digit_count = 0;
            int MSD = 0;

            long tmp = 0;
            for (int i = i_start; i < len_source; i++)
            {
                if (ptr_source[i].IsDigit(out int d))
                {
                    if (digit_count > 0) digit_count++;

                    if (MSD == 0 && d > 0)
                    {
                        MSD = d;
                        digit_count = 1;
                    }

                    tmp = tmp * 10 + d;

                    if (digit_count == max_len)
                    {
                        if (sign == 1 && tmp < 0L) return false;
                        if (sign == -1 && (tmp - 1L) < 0L) return false;
                    }
                    if (digit_count > max_len) return false;
                }
                else
                {
                    return false;
                }
            }

            result = sign * tmp;
            return true;
        }
        /// <summary>
        /// Try to parse StringEntity to float. Cannot accept whitespaces, comma insertion, and hex format (these are differ from official C# float.TryParse()).
        /// Use TryParseHex(out T) for hex data.
        /// </summary>
        public static bool TryParse(this IParseExt source, out float result)
        {
            result = 0.0f;
            if (source.Length <= 0) return false;

            if (!source.TryParse(out double tmp)) return false;

            float f_cast = (float)tmp;
            if (float.IsInfinity(f_cast)) return false;

            result = f_cast;
            return true;
        }
        /// <summary>
        /// Try to parse StringEntity to double. Cannot accept whitespaces, comma insertion, and hex format (these are differ from official C# double.TryParse()).
        /// Use TryParseHex(out T) for hex data.
        /// </summary>
        public unsafe static bool TryParse(this IParseExt source, out double result)
        {
            result = 0.0;
            if (source.Length <= 0) return false;
            if (!source.TryParseFloatFormat(out int sign, out int i_start, out int dot_pos, out int exp_pos, out int n_pow)) return false;

            //UnityEngine.Debug.Log("i_start=" + i_start.ToString() + ", dot_pos=" + dot_pos.ToString() + ", exp_pos=" + exp_pos.ToString() + ", n_pow=" + n_pow.ToString());

            char* ptr_source = (char*)source.GetUnsafePtr();

            double mantissa = 0.0;
            for (int i = exp_pos - 1; i >= i_start; i--)
            {
                if (i == dot_pos) continue;
                mantissa = mantissa * 0.1 + (double)ptr_source[i].ToInt();
            }

            int m_pow = 0;
            if (dot_pos > i_start + 1) m_pow = dot_pos - i_start - 1;
            if (dot_pos == i_start) m_pow = -1;

            double tmp = mantissa * (sign * math.pow(10.0, n_pow + m_pow));
            if (double.IsInfinity(tmp)) return false;

            result = tmp;
            return true;
        }

        private unsafe static bool TryParseFloatFormat(this IParseExt source,
                                                       out int sign,
                                                       out int i_start,
                                                       out int dot_pos,
                                                       out int exp_pos,
                                                       out int n_pow)
        {
            int len_source = source.Length;
            char* ptr_source = (char*)source.GetUnsafePtr();

            i_start = 0;
            if (ptr_source[0].IsSign(out sign)) i_start = 1;

            // eat zeros on the head
            for(int i=i_start; i<len_source; i++)
            {
                char c = ptr_source[i];
                if(c == '0')
                {
                    i_start++;
                }
                else
                {
                    break;
                }
            }

            dot_pos = -1;
            exp_pos = -1;

            n_pow = 0;
            int exp_sign = 1;

            int digit_count = 0;
            int zero_count = 0;
            bool non_zero_found = false;

            int dummy;

            // format check
            for (int i = i_start; i < len_source; i++)
            {
                char c = ptr_source[i];
                //UnityEngine.Debug.Log("parse format: i=" + i.ToString() + ", c=" + c.ToString());
                if (c.IsDigit(out dummy))
                {
                    if (exp_pos == -1) digit_count++;
                    if (dummy != 0) non_zero_found = true;
                    if (!non_zero_found && exp_pos == -1 && dummy == 0) zero_count++;
                    // do nothing
                    //UnityEngine.Debug.Log("c is number");
                }
                else if (c.IsDot())
                {
                    if (dot_pos != -1 || dot_pos == i_start + 1) return false;
                    if (exp_pos > 0 && i >= exp_pos) return false;
                    dot_pos = i;
                    //UnityEngine.Debug.Log("c is dot");
                }
                else if (c.IsExp())
                {
                    //UnityEngine.Debug.Log("c is EXP");

                    //if (exp_pos != -1) return false;
                    //if (i <= i_start + 1) return false;
                    //if (source.Length - i < 2) return false;  // [+/-] & 1 digit or lager EXP num.
                    if (exp_pos != -1 ||
                        i == i_start + 1 ||
                        (len_source - i) < 2 ||
                        !ptr_source[i + 1].IsSign(out exp_sign)) return false;

                    exp_pos = i;
                }
                else if(c.IsSign(out dummy))
                {
                    if (i != exp_pos + 1) return false;
                    // do nothing (exp_sign is read in IsExp() check.)
                }
                else
                {
                    //UnityEngine.Debug.Log("failure to parse");
                    return false;
                }
            }

            // decode exp part
            if(exp_pos > 0)
            {
                if (len_source - (exp_pos + 2) > 8) return false;  // capacity of int
                for (int i = exp_pos + 2; i < len_source; i++)
                {
                    n_pow = n_pow * 10 + ptr_source[i].ToInt();
                    //UnityEngine.Debug.Log("n_pow(1)=" + n_pow.ToString());
                }
                n_pow *= exp_sign;
            }
            else
            {
                // no [e/E+-[int]] part
                exp_pos = len_source;
            }

            if (dot_pos < 0) dot_pos = exp_pos;

            //UnityEngine.Debug.Log("ParseFloatFormat=" + true.ToString());

            return true;
        }

        unsafe public static bool TryParseHex(this IParseExt source, out int result, Endian endian = Endian.Little)
        {
            if (source.TryParseHex32(out uint buf, endian))
            {
                result = *(int*)&buf;
                return true;
            }
            else
            {
                result = 0;
                return false;
            }
        }
        unsafe public static bool TryParseHex(this IParseExt source, out long result, Endian endian = Endian.Little)
        {
            if (source.TryParseHex64(out ulong buf, endian))
            {
                result = *(long*)&buf;
                return true;
            }
            else
            {
                result = 0;
                return false;
            }
        }
        unsafe public static bool TryParseHex(this IParseExt source, out float result, Endian endian = Endian.Little)
        {
            if (source.TryParseHex32(out uint buf, endian))
            {
                result = *(float*)&buf;
                return true;
            }
            else
            {
                result = 0.0f;
                return false;
            }
        }
        unsafe public static bool TryParseHex(this IParseExt source, out double result, Endian endian = Endian.Little)
        {
            if (source.TryParseHex64(out ulong buf, endian))
            {
                result = *(double*)&buf;
                return true;
            }
            else
            {
                result = 0.0;
                return false;
            }
        }

        private unsafe static bool TryParseHex32(this IParseExt source, out uint buf, Endian endian)
        {
            const int n_digits = 8;  // accepts 8 digits set (4bit * 8)

            int len_source = source.Length;
            char* ptr_source = (char*)source.GetUnsafePtr();

            int i_start = 0;
            buf = 0;

            if (!(len_source == n_digits || len_source == n_digits + 2)) return false;

            if (ptr_source[0] == '0' && ptr_source[1] == 'x') i_start = 2;

            if(endian == Endian.Big)
            {
                for (int i = i_start; i < len_source; i++)
                {
                    if (ptr_source[i].IsHex(out uint h))
                    {
                        buf = (buf << 4) | h;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else if(endian == Endian.Little)
            {
                int n_byte = len_source / 2;
                int i_last = i_start / 2;
                for(int i = n_byte - 1; i>=i_last; i--)
                {
                    char c0 = ptr_source[2 * i];
                    char c1 = ptr_source[2 * i + 1];

                    if(c0.IsHex(out uint h0) && c1.IsHex(out uint h1))
                    {
                        buf = (buf << 4) | h0;
                        buf = (buf << 4) | h1;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        private unsafe static bool TryParseHex64(this IParseExt source, out ulong buf, Endian endian)
        {
            const int n_digits = 16;  // accepts 16 digits set (4bit * 16)

            int len_source = source.Length;
            char* ptr_source = (char*)source.GetUnsafePtr();

            int i_start = 0;
            buf = 0;

            if (!(len_source == n_digits || len_source == n_digits + 2)) return false;

            if (ptr_source[0] == '0' && ptr_source[1] == 'x') i_start = 2;

            if (endian == Endian.Big)
            {
                for (int i = i_start; i < len_source; i++)
                {
                    if (ptr_source[i].IsHex(out uint h))
                    {
                        buf = (buf << 4) | h;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else if (endian == Endian.Little)
            {
                int n_byte = len_source / 2;
                int i_last = i_start / 2;
                for (int i = n_byte - 1; i >= i_last; i--)
                {
                    char c0 = ptr_source[2 * i];
                    char c1 = ptr_source[2 * i + 1];

                    if (c0.IsHex(out uint h0) && c1.IsHex(out uint h1))
                    {
                        buf = (buf << 4) | h0;
                        buf = (buf << 4) | h1;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Check the value is integral value or not.
        /// </summary>
        public static bool IsIntegral(this IParseExt value)
        {
            return value.TryParse(out long dummy);
        }
        /// <summary>
        /// Check the value is numeric value or not.
        /// </summary>
        public static bool IsNumeric(this IParseExt value)
        {
            return value.TryParse(out double dummy);
        }
        /// <summary>
        /// Check the value is 32bit hex data or not.
        /// </summary>
        public static bool IsHex32(this IParseExt value)
        {
            return value.TryParseHex32(out uint dummy, Endian.Little);
        }
        /// <summary>
        /// Check the value is 64bit hex data or not.
        /// </summary>
        public static bool IsHex64(this IParseExt value)
        {
            return value.TryParseHex64(out ulong dummy, Endian.Little);
        }
    }

    internal struct Base64Info
    {
        public uint store;
        public int bytePos;
        public Boolean insertLF;

        public void Clear()
        {
            store = 0;
            bytePos = 0;
        }
    }

    /// <summary>
    /// The Encoder for MIME Base64 (RFC 2045).
    /// </summary>
    public struct NativeBase64Encoder : IDisposable
    {
        private Base64EncodeMap _map;
        private PtrHandle<Base64Info> _info;

        public unsafe NativeBase64Encoder(Allocator alloc)
        {
            _map = new Base64EncodeMap(alloc);
            _info = new PtrHandle<Base64Info>(alloc);
            _info.Target->Clear();
            _info.Target->insertLF = true;
        }
        public unsafe void Clear()
        {
            _info.Target->Clear();
        }
        /// <summary>
        /// Inserting CRLF or not.
        /// </summary>
        public unsafe bool InsertLineBrakes
        {
            get { return _info.Target->insertLF; }
            set { _info.Target->insertLF = value; }
        }
        /// <summary>
        /// convert bytes into chars in Base64 format.
        /// </summary>
        /// <param name="buff">output</param>
        /// <param name="bytes">source</param>
        /// <param name="splitData">additional bytes will be input or not. (false: call Terminate() internally.)</param>
        public unsafe void GetChars(NativeList<char> buff, NativeArray<byte> bytes, bool splitData = false)
        {
            this.GetChars(buff, (byte*)bytes.GetUnsafePtr(), bytes.Length, splitData);
        }
        /// <summary>
        /// convert bytes into chars in Base64 format.
        /// </summary>
        /// <param name="buff">output</param>
        /// <param name="bytes">source</param>
        /// <param name="splitData">additional bytes will be input or not. (false: call Terminate() internally.)</param>
        public unsafe void GetChars(NativeList<char> buff, NativeList<byte> bytes, bool splitData = false)
        {
            this.GetChars(buff, (byte*)bytes.GetUnsafePtr(), bytes.Length, splitData);
        }
        /// <summary>
        /// convert bytes into chars in Base64 format.
        /// </summary>
        /// <param name="buff">output</param>
        /// <param name="byte_ptr">source ptr</param>
        /// <param name="byte_len">source length</param>
        /// <param name="splitData">additional bytes will be input or not. (false: call Terminate() internally.</param>
        public unsafe void GetChars(NativeList<char> buff, byte* byte_ptr, int byte_len, bool splitData = false)
        {
            if (byte_len < 0) throw new ArgumentOutOfRangeException("invalid bytes length.");

            uint store = _info.Target->store;
            int bytePos = _info.Target->bytePos;

            int charcount = 0;
            for(uint i=0; i<byte_len; i++)
            {
                if (_info.Target->insertLF)
                {
                    if (charcount == Base64Const.LineBreakPos)
                    {
                        buff.Add('\r');
                        buff.Add('\n');
                        charcount = 0;
                    }
                }

                store = (store << 8) | byte_ptr[i];
                bytePos++;

                // encoding 3 bytes -> 4 chars
                if(bytePos == 3)
                {
                    buff.Add(_map[(store & 0xfc0000) >> 18]);
                    buff.Add(_map[(store & 0x03f000) >> 12]);
                    buff.Add(_map[(store & 0x000fc0) >>  6]);
                    buff.Add(_map[(store & 0x00003f)]);
                    charcount += 4;

                    store = 0;
                    bytePos = 0;
                }
            }

            _info.Target->store = store;
            _info.Target->bytePos = bytePos;

            if (!splitData) this.Terminate(buff);
        }
        /// <summary>
        /// apply termination treatment.
        /// </summary>
        /// <param name="buff">output</param>
        public unsafe void Terminate(NativeList<char> buff)
        {
            uint tmp = _info.Target->store;
            switch (_info.Target->bytePos)
            {
                case 0:
                    // do nothing
                    break;
                case 1:
                    // two character padding needed
                    buff.Add(_map[(tmp & 0xfc) >> 2]);
                    buff.Add(_map[(tmp & 0x03) << 4]);
                    buff.Add(_map[64]);  // pad
                    buff.Add(_map[64]);  // pad
                    break;
                case 2:
                    // one character padding needed
                    buff.Add(_map[(tmp & 0xfc00) >> 10]);
                    buff.Add(_map[(tmp & 0x03f0) >>  4]);
                    buff.Add(_map[(tmp & 0x000f) <<  2]);
                    buff.Add(_map[64]);  // pad
                    break;
            }
            _info.Target->store = 0;
            _info.Target->bytePos = 0;
        }
        public void Dispose()
        {
            _map.Dispose();
            _info.Dispose();
        }
    }
    /// <summary>
    /// The Decoder for MIME Base64 (RFC 2045).
    /// </summary>
    public struct NativeBase64Decoder : IDisposable
    {
        private Base64DecodeMap _map;
        private PtrHandle<Base64Info> _info;

        public unsafe NativeBase64Decoder(Allocator alloc)
        {
            _map = new Base64DecodeMap(alloc);
            _info = new PtrHandle<Base64Info>(alloc);
            _info.Target->Clear();
        }
        public void Dispose()
        {
            _map.Dispose();
            _info.Dispose();
        }
        public unsafe void Clear()
        {
            _info.Target->Clear();
        }
        /// <summary>
        /// convert Base64 format chars into bytes.
        /// </summary>
        /// <param name="buff">output</param>
        /// <param name="str">source</param>
        public unsafe void GetBytes(NativeList<byte> buff, NativeArray<char> str)
        {
            this.GetBytes(buff, (char*)str.GetUnsafePtr(), str.Length);
        }
        /// <summary>
        /// convert Base64 format chars into bytes.
        /// </summary>
        /// <param name="buff">output</param>
        /// <param name="str">source</param>
        public unsafe void GetBytes(NativeList<byte> buff, NativeList<char> str)
        {
            this.GetBytes(buff, (char*)str.GetUnsafePtr(), str.Length);
        }
        /// <summary>
        /// convert Base64 format chars into bytes.
        /// </summary>
        /// <param name="buff">output</param>
        /// <param name="str">source</param>
        public unsafe void GetBytes(NativeList<byte> buff, IJaggedArraySliceBase<char> str)
        {
            this.GetBytes(buff, (char*)str.GetUnsafePtr(), str.Length);
        }
        /// <summary>
        /// convert Base64 format chars into bytes.
        /// </summary>
        /// <param name="buff">output</param>
        /// <param name="char_ptr">source ptr</param>
        /// <param name="char_len">source length</param>
        public unsafe void GetBytes(NativeList<byte> buff, char* char_ptr, int char_len)
        {
            if (char_len < 0) throw new ArgumentOutOfRangeException("invalid chars length.");

            uint store = _info.Target->store;
            int bytePos = _info.Target->bytePos;

            for(int i=0; i<char_len; i++)
            {
                char c = char_ptr[i];
                if (this.IsWhiteSpace(c)) continue;

                if(c == '=')
                {
                    switch (bytePos)
                    {
                        case 0:
                        case 1:
                            
                            /*
                            var sb = new StringBuilder();
                            sb.Append("bytePos = " + bytePos.ToString() + '\n');
                            sb.Append("i = " + i.ToString() + "\n\n");
                            sb.Append("decoded:\n");
                            for(int j=0; j<buff.Length; j++)
                            {
                                sb.Append(buff[j].ToString() + ' ');
                            }
                            sb.Append('\n');
                            sb.Append("currect char: " + c + '\n');
                            sb.Append("left char: " + c + '\n');
                            int c_count = 0;
                            for (int j=i+1; j<char_len; j++)
                            {
                                sb.Append(char_ptr[j]);
                                c_count++;
                                if(c_count == 16)
                                {
                                    sb.Append('\n');
                                    c_count = 0;
                                }
                            }
                            sb.Append('\n');
                            UnityEngine.Debug.Log(sb);
                            */

                            throw new ArgumentException("invalid padding detected.");
                            //break;
                        case 2:
                            // pick 1 byte from "**==" code
                            buff.Add((byte)((store & 0x0ff0) >> 4));
                            bytePos = 0;
                            break;
                        case 3:
                            // pick 2 byte from "***=" code
                            buff.Add((byte)((store & 0x03fc00) >> 10));
                            buff.Add((byte)((store & 0x0003fc) >>  2));
                            bytePos = 0;
                            break;
                    }
                    return;
                }
                else
                {
                    uint b = _map[c];
                    if (b != 255)
                    {
                        store = (store << 6) | (b & 0x3f);
                        bytePos++;
                    }
                }

                if(bytePos == 4)
                {
                    buff.Add((byte)((store & 0xff0000) >> 16));
                    buff.Add((byte)((store & 0x00ff00) >>  8));
                    buff.Add((byte)((store & 0x0000ff)));
                    store = 0;
                    bytePos = 0;
                }
            }
            _info.Target->store = store;
            _info.Target->bytePos = bytePos;
        }
        private bool IsWhiteSpace(char c)
        {
            return (c == ' ' || c == '\t' || c == '\n' || c == '\r');
        }
    }

    namespace Impl
    {

        static class CharExt
        {
            public static bool IsSign(this char c, out int sign)
            {
                if (c == '-')
                {
                    sign = -1;
                    return true;
                }
                else if (c == '+')
                {
                    sign = 1;
                    return true;
                }
                sign = 1;
                return false;
            }
            public static bool IsDigit(this char c, out int digit)
            {
                if ('0' <= c && c <= '9')
                {
                    digit = c.ToInt();
                    return true;
                }
                digit = 0;
                return false;
            }
            public static bool IsHex(this char c, out uint hex)
            {
                if (c.IsDigit(out int d))
                {
                    hex = (uint)d;
                    return true;
                }
                else if ('A' <= c && c <= 'F')
                {
                    hex = (uint)(c - 'A' + 10);
                    return true;
                }
                else if ('a' <= c && c <= 'f')
                {
                    hex = (uint)(c - 'a' + 10);
                    return true;
                }
                hex = 0;
                return false;
            }
            public static bool IsDot(this char c)
            {
                if (c == '.') return true;
                return false;
            }
            public static bool IsExp(this char c)
            {
                if (c == 'e' || c == 'E') return true;
                return false;
            }
            public static int ToInt(this char c)
            {
                return c - '0';
            }
        }

        static class Base64Ext
        {
            public static bool ByteToBase64Char(this byte b, out char c)
            {
                c = ' ';
                if (b >= 64) return false;

                if(b <= 25)
                {
                    c = (char)('A' + b);
                }
                else if( b <= 51)
                {
                    c = (char)('a' + b);
                }
                else if(b <= 61)
                {
                    c = (char)('0' + (b - 52));
                }
                else if(b == 62)
                {
                    c = '+';
                }
                else if(b == 63)
                {
                    c = '/';
                }
                else
                {
                    return false;
                }
                return true;
            }
            public static bool Base64CharToByte(this char c, out byte b)
            {
                b = 0;

                if('A' <= c && c <= 'Z')
                {
                    b = (byte)(c - 'A');
                }
                else if('a' <= c && c <= 'z')
                {
                    b = (byte)(c - 'a' + 26);
                }
                else if ('0' <= c && c <= '9')
                {
                    b = (byte)(c - '0' + 52);
                }
                else if (c == '+')
                {
                    b = 62;
                }
                else if (c == '/')
                {
                    b = 63;
                }
                else
                {
                    return false;
                }

                return true;
            }
        }

        internal struct Base64EncodeMap : IDisposable
        {
            private NativeArray<byte> _map;

            public Base64EncodeMap(Allocator alloc)
            {
                _map = new NativeArray<byte>(65, alloc);

                int i = 0;
                for(byte j=65; j<=90; j++)  // 'A' ~ 'Z'
                {
                    _map[i] = j;
                    i++;
                }
                for(byte j=97; j<=122; j++) // 'a' ~ 'z'
                {
                    _map[i] = j;
                    i++;
                }
                for(byte j=48; j<=57; j++)  // '0' ~ '9'
                {
                    _map[i] = j;
                    i++;
                }
                _map[i] = 43; i++; // '+'
                _map[i] = 47; i++; // '/'
                _map[i] = 61;      // '='
            }
            public void Dispose()
            {
                _map.Dispose();
            }
            public char this[uint index]
            {
                get
                {
                    if (index > 65) throw new ArgumentOutOfRangeException("input byte must be in range [0x00, 0x40].");
                    return (char)_map[(int)index];
                }
            }
        }
        internal struct Base64DecodeMap : IDisposable
        {
            private NativeArray<byte> _map;
            public Base64DecodeMap(Allocator alloc)
            {
                _map = new NativeArray<byte>(80, alloc);

                int i = 0;
                _map[i] = 62; i++;       // 0x2b, '+'
                for(int j=0; j<3; j++)
                {
                    _map[i] = 255; i++;  // invalid code
                }
                _map[i] = 63; i++;       // 0x2f, '/'
                for(byte j=52; j<=61; j++)
                {
                    _map[i] = j; i++;    // '0' ~ '9'
                }
                for(byte j=0; j<7; j++)
                {
                    _map[i] = 255; i++;  // invalid code
                }
                for(byte j=0; j<=25; j++)
                {
                    _map[i] = j; i++;    // 'A' ~ 'Z'
                }
                for(byte j=0; j<6; j++)
                {
                    _map[i] = 255; i++;  // invalid code
                }
                for (byte j = 26; j <= 51; j++)
                {
                    _map[i] = j; i++;    // 'a' ~ 'z'
                }
            }
            public void Dispose()
            {
                _map.Dispose();
            }

            public byte this[uint index]
            {
                get
                {
                    if (index < 0x2b) return 255;
                    if (index > 0x7a) return 255;

                    return _map[(int)(index - 0x2b)];
                }
            }

        }
    }
}
