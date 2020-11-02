using System;
using System.Runtime.CompilerServices;

using System.Collections;
using System.Collections.Generic;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine;


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
            _alloc = alloc;
            _ptr = (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), _alloc);
            _isCreated = true;
        }
        public PtrHandle(T value, Allocator alloc)
        {
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
                UnsafeUtility.Free((void*)_ptr, _alloc);
            }
            else
            {
                throw new InvalidOperationException("Dispose() was twise, or not initialized target.");
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
    }
}


