using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NativeStringCollections
{
    /// <summary>
    /// interface for async parser.
    /// </summary>
    public interface ITextFileParser
    {
        /// <summary>
        /// called once at the first in main thread (you can use managed object in this function).
        /// </summary>
        void Init();

        /// <summary>
        /// called every time at first on reading file.
        /// </summary>
        void Clear();

        /// <summary>
        /// when you returned 'false', the AsyncTextFileLoader discontinue calling the 'ParseLines()'
        /// and jump to calling 'PostReadProc()'.
        /// </summary>
        /// <param name="lines"></param>
        /// <returns>continue reading lines or not.</returns>
        bool ParseLines(NativeStringList lines);

        /// <summary>
        /// called every time at last on reading file.
        /// </summary>
        void PostReadProc();

        /// <summary>
        /// called when the AsyncTextFileLoader.UnLoadFile(index) function was called.
        /// </summary>
        void UnLoad();
    }
}