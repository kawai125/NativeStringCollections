
#define NATIVE_STRING_COLLECTION_TRACE_REALLOCATION

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


    public class NativeStringList : IDisposable, IEnumerable<StringEntity>
    {
        private NativeList<char> char_arr;
        private NativeList<ElemIndex> elemIndexList;
        private NativeArray<ulong> genTrace;
        private bool allocated = false;

        private void Init(int char_array_size, int elem_size, Allocator alloc)
        {
            this.char_arr = new NativeList<char>(char_array_size, alloc);
            this.elemIndexList = new NativeList<ElemIndex>(elem_size, alloc);
            this.genTrace = new NativeArray<ulong>(1, Allocator.Persistent);
            this.allocated = true;
        }
        public void Dispose()
        {
            if (this.allocated)
            {
                this.char_arr.Dispose();
                this.elemIndexList.Dispose();
                this.genTrace.Dispose();

                this.allocated = false;
            }
        }

        public bool IsCreated { get { return this.allocated; } }

        /// <summary>
        /// The constructor
        /// </summary>
        public NativeStringList(int char_array_size, int elem_size, Allocator alloc)
        {
            this.Init(char_array_size, elem_size, alloc);
        }
        /// <summary>
        /// Construct with Allocator.Persistent
        /// </summary>
        public NativeStringList(int char_array_size, int elem_size)
        {
            this.Init(char_array_size, elem_size, Allocator.Persistent);
        }
        /// <summary>
        /// Construct with char_array_size = 128, elem_size = 16, Allocator.Persistent
        /// </summary>
        public NativeStringList()
        {
            this.Init(128, 16, Allocator.Persistent);
        }
        ~NativeStringList()
        {
            this.Dispose();
        }

        public void Clear()
        {
            this.char_arr.Clear();
            this.elemIndexList.Clear();
        }

        public int Length { get { return this.elemIndexList.Length; } }
        public int Size { get { return this.char_arr.Length; } }
        public int Capacity
        {
            get
            {
                return this.char_arr.Capacity;
            }
            set
            {
                if (value < this.char_arr.Length)
                {
                    throw new ArgumentOutOfRangeException("the new capacity is too small. capacity = " + value.ToString()
                        + ", Length = " + this.char_arr.Length.ToString());
                }
                if (value == this.char_arr.Length) return;

                var tmp = new NativeList<char>(value, Allocator.Persistent);
                tmp.Clear();
                tmp.AddRange(this.char_arr);

                this.char_arr.Dispose();
                this.char_arr = tmp;
            }
        }
        public int IndexCapacity
        {
            get
            {
                return this.elemIndexList.Capacity;
            }
            set
            {
                if (value < this.elemIndexList.Length)
                {
                    throw new ArgumentOutOfRangeException("the new capacity is too small. capacity = " + value.ToString()
                        + ", Length = " + this.elemIndexList.Length.ToString());
                }
                if (value == this.elemIndexList.Length) return;

                var tmp = new NativeList<ElemIndex>(value, Allocator.Persistent);
                tmp.Clear();
                tmp.AddRange(this.elemIndexList);

                this.elemIndexList.Dispose();
                this.elemIndexList = tmp;
            }
        }

        unsafe public StringEntity this[int index]
        {
            get
            {
                var elem_index = this.elemIndexList[index];
#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            //    UnityEngine.Debug.Log("index: " + index.ToString()
            //                      +  "ptr: " + ((int)this.char_arr.GetUnsafePtr()).ToString() + ", Start: " + elem_index.Start.ToString() + ", Length: " + elem_index.Length.ToString()
            //                      + ", GenPtr: " + ((ulong)this.GetGenPtr()).ToString() + ", Gen: " + this.GetGen().ToString());
                return new StringEntity((char*)this.char_arr.GetUnsafePtr(), this.GetGenPtr(), this.GetGen(), elem_index.Start, elem_index.Length);
#else
                return new StringEntity((char*)this.char_arr.GetUnsafePtr(), elem_index.Start, elem_index.Length);
#endif
            }
        }
        public StringEntity At(int index)
        {
            this.CheckElemIndex(index);
            return this[index];
        }
        unsafe public string SubString(int index)
        {
            var elem_index = this.elemIndexList[index];
            return new string((char*)this.char_arr.GetUnsafeReadOnlyPtr(), elem_index.Start, elem_index.Length);
        }
        public IEnumerator<StringEntity> GetEnumerator()
        {
            for (int i = 0; i < this.Length; i++)
                yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public void Add(string str)
        {
#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            ulong prev_sig = this.GetGenSigneture();
#endif

            int Start = this.char_arr.Length;
            for (int i = 0; i < str.Length; i++)
            {
                this.char_arr.Add(str[i]);
            }
            this.elemIndexList.Add(new ElemIndex(Start, str.Length));

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            ulong next_sig = this.GetGenSigneture();
            if (prev_sig != next_sig) this.NextGen();
#endif
        }
        unsafe public void Add(char* ptr, int Length)
        {
#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            ulong prev_sig = this.GetGenSigneture();
#endif

            int Start = this.char_arr.Length;
            for (int i = 0; i < Length; i++)
            {
                this.char_arr.Add(ptr[i]);
            }
            this.elemIndexList.Add(new ElemIndex(Start, Length));

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            ulong next_sig = this.GetGenSigneture();
            if (prev_sig != next_sig) this.NextGen();
#endif
        }
        public void Add(IStringEntityBase entity)
        {
#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            ulong prev_sig = this.GetGenSigneture();
#endif

            int Start = this.char_arr.Length;
            for (int i = 0; i < entity.Length; i++)
            {
                this.char_arr.Add(entity[i]);
            }
            this.elemIndexList.Add(new ElemIndex(Start, entity.Length));

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            ulong next_sig = this.GetGenSigneture();
            if (prev_sig != next_sig) this.NextGen();
#endif
        }

        /// <summary>
        /// Get the index of the entity. This means "is the entity points effective area.", cannot guarantee the consistency of pointing content.
        /// Use 'IndexOf(string key)' API for check contents.
        /// </summary>
        /// <param name="key">entity</param>
        /// <returns>index or -1 (not found)</returns>
        unsafe public int IndexOf(IStringEntityBase key)
        {
            if (this.elemIndexList.Length < 1) return -1;
            if (!key.EqualsStringEntity((char*)this.char_arr.GetUnsafePtr(), key.Start, key.Length)) return -1;

            int left = 0;
            int right = this.elemIndexList.Length - 1;
            while (right >= left)
            {
                int mid = left + (right - left) / 2;
                IStringEntityBase entity = this[mid];

                if (key.Equals(entity)) return mid;
                else if (entity.Start > key.Start) right = mid - 1;
                else if (entity.Start < key.Start) left = mid + 1;
                else return -1;
            }
            return -1;
        }
        /// <summary>
        /// Get the index of the entity. This method performs full search.
        /// </summary>
        public int IndexOf(string key)
        {
            if (this.elemIndexList.Length < 1) return -1;
            for(int i=0; i<this.elemIndexList.Length; i++)
            {
                var entity = this[i];
                if (entity.Equals(key)) return i;
            }
            return -1;
        }

        public void RemoveAt(int index)
        {
            this.CheckElemIndex(index);
            for (int i = index; i < this.Length - 1; i++)
            {
                this.elemIndexList[i] = this.elemIndexList[i + 1];
            }
            this.elemIndexList.ResizeUninitialized(this.Length - 1);
        }
        public void RemoveRange(int index, int count)
        {
            this.CheckElemIndex(index);
            if (count < 0) throw new ArgumentOutOfRangeException("count must be > 0. count = " + count.ToString());
            if (index + count >= this.Length)
            {
                string txt = "invalid range. index = " + index.ToString()
                           + ", count = " + count.ToString() + ", must be < Length = " + this.Length.ToString();
                throw new ArgumentOutOfRangeException(txt);
            }

            for(int i=index; i<this.Length - count; i++)
            {
                this.elemIndexList[i] = this.elemIndexList[i + count];
            }
            this.elemIndexList.ResizeUninitialized(this.Length - count);
        }

        /// <summary>
        /// Re adjust the internal char buffer to justifing data after calling RemoveAt() or RemoveRange().
        /// All StringEntities are disabled after calling this function.
        /// </summary>
        public void ReAdjustment()
        {
            //return;

            if (this.Length <= 0) return;

            int gap = 0;
            int index_start = 1;
            ElemIndex prev_index = this.elemIndexList[0];

            //UnityEngine.Debug.Log("elemIndexList[0]: Start = " + prev_index.Start
            //            + ", Length = " + prev_index.Length.ToString() + ", End =" + prev_index.End.ToString() );

            // this[0] is not on the head
            if (prev_index.Start > 0)
            {
                index_start = 0;
                prev_index = new ElemIndex(0, 0);
            }

            for(int i_index=index_start; i_index<this.elemIndexList.Length; i_index++)
            {
                ElemIndex now_index = this.elemIndexList[i_index];
                gap = now_index.Start - prev_index.End;
                
            //    UnityEngine.Debug.Log("now_index: Start = " + now_index.Start
            //             + ", Length = " + now_index.Length.ToString() + ", End =" + now_index.End.ToString() + ", gap = " + gap.ToString());

                if (gap > 0)
                {
            //        UnityEngine.Debug.Log("index = " + i_index
            //           + ", shift data: [" + now_index.Start.ToString() + "-" + (now_index.End-1).ToString()
            //            + "] -> [" + prev_index.End.ToString() + "-" + (prev_index.End+now_index.Length-1).ToString() + "]");

                    int i_start = prev_index.End;
                    for (int i_data=0; i_data<now_index.Length; i_data++)
                    {
                        this.char_arr[i_start + i_data] = this.char_arr[now_index.Start + i_data];
                    }
                }

                prev_index = new ElemIndex(prev_index.End, now_index.Length);
                this.elemIndexList[i_index] = prev_index;
            }

            this.char_arr.ResizeUninitialized( this.char_arr.Length - gap );
            if (gap > 0) this.NextGen();
        }
        /// <summary>
        /// Shrink internal buffer size to fit present data length.
        /// Calling ReAdjuxtment() previously is recommended to eliminate the gap data.
        /// </summary>
        public void ShrinkToFit()
        {
            return;

            ulong prev_sig = this.GetGenSigneture();

            this.char_arr.Capacity = this.char_arr.Length;
            this.elemIndexList.Capacity = this.elemIndexList.Length;

            ulong next_sig = this.GetGenSigneture();
            if (prev_sig != next_sig) this.NextGen();
        }

        private void CheckElemIndex(int index)
        {
            if (index < 0 || this.Length <= index)
            {
                throw new IndexOutOfRangeException("index = " + index.ToString() + ", must be in range of [0~" + (this.Length - 1).ToString() + "].");
            }
        }
        private void NextGen()
        {
            ulong now_gen = this.genTrace[0];
            this.genTrace[0] = now_gen + 1;
        }
        private ulong GetGen() { return this.genTrace[0]; }
        unsafe private ulong* GetGenPtr() { return (ulong*)this.genTrace.GetUnsafeReadOnlyPtr(); }
        unsafe private ulong GetGenSigneture() { return (ulong)this.char_arr.GetUnsafePtr(); }
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
            this.Start = start;
            this.Length = Length;
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
        //    UnityEngine.Debug.Log("ptr: " + ((int)this.root_ptr).ToString() + ", Start: " + this.Start.ToString() + ", Length: " + this.Length.ToString());
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
            this.Start = start;
            this.Length = Length;
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

    /*
    [StructLayout(LayoutKind.Auto)]
    public unsafe struct ReadOnlyStringEntity :
       IParseExt,
       IStringEntityBase,
       IEquatable<string>, IEquatable<char[]>, IEquatable<IEnumerable<char>>, IEquatable<char>,
       IEnumerable<char>
    {
        private char* root_ptr;

        public int Start { get; private set; }
        public int Length { get; private set; }

        public int End { get { return this.Start + this.Length; } }

        public ReadOnlyStringEntity(char* ptr, int start, int Length)
        {
            this.root_ptr = ptr;
            this.Start = start;
            this.Length = Length;
        }

        public char this[int index]
        {
            get
            {
                return *(this.root_ptr + this.Start + index);
            }
        }
        public char At(int index)
        {
            this.CheckCharIndex(index);
            return this[index];
        }

        public IEnumerator<char> GetEnumerator()
        {
            for (int i = 0; i < this.Length; i++)
                yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public bool EqualsStringEntity(char* ptr, int Start, int Length)
        {
            if (this.Length != Length) return false;
            if (this.root_ptr == ptr && this.Start == Start) return true;
            for (int i = 0; i < Length; i++)
            {
                if (this[i] != ptr[i]) return false;
            }
            return true;
        }
        public bool Equals(IStringEntityBase entity) { return entity.EqualsStringEntity(this.root_ptr, this.Start, this.Length); }
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
        public bool Equals(char c) { return (this.Length == 1 && this[0] == c); }
        public bool Equals(IEnumerable<char> str_itr) { return this.SequenceEqual<char>(str_itr); }
        public static bool operator ==(ReadOnlyStringEntity lhs, IEnumerable<char> rhs) { return lhs.Equals(rhs); }
        public static bool operator !=(ReadOnlyStringEntity lhs, IEnumerable<char> rhs) { return !lhs.Equals(rhs); }
        public override bool Equals(object obj)
        {
            return obj is IStringEntityBase && ((IStringEntityBase)obj).EqualsStringEntity(this.root_ptr, this.Start, this.Length);
        }
        public override int GetHashCode()
        {
            int hash = this.Length.GetHashCode();
            for (int i = 0; i < this.Length; i++)
            {
                hash = hash ^ this[i].GetHashCode();
            }
            return hash;
        }

        public override string ToString() { return new string(this.root_ptr, this.Start, this.Length); }
        public char[] ToCharArray()
        {
            char[] ret = new char[this.Length];
            for (int i = 0; i < this.Length; i++)
            {
                ret[i] = this[i];
            }
            return ret;
        }

        private void CheckCharIndex(int index)
        {
            if (index < 0 || this.Length <= index)
            {
                throw new IndexOutOfRangeException("index = " + index.ToString() + ", must be in range of [0~" + (this.Length - 1).ToString() + "].");
            }
        }
    }
    */

    public static class StringEntityExtentions
    {

        public static bool TryParse(this IParseExt value, out bool result)
        {
            if (value.Length == 5)
            {
                // match "False" or "false"
                if (value[0] == 'f' || value[0] == 'F')
                {
                    if (value[1] == 'a' && value[2] == 'l' && value[3] == 's' && value[4] == 'e')
                    {
                        result = false;
                        return true;
                    }
                }
            }
            else if (value.Length == 4)
            {
                // match "true" or "True"
                if (value[0] == 't' || value[0] == 'T')
                {
                    if (value[1] == 'r' && value[2] == 'u' && value[3] == 'e')
                    {
                        result = true;
                        return true;
                    }
                }
            }
            else if(value.Length == 1)
            {
                // match "0" or "1"
                if (value[0] == '0')
                {
                    result = false;
                    return true;
                }
                else if(value[0] == '1')
                {
                    result = true;
                    return true;
                }
            }
            result = false;
            return false;
        }

        public static bool TryParse(this IParseExt value, out int result)
        {
            const int max_len = 10;
            const int max_val = 214748364;  // check at (max_len - 1) digit

            result = 0;
            if (value.Length <= 0) return false;

            int i_start = 0;
            if (value[0].IsSign(out int sign)) i_start = 1;

            if (value.Length - i_start > max_len) return false;

            int tmp = 0;
            for (int i = i_start; i < value.Length; i++)
            {
                if (value[i].IsDigit(out int d))
                {
                    tmp = tmp * 10 + d;
                    if (i == (max_len - i_start - 1) && tmp > max_val) return false;
                    if (i == (max_len - i_start) && ((sign == -1 && d == 8) || (sign == 1 && d == 7))) return false;
                }
                else
                {
                    return false;
                }
            }
            result = sign * tmp;
            return true;
        }
        public static bool TryParse(this IParseExt value, out long result)
        {
            const int max_len = 19;
            const long max_val = 922337203685477580;  // check at (max_len - 1) digit

            result = 0;
            if (value.Length <= 0) return false;

            int i_start = 0;
            if (value[0].IsSign(out int sign)) i_start = 1;

            if (value.Length - i_start > max_len) return false;

            long tmp = 0;
            for (int i = i_start; i < value.Length; i++)
            {
                if (value[i].IsDigit(out int d))
                {
                    tmp = tmp * 10 + d;
                    if (i == (max_len - 1 - i_start) && tmp > max_val) return false;
                    if (i == (max_len - i_start) && ((sign == -1 && d == 8) || (sign == 1 && d == 7))) return false;
                }
                else
                {
                    return false;
                }
            }
            result = sign * tmp;
            return true;
        }

        public static bool TryParse(this IParseExt value, out float result)
        {
            result = 0.0f;
            if (value.Length <= 0) return false;
            if (value.TryParseFloatFormat(out int sign, out int i_start, out int dot_pos, out int exp_pos, out int n_pow)) return false;

            float mantissa = 0.0f;
            for (int i = exp_pos - 1; i >= i_start; i--)
            {
                if (i == dot_pos) continue;
                mantissa = mantissa * 0.1f + (float)value[i].ToInt();
            }

            mantissa *= sign;

            // range check (-1.17549e-38 ~ 3.40282e+38)
            if (math.abs(n_pow) > 38)
            {
                return false;
            }
            else if (math.abs(n_pow) == 38 && (mantissa <= -1.17549f || 3.40282f <= mantissa))
            {
                return false;
            }

            result = mantissa * math.pow(10.0f, n_pow);
            return true;
        }
        public static bool TryParse(this IParseExt value, out double result)
        {
            result = 0.0f;
            if (value.Length <= 0) return false;
            if (value.TryParseFloatFormat(out int sign, out int i_start, out int dot_pos, out int exp_pos, out int n_pow)) return false;

            double mantissa = 0.0;
            for (int i = exp_pos - 1; i >= i_start; i--)
            {
                if (i == dot_pos) continue;
                mantissa = mantissa * 0.1 + (double)value[i].ToInt();
            }

            mantissa *= sign;

            // range check (2.22507e-308 ~ 1.79769e+308)
            if (math.abs(n_pow) > 308)
            {
                return false;
            }
            else if (math.abs(n_pow) == 308 && (mantissa <= -2.22507 || 1.79769 <= mantissa))
            {
                return false;
            }

            result = mantissa * math.pow(10.0, n_pow);
            return true;
        }

        private static bool TryParseFloatFormat(this IParseExt value, out int sign, out int i_start, out int dot_pos, out int exp_pos, out int n_pow)
        {
            i_start = 0;
            if (value[0].IsSign(out sign)) i_start = 1;

            dot_pos = -1;
            exp_pos = -1;

            n_pow = 0;
            int exp_sign = 1;

            // format check
            for (int i = i_start; i < value.Length; i++)
            {
                char c = value[i];
                if (c.IsDigit(out int dummy))
                {
                    // do nothing
                }
                else if (c.IsDot())
                {
                    if (dot_pos != -1 || dot_pos == i_start + 1) return false;
                    if (exp_pos > 0 && i >= exp_pos) return false;
                    dot_pos = i;
                }
                else if (c.IsExp())
                {
                    if (exp_pos != -1 ||
                        i == i_start + 1 ||
                        (value.Length - i) < 2 ||
                        value[i + 1].IsSign(out exp_sign)) return false;

                    exp_pos = i;
                }
                else
                {
                    return false;
                }
            }

            // decode exp part
            for (int i = value.Length - 1; i > exp_pos + 1; i--)
            {
                n_pow = n_pow * 10 + value[i].ToInt();
            }
            n_pow *= exp_sign;

            // normalize mantissa (force format as f.fff...)
            if (dot_pos > i_start + 1)
            {
                n_pow += (dot_pos - (i_start + 1));
            }

            return true;
        }


        unsafe public static bool TryParseHex(this IParseExt value, out int result)
        {
            if (value.TryParseHex32(out uint buf))
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
        unsafe public static bool TryParseHex(this IParseExt value, out long result)
        {
            if (value.TryParseHex64(out ulong buf))
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
        unsafe public static bool TryParseHex(this IParseExt value, out float result)
        {
            if (value.TryParseHex32(out uint buf))
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
        unsafe public static bool TryParseHex(this IParseExt value, out double result)
        {
            if (value.TryParseHex64(out ulong buf))
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

        private static bool TryParseHex32(this IParseExt value, out uint buf)
        {
            const int max_digits = 8;  // accepts max 8 digits

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
        private static bool TryParseHex64(this IParseExt value, out ulong buf)
        {
            const int max_digits = 16;  // accepts max 16 digits

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

        struct ElemIndex
        {
            public int Start { get; private set; }
            public int Length { get; private set; }

            public int End { get { return this.Start + this.Length; } }

            public ElemIndex(int st, int len)
            {
                this.Start = st;
                this.Length = len;
            }
        }

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
                else
                {
                    if ('A' <= c && c <= 'F')
                    {
                        hex = (uint)(c - 'A' + 10);
                        return true;
                    }
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
