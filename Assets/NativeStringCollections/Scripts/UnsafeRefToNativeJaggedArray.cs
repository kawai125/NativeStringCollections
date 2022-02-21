using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NativeStringCollections.Utility
{
    public static class NativeJaggedArrayExt
    {
        public static UnsafeRefToNativeJaggedArray<T> GetUnsafeRef<T>(this NativeJaggedArray<T> target)
            where T : unmanaged, IEquatable<T>
        {
            return new UnsafeRefToNativeJaggedArray<T>(target);
        }
    }

    /// <summary>
    /// This unsafe reference disables the NativeContiner safety system.
    /// Use only for passing reference to BurstCompiler.CompileFunctionPointer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UnsafeRefToNativeJaggedArray<T>
        where T : unmanaged, IEquatable<T>
    {
        private UnsafeRefToNativeList<T> _buff;
        private UnsafeRefToNativeList<ElemIndex> _elemIndexList;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [NativeDisableUnsafePtrRestriction]
        private long* genTracePtr;
        [NativeDisableUnsafePtrRestriction]
        private long* genSignaturePtr;
#endif

        /// <summary>
        /// Create the unsafe reference to NativeJaggedArray<T>.
        /// </summary>
        /// <param name="view_tgt"></param>
        public UnsafeRefToNativeJaggedArray(NativeJaggedArray<T> view_tgt)
        {
            _buff = new UnsafeRefToNativeList<T>(view_tgt._buff);
            _elemIndexList = new UnsafeRefToNativeList<ElemIndex>(view_tgt._elemIndexList);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            genTracePtr = (long*)view_tgt.genTrace.GetUnsafePtr();
            genSignaturePtr = view_tgt.genSignature.Target;
#endif
        }


        public void Clear()
        {
            this._buff.Clear();
            this._elemIndexList.Clear();
        }

        public int Length { get { return this._elemIndexList.Length; } }
        public int Size { get { return this._buff.Length; } }
        public int Capacity
        {
            get
            {
                return this._buff.Capacity;
            }
            set
            {
                CheckCapacity(value, this._buff.Length);
                if (value == this._buff.Length) return;

                this._buff.Capacity = value;

                this.UpdateSignature();
            }
        }
        public int IndexCapacity
        {
            get
            {
                return this._elemIndexList.Capacity;
            }
            set
            {
                CheckCapacity(value, this._elemIndexList.Length);
                if (value == this._elemIndexList.Length) return;

                this._elemIndexList.Capacity = value;
            }
        }

        public unsafe NativeJaggedArraySlice<T> this[int index]
        {
            get
            {
                var elem_index = this._elemIndexList[index];
                T* elem_ptr = (T*)this._buff.GetUnsafePtr() + elem_index.Start;
                //        UnityEngine.Debug.Log("index: " + index.ToString()
                //                          + ", ptr: " + ((int)this.char_arr.GetUnsafePtr()).ToString() + ", Start: " + elem_index.Start.ToString() + ", Length: " + elem_index.Length.ToString()
                //                          + ", GenPtr: " + ((long)this.GetGenPtr()).ToString() + ", Gen: " + this.GetGen().ToString());
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new NativeJaggedArraySlice<T>(elem_ptr, elem_index.Length, this.GetGenPtr(), this.GetGen());
#else
                return new NativeJaggedArraySlice<T>(elem_ptr, elem_index.Length);
#endif
            }
        }
        public NativeJaggedArraySlice<T> At(int index)
        {
            this.CheckElemIndex(index);
            return this[index];
        }
        public NativeJaggedArraySlice<T> Last
        {
            get
            {
                int index = this.Length - 1;
                return this[index];
            }
        }

        public unsafe void Add(T* ptr, int Length)
        {
            int Start = this._buff.Length;
            this._buff.AddRange((void*)ptr, Length);
            this._elemIndexList.Add(new ElemIndex(Start, Length));

            this.UpdateSignature();
        }
        /// <summary>
        /// specialize for NativeJaggedArraySlice<T>
        /// </summary>
        /// <param name="slice"></param>
        public unsafe void Add(NativeJaggedArraySlice<T> slice)
        {
            this.Add((T*)slice.GetUnsafePtr(), slice.Length);
        }
        /// <summary>
        /// specialize for ReadOnlyNativeJaggedArraySlice<T>
        /// </summary>
        /// <param name="slice"></param>
        public unsafe void Add(ReadOnlyNativeJaggedArraySlice<T> slice)
        {
            this.Add((T*)slice.GetUnsafePtr(), slice.Length);
        }
        /// <summary>
        /// specialize for UnsafeRefToNativeList<T>
        /// </summary>
        /// <param name="list"></param>
        public unsafe void Add(UnsafeRefToNativeList<T> list)
        {
            this.Add((T*)list.GetUnsafePtr(), list.Length);
        }

        /// <summary>
        /// Get the index of the slice.
        /// </summary>
        /// <param name="key">slice</param>
        /// <returns>index or -1 (not found)</returns>
        public unsafe int IndexOf<TSlice>(TSlice key)
            where TSlice : IJaggedArraySliceBase<T>
        {
            if (this._elemIndexList.Length < 1) return -1;
            for (int i = 0; i < this._elemIndexList.Length; i++)
            {
                var slice = this[i];
                if (slice.Equals(key)) return i;
            }
            return -1;
        }



        public void RemoveAt(int index)
        {
            this.CheckElemIndex(index);
            for (int i = index; i < this.Length - 1; i++)
            {
                this._elemIndexList[i] = this._elemIndexList[i + 1];
            }
            this._elemIndexList.RemoveAtSwapBack(this.Length - 1);
        }
        public void RemoveRange(int index, int count)
        {
            this.CheckIndexRange(index, count);
            for (int i = index; i < this.Length - count; i++)
            {
                this._elemIndexList[i] = this._elemIndexList[i + count];
            }
            for (int i = 0; i < count; i++)
            {
                this._elemIndexList.RemoveAtSwapBack(this.Length - 1);
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
            ElemIndex prev_index = this._elemIndexList[0];

            //UnityEngine.Debug.Log("elemIndexList[0]: Start = " + prev_index.Start
            //            + ", Length = " + prev_index.Length.ToString() + ", End =" + prev_index.End.ToString() );

            // this[0] is not on the head
            if (prev_index.Start > 0)
            {
                index_start = 0;
                prev_index = new ElemIndex(0, 0);
            }

            for (int i_index = index_start; i_index < this._elemIndexList.Length; i_index++)
            {
                ElemIndex now_index = this._elemIndexList[i_index];
                gap = now_index.Start - prev_index.End;

                //    UnityEngine.Debug.Log("now_index: Start = " + now_index.Start
                //             + ", Length = " + now_index.Length.ToString() + ", End =" + now_index.End.ToString() + ", gap = " + gap.ToString());

                if (gap > 0)
                {
                    //        UnityEngine.Debug.Log("index = " + i_index
                    //           + ", shift data: [" + now_index.Start.ToString() + "-" + (now_index.End-1).ToString()
                    //            + "] -> [" + prev_index.End.ToString() + "-" + (prev_index.End+now_index.Length-1).ToString() + "]");

                    int i_start = prev_index.End;
                    for (int i_data = 0; i_data < now_index.Length; i_data++)
                    {
                        this._buff[i_start + i_data] = this._buff[now_index.Start + i_data];
                    }
                }

                prev_index = new ElemIndex(prev_index.End, now_index.Length);
                this._elemIndexList[i_index] = prev_index;
            }

            for (int i = 0; i < gap; i++)
            {
                this._buff.RemoveAtSwapBack(this._buff.Length - 1);
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (gap > 0) this.NextGen();
#endif
        }
        /// <summary>
        /// Shrink internal buffer size to fit present data length.
        /// Calling ReAdjuxtment() previously is recommended to eliminate the gap data.
        /// </summary>
        public void ShrinkToFit()
        {
            this._buff.ResizeUninitialized(this._buff.Length);
            this._elemIndexList.ResizeUninitialized(this._elemIndexList.Length);

            this.UpdateSignature();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckCapacity(int new_cap, int old_len)
        {
            if (new_cap < old_len)
            {
                throw new ArgumentOutOfRangeException($"the new capacity is too small. capacity = {new_cap} must be >= Length = {old_len}.");
            }
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckElemIndex(int index)
        {
            if (index < 0 || this.Length <= index)
            {
                throw new IndexOutOfRangeException($"index = {index}, must be in range of [0~{Length - 1}].");
            }
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndexRange(int index, int count)
        {
            this.CheckElemIndex(index);
            if (count < 0) throw new ArgumentOutOfRangeException($"count must be > 0. count = {count}");
            if (index + count > this.Length)
            {
                throw new ArgumentOutOfRangeException($"invalid range. index = {index}, count = {count}, (index + count) must be <= Length = {Length}.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        unsafe private void UpdateSignature()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            long now_sig = GetGenSigneture();
            if (now_sig != *genSignaturePtr)
            {
                this.NextGen();
                *genSignaturePtr = now_sig;
            }
#endif
        }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private void NextGen()
        {
            long now_gen = *genTracePtr;
            *genTracePtr = now_gen + 1;
        }
        private unsafe long GetGenSigneture() { return (long)this._buff.GetUnsafePtr(); }
        private long GetGen() { return *genTracePtr; }
        unsafe private long* GetGenPtr() { return genTracePtr; }
#endif
    }
}
