using System;
using System.Collections.Generic;

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
        public void AddDelim<T>(T delim)
            where T : IJaggedArraySliceBase<Char16>
        {
            if (_delims.IndexOf(delim) < 0) _delims.Add(delim);
        }
        public void AddDelim(string delim)
        {
            if (_delims.IndexOf(delim) < 0) _delims.Add(delim);
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

        private unsafe void SplitImpl(Char16* source_ptr, int source_len, NativeStringList result)
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
                        Char16* st_ptr = source_ptr + start;
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
                Char16* st_ptr = source_ptr + start;
                result.Add(st_ptr, len);
            }
        }

        public unsafe void Split(NativeList<Char16> source, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();

            // redirect to using Char16.IsWhiteSpace() function
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

            this.SplitImpl((Char16*)source.GetUnsafePtr(), source.Length, result);
        }
        public NativeStringList Split(NativeList<Char16> input, Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            this.Split(input, tmp);
            return tmp;
        }
        public unsafe void Split<T>(in T source, NativeStringList result, bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
        {
            if (!append) result.Clear();
            var res = new StringSplitterExt.SplitResultStringList<T>(result.GetUnsafeRef());

            // redirect to using Char16.IsWhiteSpace() function
            if (_delims.Length == 0)
            {
                StringSplitterExt.SplitWhiteSpaceImpl(source, res);
                return;
            }
            // redirect to using single char delim function
            if (_delims.Length == 1 && _delims[0].Length == 1)
            {
                StringSplitterExt.SplitCharImpl(source, _delims[0][0], res);
                return;
            }

            this.SplitImpl((Char16*)source.GetUnsafePtr(), source.Length, result);
        }
    }

    public static class StringSplitterExt
    {
        internal interface IAddSlice<T>
                where T : IJaggedArraySliceBase<Char16>
        {
            void Add(T slice);
        }
        internal struct SplitResultList<T> : IAddSlice<T>
            where T : unmanaged, IJaggedArraySliceBase<Char16>
        {
            private UnsafeRefToNativeList<T> _res;
            public SplitResultList(UnsafeRefToNativeList<T> result)
            {
                _res = result;
            }
            public void Add(T slice)
            {
                _res.Add(slice);
            }
        }
        internal struct SplitResultStringList<T> : IAddSlice<T>
            where T : IJaggedArraySliceBase<Char16>
        {
            private UnsafeRefToNativeStringList _res;
            public SplitResultStringList(UnsafeRefToNativeStringList result)
            {
                _res = result;
            }
            public unsafe void Add(T slice)
            {
                _res.Add((Char16*)slice.GetUnsafePtr(), slice.Length);
            }
        }

        internal static unsafe void SplitWhiteSpaceImpl<Ts, Tr>(Ts source, Tr result)
            where Ts : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<Ts>
            where Tr : IAddSlice<Ts>
        {
            int len_source = source.Length;
            Char16* ptr_source = (Char16*)source.GetUnsafePtr();
            int start = 0;
            for (int i = 0; i < len_source; i++)
            {
                if (ptr_source[i].IsWhiteSpace())
                {
                    int len = (i - start);
                    if (len > 0)
                    {
                        result.Add(source.Slice(start, i));
                    }
                    start = i + 1;
                }
            }

            // final part
            if (start < len_source)
            {
                result.Add(source.Slice(start, len_source));
            }
        }
        internal static unsafe void SplitCharImpl<Ts, Tr>(Ts source, Char16 delim, Tr result)
            where Ts : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<Ts>
            where Tr : IAddSlice<Ts>
        {
            int len_source = source.Length;
            Char16* ptr_source = (Char16*)source.GetUnsafePtr();
            int start = 0;
            for (int i = 0; i < len_source; i++)
            {
                if (ptr_source[i] == delim)
                {
                    int len = (i - start);
                    if (len > 0)
                    {
                        result.Add(source.Slice(start, i));
                    }
                    start = i + 1;
                }
            }

            // final part
            if (start < len_source)
            {
                result.Add(source.Slice(start, len_source));
            }
        }
        internal static unsafe void SplitStringImpl<Ts, Tdel, Tr>(Ts source, Tdel delim, Tr result)
            where Ts : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<Ts>
            where Tdel : IJaggedArraySliceBase<Char16>
            where Tr : IAddSlice<Ts>
        {
            int len_source = source.Length;
            Char16* ptr_source = (Char16*)source.GetUnsafePtr();
            int len_delim = delim.Length;
            Char16* ptr_delim = (Char16*)delim.GetUnsafePtr();

            if (len_delim == 0)
            {
                result.Add(source);
                return;
            }

            int start = 0;
            for (int i = 0; i < len_source; i++)
            {
                //if (i < start) continue;

                if (ptr_source[i].Equals(ptr_delim[0]))
                {
                    if (len_source - i < len_delim) break;

                    // delim check
                    bool is_delim = true;
                    for (int j = 1; j < len_delim; j++)
                    {
                        if (!ptr_source[i + j].Equals(ptr_delim[j]))
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
                            result.Add(source.Slice(start, i));
                        }
                        start = i + len_delim;
                        i = start;
                    }
                }
            }

            // final part
            if (start < len_source)
            {
                result.Add(source.Slice(start, len_source));
            }
        }

        /// <summary>
        /// split  by Char16.IsWhiteSpace() delimiter.
        /// </summary>
        public static void Split<T>(this T source, UnsafeRefToNativeList<T> result, bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
        {
            if (!append) result.Clear();
            var res = new SplitResultList<T>(result);
            SplitWhiteSpaceImpl(source, res);
        }
        /// <summary>
        /// split  by Char16.IsWhiteSpace() delimiter.
        /// </summary>
        public static void Split<T>(this T source, UnsafeRefToNativeStringList result, bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
        {
            if (!append) result.Clear();
            var res = new SplitResultStringList<T>(result);
            SplitWhiteSpaceImpl(source, res);
        }
        /// <summary>
        /// split by single Char16 delimiter.
        /// </summary>
        public static void Split<T>(this T source, Char16 delim, UnsafeRefToNativeList<T> result, bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
        {
            if (!append) result.Clear();
            var res = new SplitResultList<T>(result);
            SplitCharImpl(source, delim, res);
        }
        /// <summary>
        /// split by single Char16 delimiter.
        /// </summary>
        public static void Split<T>(this T source, Char16 delim, UnsafeRefToNativeStringList result, bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
        {
            if (!append) result.Clear();
            var res = new SplitResultStringList<T>(result);
            SplitCharImpl(source, delim, res);
        }
        /// <summary>
        /// split by IJaggedArraySliceBase<Char16> delimiter.
        /// </summary>
        public static void Split<T, Tdel>(this T source, Tdel delim, UnsafeRefToNativeList<T> result, bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
            where Tdel : IJaggedArraySliceBase<Char16>
        {
            if (!append) result.Clear();
            var res = new SplitResultList<T>(result);
            SplitStringImpl(source, delim, res);
        }
        /// <summary>
        /// split by IJaggedArraySliceBase<Char16> delimiter.
        /// </summary>
        public static void Split<T, Tdel>(this T source, Tdel delim, UnsafeRefToNativeStringList result, bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
            where Tdel : IJaggedArraySliceBase<Char16>
        {
            if (!append) result.Clear();
            var res = new SplitResultStringList<T>(result);
            SplitStringImpl(source, delim, res);
        }

        /// <summary>
        /// split into NativeStringList by Char16.IsWhiteSpace() delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static unsafe void Split(this NativeList<Char16> source, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();
            var se = source.ToStringEntity();
            se.Split(result.GetUnsafeRef());
        }
        /// <summary>
        /// split into NativeStringList by Char16.IsWhiteSpace() delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="alloc"></param>
        /// <returns>result</returns>
        public static NativeStringList Split(this NativeList<Char16> source, Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            source.Split(tmp, false);
            return tmp;
        }

        /// <summary>
        /// split into NativeList(IJaggedArraySliceBase<Char16>) by Char16.IsWhiteSpace() delimiter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static void Split<T>(this T source, NativeList<T> result, bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
        {
            if (!append) result.Clear();
            source.Split(result.GetUnsafeRef());
        }
        /// <summary>
        /// split into NativeStringList by Char16.IsWhiteSpace() delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static unsafe void Split<T>(this T source, NativeStringList result, bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
        {
            if (!append) result.Clear();
            source.Split(result.GetUnsafeRef());
        }
        /// <summary>
        /// split into NativeStringList by Char16.IsWhiteSpace() delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeStringList Split<T>(this T source, Allocator alloc)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
        {
            var tmp = new NativeStringList(alloc);
            source.Split(tmp, false);
            return tmp;
        }

        
        /// <summary>
        /// split into NativeStringList by single Char16 delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static unsafe void Split(this NativeList<Char16> source, Char16 delim, NativeStringList result, bool append = false)
        {
            if (!append) result.Clear();
            var se = source.ToStringEntity();
            se.Split(delim, result.GetUnsafeRef());
        }
        /// <summary>
        /// split into NativeStringList by single char delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns>result</returns>
        public static NativeStringList Split(this NativeList<Char16> source, Char16 delim, Allocator alloc)
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }

        /// <summary>
        /// split into NativeList(IJaggedArraySliceBase<Char16>) by single Char16 delimiter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static void Split<T>(this T source,
                                    Char16 delim,
                                    NativeList<T> result,
                                    bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
        {
            if (!append) result.Clear();
            source.Split(delim, result.GetUnsafeRef());
        }
        /// <summary>
        /// split into NativeStringList by single Char16 delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static unsafe void Split<T>(this T source,
                                           Char16 delim,
                                           NativeStringList result,
                                           bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
        {
            if (!append) result.Clear();
            source.Split(delim, result.GetUnsafeRef());
        }
        /// <summary>
        /// split into NativeStringList by single Char16 delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeStringList Split<T>(this T source,
                                                Char16 delim,
                                                Allocator alloc)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }



        /// <summary>
        /// split into NativeStringList by NativeList(Char16) delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public unsafe static void Split(this NativeList<Char16> source,
                                        NativeList<Char16> delim,
                                        NativeStringList result,
                                        bool append = false)
        {
            if (!append) result.Clear();
            var se = source.ToStringEntity();
            var de = delim.ToStringEntity();
            se.Split(de, result.GetUnsafeRef());
        }
        /// <summary>
        /// split into NativeStringList by NativeList(Char16) delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns>result</returns>
        public static NativeStringList Split(this NativeList<Char16> source,
                                             NativeList<Char16> delim,
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
        public unsafe static void Split<T>(this NativeList<Char16> source,
                                           T delim,
                                           NativeStringList result,
                                           bool append = false)
            where T : IJaggedArraySliceBase<Char16>
        {
            if (!append) result.Clear();
            var se = source.ToStringEntity();
            se.Split(delim, result.GetUnsafeRef());
        }
        /// <summary>
        /// split into NativeStringList by StringEntity delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns>result</returns>
        public static NativeStringList Split<T>(this NativeList<Char16> source,
                                                T delim,
                                                Allocator alloc)
            where T : IJaggedArraySliceBase<Char16>
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }

        /// <summary>
        /// split into NativeList(IJaggedArraySliceBase<Char16>) by NativeList(Char16) delimiter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static void Split<T>(this T source,
                                    NativeList<Char16> delim,
                                    NativeList<T> result,
                                    bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
        {
            if (!append) result.Clear();
            source.Split(delim.ToStringEntity(), result.GetUnsafeRef());
        }
        /// <summary>
        /// split into NativeList(IJaggedArraySliceBase<Char16>) by IJaggedArraySliceBase<Char16> delimiter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public static void Split<T, Tdel>(this T source,
                                          Tdel delim,
                                          NativeList<T> result,
                                          bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
            where Tdel : IJaggedArraySliceBase<Char16>
        {
            if (!append) result.Clear();
            source.Split(delim, result.GetUnsafeRef());
        }
        /// <summary>
        /// split into NativeStringList by NativeList(Char16) delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="result"></param>
        /// <param name="append"></param>
        public unsafe static void Split<T>(this T source,
                                           NativeList<Char16> delim,
                                           NativeStringList result,
                                           bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
        {
            if (!append) result.Clear();
            var de = delim.ToStringEntity();
            source.Split(de, result.GetUnsafeRef());
        }
        /// <summary>
        /// split into NativeStringList by NativeList(Char16) delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeStringList Split<T>(this T source,
                                                NativeList<Char16> delim,
                                                Allocator alloc)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
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
        public unsafe static void Split<T, Tdel>(this T source,
                                                 Tdel delim,
                                                 NativeStringList result,
                                                 bool append = false)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
            where Tdel : IJaggedArraySliceBase<Char16>
        {
            if (!append) result.Clear();
            source.Split(delim, result.GetUnsafeRef());
        }
        /// <summary>
        /// split into NativeStringList by StringEntity delimiter.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="delim"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeStringList Split<T, Tdel>(this T source,
                                                      Tdel delim,
                                                      Allocator alloc)
            where T : unmanaged, IJaggedArraySliceBase<Char16>, ISlice<T>
            where Tdel : IJaggedArraySliceBase<Char16>
        {
            var tmp = new NativeStringList(alloc);
            source.Split(delim, tmp, false);
            return tmp;
        }
    }
}