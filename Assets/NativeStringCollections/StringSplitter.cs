using System;
using System.Text;
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

    public struct StringSplitter : IDisposable
    {
        private NativeStringList _delims;

        public StringSplitter(Allocator alloc)
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
                    int elem_len = i - start;
                    if (elem_len > 0)
                    {
                        char* st_ptr = source_ptr + start;
                        result.Add(st_ptr, elem_len);
                    }
                    start = i + match_len;
                    i += match_len;
                }
            }

            // final part
            if(start < source_len)
            {
                int len = source_len - start;
                char* st_ptr = source_ptr + start;
                result.Add(st_ptr, len);
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
        public unsafe void Split<T>(in T source, NativeStringList result, bool append = false)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
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

    public static class StringSplitterExt
    {
        // result container interface
        private interface IAddResult
        {
            void AddResult(IJaggedArraySliceBase<char> entity);
        }

        // result container
        private struct Result_NSL : IAddResult
        {
            public NativeStringList result;

            public unsafe void AddResult(IJaggedArraySliceBase<char> entity)
            {
                result.Add((char*)entity.GetUnsafePtr(), entity.Length);
            }
        }
        private struct Result_NL_SE<T> : IAddResult 
            where T : unmanaged, IJaggedArraySliceBase<char>
        {
            public NativeList<T> result;

            public void AddResult(IJaggedArraySliceBase<char> entity)
            {
                result.Add((T)entity);
            }
        }
        private static unsafe void SplitWhiteSpaceImpl<T>(in T source, IAddResult result)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            int start = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (Char.IsWhiteSpace(source[i]))
                {
                    int len = (i - start);
                    if (len > 0)
                    {
                        result.AddResult(source.Slice(start, i));
                    }
                    start = i + 1;
                }
            }

            // final part
            if (start < source.Length)
            {
                result.AddResult(source.Slice(start, source.Length));
            }
        }
        private static unsafe void SplitCharImpl<T>(in T source, char delim, IAddResult result)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            int start = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == delim)
                {
                    int len = (i - start);
                    if (len > 0)
                    {
                        result.AddResult(source.Slice(start, i));
                    }
                    start = i + 1;
                }
            }

            // final part
            if (start < source.Length)
            {
                result.AddResult(source.Slice(start, source.Length));
            }
        }
        private static unsafe void SplitStringImpl<T>(in T source, IJaggedArraySliceBase<char> delim, IAddResult result)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            int start = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (i < start) continue;

                if (source[i] == delim[0])
                {
                    if (source.Length - i < delim.Length) break;

                    // delim check
                    bool is_delim = true;
                    for (int j = 1; j < delim.Length; j++)
                    {
                        if (source[i + j] != delim[j])
                        {
                            is_delim = false;
                            break;
                        }
                    }

                    if (is_delim)
                    {
                        int len = (i - start);
                        if (len > 0)
                        {
                            result.AddResult(source.Slice(start, i));
                        }
                        start = i + delim.Length;
                    }
                }
            }

            // final part
            if (start < source.Length)
            {
                result.AddResult(source.Slice(start, source.Length));
            }
        }

        /// <summary>
        /// split into NativeStringList by Char.IsWhiteSpace() delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static unsafe void Split(this NativeList<char> source, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();
            var se = source.ToStringEntity();
            var res = new Result_NSL();
            res.result = result;
            SplitWhiteSpaceImpl(se, res);
        }
        /// <summary>
        /// split into NativeStringList by Char.IsWhiteSpace() delimiter.
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
        /// split into NativeList(IJaggedArraySliceBase<char>) by Char.IsWhiteSpace() delimiter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static void Split<T>(this T source, NativeList<T> result, bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<char>, ISlice<T>
        {
            if (!append) result.Clear();
            var res = new Result_NL_SE<T>();
            res.result = result;
            SplitWhiteSpaceImpl(source, res);
        }
        /// <summary>
        /// split into NativeStringList by Char.IsWhiteSpace() delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static unsafe void Split<T>(this T source, NativeStringList result, bool append = false)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            if (!append) result.Clear();
            var res = new Result_NSL();
            res.result = result;
            SplitWhiteSpaceImpl(source, res);
        }
        /// <summary>
        /// split into NativeStringList by Char.IsWhiteSpace() delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeStringList Split<T>(this T source, Allocator alloc)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            var tmp = new NativeStringList(alloc);
            source.Split(tmp, false);
            return tmp;
        }

        
        /// <summary>
        /// split into NativeStringList by single char delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static unsafe void Split(this NativeList<char> source, char delim, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();
            var se = source.ToStringEntity();
            var res = new Result_NSL();
            res.result = result;
            SplitCharImpl(se, delim, res);
        }
        /// <summary>
        /// split into NativeStringList by single char delimiter.
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
        /// split into NativeList(IJaggedArraySliceBase<char>) by single char delimiter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static void Split<T>(this T source,
                                    char delim,
                                    NativeList<T> result,
                                    bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<char>, ISlice<T>
        {
            if (!append) result.Clear();
            var res = new Result_NL_SE<T>();
            res.result = result;
            SplitCharImpl(source, delim, res);
        }
        /// <summary>
        /// split into NativeStringList by single char delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static unsafe void Split<T>(this T source,
                                           char delim,
                                           NativeStringList result,
                                           bool append = false)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            if (!append) result.Clear();
            var res = new Result_NSL();
            res.result = result;
            SplitCharImpl(source, delim, res);
        }
        /// <summary>
        /// split into NativeStringList by single char delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeStringList Split<T>(this T source,
                                                char delim,
                                                Allocator alloc)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }



        /// <summary>
        /// split into NativeStringList by NativeList(char) delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public unsafe static void Split(this NativeList<char> source,
                                        NativeList<char> delim,
                                        NativeStringList result,
                                        bool append = false)
        {
            if (!append) result.Clear();
            var se = source.ToStringEntity();
            var de = delim.ToStringEntity();
            var res = new Result_NSL();
            res.result = result;
            SplitStringImpl(se, de, res);
        }
        /// <summary>
        /// split into NativeStringList by NativeList(char) delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns>result</returns>
        public static NativeStringList Split(this NativeList<char> source,
                                             NativeList<char> delim,
                                             Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }
        /// <summary>
        /// split into NativeStringList by StringEntity delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public unsafe static void Split(this NativeList<char> source,
                                        IJaggedArraySliceBase<char> delim,
                                        NativeStringList result,
                                        bool append = false)
        {
            if (!append) result.Clear();
            var se = source.ToStringEntity();
            var res = new Result_NSL();
            res.result = result;
            SplitStringImpl(se, delim, res);
        }
        /// <summary>
        /// split into NativeStringList by StringEntity delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns>result</returns>
        public static NativeStringList Split(this NativeList<char> source,
                                             IJaggedArraySliceBase<char> delim,
                                             Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }

        /// <summary>
        /// split into NativeList(IJaggedArraySliceBase<char>) by NativeList(char) delimiter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static void Split<T>(this T source,
                                    NativeList<char> delim,
                                    NativeList<T> result,
                                    bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<char>, ISlice<T>
        {
            if (!append) result.Clear();
            var res = new Result_NL_SE<T>();
            res.result = result;
            SplitStringImpl(source, delim.ToStringEntity(), res);
        }
        /// <summary>
        /// split into NativeList(IJaggedArraySliceBase<char>) by IJaggedArraySliceBase<char> delimiter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static void Split<T>(this T source,
                                    IJaggedArraySliceBase<char> delim,
                                    NativeList<T> result,
                                    bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<char>, ISlice<T>
        {
            if (!append) result.Clear();
            var res = new Result_NL_SE<T>();
            res.result = result;
            SplitStringImpl(source, delim, res);
        }
        /// <summary>
        /// split into NativeStringList by NativeList(char) delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public unsafe static void Split<T>(this T source,
                                           NativeList<char> delim,
                                           NativeStringList result,
                                           bool append = false)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            if (!append) result.Clear();
            var de = delim.ToStringEntity();
            var res = new Result_NSL();
            res.result = result;
            SplitStringImpl(source, de, res);
        }
        /// <summary>
        /// split into NativeStringList by NativeList(char) delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeStringList Split<T>(this T source,
                                                NativeList<char> delim,
                                                Allocator alloc)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }
        
        /// <summary>
        /// split into NativeStringList by StringEntity delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public unsafe static void Split<T>(this T source,
                                           IJaggedArraySliceBase<char> delim,
                                           NativeStringList result,
                                           bool append = false)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            if (!append) result.Clear();
            var res = new Result_NSL();
            res.result = result;
            SplitStringImpl(source, delim, res);
        }
        /// <summary>
        /// split into NativeStringList by StringEntity delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeStringList Split<T>(this T source,
                                                IJaggedArraySliceBase<char> delim,
                                                Allocator alloc)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }
    }
}