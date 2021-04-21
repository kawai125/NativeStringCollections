using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections
{
    using NativeStringCollections.Impl;


    unsafe public interface IJaggedArraySliceBase<T> where T: unmanaged, IEquatable<T>
    {
        int Length { get; }
        T this[int index] { get; }
        bool Equals(NativeJaggedArraySlice<T> slice);
        bool Equals(ReadOnlyNativeJaggedArraySlice<T> slice);
        bool Equals(T* ptr, int Length);
        int IndexOf(T key, int start);
        void* GetUnsafePtr();
    }
    public interface ISlice<T>
    {
        T Slice(int begin = -1, int end = -1);
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct NativeJaggedArraySlice<T> :
        IJaggedArraySliceBase<T>,
        IEnumerable<T>,
        IEquatable<NativeJaggedArraySlice<T>>,
        IEquatable<ReadOnlyNativeJaggedArraySlice<T>>,
        IEquatable<IEnumerable<T>>, IEquatable<T>,
        IEquatable<NativeList<T>>, IEquatable<NativeArray<T>>,
        ISlice<NativeJaggedArraySlice<T>>
        where T: unmanaged, IEquatable<T>
    {
        [NativeDisableUnsafePtrRestriction]
        internal readonly T* _ptr;
        internal readonly int _len;

        public int Length { get { return _len; } }


#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [NativeDisableUnsafePtrRestriction]
        internal readonly long* _gen_ptr;
        internal readonly long _gen_entity;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public NativeJaggedArraySlice(T* ptr, int Length, long* gen_ptr, long gen_entity)
        {
            _ptr = ptr;
            _len = Length;

            _gen_ptr = gen_ptr;
            _gen_entity = gen_entity;
        }
#else
        public NativeJaggedArraySlice(T* ptr, int Length)
        {
            _ptr = ptr;
            _len = Length;
        }
#endif
        public NativeJaggedArraySlice(NativeJaggedArraySlice<T> slice)
        {
            _ptr = slice._ptr;
            _len = slice._len;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            _gen_ptr = slice._gen_ptr;
            _gen_entity = slice._gen_entity;
#endif
        }

        public T this[int index]
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
        public T At(int index)
        {
            this.CheckReallocate();
            this.CheckElemIndex(index);
            return this[index];
        }
        public NativeJaggedArraySlice<T> Slice(int begin = -1, int end = -1)
        {
            if (begin < 0) begin = 0;
            if (end < 0) end = _len;
            this.CheckSliceRange(begin, end);

            int new_len = end - begin;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.CheckReallocate();
            return new NativeJaggedArraySlice<T>(_ptr + begin, new_len, _gen_ptr, _gen_entity);
#else
            return new NativeJaggedArraySlice<T>(_ptr + begin, new_len);
#endif
        }

        public IEnumerator<T> GetEnumerator()
        {
            this.CheckReallocate();
            for (int i = 0; i < this.Length; i++)
                yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public ReadOnlyNativeJaggedArraySlice<T> GetReadOnlySlice() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.CheckReallocate();
            return new ReadOnlyNativeJaggedArraySlice<T>(_ptr, _len, _gen_ptr, _gen_entity);
#else
            return new ReadOnlyNativeJaggedArraySlice<T>(_ptr, _len);
#endif
        }
        public static implicit operator ReadOnlyNativeJaggedArraySlice<T>(NativeJaggedArraySlice<T> val)
        {
            return val.GetReadOnlySlice();
        }

        public bool Equals(T* ptr, int Length)
        {
            this.CheckReallocate();
            if (_len != Length) return false;

            // pointing same target
            if (_ptr == ptr) return true;

            for(int i=0; i<_len; i++)
            {
                if (!_ptr[i].Equals(ptr[i])) return false;
            }

            return true;
        }
        public bool Equals(NativeJaggedArraySlice<T> slice)
        {
            this.CheckReallocate();
            return slice.Equals(_ptr, _len);
        }
        public bool Equals(ReadOnlyNativeJaggedArraySlice<T> slice)
        {
            this.CheckReallocate();
            return slice.Equals(_ptr, _len);
        }
        public bool Equals(NativeList<T> list)
        {
            this.CheckReallocate();
            return this.Equals((T*)list.GetUnsafePtr(), list.Length);
        }
        public bool Equals(NativeArray<T> array)
        {
            this.CheckReallocate();
            return this.Equals((T*)array.GetUnsafePtr(), array.Length);
        }
        public bool Equals(IEnumerable<T> in_itr)
        {
            this.CheckReallocate();
            return this.SequenceEqual<T>(in_itr);
        }
        public bool Equals(T rhs)
        {
            if (this.Length == 1 && _ptr->Equals(rhs)) return true;
            return false;
        }
        public static bool operator ==(NativeJaggedArraySlice<T> lhs, NativeJaggedArraySlice<T> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(NativeJaggedArraySlice<T> lhs, NativeJaggedArraySlice<T> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(NativeJaggedArraySlice<T> lhs, ReadOnlyNativeJaggedArraySlice<T> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(NativeJaggedArraySlice<T> lhs, ReadOnlyNativeJaggedArraySlice<T> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(NativeJaggedArraySlice<T> lhs, IEnumerable<T> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(NativeJaggedArraySlice<T> lhs, IEnumerable<T> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(NativeJaggedArraySlice<T> lhs, NativeList<T> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(NativeJaggedArraySlice<T> lhs, NativeList<T> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(NativeJaggedArraySlice<T> lhs, NativeArray<T> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(NativeJaggedArraySlice<T> lhs, NativeArray<T> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(NativeJaggedArraySlice<T> lhs, T rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(NativeJaggedArraySlice<T> lhs, T rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(IEnumerable<T> lhs, NativeJaggedArraySlice<T> rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(IEnumerable<T> lhs, NativeJaggedArraySlice<T> rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(NativeList<T> lhs, NativeJaggedArraySlice<T> rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(NativeList<T> lhs, NativeJaggedArraySlice<T> rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(NativeArray<T> lhs, NativeJaggedArraySlice<T> rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(NativeArray<T> lhs, NativeJaggedArraySlice<T> rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(T lhs, NativeJaggedArraySlice<T> rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(T lhs, NativeJaggedArraySlice<T> rhs) { return !rhs.Equals(lhs); }
        public override bool Equals(object obj)
        {
            return obj is NativeJaggedArraySlice<T> && ((IJaggedArraySliceBase<T>)obj).Equals(_ptr, _len);
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

        public int IndexOf(T key, int start = 0 )
        {
            this.CheckReallocate();
            this.CheckElemIndex(start);

            for(int i=start; i<_len; i++)
            {
                if (_ptr[i].Equals(key)) return i;
            }
            return -1;
        }

        public override string ToString()
        {
            this.CheckReallocate();
            var sb = new System.Text.StringBuilder();
            sb.Append($"NativeJaggedArraySlice<{nameof(T)}> length = {this.Length}, elems = ");
            if(this.Length > 0)
            {
                sb.Append('{');
                bool first_elem = true;
                for(int i=0; i<this.Length; i++)
                {
                    sb.Append('[');
                    if (!first_elem)
                    {
                        sb.Append(", ");
                    }
                    else
                    {
                        first_elem = false;
                    }
                    sb.Append(this[i].ToString());
                    sb.Append(']');
                }
                sb.Append('}');
            }
            else
            {
                sb.Append("empty.");
            }
            return sb.ToString();
        }
        public T[] ToArray()
        {
            this.CheckReallocate();
            T[] ret = new T[_len];
            for(int i=0; i<_len; i++)
            {
                ret[i] = this[i];
            }
            return ret;
        }
        public NativeArray<T> ToNativeArray(Allocator alloc)
        {
            this.CheckReallocate();
            var ret = new NativeArray<T>(_len, alloc);
            UnsafeUtility.MemCpy(ret.GetUnsafePtr(), this.GetUnsafePtr(), UnsafeUtility.SizeOf<T>() * _len);
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
            if(_gen_ptr == null && _gen_entity == -1) return;  // ignore case for NativeJaggedArraySliceGeneratorExt
            if( *(_gen_ptr) != _gen_entity)
            {
                throw new InvalidOperationException("this slice is invalid reference.");
            }
#endif
        }

        public void* GetUnsafePtr() { return _ptr; }
    }
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct ReadOnlyNativeJaggedArraySlice<T> :
        IJaggedArraySliceBase<T>,
        IEnumerable<T>,
        IEquatable<NativeJaggedArraySlice<T>>,
        IEquatable<ReadOnlyNativeJaggedArraySlice<T>>,
        IEquatable<IEnumerable<T>>, IEquatable<T>,
        IEquatable<NativeList<T>>, IEquatable<NativeArray<T>>,
        ISlice<ReadOnlyNativeJaggedArraySlice<T>>
        where T : unmanaged, IEquatable<T>
    {
        private readonly NativeJaggedArraySlice<T> _slice;

        public int Length { get { return _slice.Length; } }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public ReadOnlyNativeJaggedArraySlice(T* ptr, int Length, long* gen_ptr, long gen_entity)
        {
            _slice = new NativeJaggedArraySlice<T>(ptr, Length, gen_ptr, gen_entity);
        }
#else
        public ReadOnlyNativeJaggedArraySlice(T* ptr, int Length)
        {
            _slice = new NativeJaggedArraySlice<T>(ptr, Length);
        }
#endif
        public ReadOnlyNativeJaggedArraySlice(NativeJaggedArraySlice<T> slice)
        {
            _slice = slice;
        }

        public T this[int index] { get { return _slice[index]; } }
        public T At(int index)
        {
            return _slice.At(index);
        }
        public ReadOnlyNativeJaggedArraySlice<T> Slice(int begin = -1, int end = -1)
        {
            return _slice.Slice(begin, end).GetReadOnlySlice();
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < this.Length; i++)
                yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public bool Equals(T* ptr, int Length)
        {
            return _slice.Equals(ptr, Length);
        }
        public bool Equals(NativeJaggedArraySlice<T> slice)
        {
            return _slice.Equals(slice);
        }
        public bool Equals(ReadOnlyNativeJaggedArraySlice<T> slice)
        {
            return _slice.Equals(slice);
        }
        public bool Equals(T value)
        {
            return _slice.Equals(value);
        }
        public bool Equals(NativeList<T> list)
        {
            return _slice.Equals(list);
        }
        public bool Equals(NativeArray<T> array)
        {
            return _slice.Equals(array);
        }
        public bool Equals(IEnumerable<T> in_itr)
        {
            return _slice.SequenceEqual<T>(in_itr);
        }
        public static bool operator ==(ReadOnlyNativeJaggedArraySlice<T> lhs, NativeJaggedArraySlice<T> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyNativeJaggedArraySlice<T> lhs, NativeJaggedArraySlice<T> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyNativeJaggedArraySlice<T> lhs, ReadOnlyNativeJaggedArraySlice<T> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyNativeJaggedArraySlice<T> lhs, ReadOnlyNativeJaggedArraySlice<T> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyNativeJaggedArraySlice<T> lhs, IEnumerable<T> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyNativeJaggedArraySlice<T> lhs, IEnumerable<T> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyNativeJaggedArraySlice<T> lhs, NativeList<T> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyNativeJaggedArraySlice<T> lhs, NativeList<T> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyNativeJaggedArraySlice<T> lhs, NativeArray<T> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyNativeJaggedArraySlice<T> lhs, NativeArray<T> rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(ReadOnlyNativeJaggedArraySlice<T> lhs, T rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyNativeJaggedArraySlice<T> lhs, T rhs) { return !lhs.Equals(rhs); }
        public static bool operator ==(IEnumerable<T> lhs, ReadOnlyNativeJaggedArraySlice<T> rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(IEnumerable<T> lhs, ReadOnlyNativeJaggedArraySlice<T> rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(NativeList<T> lhs, ReadOnlyNativeJaggedArraySlice<T> rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(NativeList<T> lhs, ReadOnlyNativeJaggedArraySlice<T> rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(NativeArray<T> lhs, ReadOnlyNativeJaggedArraySlice<T> rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(NativeArray<T> lhs, ReadOnlyNativeJaggedArraySlice<T> rhs) { return !rhs.Equals(lhs); }
        public static bool operator ==(T lhs, ReadOnlyNativeJaggedArraySlice<T> rhs) { return rhs.Equals(lhs); }
        public static bool operator !=(T lhs, ReadOnlyNativeJaggedArraySlice<T> rhs) { return !rhs.Equals(lhs); }
        public override bool Equals(object obj)
        {
            return obj is ReadOnlyNativeJaggedArraySlice<T> && ((IJaggedArraySliceBase<T>)obj).Equals(_slice);
        }
        public override int GetHashCode()
        {
            return _slice.GetHashCode();
        }

        public int IndexOf(T key, int start = 0) { return _slice.IndexOf(key, start); }

        public override string ToString()
        {
            return "ReadOnly" + _slice.ToString();
        }
        public T[] ToArray()
        {
            return _slice.ToArray();
        }

        public void* GetUnsafePtr() { return _slice.GetUnsafePtr(); }
    }


    namespace Utility
    {
        public static class NativeJaggedArraySliceGeneratorExt
        {
            /// <summary>
            /// NativeJaggedArraySlice generator for NativeList.
            /// it is not guaranteed the referenced data after something modify of source NativeList.
            /// </summary>
            /// <param name="source"></param>
            /// <returns></returns>
            public unsafe static NativeJaggedArraySlice<T> ToNativeJaggedArraySlice<T>(this NativeList<T> source)
                where T : unmanaged, IEquatable<T>
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new NativeJaggedArraySlice<T>((T*)source.GetUnsafePtr(), source.Length, null, -1);
#else
                return new NativeJaggedArraySlice<T>((T*)source.GetUnsafePtr(), source.Length);
#endif
            }
            /// <summary>
            /// StringEntity generator for NativeList.
            /// it is not guaranteed the referenced data after something modify of source NativeList.
            /// </summary>
            /// <param name="source"></param>
            /// <returns></returns>
            public unsafe static NativeJaggedArraySlice<T> ToNativeJaggedArraySlice<T>(this NativeArray<T> source)
                where T : unmanaged, IEquatable<T>
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new NativeJaggedArraySlice<T>((T*)source.GetUnsafePtr(), source.Length, null, -1);
#else
                return new NativeJaggedArraySlice<T>((T*)source.GetUnsafePtr(), source.Length);
#endif
            }
        }
    }
}
