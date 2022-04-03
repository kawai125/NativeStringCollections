using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NativeStringCollections.Impl
{
    public static class HashUtility
    {
        public static int Combine(int h1, int h2)
        {
            int rol5 = (h1 << 5) | (h1 >> 27);
            return (rol5 + h1) ^ h2;
        }
    }
}