using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections
{
    using NativeStringCollections.Impl;

    // Note: the implementation of StringEntity is almost same with NativeJaggedArraySlice<T>.
    //       however, if write as "struct StringEntity { private NativeJaggedArraySlice<char> _slice; }"
    //       cause CS1292 with StringSplitter.Split() in UnityEditor.
    //       (MS Visual Studio compiles successfully.)
    //
    // Date : 2021/2/18
    //        Unity 2019.4.20f1
    //        MS Visual Studio Community 16.8.5
    //        MS .NET framework 4.8.04084

    public unsafe readonly struct StringEntity :
        IParseExt,
        IJaggedArraySliceBase<char>,
        ISlice<StringEntity>,
        IEquatable<string>, IEquatable<char[]>, IEquatable<IEnumerable<char>>, IEquatable<char>,
        IEnumerable<char>
    {
        [NativeDisableUnsafePtrRestriction]
        private readonly char* _ptr;
        private readonly int _len;

        public int Length { get { return _len; } }


#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [NativeDisableUnsafePtrRestriction]
        private readonly long* _gen_ptr;
        private readonly long _gen_entity;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
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
        public StringEntity(NativeJaggedArraySlice<char> slice)
        {
            _ptr = slice._ptr;
            _len = slice._len;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            _gen_ptr = slice._gen_ptr;
            _gen_entity = slice._gen_entity;
#endif
        }
        public StringEntity(StringEntity entity)
        {
            this = entity;
        }

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
                this.CheckElemIndex(index);
                *(_ptr + index) = value;
            }
        }
        public char At(int index)
        {
            this.CheckReallocate();
            this.CheckElemIndex(index);
            return this[index];
        }
        public StringEntity Slice(int begin = -1, int end = -1)
        {
            if (begin < 0) begin = 0;
            if (end < 0) end = _len;
            this.CheckSliceRange(begin, end);

            int new_len = end - begin;

            this.CheckReallocate();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
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

        public ReadOnlyStringEntity GetReadOnlySlice()
        {
            this.CheckReallocate();
            return new ReadOnlyStringEntity(this);
        }
        public static implicit operator ReadOnlyStringEntity(StringEntity val)
        {
            return val.GetReadOnlySlice();
        }

        public bool Equals(char* ptr, int Length)
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
        public bool Equals(StringEntity entity)
        {
            this.CheckReallocate();
            return entity.Equals(_ptr, _len);
        }
        public bool Equals(ReadOnlyStringEntity entity)
        {
            this.CheckReallocate();
            return entity.Equals(_ptr, _len);
        }
        public bool Equals(NativeJaggedArraySlice<char> slice)
        {
            this.CheckReallocate();
            return slice.Equals(_ptr, _len);
        }
        public bool Equals(ReadOnlyNativeJaggedArraySlice<char> slice)
        {
            this.CheckReallocate();
            return slice.Equals(_ptr, _len);
        }
        public bool Equals(string str)
        {
            if (this.Length != str.Length) return false;
            return this.SequenceEqual<char>(str);
        }
        public bool Equals(char[] c_arr)
        {
            if (this.Length != c_arr.Length) return false;
            return this.SequenceEqual<char>(c_arr);
        }
        public bool Equals(char c)
        {
            return (this.Length == 1 && this[0] == c);
        }
        public bool Equals(IEnumerable<char> in_itr)
        {
            this.CheckReallocate();
            return this.SequenceEqual<char>(in_itr);
        }
        public static bool operator ==(StringEntity lhs, StringEntity rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(StringEntity lhs, StringEntity rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(StringEntity lhs, ReadOnlyStringEntity rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(StringEntity lhs, ReadOnlyStringEntity rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(StringEntity lhs, IEnumerable<char> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(StringEntity lhs, IEnumerable<char> rhs) { return !lhs.Equals(rhs); }
        public override bool Equals(object obj)
        {
            return obj is StringEntity && ((IJaggedArraySliceBase<char>)obj).Equals(_ptr, _len);
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

        public ReadOnlyStringEntity GetReadOnly()
        {
            return new ReadOnlyStringEntity(this);
        }

        public override string ToString()
        {
            this.CheckReallocate();
            return new string(_ptr, 0, _len);
        }
        public char[] ToArray()
        {
            this.CheckReallocate();
            char[] ret = new char[_len];
            for (int i = 0; i < _len; i++)
            {
                ret[i] = this[i];
            }
            return ret;
        }
        public NativeArray<char> ToNativeArray(Allocator alloc)
        {
            this.CheckReallocate();
            var ret = new NativeArray<char>(_len, alloc);
            UnsafeUtility.MemCpy(ret.GetUnsafePtr(), this.GetUnsafePtr(), UnsafeUtility.SizeOf<char>() * _len);
            return ret;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckElemIndex(int index)
        {
            if (index < 0 || _len <= index)
            {
                // simple exception patterns only can be used in BurstCompiler.
                throw new IndexOutOfRangeException("index is out of range.");
            }
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckSliceRange(int begin, int end)
        {
            int new_len = end - begin;
            if (end > _len) throw new ArgumentOutOfRangeException($"invalid range. end = {end} <= Length = {_len}");
            if (new_len < 0) throw new ArgumentOutOfRangeException($"invalid range. begin = {begin} > end = {end}.");
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckReallocate()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_gen_ptr == null && _gen_entity == -1) return;  // ignore case for NativeJaggedArraySliceGeneratorExt
            if (*(_gen_ptr) != _gen_entity)
            {
                throw new InvalidOperationException("this StringEntity is invalid reference.");
            }
#endif
        }

        public void* GetUnsafePtr() { return _ptr; }
    }

    public readonly unsafe struct ReadOnlyStringEntity :
        IParseExt,
        IJaggedArraySliceBase<char>,
        ISlice<ReadOnlyStringEntity>,
        IEquatable<string>, IEquatable<char[]>, IEquatable<IEnumerable<char>>, IEquatable<char>,
        IEnumerable<char>
    {
        private readonly StringEntity _entity;

        public int Length { get { return _entity.Length; } }

        public ReadOnlyStringEntity(NativeJaggedArraySlice<char> slice)
        {
            _entity = new StringEntity(slice);
        }
        public ReadOnlyStringEntity(StringEntity entity)
        {
            _entity = entity;
        }

        public char this[int index]
        {
            get { return _entity[index]; }
        }
        public char At(int index) { return _entity.At(index); }
        public ReadOnlyStringEntity Slice(int begin = -1, int end = -1)
        {
            return new ReadOnlyStringEntity(_entity.Slice(begin, end));
        }

        public IEnumerator<char> GetEnumerator()
        {
            for (int i = 0; i < this.Length; i++)
                yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public bool Equals(char* ptr, int Length)
        {
            return _entity.Equals(ptr, Length);
        }
        public unsafe bool Equals(NativeJaggedArraySlice<char> slice)
        {
            return _entity.Equals(slice);
        }
        public unsafe bool Equals(ReadOnlyNativeJaggedArraySlice<char> slice)
        {
            return _entity.Equals(slice);
        }
        public bool Equals(StringEntity entity)
        {
            return entity.Equals((char*)_entity.GetUnsafePtr(), _entity.Length);
        }
        public bool Equals(ReadOnlyStringEntity entity)
        {
            return entity.Equals((char*)_entity.GetUnsafePtr(), _entity.Length);
        }
        public bool Equals(string str)
        {
            if (_entity.Length != str.Length) return false;
            return this.SequenceEqual<char>(str);
        }
        public bool Equals(char[] c_arr)
        {
            if (_entity.Length != c_arr.Length) return false;
            return this.SequenceEqual<char>(c_arr);
        }
        public bool Equals(char c)
        {
            return (_entity.Length == 1 && this[0] == c);
        }
        public bool Equals(IEnumerable<char> str_itr)
        {
            return this.SequenceEqual<char>(str_itr);
        }
        public static bool operator ==(ReadOnlyStringEntity lhs, StringEntity rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyStringEntity lhs, StringEntity rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyStringEntity lhs, ReadOnlyStringEntity rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyStringEntity lhs, ReadOnlyStringEntity rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyStringEntity lhs, IEnumerable<char> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyStringEntity lhs, IEnumerable<char> rhs) { return !lhs.Equals(rhs); }
        public override bool Equals(object obj)
        {
            return obj is IJaggedArraySliceBase<char> && ((IJaggedArraySliceBase<char>)obj).Equals((char*)_entity.GetUnsafePtr(), _entity.Length);
        }
        public override int GetHashCode()
        {
            return _entity.GetHashCode();
        }

        public override string ToString()
        {
            return new string((char*)_entity.GetUnsafePtr(), 0, _entity.Length);
        }
        public char[] ToArray()
        {
            return _entity.ToArray();
        }
        public NativeArray<char> ToNativeArray(Allocator alloc)
        {
            return _entity.ToNativeArray(alloc);
        }
        public void* GetUnsafePtr() { return _entity.GetUnsafePtr(); }
    }


    namespace Utility
    {
        public static class StringEntityGeneratorExt
        {
            /// <summary>
            /// StringEntity generator for NativeList.
            /// it is not guaranteed the referenced data after something modify of source NativeList.
            /// </summary>
            /// <param name="source"></param>
            /// <returns></returns>
            public unsafe static StringEntity ToStringEntity(this NativeList<char> source)
            {
                return new StringEntity(source.ToNativeJaggedArraySlice());
            }
            /// <summary>
            /// StringEntity generator for NativeList.
            /// it is not guaranteed the referenced data after something modify of source NativeList.
            /// </summary>
            /// <param name="source"></param>
            /// <returns></returns>
            public unsafe static StringEntity ToStringEntity(this NativeArray<char> source)
            {
                return new StringEntity(source.ToNativeJaggedArraySlice());
            }
        }
    }
}
