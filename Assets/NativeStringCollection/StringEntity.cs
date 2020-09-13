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


    unsafe public interface IStringEntityBase
    {
        int Length { get; }
        char this[int index] { get; }
        IStringEntityBase Slice(int begin = -1, int end = -1);
        bool EqualsStringEntity(IStringEntityBase entityBase);
        bool EqualsStringEntity(char* ptr, int Length);
        bool Equals(IStringEntityBase entityBase);
        bool Equals(char* ptr, int Length);
        void* GetUnsafePtr();
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct StringEntity :
        IParseExt,
        IStringEntityBase,
        IEquatable<string>, IEquatable<char[]>, IEquatable<IEnumerable<char>>, IEquatable<char>,
        IEnumerable<char>
    {
        [NativeDisableUnsafePtrRestriction]
        private readonly char* _ptr;
        private readonly int _len;

        public int Length { get { return _len; } }


#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
        [NativeDisableUnsafePtrRestriction]
        private readonly long* _gen_ptr;
        private readonly long _gen_entity;
#endif

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
        public StringEntity(char* ptr, int Length, long* gen_ptr, long gen_entity)
        {
            _ptr = ptr;
            _len = Length;

            _gen_ptr = gen_ptr;
            _gen_entity = gen_entity;
        }
#else
        public StringEntity(char* ptr, int Length)
        {
            _ptr = ptr;
            _len = Length;
        }
#endif

        public char this[int index]
        {
            get
            {
                this.CheckReallocate();
                return *(_ptr + index);
            }
            set
            {
                this.CheckReallocate();
                this.CheckCharIndex(index);
                *(_ptr + index) = value;
            }
        }
        public char At(int index)
        {
            this.CheckReallocate();
            this.CheckCharIndex(index);
            return this[index];
        }
        public IStringEntityBase Slice(int begin = -1, int end = -1)
        {
            if (begin < 0) begin = 0;
            if (end < 0) end = _len;

            int new_len = end - begin;
            if (new_len < 0) throw new ArgumentOutOfRangeException("invalid range. the begin > the end.");

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            this.CheckReallocate();
            return new StringEntity(_ptr + begin, new_len, _gen_ptr, _gen_entity);
#else
            return new StringEntity(_ptr + begin, new_len);
#endif
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
            return new ReadOnlyStringEntity(_ptr, _len, _gen_ptr, _gen_entity);
#else
            return new ReadOnlyStringEntity(_ptr, _len);
#endif
        }
        public static implicit operator ReadOnlyStringEntity(StringEntity val)
        {
            return val.GetReadOnlyEntity();
        }

        public bool EqualsStringEntity(char* ptr, int Length)
        {
            this.CheckReallocate();
            if (_len != Length) return false;

            // pointing same target
            if (_ptr == ptr) return true;

            for(int i=0; i<_len; i++)
            {
                if (_ptr[i] != ptr[i]) return false;
            }

            return true;
        }
        public bool EqualsStringEntity(IStringEntityBase entity)
        {
            return entity.EqualsStringEntity(_ptr, _len);
        }
        public bool Equals(IStringEntityBase entity)
        {
            this.CheckReallocate();
            return entity.Equals(_ptr, _len);
        }
        public bool Equals(char* ptr, int Length)
        {
            this.CheckReallocate();
            if (_len != Length) return false;

            for (int i = 0; i < Length; i++)
            {
                if (_ptr[i] != ptr[i]) return false;
            }

            return true;
        }
        public bool Equals(string str)
        {
            this.CheckReallocate();
            if (_len != str.Length) return false;
            return this.SequenceEqual<char>(str);
        }
        public bool Equals(char[] c_arr)
        {
            this.CheckReallocate();
            if (_len != c_arr.Length) return false;
            return this.SequenceEqual<char>(c_arr);
        }
        public bool Equals(char c)
        {
            this.CheckReallocate();
            return (_len == 1 && this[0] == c);
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
            return obj is IStringEntityBase && ((IStringEntityBase)obj).EqualsStringEntity(_ptr, _len);
        }
        public override int GetHashCode()
        {
            this.CheckReallocate();
            int hash = _len.GetHashCode();
            for(int i=0; i<_len; i++)
            {
                hash = hash ^ this[i].GetHashCode();
            }
            return hash;
        }

        public override string ToString()
        {
            this.CheckReallocate();
            return new string(_ptr, 0, _len);
        }
        public char[] ToCharArray()
        {
            this.CheckReallocate();
            char[] ret = new char[_len];
            for(int i=0; i<_len; i++)
            {
                ret[i] = this[i];
            }
            return ret;
        }

        private void CheckCharIndex(int index)
        {
#if UNITY_ASSERTIONS
            if (index < 0 || _len <= index)
            {
                // simple exception patterns only can be used in BurstCompiler.
                throw new IndexOutOfRangeException("index is out of range.");
            }
#endif
        }
        private void CheckReallocate()
        {
#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            if(_gen_ptr == null && _gen_entity == -1) return;  // ignore case for StringEntityGeneratorExt
            if( *(_gen_ptr) != _gen_entity)
            {
                throw new InvalidOperationException("this entity is invalid reference.");
            }
#endif
        }

        public void* GetUnsafePtr() { return _ptr; }
    }
    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct ReadOnlyStringEntity :
        IParseExt,
        IStringEntityBase,
        IEquatable<string>, IEquatable<char[]>, IEquatable<IEnumerable<char>>, IEquatable<char>,
        IEnumerable<char>
    {
        [NativeDisableUnsafePtrRestriction]
        private readonly char* _ptr;
        private readonly int _len;

        public int Length { get { return _len; } }

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
        [NativeDisableUnsafePtrRestriction]
        private readonly long* _gen_ptr;
        private readonly long _gen_entity;
#endif

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
        public ReadOnlyStringEntity(char* ptr, int Length, long* gen_ptr, long gen_entity)
        {
            _ptr = ptr;
            _len = Length;

            _gen_ptr = gen_ptr;
            _gen_entity = gen_entity;
        }
#else
        public ReadOnlyStringEntity(char* ptr, int Length)
        {
            _ptr = ptr;
            _len = Length;
        }
#endif

        public char this[int index]
        {
            get
            {
                this.CheckReallocate();
                return *(_ptr + index);
            }
        }
        public char At(int index)
        {
            this.CheckReallocate();
            this.CheckCharIndex(index);
            return this[index];
        }
        public IStringEntityBase Slice(int begin = -1, int end = -1)
        {
            if (begin < 0) begin = 0;
            if (end < 0) end = _len;

            int new_len = end - begin;
            if (new_len < 0) throw new ArgumentOutOfRangeException("invalid range. the begin > the end.");

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            this.CheckReallocate();
            return new ReadOnlyStringEntity(_ptr + begin, new_len, _gen_ptr, _gen_entity);
#else
            return new ReadOnlyStringEntity(_ptr + begin, new_len);
#endif
        }

        public IEnumerator<char> GetEnumerator()
        {
            this.CheckReallocate();
            for (int i = 0; i < _len; i++)
                yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public bool EqualsStringEntity(char* ptr, int Length)
        {
            this.CheckReallocate();
            if (_len != Length) return false;

            // pointing same target
            if (_ptr == ptr) return true;

            for (int i = 0; i < _len; i++)
            {
                if (_ptr[i] != ptr[i]) return false;
            }

            return true;
        }
        public bool EqualsStringEntity(IStringEntityBase entity)
        {
            return entity.EqualsStringEntity(_ptr, _len);
        }
        public bool Equals(IStringEntityBase entity)
        {
            this.CheckReallocate();
            return entity.Equals(_ptr, _len);
        }
        public bool Equals(char* ptr, int Length)
        {
            this.CheckReallocate();
            if (_len != Length) return false;

            for (int i = 0; i < Length; i++)
            {
                if (_ptr[i] != ptr[i]) return false;
            }

            return true;
        }
        public bool Equals(string str)
        {
            this.CheckReallocate();
            if (_len != str.Length) return false;
            return this.SequenceEqual<char>(str);
        }
        public bool Equals(char[] c_arr)
        {
            this.CheckReallocate();
            if (_len != c_arr.Length) return false;
            return this.SequenceEqual<char>(c_arr);
        }
        public bool Equals(char c)
        {
            this.CheckReallocate();
            return (_len == 1 && this[0] == c);
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
            return obj is IStringEntityBase && ((IStringEntityBase)obj).EqualsStringEntity(_ptr, _len);
        }
        public override int GetHashCode()
        {
            this.CheckReallocate();
            int hash = _len.GetHashCode();
            for (int i = 0; i < _len; i++)
            {
                hash = hash ^ this[i].GetHashCode();
            }
            return hash;
        }

        public override string ToString()
        {
            this.CheckReallocate();
            return new string(_ptr, 0, _len);
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
            if(_gen_entity == -1) return;  // ignore case for StringEntityGeneratorExt
            if (*(_gen_ptr) != _gen_entity)
            {
                throw new InvalidOperationException("this entity is invalid reference.");
            }
#endif
        }

        public void* GetUnsafePtr() { return _ptr; }
    }


    namespace Utility
    {
        public static class StringEntityGeneratorExt
        {
            /// <summary>
            /// StringEntity generator for NativeList.
            /// </summary>
            /// <param name="source"></param>
            /// <returns><char></returns>
            public unsafe static StringEntity ToStringEntity(this NativeList<char> source)
            {
#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
                return new StringEntity((char*)source.GetUnsafePtr(), source.Length, null, -1);
#else
                return new StringEntity((char*)source.GetUnsafePtr(), source.Length);
#endif
            }
        }
    }
}
