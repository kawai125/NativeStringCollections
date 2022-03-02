using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
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
        IJaggedArraySliceBase<Char16>,
        ISlice<StringEntity>,
        IEnumerable<Char16>,
        IEquatable<StringEntity>,
        IEquatable<ReadOnlyStringEntity>,
        IEquatable<Char16[]>, IEquatable<IEnumerable<Char16>>, IEquatable<Char16>,
        IEquatable<char[]>, IEquatable<IEnumerable<char>>, IEquatable<char>,
        IEquatable<string>
    {
        [NativeDisableUnsafePtrRestriction]
        internal readonly Char16* _ptr;
        internal readonly int _len;

        public int Length { get { return _len; } }


#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [NativeDisableUnsafePtrRestriction]
        internal readonly long* _gen_ptr;
        internal readonly long _gen_entity;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public StringEntity(Char16* ptr, int Length, long* gen_ptr, long gen_entity)
        {
            _ptr = ptr;
            _len = Length;

            _gen_ptr = gen_ptr;
            _gen_entity = gen_entity;
        }
#else
        public StringEntity(Char16* ptr, int Length)
        {
            _ptr = ptr;
            _len = Length;
        }
#endif
        public StringEntity(NativeJaggedArraySlice<Char16> slice)
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

        public Char16 this[int index]
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
        public Char16 At(int index)
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

        public IEnumerator<Char16> GetEnumerator()
        {
            this.CheckReallocate();
            for (int i = 0; i < this.Length; i++)
                yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public static implicit operator ReadOnlyStringEntity(StringEntity val)
        {
            return val.GetReadOnly();
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
        public bool Equals(NativeJaggedArraySlice<Char16> slice)
        {
            this.CheckReallocate();
            return slice.Equals(_ptr, _len);
        }
        public bool Equals(ReadOnlyNativeJaggedArraySlice<Char16> slice)
        {
            this.CheckReallocate();
            return slice.Equals(_ptr, _len);
        }
        public bool Equals(Char16[] c_arr)
        {
            this.CheckReallocate();
            if (this.Length != c_arr.Length) return false;
            return this.SequenceEqual<Char16>(c_arr);
        }
        public bool Equals(Char16 c)
        {
            this.CheckReallocate();
            return (this.Length == 1 && this[0] == c);
        }
        public bool Equals(IEnumerable<Char16> in_itr)
        {
            this.CheckReallocate();
            return this.SequenceEqual<Char16>(in_itr);
        }
        public bool Equals(char[] c_arr)
        {
            this.CheckReallocate();
            if (this.Length != c_arr.Length) return false;
            var tmp = Utility.StringEntityGeneratorExt.StringEntityCastToCharSlice(this);
            return tmp.SequenceEqual<char>(c_arr);
        }
        public bool Equals(char c)
        {
            this.CheckReallocate();
            return (this.Length == 1 && this[0] == c);
        }
        public bool Equals(IEnumerable<char> in_itr)
        {
            this.CheckReallocate();
            var tmp = Utility.StringEntityGeneratorExt.StringEntityCastToCharSlice(this);
            return tmp.SequenceEqual<char>(in_itr);
        }
        public bool Equals(string str)
        {
            this.CheckReallocate();
            if (this.Length != str.Length) return false;
            var tmp = Utility.StringEntityGeneratorExt.StringEntityCastToCharSlice(this);
            return tmp.SequenceEqual<char>(str);
        }
        public static bool operator ==(StringEntity lhs, StringEntity rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(StringEntity lhs, StringEntity rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(StringEntity lhs, ReadOnlyStringEntity rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(StringEntity lhs, ReadOnlyStringEntity rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(StringEntity lhs, IEnumerable<Char16> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(StringEntity lhs, IEnumerable<Char16> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(StringEntity lhs, IEnumerable<char> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(StringEntity lhs, IEnumerable<char> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(StringEntity lhs, Char16[] rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(StringEntity lhs, Char16[] rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(StringEntity lhs, char[] rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(StringEntity lhs, char[] rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(StringEntity lhs, Char16 rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(StringEntity lhs, Char16 rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(StringEntity lhs, char rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(StringEntity lhs, char rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(IEnumerable<Char16> lhs, StringEntity rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(IEnumerable<Char16> lhs, StringEntity rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(IEnumerable<char> lhs, StringEntity rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(IEnumerable<char> lhs, StringEntity rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(Char16[] lhs, StringEntity rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(Char16[] lhs, StringEntity rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(char[] lhs, StringEntity rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(char[] lhs, StringEntity rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(Char16 lhs, StringEntity rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(Char16 lhs, StringEntity rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(char lhs, StringEntity rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(char lhs, StringEntity rhs) { return !rhs.Equals(lhs); }
        public override bool Equals(object obj)
        {
            return obj is StringEntity && ((IJaggedArraySliceBase<Char16>)obj).Equals(_ptr, _len);
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

        public int IndexOf(Char16 key, int start = 0)
        {
            this.CheckReallocate();
            this.CheckElemIndex(start);

            for (int i = start; i < _len; i++)
            {
                if (_ptr[i].Equals(key)) return i;
            }
            return -1;
        }
        public int IndexOf(string key, int start = 0)
        {
            this.CheckReallocate();
            this.CheckElemIndex(start);

            fixed (char* char_p = key)
            {
                Char16* tgt_ptr = (Char16*)char_p;
                return NativeJaggedArraySliceExt.SpanIndexOf(_ptr, Length, tgt_ptr, key.Length, start);
            }
        }
        public int LastIndexOf(Char16 key)
        {
            return this.LastIndexOf(key, Length - 1);
        }
        public int LastIndexOf(Char16 key, int start)
        {
            this.CheckReallocate();
            this.CheckElemIndex(start);

            for (int i = start; i >= 0; i--)
            {
                if (_ptr[i].Equals(key)) return i;
            }
            return -1;
        }
        public int LastIndexOf(string key)
        {
            return this.LastIndexOf(key, Length - key.Length);
        }
        public int LastIndexOf(string key, int start)
        {
            this.CheckReallocate();
            this.CheckElemIndex(start);

            fixed (char* char_p = key)
            {
                Char16* ptr = (Char16*)char_p;
                return NativeJaggedArraySliceExt.SpanLastIndexOf(_ptr, Length, ptr, key.Length, start);
            }
        }

        public ReadOnlyStringEntity GetReadOnly()
        {
            return new ReadOnlyStringEntity(this);
        }

        public override string ToString()
        {
            this.CheckReallocate();
            return new string((char*)_ptr, 0, _len);
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
        public NativeArray<Char16> AsArray()
        {
            var arr = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Char16>(_ptr, _len, Allocator.None);
            return arr;
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

        public void* GetUnsafePtr()
        {
            this.CheckReallocate();
            return _ptr;
        }
    }

    public readonly unsafe struct ReadOnlyStringEntity :
        IJaggedArraySliceBase<Char16>,
        ISlice<ReadOnlyStringEntity>,
        IEnumerable<Char16>,
        IEquatable<StringEntity>,
        IEquatable<ReadOnlyStringEntity>,
        IEquatable<Char16[]>, IEquatable<IEnumerable<Char16>>, IEquatable<Char16>,
        IEquatable<char[]>, IEquatable<IEnumerable<char>>, IEquatable<char>,
        IEquatable<string>
    {
        private readonly StringEntity _entity;

        public int Length { get { return _entity.Length; } }

        public ReadOnlyStringEntity(NativeJaggedArraySlice<Char16> slice)
        {
            _entity = new StringEntity(slice);
        }
        public ReadOnlyStringEntity(StringEntity entity)
        {
            _entity = entity;
        }

        public Char16 this[int index]
        {
            get { return _entity[index]; }
        }
        public Char16 At(int index) { return _entity.At(index); }
        public ReadOnlyStringEntity Slice(int begin = -1, int end = -1)
        {
            return new ReadOnlyStringEntity(_entity.Slice(begin, end));
        }

        public IEnumerator<Char16> GetEnumerator()
        {
            for (int i = 0; i < this.Length; i++)
                yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public unsafe bool Equals(NativeJaggedArraySlice<Char16> slice)
        {
            return _entity.Equals(slice);
        }
        public unsafe bool Equals(ReadOnlyNativeJaggedArraySlice<Char16> slice)
        {
            return _entity.Equals(slice);
        }
        public bool Equals(StringEntity entity)
        {
            return entity.Equals((Char16*)_entity.GetUnsafePtr(), _entity.Length);
        }
        public bool Equals(ReadOnlyStringEntity entity)
        {
            return entity.Equals((Char16*)_entity.GetUnsafePtr(), _entity.Length);
        }
        public bool Equals(Char16[] c_arr)
        {
            return _entity.Equals(c_arr);
        }
        public bool Equals(Char16 c)
        {
            return _entity.Equals(c);
        }
        public bool Equals(IEnumerable<Char16> in_itr)
        {
            return _entity.Equals(in_itr);
        }
        public bool Equals(char[] c_arr)
        {
            return _entity.Equals(c_arr);
        }
        public bool Equals(char c)
        {
            return _entity.Equals(c);
        }
        public bool Equals(IEnumerable<char> str_itr)
        {
            return _entity.Equals(str_itr);
        }
        public bool Equals(string str)
        {
            return _entity.Equals(str);
        }
        public static bool operator ==(ReadOnlyStringEntity lhs, StringEntity rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyStringEntity lhs, StringEntity rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyStringEntity lhs, ReadOnlyStringEntity rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyStringEntity lhs, ReadOnlyStringEntity rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyStringEntity lhs, IEnumerable<Char16> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyStringEntity lhs, IEnumerable<Char16> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyStringEntity lhs, IEnumerable<char> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyStringEntity lhs, IEnumerable<char> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyStringEntity lhs, Char16[] rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyStringEntity lhs, Char16[] rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyStringEntity lhs, char[] rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyStringEntity lhs, char[] rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyStringEntity lhs, Char16 rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyStringEntity lhs, Char16 rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyStringEntity lhs, char rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyStringEntity lhs, char rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(IEnumerable<Char16> lhs, ReadOnlyStringEntity rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(IEnumerable<Char16> lhs, ReadOnlyStringEntity rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(IEnumerable<char> lhs, ReadOnlyStringEntity rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(IEnumerable<char> lhs, ReadOnlyStringEntity rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(Char16[] lhs, ReadOnlyStringEntity rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(Char16[] lhs, ReadOnlyStringEntity rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(char[] lhs, ReadOnlyStringEntity rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(char[] lhs, ReadOnlyStringEntity rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(Char16 lhs, ReadOnlyStringEntity rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(Char16 lhs, ReadOnlyStringEntity rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(char lhs, ReadOnlyStringEntity rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(char lhs, ReadOnlyStringEntity rhs) { return !rhs.Equals(lhs); }
        public override bool Equals(object obj)
        {
            return obj is IJaggedArraySliceBase<char> && ((IJaggedArraySliceBase<char>)obj).Equals((char*)_entity.GetUnsafePtr(), _entity.Length);
        }
        public override int GetHashCode()
        {
            return _entity.GetHashCode();
        }

        public int IndexOf(Char16 key, int start = 0) { return _entity.IndexOf(key, start); }
        public int IndexOf(string key, int start = 0) { return _entity.IndexOf(key, start); }
        public int LastIndexOf(Char16 key) { return _entity.LastIndexOf(key); }
        public int LastIndexOf(string key) { return _entity.LastIndexOf(key); }
        public int LastIndexOf(Char16 key, int start) { return _entity.LastIndexOf(key, start); }
        public int LastIndexOf(string key, int start) { return _entity.LastIndexOf(key, start); }

        public override string ToString()
        {
            return new string((char*)_entity.GetUnsafePtr(), 0, _entity.Length);
        }
        public char[] ToArray()
        {
            return _entity.ToArray();
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
                return new StringEntity(CharSliceCastToStringEntity(source.ToNativeJaggedArraySlice()));
            }
            /// <summary>
            /// StringEntity generator for NativeList.
            /// it is not guaranteed the referenced data after something modify of source NativeList.
            /// </summary>
            /// <param name="source"></param>
            /// <returns></returns>
            public unsafe static StringEntity ToStringEntity(this NativeList<Char16> source)
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
                var tmp = source.ToNativeJaggedArraySlice();
                return new StringEntity(CharSliceCastToStringEntity(source.ToNativeJaggedArraySlice()));
            }
            /// <summary>
            /// StringEntity generator for NativeList.
            /// it is not guaranteed the referenced data after something modify of source NativeList.
            /// </summary>
            /// <param name="source"></param>
            /// <returns></returns>
            public unsafe static StringEntity ToStringEntity(this NativeArray<Char16> source)
            {
                var tmp = source.ToNativeJaggedArraySlice();
                return new StringEntity(source.ToNativeJaggedArraySlice());
            }

            internal unsafe static NativeJaggedArraySlice<char> StringEntityCastToCharSlice(StringEntity se)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new NativeJaggedArraySlice<char>((char*)se._ptr, se._len, se._gen_ptr, se._gen_entity);
#else
                return new NativeJaggedArraySlice<char>((char*)se._ptr, se._len);
#endif
            }
            internal unsafe static StringEntity CharSliceCastToStringEntity(NativeJaggedArraySlice<char> slice)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new StringEntity((Char16*)slice._ptr, slice._len, slice._gen_ptr, slice._gen_entity);
#else
                return new StringEntity((Char16*)slice._ptr, slice._len);
#endif
            }
        }
    }
}
