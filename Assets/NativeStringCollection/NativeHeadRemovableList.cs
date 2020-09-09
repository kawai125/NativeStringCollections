using System;
using System.Runtime.CompilerServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections.Impl
{
    [NativeContainer]
    public struct NativeHeadRemovableList<T> : IDisposable where T : struct
    {
        private NativeList<T> _list;
        private int _start;
        private bool _allocated;

        public NativeHeadRemovableList(Allocator alloc)
        {
            _list = new NativeList<T>(0, alloc);
            _start = 0;
            _allocated = true;
        }
        public NativeHeadRemovableList(int size, Allocator alloc)
        {
            _list = new NativeList<T>(size, alloc);
            _start = 0;
            _allocated = true;
        }

        public int Capacity
        {
            get { return _list.Capacity - _start; }
            set
            {
                // Length check
                if (value < Length) throw new ArgumentOutOfRangeException("the Capacity must be > Length = " + Length.ToString());

                this.Shrink();
                _list.Capacity = value;
            }
        }
        public bool IsCreated { get { return _allocated; } }
        public T this[int index]
        {
            get { return _list[_start + index]; }
            set { _list[_start + index] = value; }
        }
        public int Length { get { return _list.Length - _start; } }

        public void Add(T value) { _list.Add(value); }
        public void AddRange(NativeArray<T> elements) { _list.AddRange(elements); }
        public unsafe void AddRange(void* elements, int count) { _list.AddRange(elements, count); }

        public void Clear()
        {
            _list.Clear();
            _start = 0;
        }
        public void CopyFrom(T[] array) { _list.CopyFrom(array); }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        void Dispose(bool disposing)
        {
            // disposing managed resource
            if (disposing)
            {

            }
            // disposing unmanaged resource
            if (_allocated)
            {
                _list.Dispose();

                _allocated = false;
            }
        }

        public void RemoveAtSwapBack(int index)
        {
            _list.RemoveAtSwapBack(_start + index);
        }

        public void RemoveHead(int count = 1)
        {
            if (count < 1 || Length < count) throw new ArgumentOutOfRangeException("the count must be in the range of [1," + Length.ToString() + ")");

            _start += count;
        }

        public void ResizeUninitialized(int length)
        {
            if (length == 0)
            {
                this.Clear();
            }
            else
            {
                if (length < Length)
                {
                    // this will reduce the cost of calling Shrink().
                    _list.ResizeUninitialized(_start + length);
                }
                this.Shrink();
                _list.ResizeUninitialized(length);
            }
        }

        public void Shrink()
        {
            // remove deleted head area
            int new_size = Length;
            if (new_size > 0)
            {
                for (int i = 0; i < _start; i++)
                {
                    _list[i] = _list[_start + i];
                }
                _list.ResizeUninitialized(new_size);
            }
            else
            {
                _list.Clear();
            }
            _start = 0;
        }

        public unsafe void* GetUnsafePtr()
        {
            byte* ptr = (byte*)_list.GetUnsafePtr();
            int struct_size = Unsafe.SizeOf<T>();
            ptr += struct_size * _start;
            return (void*)ptr;
        }
    }
}