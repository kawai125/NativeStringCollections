using System;
using System.Runtime.InteropServices;

namespace NativeStringCollections
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Char16 :
        IEquatable<Char16>,
        IEquatable<char>, IEquatable<UInt16>, IEquatable<byte>
    {
        internal UInt16 Value;

        public Char16(Char16 c) { Value = c; }
        public Char16(char c) { Value = c; }
        public Char16(UInt16 c) { Value = c; }
        public Char16(byte c) { Value = c; }

        public static implicit operator Char16(char c)
        {
            return new Char16 { Value = (UInt16)c };
        }

        public static implicit operator Char16(UInt16 c)
        {
            return new Char16 { Value = c };
        }

        public static implicit operator Char16(byte c)
        {
            return new Char16 { Value = c };
        }
        public static implicit operator char(Char16 c)
        {
            return (char)c.Value;
        }
        public static implicit operator UInt16(Char16 c)
        {
            return c.Value;
        }

        public bool Equals(Char16 c) { return Value == c.Value; }
        public bool Equals(char c) { return Value == (UInt16)c; }
        public bool Equals(UInt16 c) { return Value == (UInt16)c; }
        public bool Equals(byte c) { return Value == (UInt16)c; }

        public static bool operator == (Char16 lhs, Char16 rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(Char16 lhs, Char16 rhs) { return !lhs.Equals(rhs); }

        public static bool operator ==(Char16 lhs, char rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(Char16 lhs, char rhs) { return !lhs.Equals(rhs); }

        public static bool operator ==(Char16 lhs, UInt16 rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(Char16 lhs, UInt16 rhs) { return !lhs.Equals(rhs); }

        public static bool operator ==(Char16 lhs, byte rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(Char16 lhs, byte rhs) { return !lhs.Equals(rhs); }

        public static bool operator ==(char lhs, Char16 rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(char lhs, Char16 rhs) { return !rhs.Equals(lhs); }

        public static bool operator ==(UInt16 lhs, Char16 rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(UInt16 lhs, Char16 rhs) { return !rhs.Equals(lhs); }

        public static bool operator ==(byte lhs, Char16 rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(byte lhs, Char16 rhs) { return !rhs.Equals(lhs); }

        public override bool Equals(object obj)
        {
            return obj is Char16 && ((Char16)obj).Equals(this);
        }
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator < (Char16 lhs, Char16 rhs) { return lhs.Value < rhs.Value; }
        public static bool operator > (Char16 lhs, Char16 rhs) { return lhs.Value > rhs.Value; }
        public static bool operator <=(Char16 lhs, Char16 rhs) { return lhs.Value <= rhs.Value; }
        public static bool operator >=(Char16 lhs, Char16 rhs) { return lhs.Value >= rhs.Value; }

        public static bool operator <(Char16 lhs, char rhs) { return lhs.Value < rhs; }
        public static bool operator >(Char16 lhs, char rhs) { return lhs.Value > rhs; }
        public static bool operator <=(Char16 lhs, char rhs) { return lhs.Value <= rhs; }
        public static bool operator >=(Char16 lhs, char rhs) { return lhs.Value >= rhs; }

        public static bool operator <(Char16 lhs, UInt16 rhs) { return lhs.Value < rhs; }
        public static bool operator >(Char16 lhs, UInt16 rhs) { return lhs.Value > rhs; }
        public static bool operator <=(Char16 lhs, UInt16 rhs) { return lhs.Value <= rhs; }
        public static bool operator >=(Char16 lhs, UInt16 rhs) { return lhs.Value >= rhs; }

        public static bool operator <(Char16 lhs, byte rhs) { return lhs.Value < rhs; }
        public static bool operator >(Char16 lhs, byte rhs) { return lhs.Value > rhs; }
        public static bool operator <=(Char16 lhs, byte rhs) { return lhs.Value <= rhs; }
        public static bool operator >=(Char16 lhs, byte rhs) { return lhs.Value >= rhs; }

        public static bool operator <(char lhs, Char16 rhs) { return lhs < rhs.Value; }
        public static bool operator >(char lhs, Char16 rhs) { return lhs > rhs.Value; }
        public static bool operator <=(char lhs, Char16 rhs) { return lhs <= rhs.Value; }
        public static bool operator >=(char lhs, Char16 rhs) { return lhs >= rhs.Value; }

        public static bool operator <(UInt16 lhs, Char16 rhs) { return lhs < rhs.Value; }
        public static bool operator >(UInt16 lhs, Char16 rhs) { return lhs > rhs.Value; }
        public static bool operator <=(UInt16 lhs, Char16 rhs) { return lhs <= rhs.Value; }
        public static bool operator >=(UInt16 lhs, Char16 rhs) { return lhs >= rhs.Value; }

        public static bool operator <(byte lhs, Char16 rhs) { return lhs < rhs.Value; }
        public static bool operator >(byte lhs, Char16 rhs) { return lhs > rhs.Value; }
        public static bool operator <=(byte lhs, Char16 rhs) { return lhs <= rhs.Value; }
        public static bool operator >=(byte lhs, Char16 rhs) { return lhs >= rhs.Value; }

        public override string ToString()
        {
            return ((char)Value).ToString();
        }
        public char ToChar()
        {
            return (char)Value;
        }
    }

    public static class Char16Ext
    {
        /// <summary>
        /// match target is same as System.Char.IsWhiteSpace()
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static bool IsWhiteSpace(this Char16 c)
        {
            if (c.Value == 0x20 ||
               c.Value == 0xA0 ||
               c.Value == 0x1680 ||
               (0x2000 <= c.Value && c.Value <= 0x200A) ||
               c.Value == 0x202F ||
               c.Value == 0x205F ||
               c.Value == 0x3000 ||
               c.Value == 0x2028 ||
               c.Value == 0x2029 ||
               c.Value == 0x0009 ||
               c.Value == 0x000A ||
               c.Value == 0x000B ||
               c.Value == 0x0085)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    namespace Impl
    {
        public struct UTF16CodeSet
        {
            public const UInt16 code_tab = 0x09;
            public const UInt16 code_LF = 0x0a;
            public const UInt16 code_CR = 0x0d;
            public const UInt16 code_space = 0x20;

            public const UInt16 code_minus = 0x2d;
            public const UInt16 code_plus = 0x2b;

            public const UInt16 code_dot = 0x2e;

            public const UInt16 code_0 = 0x30;
            public const UInt16 code_9 = 0x39;

            public const UInt16 code_A = 0x41;
            public const UInt16 code_E = 0x45;
            public const UInt16 code_F = 0x46;
            public const UInt16 code_L = 0x4c;
            public const UInt16 code_R = 0x52;
            public const UInt16 code_S = 0x53;
            public const UInt16 code_T = 0x54;
            public const UInt16 code_U = 0x55;

            public const UInt16 code_a = 0x61;
            public const UInt16 code_e = 0x65;
            public const UInt16 code_f = 0x66;
            public const UInt16 code_l = 0x6c;
            public const UInt16 code_r = 0x72;
            public const UInt16 code_s = 0x73;
            public const UInt16 code_t = 0x74;
            public const UInt16 code_u = 0x75;

            public const UInt16 code_x = 0x78;
        }
    }
}


