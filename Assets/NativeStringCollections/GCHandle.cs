using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Runtime.InteropServices;

namespace NativeStringCollections.Utility
{
    using NativeStringCollections.Impl;

    /// <summary>
    /// Qiita: "Unity C# Job Systemに参照型を持ち込む" を利用
    /// ref: https://qiita.com/tatsunoru/items/611d0378086dc5986249
    /// </summary>
    public struct GCHandle<T> : System.IDisposable
    {
        GCHandle handle;

        // T型にキャストして返す
        public T Target
        {
            get
            {
                return (T)handle.Target;
            }

            set
            {
                handle.Target = value;
            }
        }

        // Pool経由で作成する
        public void Create(T value)
        {
            handle = GCHandlePool.Create(value);
        }

        // プール経由で開放する
        public void Dispose()
        {
            GCHandlePool.Release(handle);
            handle = default;
        }
    }
}
