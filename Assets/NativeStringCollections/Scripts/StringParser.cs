//#define DISABLE_CS_FAST_FLOAT

using System;

using UnityEngine;

using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;


namespace NativeStringCollections
{
    using NativeStringCollections.Impl;
    using NativeStringCollections.Utility;


    public enum Endian
    {
        Big,
        Little,
    }

    [BurstCompile]
    public static unsafe class StringParserExt
    {
        /// <summary>
        /// Try to parse StringEntity to bool. Cannot accept whitespaces (this is differ from official C# bool.TryParse()).
        /// </summary>
        public unsafe static bool TryParse<T>(this T source, out bool result)
            where T : IJaggedArraySliceBase<Char16>
        {
            TryParseBoolImpl((Char16*)source.GetUnsafePtr(), source.Length, out bool success, out result);
            return success;
        }
        /// <summary>
        /// Try to parse StringEntity to Int32. Cannot accept whitespaces and hex format (this is differ from official C# int.TryParse()). Use TryParseHex(out T) for hex data.
        /// </summary>
        public unsafe static bool TryParse<T>(this T source, out int result)
            where T : IJaggedArraySliceBase<Char16>
        {
            //TryParseInt32Impl((Char16*)source.GetUnsafePtr(), source.Length, out bool success, out result);
            //return success;
            return FastIntegerParser.TryParse(source, out result);
        }
        /// <summary>
        /// Try to parse StringEntity to Int64. Cannot accept whitespaces and hex format (this is differ from official C# long.TryParse()). Use TryParseHex(out T) for hex data.
        /// </summary>
        public unsafe static bool TryParse<T>(this T source, out long result)
            where T : IJaggedArraySliceBase<Char16>
        {
            //TryParseInt64Impl((Char16*)source.GetUnsafePtr(), source.Length, out bool success, out result);
            //return success;
            return FastIntegerParser.TryParse(source, out result);
        }
        /// <summary>
        /// Try to parse StringEntity to float. Cannot accept whitespaces, comma insertion, and hex format (these are differ from official C# float.TryParse()).
        /// Use TryParseHex(out T) for hex data.
        /// </summary>
        public static bool TryParse<T>(this T source, out float result)
            where T : IJaggedArraySliceBase<Char16>
        {
#if DISABLE_CS_FAST_FLOAT
            result = 0.0f;

            TryParseFloat64Impl((Char16*)source.GetUnsafePtr(), source.Length,
                                out bool success, out double f64_result);
            if (!success) return false;

            float f_cast = (float)f64_result;
            if (float.IsInfinity(f_cast)) return false;

            result = f_cast;
            return true;
#else
            return Impl.csFastFloat.FastFloatParser.TryParseFloat(source, out result);
#endif
        }
        /// <summary>
        /// Try to parse StringEntity to double. Cannot accept whitespaces, comma insertion, and hex format (these are differ from official C# double.TryParse()).
        /// Use TryParseHex(out T) for hex data.
        /// </summary>
        public unsafe static bool TryParse<T>(this T source, out double result)
            where T : IJaggedArraySliceBase<Char16>
        {
#if DISABLE_CS_FAST_FLOAT
            TryParseFloat64Impl((Char16*)source.GetUnsafePtr(), source.Length,
                                out bool success, out result);
            return success;
#else
            return Impl.csFastFloat.FastDoubleParser.TryParseDouble(source, out result);
#endif
        }

        unsafe public static bool TryParseHex<T>(this T source, out int result, Endian endian = Endian.Little)
            where T : IJaggedArraySliceBase<Char16>
        {
            TryParseHex32Impl((Char16*)source.GetUnsafePtr(), source.Length,
                              out bool success, out uint buf, endian);

            if (success)
            {
                result = *(int*)&buf;
            }
            else
            {
                result = 0;
            }
            return success;
        }
        unsafe public static bool TryParseHex<T>(this T source, out long result, Endian endian = Endian.Little)
            where T : IJaggedArraySliceBase<Char16>
        {
             TryParseHex64Impl((Char16*)source.GetUnsafePtr(), source.Length,
                               out bool success, out ulong buf, endian);

            if (success)
            {
                result = *(Int64*)&buf;
            }
            else
            {
                result = 0;
            }
            return success;
        }
        unsafe public static bool TryParseHex<T>(this T source, out float result, Endian endian = Endian.Little)
            where T : IJaggedArraySliceBase<Char16>
        {
            TryParseHex32Impl((Char16*)source.GetUnsafePtr(), source.Length,
                              out bool success, out uint buf, endian);

            if (success)
            {
                result = *(float*)&buf;
            }
            else
            {
                result = 0.0f;
            }
            return success;
        }
        unsafe public static bool TryParseHex<T>(this T source, out double result, Endian endian = Endian.Little)
            where T : IJaggedArraySliceBase<Char16>
        {
            TryParseHex64Impl((Char16*)source.GetUnsafePtr(), source.Length,
                              out bool success, out ulong buf, endian);

            if (success)
            {
                result = *(double*)&buf;
            }
            else
            {
                result = 0.0;
            }
            return success;
        }

        internal unsafe static void TryParseBoolImpl(Char16* ptr_source, int len_source,
                                                     out bool success, out bool result)
        {
            if (len_source == 5)
            {
                // match "False", "false", or "FALSE"
                if (ptr_source[0] == UTF16CodeSet.code_F)
                {
                    if (ptr_source[1] == UTF16CodeSet.code_a &&
                        ptr_source[2] == UTF16CodeSet.code_l &&
                        ptr_source[3] == UTF16CodeSet.code_s &&
                        ptr_source[4] == UTF16CodeSet.code_e)
                    {
                        result = false;
                        success = true;
                        return;
                    }
                    else if (ptr_source[1] == UTF16CodeSet.code_A &&
                        ptr_source[2] == UTF16CodeSet.code_L &&
                        ptr_source[3] == UTF16CodeSet.code_S &&
                        ptr_source[4] == UTF16CodeSet.code_E)
                    {
                        result = false;
                        success = true;
                        return;
                    }
                }
                else if (ptr_source[0] == UTF16CodeSet.code_f &&
                    ptr_source[1] == UTF16CodeSet.code_a &&
                    ptr_source[2] == UTF16CodeSet.code_l &&
                    ptr_source[3] == UTF16CodeSet.code_s &&
                    ptr_source[4] == UTF16CodeSet.code_e)
                {
                    result = false;
                    success = true;
                    return;
                }
            }
            else if (len_source == 4)
            {
                // match "True", "true", or "TRUE"
                if (ptr_source[0] == UTF16CodeSet.code_T)
                {
                    if (ptr_source[1] == UTF16CodeSet.code_r &&
                        ptr_source[2] == UTF16CodeSet.code_u &&
                        ptr_source[3] == UTF16CodeSet.code_e)
                    {
                        result = true;
                        success = true;
                        return;
                    }
                    else if (ptr_source[1] == UTF16CodeSet.code_R &&
                        ptr_source[2] == UTF16CodeSet.code_U &&
                        ptr_source[3] == UTF16CodeSet.code_E)
                    {
                        result = true;
                        success = true;
                        return;
                    }
                }
                else if (ptr_source[0] == UTF16CodeSet.code_t &&
                    ptr_source[1] == UTF16CodeSet.code_r &&
                    ptr_source[2] == UTF16CodeSet.code_u &&
                    ptr_source[3] == UTF16CodeSet.code_e)
                {
                    result = true;
                    success = true;
                    return;
                }
            }
            result = false;
            success = false;
            return;
        }
        internal unsafe static void TryParseInt32Impl(Char16* ptr_source, int len_source,
                                                      out bool success, out int result)
        {
            const int max_len = 10;

            result = 0;
            if (len_source <= 0)
            {
                success = false;
                return;
            }

            int i_start = 0;
            if (ptr_source[0].IsSign(out int sign)) i_start = 1;

            int digit_count = 0;
            int MSD = 0;

            int tmp = 0;
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
                        if ((MSD > 2) ||
                           (sign == 1 && tmp < 0) ||
                           (sign == -1 && (tmp - 1) < 0))
                        {
                            success = false;
                            return;
                        }
                    }
                    if (digit_count > max_len)
                    {
                        success = false;
                        return;
                    }
                }
                else
                {
                    success = false;
                    return;
                }
            }

            result = sign * tmp;
            success = true;
            return;
        }
        internal unsafe static void TryParseInt64Impl(Char16* ptr_source, int len_source,
                                                      out bool success, out long result)
        {
            const int max_len = 19;

            result = 0;
            if (len_source <= 0)
            {
                success = false;
                return;
            }

            int i_start = 0;
            if (ptr_source[0].IsSign(out int sign)) i_start = 1;

            int digit_count = 0;
            int MSD = 0;

            Int64 tmp = 0;
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
                        if ((sign == 1 && tmp < 0L) ||
                            (sign == -1 && (tmp - 1L) < 0))
                        {
                            success = false;
                            return;
                        }
                    }
                    if (digit_count > max_len)
                    {
                        success = false;
                        return;
                    }
                }
                else
                {
                    success = false;
                    return;
                }
            }

            result = sign * tmp;
            success = true;
            return;
        }
        internal unsafe static void TryParseFloat64Impl(Char16* ptr_source, int length,
                                                        out bool success, out double result)
        {
            result = 0.0;
            if (length <= 0)
            {
                success = false;
                return;
            }
            if (!TryParseFloatFormat(ptr_source, length,
                                     out int sign, out int i_start, out int dot_pos,
                                     out int exp_pos, out int n_pow))
            {
                success = false;
                return;
            }

            //UnityEngine.Debug.Log("i_start=" + i_start.ToString() + ", dot_pos=" + dot_pos.ToString() + ", exp_pos=" + exp_pos.ToString() + ", n_pow=" + n_pow.ToString());

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
            if (double.IsInfinity(tmp))
            {
                success = false;
                return;
            }

            result = tmp;
            success = true;
            //UnityEngine.Debug.Log($">> parsed value = {result}");
            return;
        }

        private unsafe static bool TryParseFloatFormat(Char16* ptr_source,
                                                       int length,
                                                       out int sign,
                                                       out int i_start,
                                                       out int dot_pos,
                                                       out int exp_pos,
                                                       out int n_pow)
        {
            i_start = 0;
            if (ptr_source[0].IsSign(out sign)) i_start = 1;

            bool zero_head = false; 
            // eat zeros on the head
            for (int i=i_start; i<length; i++)
            {
                UInt16 c = ptr_source[i];
                if(c == UTF16CodeSet.code_0)
                {
                    i_start++;
                    zero_head = true;
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

            int dummy;
            int dummy_sign;

            int exp_start = -1; ;

            // format check
            for (int i = i_start; i < length; i++)
            {
                Char16 c = ptr_source[i];
                //UnityEngine.Debug.Log($"parse format: i={i}, c={c}");
                if (c.IsDigit(out dummy))
                {
                    if (exp_pos == -1) digit_count++;
                    // do nothing
                    //UnityEngine.Debug.Log("c is number");
                }
                else
                {
                    if (exp_start > 0 && i >= exp_start) return false;   // after [e/E] region must be digits.

                    if (c.IsDot())
                    {
                        if (dot_pos != -1 || dot_pos == i_start + 1) return false;
                        if (exp_pos > 0 && i >= exp_pos) return false;
                        dot_pos = i;
                        //UnityEngine.Debug.Log("c is dot");
                    }
                    else if (c.IsExp())
                    {
                        //UnityEngine.Debug.Log("c is EXP");

                        if (exp_pos != -1) return false;                   // 2nd [e/E] found
                        if (digit_count == 0 && !zero_head) return false;  // no digits before [e/E]

                        if (length - i == 0) return false;                 // no digits after [e/E]
                        if ((length - i == 1) &&
                            !ptr_source[i + 1].IsDigit(out dummy)) return false;

                        exp_pos = i;
                        exp_start = i + 1;

                        if (ptr_source[i + 1].IsSign(out exp_sign))
                        {
                            if (exp_start + 1 >= length) return false;  // ended by [e/E][+/-]. no pow digits.
                            exp_start += 1;
                        }
                    }
                    else if (c.IsSign(out dummy_sign))
                    {
                        if (i != exp_pos + 1) return false;
                        exp_start = i + 1;
                    }
                    else
                    {
                        //UnityEngine.Debug.Log("failure to parse");
                        return false;
                    }
                }
            }

            // decode exp part
            if (exp_start > 0)
            {
                for (int i = 0; i < math.min(length - exp_start, 32); i++) // up to 32 digits for pow
                {
                    n_pow = n_pow * 10 + ptr_source[i + exp_start].ToInt();
                    //UnityEngine.Debug.Log("n_pow(1)=" + n_pow.ToString());
                    if (n_pow > 350)
                    {
                        if(exp_sign < 0)
                        {
                            n_pow = -400;
                            if (dot_pos < 0) dot_pos = exp_pos;
                            return true;  // underflow, round to zero.
                        }
                        else
                        {
                            return false; // overflow
                        }
                    }
                }
                n_pow *= exp_sign;
            }
            else
            {
                // no [e/E+-[int]] part
                exp_pos = length;
            }

            if (dot_pos < 0) dot_pos = exp_pos;

            //UnityEngine.Debug.Log("ParseFloatFormat=" + true.ToString());

            return true;
        }

        internal unsafe static void TryParseHex32Impl(Char16* ptr_source,
                                                      int length,
                                                      out bool success,
                                                      out uint buf,
                                                      Endian endian)
        {
            const int n_digits = 8;  // accepts 8 digits set (4bit * 8)

            int i_start = 0;
            buf = 0;

            if (!(length == n_digits || length == n_digits + 2))
            {
                success = false;
                return;
            }

            if (ptr_source[0] == UTF16CodeSet.code_0 && ptr_source[1] == UTF16CodeSet.code_x) i_start = 2;

            if(endian == Endian.Big)
            {
                for (int i = i_start; i < length; i++)
                {
                    if (ptr_source[i].IsHex(out uint h))
                    {
                        buf = (buf << 4) | h;
                    }
                    else
                    {
                        success = false;
                        return;
                    }
                }
            }
            else if(endian == Endian.Little)
            {
                int n_byte = length / 2;
                int i_last = i_start / 2;
                for(int i = n_byte - 1; i>=i_last; i--)
                {
                    Char16 c0 = ptr_source[2 * i];
                    Char16 c1 = ptr_source[2 * i + 1];

                    if(c0.IsHex(out uint h0) && c1.IsHex(out uint h1))
                    {
                        buf = (buf << 4) | h0;
                        buf = (buf << 4) | h1;
                    }
                    else
                    {
                        success = false;
                        return;
                    }
                }
            }

            success = true;
            return;
        }
        internal unsafe static void TryParseHex64Impl(Char16* ptr_source,
                                                      int length,
                                                      out bool success,
                                                      out ulong buf,
                                                      Endian endian)
        {
            const int n_digits = 16;  // accepts 16 digits set (4bit * 16)

            int i_start = 0;
            buf = 0;

            if (!(length == n_digits || length == n_digits + 2))
            {
                success = false;
                return;
            }

            if (ptr_source[0] == UTF16CodeSet.code_0 && ptr_source[1] == UTF16CodeSet.code_x) i_start = 2;

            if (endian == Endian.Big)
            {
                for (int i = i_start; i < length; i++)
                {
                    if (ptr_source[i].IsHex(out uint h))
                    {
                        buf = (buf << 4) | h;
                    }
                    else
                    {
                        success = false;
                        return;
                    }
                }
            }
            else if (endian == Endian.Little)
            {
                int n_byte = length / 2;
                int i_last = i_start / 2;
                for (int i = n_byte - 1; i >= i_last; i--)
                {
                    Char16 c0 = ptr_source[2 * i];
                    Char16 c1 = ptr_source[2 * i + 1];

                    if (c0.IsHex(out uint h0) && c1.IsHex(out uint h1))
                    {
                        buf = (buf << 4) | h0;
                        buf = (buf << 4) | h1;
                    }
                    else
                    {
                        success = false;
                        return;
                    }
                }
            }

            success = true;
            return;
        }

        /// <summary>
        /// Check the value is integral value or not.
        /// </summary>
        public static bool IsIntegral<T>(this T value)
            where T : IJaggedArraySliceBase<Char16>
        {
            return value.TryParse(out long dummy);
        }
        /// <summary>
        /// Check the value is numeric value or not.
        /// </summary>
        public static bool IsNumeric<T>(this T value)
            where T : IJaggedArraySliceBase<Char16>
        {
            return value.TryParse(out double dummy);
        }
        /// <summary>
        /// Check the value is 32bit hex data or not.
        /// </summary>
        public static bool IsHex32<T>(this T value)
            where T : IJaggedArraySliceBase<Char16>
        {
            TryParseHex32Impl((Char16*)value.GetUnsafePtr(), value.Length,
                              out bool success, out uint buf, Endian.Little);
            return success;
        }
        /// <summary>
        /// Check the value is 64bit hex data or not.
        /// </summary>
        public static bool IsHex64<T>(this T value)
            where T : IJaggedArraySliceBase<Char16>
        {
            TryParseHex64Impl((Char16*)value.GetUnsafePtr(), value.Length,
                              out bool success, out ulong buf, Endian.Little);
            return success;
        }
    }

    namespace Impl
    {
        static class Char16Ext
        {
            public static bool IsSign(this Char16 c, out int sign)
            {
                if (c == UTF16CodeSet.code_minus)
                {
                    sign = -1;
                    return true;
                }
                else if (c == UTF16CodeSet.code_plus)
                {
                    sign = 1;
                    return true;
                }
                sign = 1;
                return false;
            }
            public static bool IsDigit(this Char16 c, out int digit)
            {
                if (UTF16CodeSet.code_0 <= c && c <= UTF16CodeSet.code_9)
                {
                    digit = c.ToInt();
                    return true;
                }
                digit = 0;
                return false;
            }
            public static bool IsHex(this Char16 c, out uint hex)
            {
                if (c.IsDigit(out int d))
                {
                    hex = (uint)d;
                    return true;
                }
                else if (UTF16CodeSet.code_A <= c && c <= UTF16CodeSet.code_F)
                {
                    hex = (uint)(c - UTF16CodeSet.code_A + 10);
                    return true;
                }
                else if (UTF16CodeSet.code_a <= c && c <= UTF16CodeSet.code_f)
                {
                    hex = (uint)(c - UTF16CodeSet.code_a + 10);
                    return true;
                }
                hex = 0;
                return false;
            }
            public static bool IsDot(this Char16 c)
            {
                if (c == UTF16CodeSet.code_dot) return true;
                return false;
            }
            public static bool IsExp(this Char16 c)
            {
                if (c == UTF16CodeSet.code_e || c == UTF16CodeSet.code_E) return true;
                return false;
            }
            public static int ToInt(this Char16 c)
            {
                return (int)(c - UTF16CodeSet.code_0);
            }
        }
    }
}
