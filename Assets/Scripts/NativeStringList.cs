
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


    public class NativeStringList : IDisposable, IEnumerable<StringEntity>
    {
        private NativeList<char> char_arr;
        private NativeList<ElemIndex> elemIndexList;

        private NativeArray<ulong> genTrace;
        private ulong genSignature;

        private bool allocated = false;

        unsafe private void Init(int char_array_size, int elem_size, Allocator alloc)
        {
            this.char_arr = new NativeList<char>(char_array_size, alloc);
            this.elemIndexList = new NativeList<ElemIndex>(elem_size, alloc);

            this.genTrace = new NativeArray<ulong>(1, Allocator.Persistent);
            this.genSignature = this.GetGenSigneture();

            this.allocated = true;
        }
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            // disposing managed resource
            if (disposing)
            {
                
            }
            // disposing unmanaged resource
            if (this.allocated)
            {
                this.char_arr.Dispose();
                this.elemIndexList.Dispose();
                this.genTrace.Dispose();

                this.allocated = false;
            }
        }
        ~NativeStringList()
        {
            this.Dispose(false);
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
        /// Construct with char_array_size = 8, elem_size = 2, Allocator.Persistent
        /// </summary>
        public NativeStringList()
        {
            this.Init(8, 2, Allocator.Persistent);
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

                this.UpdateSignature();
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
            //        UnityEngine.Debug.Log("index: " + index.ToString()
            //                          + ", ptr: " + ((int)this.char_arr.GetUnsafePtr()).ToString() + ", Start: " + elem_index.Start.ToString() + ", Length: " + elem_index.Length.ToString()
            //                          + ", GenPtr: " + ((ulong)this.GetGenPtr()).ToString() + ", Gen: " + this.GetGen().ToString());
#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
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
            int Start = this.char_arr.Length;
            for (int i = 0; i < str.Length; i++)
            {
                this.char_arr.Add(str[i]);
            }
            this.elemIndexList.Add(new ElemIndex(Start, str.Length));

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            this.UpdateSignature();
#endif
        }
        unsafe public void Add(char* ptr, int Length)
        {
            int Start = this.char_arr.Length;
            for (int i = 0; i < Length; i++)
            {
                this.char_arr.Add(ptr[i]);
            }
            this.elemIndexList.Add(new ElemIndex(Start, Length));

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            this.UpdateSignature();
#endif
        }
        public void Add(IStringEntityBase entity)
        {
            int Start = this.char_arr.Length;
            for (int i = 0; i < entity.Length; i++)
            {
                this.char_arr.Add(entity[i]);
            }
            this.elemIndexList.Add(new ElemIndex(Start, entity.Length));

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            this.UpdateSignature();
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
            this.elemIndexList.RemoveAtSwapBack(this.Length - 1);
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
            for(int i=0; i<count; i++)
            {
                this.elemIndexList.RemoveAtSwapBack(this.Length - 1);
            }
        }

        /// <summary>
        /// Re adjust the internal char buffer to justifing data after calling RemoveAt() or RemoveRange().
        /// All StringEntities are disabled after calling this function.
        /// </summary>
        public void ReAdjustment()
        {
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

            for (int i = 0; i < gap; i++)
            {
                this.char_arr.RemoveAtSwapBack(this.char_arr.Length - 1);
            }

            if (gap > 0) this.NextGen();
        }
        /// <summary>
        /// Shrink internal buffer size to fit present data length.
        /// Calling ReAdjuxtment() previously is recommended to eliminate the gap data.
        /// </summary>
        public void ShrinkToFit()
        {
            this.char_arr.ResizeUninitialized( this.char_arr.Length );
            this.elemIndexList.ResizeUninitialized( this.elemIndexList.Length );

            this.UpdateSignature();
        }

        private void CheckElemIndex(int index)
        {
            if (index < 0 || this.Length <= index)
            {
                throw new IndexOutOfRangeException("index = " + index.ToString() + ", must be in range of [0~" + (this.Length - 1).ToString() + "].");
            }
        }
        unsafe private void UpdateSignature()
        {
            ulong now_sig = GetGenSigneture();
            if (now_sig != this.genSignature)
            {
                this.NextGen();
                this.genSignature = now_sig;
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
    }
}
