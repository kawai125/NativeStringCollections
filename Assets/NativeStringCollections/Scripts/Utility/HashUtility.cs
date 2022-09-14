using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ref: https://stackoverflow.com/questions/1646807/quick-and-simple-hash-code-combinations

namespace NativeStringCollections.Impl
{
    public static class HashUtility
    {
        /// <summary>
        /// generate new hash value from 2 hash values.
        /// </summary>
        public static int Combine(int h1, int h2)
        {
            int rol5 = (h1 << 5) | (h1 >> 27);
            return (rol5 + h1) ^ h2;
        }
    }
}