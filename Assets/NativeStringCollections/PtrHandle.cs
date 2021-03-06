﻿using System;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections.Utility
{
    public unsafe struct PtrHandle<T> : IDisposable where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private T* _ptr;

        private Allocator _alloc;
        private Boolean _isCreated;

        public PtrHandle(Allocator alloc)
        {
            if (alloc <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(alloc));

            _alloc = alloc;
            _ptr = (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), _alloc);
            _isCreated = true;
        }
        public PtrHandle(T value, Allocator alloc)
        {
            if (alloc <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(alloc));

            _alloc = alloc;
            _ptr = (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), _alloc);
            _isCreated = true;

            *_ptr = value;
        }

        public Boolean IsCreated { get { return (_isCreated); } }

        public void Create()
        {
            _ptr = (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), _alloc);
            _isCreated = true;
        }

        public void Dispose()
        {
            if (IsCreated)
            {
                this.CheckAllocator();
                UnsafeUtility.Free((void*)_ptr, _alloc);
                _ptr = null;
                _isCreated = false;
            }
            else
            {
                throw new InvalidOperationException("Dispose() was called twise, or not initialized target.");
            }
        }

        public T* Target
        {
            get
            {
                if (!_isCreated) throw new InvalidOperationException("target is not allocated. call Create().");
                return _ptr;
            }
        }
        public T Value
        {
            set { *_ptr = value; }
            get { return *_ptr; }
        }

        public static implicit operator T(PtrHandle<T> value) { return *value._ptr; }

        private void CheckAllocator()
        {
            if (!UnsafeUtility.IsValidAllocator(_alloc))
                throw new InvalidOperationException("The buffer can not be Disposed because it was not allocated with a valid allocator.");
        }
    }
}


