// enable the below macro to enable reallocation trace for debug.
//#define NATIVE_STRING_COLLECTION_TRACE_REALLOCATION

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections
{
    using NativeStringCollections.Impl;


    public enum Endian
    {
        Big,
        Little,
    }

    public interface IParseExt
    {
        int Length { get; }
        char this[int i] { get; }
    }

    public static class StringParserExt
    {
        /// <summary>
        /// Try to parse StringEntity to bool. Cannot accept whitespaces (this is differ from official C# bool.TryParse()).
        /// </summary>
        public static bool TryParse(this IParseExt value, out bool result)
        {
            if (value.Length == 5)
            {
                // match "False", "false", or "FALSE"
                if(value[0] == 'F')
                {
                    if (value[1] == 'a' && value[2] == 'l' && value[3] == 's' && value[4] == 'e')
                    {
                        result = false;
                        return true;
                    }
                    else if (value[1] == 'A' && value[2] == 'L' && value[3] == 'S' && value[4] == 'E')
                    {
                        result = false;
                        return true;
                    }
                }
                else if (value[0] == 'f' && value[1] == 'a' && value[2] == 'l' && value[3] == 's' && value[4] == 'e')
                {
                    result = false;
                    return true;
                }
            }
            else if (value.Length == 4)
            {
                // match "True", "true", or "TRUE"
                if(value[0] == 'T')
                {
                    if (value[1] == 'r' && value[2] == 'u' && value[3] == 'e')
                    {
                        result = true;
                        return true;
                    }
                    else if (value[1] == 'R' && value[2] == 'U' && value[3] == 'E')
                    {
                        result = true;
                        return true;
                    }
                }
                else if (value[0] == 't' && value[1] == 'r' && value[2] == 'u' && value[3] == 'e')
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
        public static bool TryParse(this IParseExt value, out int result)
        {
            const int max_len = 10;

            result = 0;
            if (value.Length <= 0) return false;

            int i_start = 0;
            if (value[0].IsSign(out int sign)) i_start = 1;

            int digit_count = 0;
            int MSD = 0;

            int tmp = 0;
            for (int i = i_start; i < value.Length; i++)
            {
                if (value[i].IsDigit(out int d))
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
        public static bool TryParse(this IParseExt value, out long result)
        {
            const int max_len = 19;

            result = 0;
            if (value.Length <= 0) return false;

            int i_start = 0;
            if (value[0].IsSign(out int sign)) i_start = 1;

            int digit_count = 0;
            int MSD = 0;

            long tmp = 0;
            for (int i = i_start; i < value.Length; i++)
            {
                if (value[i].IsDigit(out int d))
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
        public static bool TryParse(this IParseExt value, out float result)
        {
            result = 0.0f;
            if (value.Length <= 0) return false;

            if (!value.TryParse(out double tmp)) return false;

            float f_cast = (float)tmp;
            if (float.IsInfinity(f_cast)) return false;

            result = f_cast;
            return true;
        }
        /// <summary>
        /// Try to parse StringEntity to double. Cannot accept whitespaces, comma insertion, and hex format (these are differ from official C# double.TryParse()).
        /// Use TryParseHex(out T) for hex data.
        /// </summary>
        public static bool TryParse(this IParseExt value, out double result)
        {
            result = 0.0;
            if (value.Length <= 0) return false;
            if (!value.TryParseFloatFormat(out int sign, out int i_start, out int dot_pos, out int exp_pos, out int n_pow)) return false;

            //UnityEngine.Debug.Log("i_start=" + i_start.ToString() + ", dot_pos=" + dot_pos.ToString() + ", exp_pos=" + exp_pos.ToString() + ", n_pow=" + n_pow.ToString());

            double mantissa = 0.0;
            for (int i = exp_pos - 1; i >= i_start; i--)
            {
                if (i == dot_pos) continue;
                mantissa = mantissa * 0.1 + (double)value[i].ToInt();
            }

            int m_pow = 0;
            if (dot_pos > i_start + 1) m_pow = dot_pos - i_start - 1;
            if (dot_pos == i_start) m_pow = -1;

            double tmp = mantissa * (sign * math.pow(10.0, n_pow + m_pow));
            if (double.IsInfinity(tmp)) return false;

            result = tmp;
            return true;
        }

        private static bool TryParseFloatFormat(this IParseExt value, out int sign, out int i_start, out int dot_pos, out int exp_pos, out int n_pow)
        {
            i_start = 0;
            if (value[0].IsSign(out sign)) i_start = 1;

            // eat zeros on the head
            for(int i=i_start; i<value.Length; i++)
            {
                char c = value[i];
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
            for (int i = i_start; i < value.Length; i++)
            {
                char c = value[i];
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
                    //if (value.Length - i < 2) return false;  // [+/-] & 1 digit or lager EXP num.
                    if (exp_pos != -1 ||
                        i == i_start + 1 ||
                        (value.Length - i) < 2 ||
                        !value[i + 1].IsSign(out exp_sign)) return false;

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
                if (value.Length - (exp_pos + 2) > 8) return false;  // capacity of int
                for (int i = exp_pos + 2; i < value.Length; i++)
                {
                    n_pow = n_pow * 10 + value[i].ToInt();
                    //UnityEngine.Debug.Log("n_pow(1)=" + n_pow.ToString());
                }
                n_pow *= exp_sign;
            }
            else
            {
                // no [e/E+-[int]] part
                exp_pos = value.Length;
            }

            if (dot_pos < 0) dot_pos = exp_pos;

            //UnityEngine.Debug.Log("ParseFloatFormat=" + true.ToString());

            return true;
        }

        unsafe public static bool TryParseHex(this IParseExt value, out int result, Endian endian = Endian.Big)
        {
            if (value.TryParseHex32(out uint buf, endian))
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
        unsafe public static bool TryParseHex(this IParseExt value, out long result, Endian endian = Endian.Big)
        {
            if (value.TryParseHex64(out ulong buf, endian))
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
        unsafe public static bool TryParseHex(this IParseExt value, out float result, Endian endian = Endian.Big)
        {
            if (value.TryParseHex32(out uint buf, endian))
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
        unsafe public static bool TryParseHex(this IParseExt value, out double result, Endian endian = Endian.Big)
        {
            if (value.TryParseHex64(out ulong buf, endian))
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

        private static bool TryParseHex32(this IParseExt value, out uint buf, Endian endian)
        {
            const int n_digits = 8;  // accepts 8 digits set (4bit * 8)

            int i_start = 0;
            buf = 0;

            if (value[1] == 'x' && value[0] == '0') i_start = 2;

            if (value.Length - i_start != n_digits)
            {
                return false;
            }

            if(endian == Endian.Big)
            {
                for (int i = i_start; i < value.Length; i++)
                {
                    if (value[i].IsHex(out uint h))
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
                int n_byte = value.Length / 2;
                int i_last = i_start / 2;
                for(int i = n_byte - 1; i>=i_last; i--)
                {
                    char c0 = value[2 * i];
                    char c1 = value[2 * i + 1];

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
        private static bool TryParseHex64(this IParseExt value, out ulong buf, Endian endian)
        {
            const int n_digits = 16;  // accepts 16 digits set (4bit * 16)

            int i_start = 0;
            buf = 0;

            if (value[1] == 'x' && value[0] == '0') i_start = 2;

            if (value.Length - i_start != n_digits)
            {
                return false;
            }

            if (endian == Endian.Big)
            {
                for (int i = i_start; i < value.Length; i++)
                {
                    if (value[i].IsHex(out uint h))
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
                int n_byte = value.Length / 2;
                int i_last = i_start / 2;
                for (int i = n_byte - 1; i >= i_last; i--)
                {
                    char c0 = value[2 * i];
                    char c1 = value[2 * i + 1];

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
    }
}
