
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


    unsafe public interface IStringEntityBase
    {
        int Start { get; }
        int Length { get; }
        int End { get; }
        char this[int index] { get; }
        bool EqualsStringEntity(char* ptr, int Start, int Length);
        bool Equals(IStringEntityBase entityBase);
    }

    public interface IParseExt
    {
        int Length { get; }
        char this[int i] { get; }
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct StringEntity :
        IParseExt,
        IStringEntityBase,
        IEquatable<string>, IEquatable<char[]>, IEquatable<IEnumerable<char>>, IEquatable<char>,
        IEnumerable<char>
    {
        private readonly char* root_ptr;
        private readonly int start;
        private readonly int len;

        public int Start {  get { return this.start; } }
        public int Length { get { return this.len; } }
        public int End { get { return this.Start + this.Length; } }

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
        private readonly ulong* gen_ptr;
        private readonly ulong gen_entity;
#endif

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
        public StringEntity(char* ptr, ulong* gen_ptr, ulong gen_entity, int start, int Length)
        {
            this.root_ptr = ptr;
            this.start = start;
            this.len = Length;

            this.gen_ptr = gen_ptr;
            this.gen_entity = gen_entity;
        }
#else
        public StringEntity(char* ptr, int start, int Length)
        {
            this.root_ptr = ptr;
            this.start = start;
            this.len = Length;
        }
#endif

        public char this[int index]
        {
            get
            {
                this.CheckReallocate();
                return *(this.root_ptr + this.Start + index);
            }
            set
            {
                this.CheckReallocate();
                this.CheckCharIndex(index);
                *(this.root_ptr + this.Start + index) = value;
            }
        }
        public char At(int index)
        {
            this.CheckReallocate();
            this.CheckCharIndex(index);
            return this[index];
        }

        public IEnumerator<char> GetEnumerator()
        {
            this.CheckReallocate();
            for (int i = 0; i < this.Length; i++)
                yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public ReadOnlyStringEntity GetReadOnlyEntity() {
#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            this.CheckReallocate();
            return new ReadOnlyStringEntity(this.root_ptr, this.gen_ptr, this.gen_entity, this.Start, this.Length);
#else
            return new ReadOnlyStringEntity(this.root_ptr, this.Start, this.Length);
#endif
        }
        public static implicit operator ReadOnlyStringEntity(StringEntity val)
        {
            return val.GetReadOnlyEntity();
        }

        public bool EqualsStringEntity(char* ptr, int Start, int Length)
        {
            this.CheckReallocate();
            if (this.Length != Length) return false;
            if(this.root_ptr == ptr && this.Start == Start) return true;
            return true;
        }
        public bool Equals(IStringEntityBase entity)
        {
            this.CheckReallocate();
            return entity.EqualsStringEntity(this.root_ptr, this.Start, this.Length);
        }
        public bool Equals(string str)
        {
            this.CheckReallocate();
            if (this.Length != str.Length) return false;
            return this.SequenceEqual<char>(str);
        }
        public bool Equals(char[] c_arr)
        {
            this.CheckReallocate();
            if (this.Length != c_arr.Length) return false;
            return this.SequenceEqual<char>(c_arr);
        }
        public bool Equals(char c)
        {
            this.CheckReallocate();
            return (this.Length == 1 && this[0] == c);
        }
        public bool Equals(IEnumerable<char> str_itr)
        {
            this.CheckReallocate();
            return this.SequenceEqual<char>(str_itr);
        }
        public static bool operator ==(StringEntity lhs, IEnumerable<char> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(StringEntity lhs, IEnumerable<char> rhs) { return !lhs.Equals(rhs); }
        public override bool Equals(object obj)
        {
            return obj is IStringEntityBase && ((IStringEntityBase)obj).EqualsStringEntity(this.root_ptr, this.Start, this.Length);
        }
        public override int GetHashCode()
        {
            this.CheckReallocate();
            int hash = this.Length.GetHashCode();
            for(int i=0; i<this.Length; i++)
            {
                hash = hash ^ this[i].GetHashCode();
            }
            return hash;
        }

        public override string ToString()
        {
            this.CheckReallocate();
            return new string(this.root_ptr, this.Start, this.Length);
        }
        public char[] ToCharArray()
        {
            this.CheckReallocate();
            char[] ret = new char[this.Length];
            for(int i=0; i<this.Length; i++)
            {
                ret[i] = this[i];
            }
            return ret;
        }

        private void CheckCharIndex(int index)
        {
#if UNITY_ASSERTIONS
            if (index < 0 || this.Length <= index)
            {
                // simple exception patterns only can be used in BurstCompiler.
                throw new IndexOutOfRangeException("index is out of range.");
            }
#endif
        }
        private void CheckReallocate()
        {
#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            if( *(this.gen_ptr) != this.gen_entity)
            {
                throw new InvalidOperationException("this entity is invalid reference.");
            }
#endif
        }
    }
    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct ReadOnlyStringEntity :
        IParseExt,
        IStringEntityBase,
        IEquatable<string>, IEquatable<char[]>, IEquatable<IEnumerable<char>>, IEquatable<char>,
        IEnumerable<char>
    {
        private readonly char* root_ptr;
        private readonly int start;
        private readonly int len;

        public int Start { get { return this.start; } }
        public int Length { get { return this.len; } }
        public int End { get { return this.Start + this.Length; } }

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
        private readonly ulong* gen_ptr;
        private readonly ulong gen_entity;
#endif

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
        public ReadOnlyStringEntity(char* ptr, ulong* gen_ptr, ulong gen_entity, int start, int Length)
        {
            this.root_ptr = ptr;
            this.start = start;
            this.len = Length;

            this.gen_ptr = gen_ptr;
            this.gen_entity = gen_entity;
        }
#else
        public ReadOnlyStringEntity(char* ptr, int start, int Length)
        {
            this.root_ptr = ptr;
            this.start = start;
            this.len = Length;
        }
#endif

        public char this[int index]
        {
            get
            {
                this.CheckReallocate();
                return *(this.root_ptr + this.Start + index);
            }
            set
            {
                this.CheckReallocate();
                this.CheckCharIndex(index);
                *(this.root_ptr + this.Start + index) = value;
            }
        }
        public char At(int index)
        {
            this.CheckReallocate();
            this.CheckCharIndex(index);
            return this[index];
        }

        public IEnumerator<char> GetEnumerator()
        {
            this.CheckReallocate();
            for (int i = 0; i < this.Length; i++)
                yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public bool EqualsStringEntity(char* ptr, int Start, int Length)
        {
            this.CheckReallocate();
            if (this.Length != Length) return false;
            if (this.root_ptr == ptr && this.Start == Start) return true;
            return true;
        }
        public bool Equals(IStringEntityBase entity)
        {
            this.CheckReallocate();
            return entity.EqualsStringEntity(this.root_ptr, this.Start, this.Length);
        }
        public bool Equals(string str)
        {
            this.CheckReallocate();
            if (this.Length != str.Length) return false;
            return this.SequenceEqual<char>(str);
        }
        public bool Equals(char[] c_arr)
        {
            this.CheckReallocate();
            if (this.Length != c_arr.Length) return false;
            return this.SequenceEqual<char>(c_arr);
        }
        public bool Equals(char c)
        {
            this.CheckReallocate();
            return (this.Length == 1 && this[0] == c);
        }
        public bool Equals(IEnumerable<char> str_itr)
        {
            this.CheckReallocate();
            return this.SequenceEqual<char>(str_itr);
        }
        public static bool operator ==(ReadOnlyStringEntity lhs, IEnumerable<char> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyStringEntity lhs, IEnumerable<char> rhs) { return !lhs.Equals(rhs); }
        public override bool Equals(object obj)
        {
            return obj is IStringEntityBase && ((IStringEntityBase)obj).EqualsStringEntity(this.root_ptr, this.Start, this.Length);
        }
        public override int GetHashCode()
        {
            this.CheckReallocate();
            int hash = this.Length.GetHashCode();
            for (int i = 0; i < this.Length; i++)
            {
                hash = hash ^ this[i].GetHashCode();
            }
            return hash;
        }

        public override string ToString()
        {
            this.CheckReallocate();
            return new string(this.root_ptr, this.Start, this.Length);
        }
        public char[] ToCharArray()
        {
            this.CheckReallocate();
            char[] ret = new char[this.Length];
            for (int i = 0; i < this.Length; i++)
            {
                ret[i] = this[i];
            }
            return ret;
        }

        private void CheckCharIndex(int index)
        {
#if UNITY_ASSERTIONS
            if (index < 0 || this.Length <= index)
            {
                // simple exception patterns only can be used in BurstCompiler.
                throw new IndexOutOfRangeException("index is out of range.");
            }
#endif
        }
        private void CheckReallocate()
        {
#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            if (*(this.gen_ptr) != this.gen_entity)
            {
                throw new InvalidOperationException("this entity is invalid reference.");
            }
#endif
        }
    }

    public enum Endian
    {
        Big,
        Little,
    }

    public static class StringEntityExtentions
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
        /// Try to parse StringEntity to double. Cannot accept whitespaces, comma insertion, and hex format (these are differ from official C# float.TryParse()).
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
            const int max_digits = 16;  // accepts max 16 digits (4bit * 16)

            int i_start = 0;
            if (value[1] == 'x' && value[0] == '0') i_start = 2;

            if (value.Length - i_start > max_digits)
            {
                buf = 0;
                return false;
            }

            buf = 0;
            for (int i = i_start; i < value.Length; i++)
            {
                if (value[i].IsHex(out uint h))
                {
                    buf = (buf << 4) | h;
                }
                else
                {
                    buf = 0;
                    return false;
                }
            }
            return true;
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
