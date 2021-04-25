using System;
using System.Collections;
using System.Collections.Generic;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace NativeStringCollections.Utility
{
    public static class NativeStringListExt
    {
        public static UnsafeRefToNativeStringList GetUnsafeRef(this NativeStringList target)
        {
            return new UnsafeRefToNativeStringList(target);
        }
    }

    /// <summary>
    /// This unsafe reference disables the NativeContiner safety system.
    /// Use only for passing reference to BurstCompiler.CompileFunctionPointer.
    /// </summary>
    public struct UnsafeRefToNativeStringList
    {
        private UnsafeRefToNativeJaggedArray<Char16> _jarr;

        /// <summary>
        /// Create the unsafe reference to NativeStringList.
        /// </summary>
        /// <param name="list"></param>
        public UnsafeRefToNativeStringList(NativeStringList list)
        {
            _jarr = new UnsafeRefToNativeJaggedArray<Char16>(list._jarr);
        }

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

        public unsafe void Add(Char16* ptr, int Length)
        {
            _jarr.Add(ptr, Length);
        }
        public unsafe void Add(char* ptr, int Length)
        {
            _jarr.Add((Char16*)ptr, Length);
        }
        public unsafe void Add<T>(T slice)
            where T : IJaggedArraySliceBase<Char16>
        {
            this.Add((Char16*)slice.GetUnsafePtr(), slice.Length);
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
        /// Get the index of the entity.
        /// </summary>
        /// <param name="key">entity</param>
        /// <returns>index or -1 (not found)</returns>
        public int IndexOf(StringEntity key)
        {
            return _jarr.IndexOf(key);
        }
        /// <summary>
        /// Get the index of the entity.
        /// </summary>
        /// <param name="key">entity</param>
        /// <returns>index or -1 (not found)</returns>
        public int IndexOf(ReadOnlyStringEntity key)
        {
            return _jarr.IndexOf(key);
        }
        public int IndexOf<T>(T key)
            where T : IJaggedArraySliceBase<Char16>
        {
            return _jarr.IndexOf(key);
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
