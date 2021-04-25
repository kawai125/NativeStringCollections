using System;
using System.Diagnostics;

using UnityEngine;

using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;


namespace NativeStringCollections
{
    using NativeStringCollections.Impl;
    using NativeStringCollections.Utility;

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
        internal PtrHandle<Base64Info> _info;

        public unsafe NativeBase64Encoder(Allocator alloc)
        {
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
        public unsafe void GetChars(NativeList<Char16> buff, NativeArray<byte> bytes, bool splitData = false)
        {
            GetCharsImpl(_info.Target,
                         buff.GetUnsafeRef(),
                         (byte*)bytes.GetUnsafePtr(), bytes.Length, splitData);
        }
        /// <summary>
        /// convert bytes into chars in Base64 format.
        /// </summary>
        /// <param name="buff">output</param>
        /// <param name="bytes">source</param>
        /// <param name="splitData">additional bytes will be input or not. (false: call Terminate() internally.)</param>
        public unsafe void GetChars(NativeList<Char16> buff, NativeList<byte> bytes, bool splitData = false)
        {
            GetCharsImpl(_info.Target,
                         buff.GetUnsafeRef(),
                         (byte*)bytes.GetUnsafePtr(), bytes.Length, splitData);
        }
        /// <summary>
        /// convert bytes into chars in Base64 format.
        /// </summary>
        /// <param name="buff">output</param>
        /// <param name="byte_ptr">source ptr</param>
        /// <param name="byte_len">source length</param>
        /// <param name="splitData">additional bytes will be input or not. (false: call Terminate() internally.</param>
        public unsafe void GetChars(NativeList<Char16> buff, byte* byte_ptr, int byte_len, bool splitData = false)
        {
            GetCharsImpl(_info.Target,
                         buff.GetUnsafeRef(),
                         byte_ptr, byte_len, splitData);
        }
        /// <summary>
        /// apply termination treatment.
        /// </summary>
        /// <param name="buff">output</param>
        public unsafe void Terminate(NativeList<Char16> buff)
        {
            TerminateImpl(_info.Target, buff.GetUnsafeRef());
        }
        public void Dispose()
        {
            _info.Dispose();
        }

        internal static unsafe void GetCharsImpl(Base64Info* ptr_info,
                                                 UnsafeRefToNativeList<Char16> buff,
                                                 byte* byte_ptr, int byte_len, bool splitData)
        {
            CheckLengthIsPositive(byte_len);

            uint store = ptr_info->store;
            int bytePos = ptr_info->bytePos;

            int charcount = 0;
            for (uint i = 0; i < byte_len; i++)
            {
                if (ptr_info->insertLF)
                {
                    if (charcount == Base64Const.LineBreakPos)
                    {
                        buff.Add(UTF16CodeSet.code_CR);
                        buff.Add(UTF16CodeSet.code_LF);
                        charcount = 0;
                    }
                }

                store = (store << 8) | byte_ptr[i];
                bytePos++;

                // encoding 3 bytes -> 4 chars
                if (bytePos == 3)
                {
                    buff.Add(Base64EncodeTable.Get((store & 0xfc0000) >> 18));
                    buff.Add(Base64EncodeTable.Get((store & 0x03f000) >> 12));
                    buff.Add(Base64EncodeTable.Get((store & 0x000fc0) >> 6));
                    buff.Add(Base64EncodeTable.Get((store & 0x00003f)));
                    charcount += 4;

                    store = 0;
                    bytePos = 0;
                }
            }

            ptr_info->store = store;
            ptr_info->bytePos = bytePos;

            if (!splitData) TerminateImpl(ptr_info, buff);
        }
        internal static unsafe void TerminateImpl(Base64Info* ptr_info, UnsafeRefToNativeList<Char16> buff)
        {
            uint tmp = ptr_info->store;
            switch (ptr_info->bytePos)
            {
                case 0:
                    // do nothing
                    break;
                case 1:
                    // two character padding needed
                    buff.Add(Base64EncodeTable.Get((tmp & 0xfc) >> 2));
                    buff.Add(Base64EncodeTable.Get((tmp & 0x03) << 4));
                    buff.Add(Base64EncodeTable.Get(64));  // pad
                    buff.Add(Base64EncodeTable.Get(64));  // pad
                    break;
                case 2:
                    // one character padding needed
                    buff.Add(Base64EncodeTable.Get((tmp & 0xfc00) >> 10));
                    buff.Add(Base64EncodeTable.Get((tmp & 0x03f0) >> 4));
                    buff.Add(Base64EncodeTable.Get((tmp & 0x000f) << 2));
                    buff.Add(Base64EncodeTable.Get(64));  // pad
                    break;
            }
            ptr_info->store = 0;
            ptr_info->bytePos = 0;
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckLengthIsPositive(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException($"invalid length. lenght={length}");
        }
    }
    /// <summary>
    /// The Decoder for MIME Base64 (RFC 2045).
    /// </summary>
    public struct NativeBase64Decoder : IDisposable
    {
        internal PtrHandle<Base64Info> _info;

        public unsafe NativeBase64Decoder(Allocator alloc)
        {
            _info = new PtrHandle<Base64Info>(alloc);
            _info.Target->Clear();
        }
        public void Dispose()
        {
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
        /// <returns>convert successfull or not</returns>
        public unsafe bool GetBytes(NativeList<byte> buff, NativeArray<Char16> str)
        {
            return GetBytesImpl(_info.Target,
                                buff.GetUnsafeRef(),
                                (Char16*)str.GetUnsafePtr(), str.Length);
        }
        /// <summary>
        /// convert Base64 format chars into bytes.
        /// </summary>
        /// <param name="buff">output</param>
        /// <param name="str">source</param>
        /// <returns>convert successfull or not</returns>
        public unsafe bool GetBytes(NativeList<byte> buff, NativeList<Char16> str)
        {
            return GetBytesImpl(_info.Target,
                                buff.GetUnsafeRef(),
                                (Char16*)str.GetUnsafePtr(), str.Length);
        }
        /// <summary>
        /// convert Base64 format chars into bytes.
        /// </summary>
        /// <param name="buff">output</param>
        /// <param name="str">source</param>
        /// <returns>convert successfull or not</returns>
        public unsafe bool GetBytes<T>(NativeList<byte> buff, T str)
            where T : IJaggedArraySliceBase<Char16>
        {
            return GetBytesImpl(_info.Target,
                                buff.GetUnsafeRef(),
                                (Char16*)str.GetUnsafePtr(), str.Length);
        }
        /// <summary>
        /// convert Base64 format chars into bytes.
        /// </summary>
        /// <param name="buff">output</param>
        /// <param name="char_ptr">source ptr</param>
        /// <param name="char_len">source length</param>
        /// <returns>convert successfull or not</returns>
        public unsafe bool GetBytes(NativeList<byte> buff, Char16* char_ptr, int char_len)
        {
            return GetBytesImpl(_info.Target,
                                buff.GetUnsafeRef(),
                                char_ptr, char_len);
        }
        internal static unsafe bool GetBytesImpl(Base64Info* info_ptr,
                                                 UnsafeRefToNativeList<byte> buff,
                                                 Char16* char_ptr,
                                                 int char_len)
        {
            CheckLengthIsPositive(char_len);

            uint store = info_ptr->store;
            int bytePos = info_ptr->bytePos;

            for (int i = 0; i < char_len; i++)
            {
                Char16 c = char_ptr[i];
                if (IsWhiteSpace(c)) continue;

                if (c == '=')
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

#if UNITY_EDITOR
                            throw new ArgumentException("invalid padding was detected.");
#else
                            return false;
#endif
                        case 2:
                            // pick 1 byte from "**==" code
                            buff.Add((byte)((store & 0x0ff0) >> 4));
                            bytePos = 0;
                            break;
                        case 3:
                            // pick 2 byte from "***=" code
                            buff.Add((byte)((store & 0x03fc00) >> 10));
                            buff.Add((byte)((store & 0x0003fc) >> 2));
                            bytePos = 0;
                            break;
                    }
                    return true;
                }
                else
                {
                    uint b = Base64DecodeTable.Get(c);
                    if (b != 255)
                    {
                        store = (store << 6) | (b & 0x3f);
                        bytePos++;
                    }
                    else
                    {
                        // invalid char for Base64
#if UNITY_EDITOR
                        throw new ArgumentException($"invalid code was detected. char={c}");
#else
                        return false;
#endif
                    }
                }

                if (bytePos == 4)
                {
                    buff.Add((byte)((store & 0xff0000) >> 16));
                    buff.Add((byte)((store & 0x00ff00) >> 8));
                    buff.Add((byte)((store & 0x0000ff)));
                    store = 0;
                    bytePos = 0;
                }
            }
            info_ptr->store = store;
            info_ptr->bytePos = bytePos;

            return true;
        }
        private static bool IsWhiteSpace(Char16 c)
        {
            return (c == UTF16CodeSet.code_space ||
                    c == UTF16CodeSet.code_tab ||
                    c == UTF16CodeSet.code_LF ||
                    c == UTF16CodeSet.code_CR);
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckLengthIsPositive(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException($"invalid bytes length. lenght={length}");
        }
    }

    namespace Utility
    {
        public static class Base64EncoderExt
        {
            public static UnsafeRefToNativeBase64Encoder GetUnsafeRef(this NativeBase64Encoder target)
            {
                return new UnsafeRefToNativeBase64Encoder(target);
            }

            public static UnsafeRefToNativeBase64Decoder GetUnsafeRef(this NativeBase64Decoder target)
            {
                return new UnsafeRefToNativeBase64Decoder(target);
            }
        }

        public unsafe struct UnsafeRefToNativeBase64Encoder
        {
            private Base64Info* info_ptr;

            public UnsafeRefToNativeBase64Encoder(NativeBase64Encoder encoder)
            {
                info_ptr = encoder._info.Target;
            }

            public void Clear() { info_ptr->Clear(); }

            public void GetChars(UnsafeRefToNativeList<Char16> buff,
                                 UnsafeRefToNativeList<byte> bytes,
                                 bool splitData = false)
            {
                NativeBase64Encoder.GetCharsImpl(info_ptr, buff, (byte*)bytes.GetUnsafePtr(), bytes.Length, splitData);
            }
            public void GetChars(UnsafeRefToNativeList<Char16> buff,
                                 byte* byte_ptr,
                                 int byte_len,
                                 bool splitData = false)
            {
                NativeBase64Encoder.GetCharsImpl(info_ptr, buff, byte_ptr, byte_len, splitData);
            }
            public void Terminate(UnsafeRefToNativeList<Char16> buff)
            {
                NativeBase64Encoder.TerminateImpl(info_ptr, buff);
            }
        }
        public unsafe struct UnsafeRefToNativeBase64Decoder
        {
            private Base64Info* info_ptr;

            public UnsafeRefToNativeBase64Decoder(NativeBase64Decoder decoder)
            {
                info_ptr = decoder._info.Target;
            }

            public void Clear() { info_ptr->Clear(); }

            public bool GetBytes<T>(UnsafeRefToNativeList<byte> buff, T str)
                where T : IJaggedArraySliceBase<Char16>
            {
                return NativeBase64Decoder.GetBytesImpl(info_ptr, buff, (Char16*)str.GetUnsafePtr(), str.Length);
            }
            public bool GetBytes(UnsafeRefToNativeList<byte> buff, UnsafeRefToNativeList<Char16> str)
            {
                return NativeBase64Decoder.GetBytesImpl(info_ptr, buff, (Char16*)str.GetUnsafePtr(), str.Length);
            }
            public bool GetBytes(UnsafeRefToNativeList<byte> buff, Char16* char_ptr, int char_len)
            {
                return NativeBase64Decoder.GetBytesImpl(info_ptr, buff, char_ptr, char_len);
            }
        }
    }

    namespace Impl
    {
        internal static class Base64EncodeTable
        {
            private static readonly byte[] _table =
            {
                // 'A' ~ 'Z'
                                65, 66, 67, 68, 69, 70,
                71, 72, 73, 74, 75, 76, 77, 78, 79, 80,
                81, 82, 83, 84, 85, 86, 87, 88, 89, 90,

                // 'a' ~ 'z'
                                               97,  98,  99, 100,
                101, 102, 103, 104, 105, 106, 107, 108, 109, 110,
                111, 112, 113, 114, 115, 116, 117, 118, 119, 120,
                121, 122,

                // '0' ~ '9'
                48, 49, 50, 51, 52, 53, 54, 55, 56, 57,

                43, // '+'
                47, // '/'
                61, // '='
            };

            public static Char16 Get(uint data)
            {
                if (data > 65)
                    return new Char16 { Value = 0 };  // returns NULL code for invalid byte data.
                else
                {
                    return (Char16)_table[data];
                }
            }
        }
        internal static class Base64DecodeTable
        {
            private static readonly byte[] _table =
            {
                 62,                                     // 0x2b, '+'
                255, 255, 255,                           // invalid code
                 63,                                     // 0x2f, '/'
                52, 53, 54, 55, 56, 57, 58, 59, 60, 61,  // '0' ~ '9'
                255, 255, 255, 255, 255, 255, 255,       // invalid code
                 0,  1,  2,  3,  4,  5,  6,  7,  8,  9,  // 'A' ~ 'Z'
                10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
                20, 21, 22, 23, 24, 25,
                255, 255, 255, 255, 255, 255,            // invalid code
                                    26, 27, 28, 29, 30,  // 'a' ~ 'z'
                31, 32, 33, 34, 35, 36, 37, 38, 39, 40,
                41, 42, 43, 44, 45, 46, 47, 48, 49, 50,
                51,
            };

            public static byte Get(int c)
            {
                if (c < 0x2b) return 255;
                if (c > 0x7a) return 255;

                return _table[c - 0x2b];
            }
        }
    }
}
