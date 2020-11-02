// enable the below macro to enable reallocation trace for debug.
//#define NATIVE_STRING_COLLECTION_TRACE_REALLOCATION

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NativeStringCollections
{
    using NativeStringCollections.Utility;
    using NativeStringCollections.Impl;


    public struct NativeStringList : IDisposable, IEnumerable<StringEntity>
    {
        private NativeList<char> char_arr;
        private NativeList<ElemIndex> elemIndexList;
        private Allocator _alloc;

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
        private NativeArray<long> genTrace;
        private PtrHandle<long> genSignature;
#endif

        public unsafe NativeStringList(Allocator alloc)
        {
            char_arr = new NativeList<char>(alloc);
            elemIndexList = new NativeList<ElemIndex>(alloc);
            _alloc = alloc;

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            genTrace = new NativeArray<long>(1, alloc);
            genSignature = new PtrHandle<long>((long)char_arr.GetUnsafePtr(), alloc);  // sigunature = address value of ptr for char_arr.
#endif
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        public void Dispose(bool disposing)
        {
            // disposing managed resource
            if (disposing)
            {
                
            }
            // disposing unmanaged resource
            if (char_arr.IsCreated)
            {
                this.char_arr.Dispose();
                this.elemIndexList.Dispose();

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
                this.genTrace.Dispose();
                this.genSignature.Dispose();
#endif
            }
        }

        public bool IsCreated { get { return char_arr.IsCreated; } }

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

                var tmp = new NativeList<char>(value, _alloc);
                tmp.Clear();
                tmp.AddRange(this.char_arr);

                this.char_arr.Dispose();
                this.char_arr = tmp;

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
                this.UpdateSignature();
#endif
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

                var tmp = new NativeList<ElemIndex>(value, _alloc);
                tmp.Clear();
                tmp.AddRange(this.elemIndexList);

                this.elemIndexList.Dispose();
                this.elemIndexList = tmp;
            }
        }

        public unsafe StringEntity this[int index]
        {
            get
            {
                var elem_index = this.elemIndexList[index];
                char* elem_ptr = (char*)this.char_arr.GetUnsafePtr() + elem_index.Start;
            //        UnityEngine.Debug.Log("index: " + index.ToString()
            //                          + ", ptr: " + ((int)this.char_arr.GetUnsafePtr()).ToString() + ", Start: " + elem_index.Start.ToString() + ", Length: " + elem_index.Length.ToString()
            //                          + ", GenPtr: " + ((long)this.GetGenPtr()).ToString() + ", Gen: " + this.GetGen().ToString());
#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
                return new StringEntity(elem_ptr, elem_index.Length, this.GetGenPtr(), this.GetGen());
#else
                return new StringEntity(elem_ptr, elem_index.Length);
#endif
            }
        }
        public StringEntity At(int index)
        {
            this.CheckElemIndex(index);
            return this[index];
        }
        public StringEntity Last
        {
            get
            {
                int index = this.Length - 1;
                return this[index];
            }
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

        public void Add(IEnumerable<char> str)
        {
            int start = this.char_arr.Length;
            int len = 0;
            foreach(var c in str)
            {
                this.char_arr.Add(c);
                len++;
            }
            this.elemIndexList.Add(new ElemIndex(start, len));

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            this.UpdateSignature();
#endif
        }
        public unsafe void Add(char* ptr, int Length)
        {
            int Start = this.char_arr.Length;
            this.char_arr.AddRange((void*)ptr, Length);
            this.elemIndexList.Add(new ElemIndex(Start, Length));

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            this.UpdateSignature();
#endif
        }
        /// <summary>
        /// specialize for StringEntity
        /// </summary>
        /// <param name="entity"></param>
        public unsafe void Add(StringEntity entity)
        {
            this.Add((char*)entity.GetUnsafePtr(), entity.Length);
        }
        /// <summary>
        /// specialize for ReadOnlyStringEntity
        /// </summary>
        /// <param name="entity"></param>
        public unsafe void Add(ReadOnlyStringEntity entity)
        {
            this.Add((char*)entity.GetUnsafePtr(), entity.Length);
        }
        /// <summary>
        /// specialize for NativeList<char>
        /// </summary>
        /// <param name="entity"></param>
        public unsafe void Add(NativeList<char> str)
        {
            this.Add((char*)str.GetUnsafePtr(), str.Length);
        }

        /// <summary>
        /// Get the index of the entity.
        /// </summary>
        /// <param name="key">entity</param>
        /// <returns>index or -1 (not found)</returns>
        public unsafe int IndexOf(IEnumerable<char> key)
        {
            if (this.elemIndexList.Length < 1) return -1;
            for (int i = 0; i < this.elemIndexList.Length; i++)
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

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            if (gap > 0) this.NextGen();
#endif
        }
        /// <summary>
        /// Shrink internal buffer size to fit present data length.
        /// Calling ReAdjuxtment() previously is recommended to eliminate the gap data.
        /// </summary>
        public void ShrinkToFit()
        {
            this.char_arr.ResizeUninitialized( this.char_arr.Length );
            this.elemIndexList.ResizeUninitialized( this.elemIndexList.Length );

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
            this.UpdateSignature();
#endif
        }

        private void CheckElemIndex(int index)
        {
            if (index < 0 || this.Length <= index)
            {
                throw new IndexOutOfRangeException("index = " + index.ToString() + ", must be in range of [0~" + (this.Length - 1).ToString() + "].");
            }
        }

#if NATIVE_STRING_COLLECTION_TRACE_REALLOCATION
        unsafe private void UpdateSignature()
        {
            long now_sig = GetGenSigneture();
            if (now_sig != this.genSignature)
            {
                this.NextGen();
                this.genSignature.Value = now_sig;
            }
        }
        private void NextGen()
        {
            long now_gen = this.genTrace[0];
            this.genTrace[0] = now_gen + 1;
        }
        unsafe private long GetGenSigneture() { return (long)this.char_arr.GetUnsafePtr(); }
        private long GetGen() { return this.genTrace[0]; }
        unsafe private long* GetGenPtr() { return (long*)this.genTrace.GetUnsafePtr(); }
#endif
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
