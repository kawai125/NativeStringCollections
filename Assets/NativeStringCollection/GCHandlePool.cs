using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Runtime.InteropServices;

namespace NativeStringCollections.Impl
{
    /// <summary>
    /// Qiita: "Unity C# Job Systemに参照型を持ち込む" を利用
    /// ref: https://qiita.com/tatsunoru/items/611d0378086dc5986249
    /// </summary>
    public static class GCHandlePool
    {
        // 使い回すためのスタックコンテナ
        // protected にできない
        private static readonly Stack<GCHandle> stack = new Stack<GCHandle>();

        // GCHandleを生成する(プールされたオブジェクトがあるならそれを返す)
        public static GCHandle Create<T>(T value)
        {
            if (stack.Count == 0)
            {
                return GCHandle.Alloc(value);
            }
            else
            {
                var ret = stack.Pop();
                ret.Target = value; // Targetにセットする
                return ret;
            }
        }

        // GCHandleを開放する
        public static void Release(GCHandle value)
        {
            if (value.IsAllocated)
            {
                value.Target = null; // Targetを開放する
                stack.Push(value); // スタックコンテナに積んで次回に使い回す
            }
        }
    }
}
