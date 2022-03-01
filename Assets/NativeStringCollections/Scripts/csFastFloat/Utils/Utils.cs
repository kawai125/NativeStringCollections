using NativeStringCollections.Impl.csFastFloat.Constants;
using NativeStringCollections.Impl.csFastFloat.Structures;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


#if HAS_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace NativeStringCollections.Impl.csFastFloat
{

    internal static unsafe class Utils
    {
#if !HAS_BITOPERATIONS
        private static readonly byte[] Log2DeBruijn = new byte[]
        {
            00, 09, 01, 10, 13, 21, 02, 29,
            11, 14, 16, 18, 22, 25, 03, 30,
            08, 12, 20, 28, 15, 17, 24, 07,
            19, 27, 23, 06, 26, 05, 04, 31
        };
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint parse_eight_digits_unrolled(ulong val)
        {
            const ulong mask = 0x000000FF000000FF;
            const ulong mul1 = 0x000F424000000064; // 100 + (1000000ULL << 32)
            const ulong mul2 = 0x0000271000000001; // 1 + (10000ULL << 32)
            val -= 0x3030303030303030;
            val = (val * 10) + (val >> 8); // val = (val * 2561) >> 8;
            val = (((val & mask) * mul1) + (((val >> 16) & mask) * mul2)) >> 32;
            return (uint)val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint parse_eight_digits_unrolled(byte* chars)
        {
            ulong val = Unsafe.ReadUnaligned<ulong>(chars);
            return parse_eight_digits_unrolled(val);
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool is_made_of_eight_digits_fast(byte* chars)
        {
            ulong val = Unsafe.ReadUnaligned<ulong>(chars);
            // We only enable paths depending on this function on little endian
            // platforms (it happens to be effectively nearly everywhere).
            // ref : https://lemire.me/blog/tag/swar/
            return BitConverter.IsLittleEndian && ((val & (val + 0x0606060606060606) & 0xF0F0F0F0F0F0F0F0) == 0x3030303030303030);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool is_integer(Char16 c, out uint cMinus0)
        {
            uint cc = (uint)(c - UTF16CodeSet.code_0);
            bool res = cc <= UTF16CodeSet.code_9 - UTF16CodeSet.code_0;
            cMinus0 = cc;
            return res;
        }
        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool is_integer(byte c, out uint cMinus0)
        {
            uint cc = (uint)(c - (byte)UTF16CodeSet.code_0);
            bool res = cc <= UTF16CodeSet.code_9 - UTF16CodeSet.code_0;
            cMinus0 = cc;
            return res;
        }
        */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static value128 compute_product_approximation(int bitPrecision, long q, ulong w)
        {
            int index = 2 * (int)(q - CalculationConstants.smallest_power_of_five);
            // For small values of q, e.g., q in [0,27], the answer is always exact because
            // The line value128 firstproduct = full_multiplication(w, power_of_five_128[index]);
            // gives the exact answer.
            value128 firstproduct = FullMultiplication(w, CalculationConstants.get_power_of_five_128(index));
            //static_assert((bit_precision >= 0) && (bit_precision <= 64), " precision should  be in (0,64]");
            ulong precision_mask = (bitPrecision < 64) ? ((ulong)(0xFFFFFFFFFFFFFFFF) >> bitPrecision) : (ulong)(0xFFFFFFFFFFFFFFFF);
            if ((firstproduct.high & precision_mask) == precision_mask)
            { // could further guard with  (lower + w < lower)
              // regarding the second product, we only need secondproduct.high, but our expectation is that the compiler will optimize this extra work away if needed.
                value128 secondproduct = FullMultiplication(w, CalculationConstants.get_power_of_five_128(index + 1));
                firstproduct.low += secondproduct.high;
                if (secondproduct.high > firstproduct.low)
                {
                    firstproduct.high++;
                }
            }
            return firstproduct;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int power(int q)
            => (((152170 + 65536) * q) >> 16) + 63;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static value128 FullMultiplication(ulong value1, ulong value2)
        {
#if HAS_INTRINSICS
      if(System.Runtime.Intrinsics.X86.Bmi2.X64.IsSupported)
      {
            ulong lo;
            ulong hi = System.Runtime.Intrinsics.X86.Bmi2.X64.MultiplyNoFlags(value1, value2, &lo);
            return new value128(hi, lo);
      }
#endif
            return Emulate64x64to128(value1, value2);
        }


        internal static value128 Emulate64x64to128(ulong x, ulong y)
        {
            ulong x0 = (uint)x, x1 = x >> 32;
            ulong y0 = (uint)y, y1 = y >> 32;
            ulong p11 = x1 * y1, p01 = x0 * y1;
            ulong p10 = x1 * y0, p00 = x0 * y0;

            ulong middle = p10 + (p00 >> 32) + (uint)p01;

            return new value128(h: p11 + (middle >> 32) + (p01 >> 32), l: (middle << 32) | (uint)p00);
        }

        private static readonly bool[] _asciiSpaceTable = new bool[]
        {
            false, false, false, false, false, false, false, false, false, true, true, true, true, true, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, true
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool is_ascii_space(Char16 c)
        {
            /*
            // ROS for one byte types can be read directly from metadata avoiding the array allocation.
            bool[] table = new bool[] {
            false, false, false, false, false, false, false, false, false, true, true, true, true, true, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, true};
            // Avoid bound checking.
            */
            return (c.Value > 32) ? false : _asciiSpaceTable[c.Value];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool is_space(byte c)
        {
            // ROS for one byte types can be read directly from metadata avoiding the array allocation.
            bool[] table = new bool[] {
            false, false, false, false, false, false, false, false, false, true, true, true, true, true, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false};

            // Avoid bound checking.
            return table[c];
        }

        internal enum strncasecmpMatch
        {
            nan,
            inf,

            infinity,

            plus_nan,
            minus_nan,

            plus_inf,
            minus_inf,
        }

        internal static bool strncasecmp(Char16* input1, strncasecmpMatch tgt)
        {
            int running_diff = 0;

            switch (tgt)
            {
                case strncasecmpMatch.nan:
                    for (int i = 0; i < ConstStringArray.nan.Length; i++)
                    {
                        running_diff |= (input1[i] ^ ConstStringArray.nan[i]);
                    }
                    return (running_diff == 0) || (running_diff == 32);

                case strncasecmpMatch.inf:
                    for (int i = 0; i < ConstStringArray.inf.Length; i++)
                    {
                        running_diff |= (input1[i] ^ ConstStringArray.inf[i]);
                    }
                    return (running_diff == 0) || (running_diff == 32);

                case strncasecmpMatch.infinity:
                    for (int i = 0; i < ConstStringArray.infinity.Length; i++)
                    {
                        running_diff |= (input1[i] ^ ConstStringArray.infinity[i]);
                    }
                    return (running_diff == 0) || (running_diff == 32);

                case strncasecmpMatch.plus_nan:
                    for (int i = 0; i < ConstStringArray.plus_nan.Length; i++)
                    {
                        running_diff |= (input1[i] ^ ConstStringArray.plus_nan[i]);
                    }
                    return (running_diff == 0) || (running_diff == 32);

                case strncasecmpMatch.minus_nan:
                    for (int i = 0; i < ConstStringArray.minus_nan.Length; i++)
                    {
                        running_diff |= (input1[i] ^ ConstStringArray.minus_nan[i]);
                    }
                    return (running_diff == 0) || (running_diff == 32);

                case strncasecmpMatch.plus_inf:
                    for (int i = 0; i < ConstStringArray.plus_inf.Length; i++)
                    {
                        running_diff |= (input1[i] ^ ConstStringArray.plus_inf[i]);
                    }
                    return (running_diff == 0) || (running_diff == 32);

                case strncasecmpMatch.minus_inf:
                    for (int i = 0; i < ConstStringArray.minus_inf.Length; i++)
                    {
                        running_diff |= (input1[i] ^ ConstStringArray.minus_inf[i]);
                    }
                    return (running_diff == 0) || (running_diff == 32);
            }

            return false;
        }

        internal static bool strncasecmp(Char16* input1, Char16* input2, int length)
        {
            int running_diff = 0;

            for (int i = 0; i < length; i++)
            {
                running_diff |= (input1[i] ^ input2[i]);
            }
            return (running_diff == 0) || (running_diff == 32);
        }
        internal static bool strncasecmp(byte* input1, byte[] input2, int length)
        {
            int running_diff = 0;

            for (int i = 0; i < length; i++)
            {
                running_diff |= (input1[i] ^ input2[i]);
            }
            return (running_diff == 0) || (running_diff == 32);
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(ulong value)
        {
            uint hi = (uint)(value >> 32);

            if (hi == 0)
            {
                return 32 + Log2SoftwareFallback((uint)value);
            }

            return Log2SoftwareFallback(hi);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Log2SoftwareFallback(uint value)
        {
            if (value == 0)
            {
                return 32;
            }

            int n = 1;
            if (value >> 16 == 0) { n += 16; value <<= 16; }
            if (value >> 24 == 0) { n += 8; value <<= 8; }
            if (value >> 28 == 0) { n += 4; value <<= 4; }
            if (value >> 30 == 0) { n += 2; value <<= 2; }
            n -= (int)(value >> 31);
            return n;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Int32BitsToSingle(int value)
#if HAS_BITOPERATIONS
      => BitConverter.Int32BitsToSingle(value);
#else
      => *((float*)&value);
#endif
    }
}
