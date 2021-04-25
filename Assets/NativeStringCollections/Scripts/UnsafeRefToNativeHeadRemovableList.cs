using System;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections.Utility
{
    using NativeStringCollections.Impl;

    internal static class NativeHeadRemovableListExt
    {
        internal static UnsafeRefToNativeHeadRemovableList<T> GetUnsafeRef<T>(this NativeHeadRemovableList<T> target)
            where T : unmanaged
        {
            return new UnsafeRefToNativeHeadRemovableList<T>(target);
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct UnsafeRefToNativeHeadRemovableList<T>
        where T : unmanaged
    {
        private UnsafeRefToNativeList<T> _list;

        [NativeDisableUnsafePtrRestriction]
        private int* _start;

        public UnsafeRefToNativeHeadRemovableList(NativeHeadRemovableList<T> list)
        {
            _list = new UnsafeRefToNativeList<T>(list._list);
            _start = list._start.Target;
        }

        public unsafe int Capacity
        {
            get { return _list.Capacity - *_start; }
            set
            {
                // Length check
                if (value < Length) throw new ArgumentOutOfRangeException("the Capacity must be > Length.");

                _list.Capacity = value + *_start;
            }
        }
        public unsafe int HeadCapacity { get { return *_start; } }
        public unsafe T this[int index]
        {
            get { return _list[*_start + index]; }
            set { _list[*_start + index] = value; }
        }
        public unsafe int Length { get { return _list.Length - *_start; } }

        public void Add(T value) { _list.Add(value); }
        public unsafe void AddRange(void* elements, int count) { _list.AddRange(elements, count); }

        public unsafe void Clear(int front_capacity = 0)
        {
            if(*_start == 0)
            {
                _list.Clear();
            }
            else
            {
                this.InitStartPoint(front_capacity);
                _list.ResizeUninitialized(*_start);
            }
        }

        public unsafe void RemoveAtSwapBack(int index)
        {
            _list.RemoveAtSwapBack(*_start + index);
        }

        public unsafe void RemoveHead(int count = 1)
        {
            if (count < 1 || Length < count) throw new ArgumentOutOfRangeException("invalid length of remove target.");

            *_start = *_start + count;
        }
        public unsafe void InsertHead(T* ptr, int length)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException("invalid size");

            /*
            var sb = new System.Text.StringBuilder();
            sb.Append($"InsertHead(before data, Length = {this.Length}, start = {_start.Value}):\n");
            for (int i = 0; i < this.Length; i++) sb.Append(this[i]);
            sb.Append('\n');
            sb.Append("  >> insert data:\n");
            for (int i = 0; i < length; i++) sb.Append(ptr[i]);
            sb.Append('\n');
            sb.Append('\n');
            */

            // when enough space exists in head
            if (length <= *_start)
            {
                *_start = *_start - length;
                UnsafeUtility.MemCpy(this.GetUnsafePtr(), ptr, UnsafeUtility.SizeOf<T>() * length);

                /*
                sb.Append($"InsertHead(without resize Length = {this.Length}):\n");
                for (int i = 0; i < this.Length; i++) sb.Append(this[i]);
                sb.Append('\n');
                UnityEngine.Debug.Log(sb.ToString());
                */
                return;
            }

            // slide internal data
            int new_length = length + this.Length;
            int len_move = this.Length;
            _list.ResizeUninitialized(new_length);
            T* dest = (T*)_list.GetUnsafePtr() + length;
            T* source = (T*)_list.GetUnsafePtr() + *_start;
            UnsafeUtility.MemMove(dest, source, UnsafeUtility.SizeOf<T>() * len_move);

            // insert data
            *_start = 0;
            UnsafeUtility.MemCpy((void*)_list.GetUnsafePtr(), (void*)ptr, UnsafeUtility.SizeOf<T>() * length);

            /*
            sb.Append($"InsertHead, Length = {this.Length}, start = {_start.Value}:\n");
            for (int i = 0; i < this.Length; i++) sb.Append(this[i]);
            sb.Append('\n');
            UnityEngine.Debug.Log(sb.ToString());
            */
        }

        public unsafe void ResizeUninitialized(int length)
        {
            if (length == 0)
            {
                this.Clear(*_start);
            }
            else
            {
                _list.ResizeUninitialized(*_start + length);
            }
        }

        public unsafe void Shrink(int front_capacity = 0)
        {
            // remove deleted head area
            if (this.Length > 0)
            {
                T* source = (T*)this.GetUnsafePtr();
                this.InitStartPoint(front_capacity);
                T* dest = (T*)this.GetUnsafePtr();

                UnsafeUtility.MemMove(dest, source, this.Length);
                _list.ResizeUninitialized(*_start + this.Length);
            }
            else
            {
                this.Clear(*_start);
            }
        }
        private void InitStartPoint(int front_capacity)
        {
            if(front_capacity < 0)
            {
                throw new ArgumentOutOfRangeException("invalid front_capacity size");
            }
            *_start = front_capacity;
        }

        public unsafe void* GetUnsafePtr()
        {
            T* ptr = (T*)_list.GetUnsafePtr();
            ptr += *_start;
            return (void*)ptr;
        }
    }
}