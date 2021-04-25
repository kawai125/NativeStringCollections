using System;
using System.Collections;
using System.Collections.Generic;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NativeStringCollections
{
    using NativeStringCollections.Utility;
    using NativeStringCollections.Impl;


    public struct NativeStringList : IDisposable, IEnumerable<StringEntity>
    {
        internal NativeJaggedArray<Char16> _jarr;

        public unsafe NativeStringList(Allocator alloc)
        {
            _jarr = new NativeJaggedArray<Char16>(alloc);
        }

        public void Dispose()
        {
            _jarr.Dispose();
        }

        public bool IsCreated { get { return _jarr.IsCreated; } }

        public void Clear()
        {
            _jarr.Clear();
        }

        public int Length { get { return _jarr.Length; } }
        public int Size { get { return _jarr.Size; } }
        public int Capacity
        {
            get { return _jarr.Capacity; }
            set { _jarr.Capacity = value; }
        }
        public int IndexCapacity
        {
            get { return _jarr.IndexCapacity; }
            set { _jarr.IndexCapacity = value; }
        }

        public unsafe StringEntity this[int index]
        {
            get { return new StringEntity(_jarr[index]); }
        }
        public StringEntity At(int index)
        {
            return new StringEntity(_jarr.At(index));
        }
        public StringEntity Last
        {
            get { return new StringEntity(_jarr.Last); }
        }
        unsafe public string SubString(int index)
        {
            return this[index].ToString();
        }
        public IEnumerator<StringEntity> GetEnumerator()
        {
            for (int i = 0; i < this.Length; i++)
                yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public unsafe void Add(string str)
        {
            fixed (char* ptr = str)
            {
                this.Add(ptr, str.Length);
            }
        }
        public unsafe void Add(char[] str)
        {
            fixed (char* ptr = str)
            {
                this.Add(ptr, str.Length);
            }
        }
        public unsafe void Add(IEnumerable<char> str)
        {
            using (var tmp = new NativeList<char>(Allocator.TempJob))
            {
                foreach (char c in str)
                {
                    tmp.Add(c);
                }
                _jarr.Add((Char16*)tmp.GetUnsafePtr(), tmp.Length);
            }
        }
        public unsafe void Add(Char16* ptr, int Length)
        {
            _jarr.Add(ptr, Length);
        }
        public unsafe void Add(char* ptr, int Length)
        {
            _jarr.Add((Char16*)ptr, Length);
        }
        public unsafe void Add<T>(T str)
            where T : IJaggedArraySliceBase<Char16>
        {
            _jarr.Add(str);
        }
        /// <summary>
        /// specialize for StringEntity
        /// </summary>
        /// <param name="entity"></param>
        public unsafe void Add(StringEntity entity)
        {
            this.Add((Char16*)entity.GetUnsafePtr(), entity.Length);
        }
        /// <summary>
        /// specialize for ReadOnlyStringEntity
        /// </summary>
        /// <param name="entity"></param>
        public unsafe void Add(ReadOnlyStringEntity entity)
        {
            this.Add((Char16*)entity.GetUnsafePtr(), entity.Length);
        }
        /// <summary>
        /// specialize for NativeList
        /// </summary>
        /// <param name="str"></param>
        public unsafe void Add(NativeList<Char16> str)
        {
            this.Add((Char16*)str.GetUnsafePtr(), str.Length);
        }
        /// <summary>
        /// specialize for NativeArray
        /// </summary>
        /// <param name="str"></param>
        public unsafe void Add(NativeArray<Char16> str)
        {
            this.Add((Char16*)str.GetUnsafePtr(), str.Length);
        }
        /// <summary>
        /// specialize for NativeList
        /// </summary>
        /// <param name="str"></param>
        public unsafe void Add(NativeList<char> str)
        {
            this.Add((char*)str.GetUnsafePtr(), str.Length);
        }
        /// <summary>
        /// specialize for NativeArray
        /// </summary>
        /// <param name="str"></param>
        public unsafe void Add(NativeArray<char> str)
        {
            this.Add((char*)str.GetUnsafePtr(), str.Length);
        }

        /// <summary>
        /// Get the index of the entity.
        /// </summary>
        /// <param name="key">entity</param>
        /// <returns>index or -1 (not found)</returns>
        public unsafe int IndexOf<T>(T key)
            where T : IJaggedArraySliceBase<Char16>
        {
            return _jarr.IndexOf(key);
        }
        /// <summary>
        /// Get the index of the entity.
        /// </summary>
        /// <param name="key">entity</param>
        /// <returns>index or -1 (not found)</returns>
        public unsafe int IndexOf(IEnumerable<char> key)
        {
            int ret = -1;
            using(var tmp = new NativeList<Char16>(Allocator.TempJob))
            {
                foreach(char c in key)
                {
                    tmp.Add((Char16)c);
                }
                ret = _jarr.IndexOf(tmp);
            }
            return ret;
        }

        public void RemoveAt(int index)
        {
            _jarr.RemoveAt(index);
        }
        public void RemoveRange(int index, int count)
        {
            _jarr.RemoveRange(index, count);
        }

        /// <summary>
        /// Re adjust the internal char buffer to justifing data after calling RemoveAt() or RemoveRange().
        /// All StringEntities are disabled after calling this function.
        /// </summary>
        public void ReAdjustment()
        {
            _jarr.ReAdjustment();
        }
        /// <summary>
        /// Shrink internal buffer size to fit present data length.
        /// Calling ReAdjuxtment() previously is recommended to eliminate the gap data.
        /// </summary>
        public void ShrinkToFit()
        {
            _jarr.ShrinkToFit();
        }
    }
}
