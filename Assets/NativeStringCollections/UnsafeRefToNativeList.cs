using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using Unity.Burst.CompilerServices;

namespace NativeStringCollections.Utility
{
    public static class NativeListExt
    {
        public static UnsafeRefToNativeList<T> GetUnsafeRef<T>(this NativeList<T> target)
            where T : unmanaged
        {
            return new UnsafeRefToNativeList<T>(target);
        }
    }

    /// <summary>
    /// This unsafe reference disables the NativeContiner safety system.
    /// Use only for passing reference to BurstCompiler.CompileFunctionPointer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UnsafeRefToNativeList<T>
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeList* _list;

        /// <summary>
        /// Create the unsafe reference to NativeList<T>.
        /// </summary>
        /// <param name="passed_list"></param>
        public UnsafeRefToNativeList(NativeList<T> passed_list)
        {
            _list = passed_list.GetUnsafeList();
        }

        public T this[int index]
        {
            get
            {
                CheckIndexInRange(index, _list->Length);
                return UnsafeUtility.ReadArrayElement<T>(_list->Ptr, AssumePositive(index));
            }
            set
            {
                CheckIndexInRange(index, _list->Length);
                UnsafeUtility.WriteArrayElement(_list->Ptr, AssumePositive(index), value);
            }
        }

        public ref T ElementAt(int index)
        {
            CheckIndexInRange(index, _list->Length);
            return ref UnsafeUtilityEx.ArrayElementAsRef<T>(_list->Ptr, index);
        }

        public int Length
        {
            get { return AssumePositive(_list->Length); }
            set { _list->Resize<T>(value, NativeArrayOptions.ClearMemory); }
        }

        public int Capacity
        {
            get { return AssumePositive(_list->Capacity); }
            set { CheckCapacityInRange(value, _list->Length); _list->SetCapacity<T>(value); }
        }

        public void AddNoResize(T value) { _list->AddNoResize(value); }

        public void AddRangeNoResize(void* ptr, int length)
        {
            CheckArgPositive(length);
            _list->AddRangeNoResize<T>(ptr, length);
        }

        public void Add(T value) { _list->Add(value); }

        public void AddRange(void* elements, int count)
        {
            CheckArgPositive(count);
            _list->AddRange<T>(elements, AssumePositive(count));
        }

        public void RemoveAtSwapBack(int index)
        {
            CheckArgInRange(index, Length);
            _list->RemoveAtSwapBack<T>(AssumePositive(index));
        }

        public void RemoveRnageSwapBack(int begin, int end)
        {
            _list->RemoveRangeSwapBack<T>(AssumePositive(begin), AssumePositive(end));
        }

        public void RemoveAt(int index)
        {
            CheckArgInRange(index, Length);
            _list->RemoveAt<T>(AssumePositive(index));
        }

        public void RemoveRange(int begin, int end)
        {
            _list->RemoveRange<T>(begin, end);
        }

        public void Clear() { _list->Clear(); }

        public void Resize(int length, NativeArrayOptions options)
        {
            _list->Resize(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), length, options);
        }

        public void ResizeUninitialized(int length) { Resize(length, NativeArrayOptions.UninitializedMemory); }

        /// <summary>
        /// Tell Burst that an integer can be assumed to map to an always positive value.
        /// This implementation is the copy of Unity.Collections.CollectionsHelper.AssumeRange().
        /// </summary>
        /// <param name="x">The integer that is always positive.</param>
        /// <returns>Returns `x`, but allows the compiler to assume it is always positive.</returns>
        [return: AssumeRange(0, int.MaxValue)]
        private static int AssumePositive(int x)
        {
            return x;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndexInRange(int value, int length)
        {
            if (value < 0)
                throw new IndexOutOfRangeException($"Value {value} must be positive.");

            if ((uint)value >= (uint)length)
                throw new IndexOutOfRangeException($"Value {value} is out of range in NativeList of '{length}' Length.");
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckCapacityInRange(int value, int length)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException($"Value {value} must be positive.");

            if ((uint)value < (uint)length)
                throw new ArgumentOutOfRangeException($"Value {value} is out of range in NativeList of '{length}' Length.");
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckArgInRange(int value, int length)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException($"Value {value} must be positive.");

            if ((uint)value >= (uint)length)
                throw new ArgumentOutOfRangeException($"Value {value} is out of range in NativeList of '{length}' Length.");
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckArgPositive(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException($"Value {value} must be positive.");
        }
    }

    public unsafe static class UnsafeViewForNativeListUtility
    {
        public static void* GetUnsafePtr<T>(this UnsafeRefToNativeList<T> list)
            where T : unmanaged
        {
            return list._list->Ptr;
        }
        public static void* GetRadOnlyUnsafePtr<T>(this UnsafeRefToNativeList<T> list)
            where T : unmanaged
        {
            return list._list->Ptr;
        }
    }

}


