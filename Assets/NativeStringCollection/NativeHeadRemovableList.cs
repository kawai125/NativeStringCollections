using System;
using System.Runtime.CompilerServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections.Impl
{
    using NativeStringCollections.Utility;

    internal struct NativeHeadRemovableList<T> : IDisposable where T : struct
    {
        private NativeList<T> _list;
        private PtrHandle<int> _start;

        public NativeHeadRemovableList(Allocator alloc)
        {
            _list = new NativeList<T>(8, alloc);
            _list.Clear();
            _start = new PtrHandle<int>(0, alloc);
        }
        public NativeHeadRemovableList(int size, Allocator alloc)
        {
            _list = new NativeList<T>(size, alloc);
            _start = new PtrHandle<int>(0, alloc);
        }

        public int Capacity
        {
            get { return _list.Capacity - _start; }
            set
            {
                // Length check
                if (value < Length) throw new ArgumentOutOfRangeException("the Capacity must be > Length.");

                this.Shrink();
                _list.Capacity = value;
            }
        }
        public bool IsCreated { get { return _list.IsCreated; } }
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
            _start.Value = 0;
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
            if (_list.IsCreated)
            {
                _list.Dispose();
                _start.Dispose();
            }
        }

        public void RemoveAtSwapBack(int index)
        {
            _list.RemoveAtSwapBack(_start + index);
        }

        public void RemoveHead(int count = 1)
        {
            if (count < 1 || Length < count) throw new ArgumentOutOfRangeException("invalid length of remove target.");

            _start.Value += count;
        }
        public unsafe void InsertHead(void* ptr, int Length)
        {
            if (Length <= 0) throw new ArgumentOutOfRangeException("invalid size");

            // slide internal data
            int len_internal = this.Length;
            this.ResizeUninitialized(Length + len_internal);
            int typeSize = Unsafe.SizeOf<T>();
            byte* source = (byte*)_list.GetUnsafePtr();
            byte* dest = source + typeSize * Length;
            UnsafeUtility.MemMove((void*)dest, (void*)source, len_internal);

            // insert data
            source = (byte*)ptr;
            dest = (byte*)_list.GetUnsafePtr();
            UnsafeUtility.MemCpy((void*)dest, (void*)source, Length);
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
            _start.Value = 0;
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