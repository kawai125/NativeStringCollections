using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Mathematics;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections
{
    using NativeStringCollections.Utility;
    using NativeStringCollections.Impl;

    public struct StringSpliter : IDisposable
    {
        private NativeStringList _delims;

        public StringSpliter(Allocator alloc)
        {
            _delims = new NativeStringList(alloc);
        }

        public List<string> Delim
        {
            get
            {
                var ret = new List<string>();
                foreach(var se in _delims)
                {
                    ret.Add(se.ToString());
                }
                return ret;
            }
        }
        public void SetDelim(NativeStringList delims)
        {
            _delims.Clear();
            foreach (var se in delims)
            {
                // check duplication
                if (_delims.IndexOf(se) < 0)
                {
                    _delims.Add(se);
                }
            }
        }
        public void AddDelim(IEnumerable<char> delim)
        {
            if (_delims.IndexOf(delim) < 0) _delims.Add(delim);
        }

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
            if (_delims.IsCreated)
            {
                _delims.Dispose();
            }
        }

        private unsafe void SplitImpl(char* source_ptr, int source_len, NativeStringList result)
        {
            // parse
            int start = 0;
            for (int i = 0; i < source_len; i++)
            {
                int match_len = 0;
                foreach (var d in _delims)
                {
                    // skip shorter delim
                    if (match_len >= d.Length) continue;

                    bool match = true;
                    for (int j = 0; j < d.Length; j++)
                    {
                        if (source_ptr[i + j] != d[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) match_len = d.Length;
                }

                if (match_len > 0)
                {
                    int elem_len = i - start - 1;
                    if (elem_len > 0)
                    {
                        char* st_ptr = source_ptr + start;
                        result.Add(st_ptr, match_len);
                    }
                    start = i + match_len;
                }
            }
        }

        public unsafe void Split(NativeList<char> source, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();

            // redirect to using Char.IsWhiteSpace() function
            if (_delims.Length == 0)
            {
                source.Split(result, append);
                return;
            }
            // redirect to using single char delim function
            if (_delims.Length == 1 && _delims[0].Length == 1)
            {
                source.Split(_delims[0][0], result, append);
                return;
            }

            this.SplitImpl((char*)source.GetUnsafePtr(), source.Length, result);
        }
        public NativeStringList Split(NativeList<char> input, Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            this.Split(input, tmp);
            return tmp;
        }
        public unsafe void Split(IStringEntityBase source, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();

            // redirect to using Char.IsWhiteSpace() function
            if (_delims.Length == 0)
            {
                source.Split(result, append);
                return;
            }
            // redirect to using single char delim function
            if (_delims.Length == 1 && _delims[0].Length == 1)
            {
                source.Split(_delims[0][0], result, append);
                return;
            }

            this.SplitImpl((char*)source.GetUnsafePtr(), source.Length, result);
        }
    }

    public static class StringSpliterExt
    {
        private static unsafe void SplitImpl(char* source_ptr, int source_len, NativeStringList result)
        {
            int start = 0;
            for (int i = 0; i < source_len; i++)
            {
                if (Char.IsWhiteSpace(source_ptr[i]))
                {
                    int len = (i - start) - 1;
                    if (len > 0)
                    {
                        char* st_ptr = source_ptr + start;
                        result.Add(st_ptr, len);
                    }
                    start = i + 1;
                }
            }
        }

        /// <summary>
        /// split function by Char.IsWhiteSpace() delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static unsafe void Split(this NativeList<char> source, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();
            SplitImpl((char*)source.GetUnsafePtr(), source.Length, result);
        }
        /// <summary>
        /// split function by Char.IsWhiteSpace() delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="alloc"></param>
        /// <returns>result</returns>
        public static NativeStringList Split(this NativeList<char> source, Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            source.Split(tmp, false);
            return tmp;
        }
        /// <summary>
        /// split function by Char.IsWhiteSpace() delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static unsafe void Split(this IStringEntityBase source, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();
            SplitImpl((char*)source.GetUnsafePtr(), source.Length, result);
        }
        /// <summary>
        /// split function by Char.IsWhiteSpace() delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeStringList Split(this IStringEntityBase source, Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            source.Split(tmp, false);
            return tmp;
        }


        private static unsafe void SplitImpl(char* source_ptr, int source_len, char delim, NativeStringList result)
        {
            int start = 0;
            for (int i = 0; i < source_len; i++)
            {
                if (source_ptr[i] == delim)
                {
                    int len = (i - start) - 1;
                    if (len > 0)
                    {
                        char* st_ptr = source_ptr + start;
                        result.Add(st_ptr, len);
                    }
                    start = i + 1;
                }
            }
        }
        /// <summary>
        /// split function by single char delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static unsafe void Split(this NativeList<char> source, char delim, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();
            SplitImpl((char*)source.GetUnsafePtr(), source.Length, delim, result);
        }
        /// <summary>
        /// split function by single char delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns>result</returns>
        public static NativeStringList Split(this NativeList<char> source, char delim, Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }
        /// <summary>
        /// split function by single char delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static unsafe void Split(this IStringEntityBase source, char delim, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();
            SplitImpl((char*)source.GetUnsafePtr(), source.Length, delim, result);
        }
        /// <summary>
        /// split function by single char delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeStringList Split(this IStringEntityBase source, char delim, Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }

        private static unsafe void SplitImpl(char* source_ptr, int source_len, char* delim_ptr, int delim_len, NativeStringList result)
        {
            int start = 0;
            for (int i = 0; i < source_len; i++)
            {
                if (source_ptr[i] == delim_ptr[0])
                {
                    // delim check
                    bool is_delim = true;
                    if (source_len - i < delim_len) return;
                    for (int j = 1; j < delim_len; j++)
                    {
                        if (source_ptr[i + j] != delim_ptr[j])
                        {
                            is_delim = false;
                            break;
                        }
                    }

                    if (is_delim)
                    {
                        int len = (i - start) - 1;
                        if (len > 0)
                        {
                            char* st_ptr = source_ptr + start;
                            result.Add(st_ptr, len);
                        }
                        start = i + delim_len;
                    }
                }
            }
        }
        /// <summary>
        /// split function by NativeList<char> delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public unsafe static void Split(this NativeList<char> source, NativeList<char> delim, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();
            if (delim.Length <= 0) throw new ArgumentException("delim is empty.");
            SplitImpl((char*)source.GetUnsafePtr(), source.Length, (char*)delim.GetUnsafePtr(), delim.Length, result);
        }
        /// <summary>
        /// split function by NativeList<char> delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns>result</returns>
        public static NativeStringList Split(this NativeList<char> source, NativeList<char> delim, Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }
        /// <summary>
        /// split function by NativeList<char> delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public unsafe static void Split(this IStringEntityBase source, NativeList<char> delim, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();
            if (delim.Length <= 0) throw new ArgumentException("delim is empty.");
            SplitImpl((char*)source.GetUnsafePtr(), source.Length, (char*)delim.GetUnsafePtr(), delim.Length, result);
        }
        /// <summary>
        /// split function by NativeList<char> delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeStringList Split(this IStringEntityBase source, NativeList<char> delim, Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }
        /// <summary>
        /// split function by StringEntity delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public unsafe static void Split(this NativeList<char> source, IStringEntityBase delim, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();
            if (delim.Length <= 0) throw new ArgumentException("delim is empty.");
            SplitImpl((char*)source.GetUnsafePtr(), source.Length, (char*)delim.GetUnsafePtr(), delim.Length, result);
        }
        /// <summary>
        /// split function by StringEntity delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns>result</returns>
        public static NativeStringList Split(this NativeList<char> source, IStringEntityBase delim, Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }
        /// <summary>
        /// split function by StringEntity delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public unsafe static void Split(this IStringEntityBase source, IStringEntityBase delim, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();
            if (delim.Length <= 0) throw new ArgumentException("delim is empty.");
            SplitImpl((char*)source.GetUnsafePtr(), source.Length, (char*)delim.GetUnsafePtr(), delim.Length, result);
        }
        /// <summary>
        /// split function by StringEntity delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeStringList Split(this IStringEntityBase source, IStringEntityBase delim, Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }
    }
}