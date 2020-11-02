using System;
using System.Runtime.CompilerServices;

using System.Collections;
using System.Collections.Generic;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;


namespace NativeStringCollections.Utility
{
    public struct Boolean : IEquatable<bool>
    {
        private byte _b;

        public Boolean(bool b)
        {
            if (b)
            {
                _b = 1;
            }
            else
            {
                _b = 0;
            }
        }

        public bool Value
        {
            get { return (_b == 1); }
            set { this = new Boolean(value); }
        }

        public static bool operator true(Boolean b) => b.Value;
        public static bool operator false(Boolean b) => b.Value;
        public static implicit operator bool(Boolean b) => b.Value;
        public static implicit operator Boolean(bool b) => new Boolean(b);

        public static Boolean True { get { return new Boolean(true); } }
        public static Boolean False { get { return new Boolean(false); } }

        public bool Equals(bool b) { return (Value == b); }

        public override int GetHashCode()
        {
            return _b.GetHashCode();
        }
        public override string ToString()
        {
            if (Value)
            {
                return "true";
            }
            else
            {
                return "false";
            }
        }
    }
}


