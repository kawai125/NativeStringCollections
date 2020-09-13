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


    public static class StringStripperExt
    {
        private unsafe static void StripImpl(char* source_ptr, int source_len, bool left, bool right, NativeList<char> result)
        {
            // L side
            int start = 0;
            if (left)
            {
                for (int i = 0; i < source_len; i++)
                {
                    if (!char.IsWhiteSpace(source_ptr[i]))
                    {
                        start = i - 1;
                        break;
                    }
                }
            }

            // R side
            int last = source_len - 1;
            if (right)
            {
                for (int i = last; i >= 0; i--)
                {
                    if (!char.IsWhiteSpace(source_ptr[i]))
                    {
                        last = i - 1;
                        break;
                    }
                }
            }
            
            // make result
            int len = last - start + 1;
            if(len > 0) result.AddRange((void*)(source_ptr + start), len);
        }
        private unsafe static void StripImpl(char* source_ptr, int source_len, char target, bool left, bool right, NativeList<char> result)
        {
            // L side
            int start = 0;
            if (left)
            {
                for (int i = 0; i < source_len; i++)
                {
                    if (source_ptr[i] != target)
                    {
                        start = i + 1;
                        break;
                    }
                }
            }

            // R side
            int last = source_len - 1;
            if (right)
            {
                for (int i = last; i >= 0; i--)
                {
                    if (source_ptr[i] != target)
                    {
                        last = i - 1;
                        break;
                    }
                }
            }

            // make result
            int len = last - start + 1;
            if (len > 0) result.AddRange((void*)(source_ptr + start), len);
        }
        private unsafe static void StripImpl(char* source_ptr, int source_len,
                                             char* tgt_ptr, int tgt_len,
                                             bool left, bool right,
                                             NativeList<char> result)
        {
            // init
            int start = 0;
            int last = source_len - tgt_len;

            // L side
            if (left)
            {
                for (int i = start; i < last; i++)
                {
                    bool match = true;
                    for (int j = 0; j < tgt_len; j++)
                    {
                        if (source_ptr[i + j] != tgt_ptr[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        start = i + tgt_len;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // R side
            if (right)
            {
                for (int i = last; i >= start; i--)
                {
                    bool match = true;
                    for (int j = 0; j < tgt_len; j++)
                    {
                        if (source_ptr[i + j] != tgt_ptr[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        last = i - tgt_len;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // make result
            int len = last - start + 1;
            if (len > 0) result.AddRange((void*)(source_ptr + start), len);
        }

        /// <summary>
        /// strip char.IsWhiteSpece() charactors from left side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="result"></param>
        public unsafe static void Lstrip(this NativeList<char> source, NativeList<char> result)
        {
            result.Clear();
            StripImpl((char*)source.GetUnsafePtr(), source.Length, true, false, result);
        }
        /// <summary>
        /// strip char.IsWhiteSpece() charactors from right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="result"></param>
        public unsafe static void Rstrip(this NativeList<char> source, NativeList<char> result)
        {
            result.Clear();
            StripImpl((char*)source.GetUnsafePtr(), source.Length, false, true, result);
        }
        /// <summary>
        /// strip char.IsWhiteSpece() charactors from left and right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="result"></param>
        public unsafe static void Strip(this NativeList<char> source, NativeList<char> result)
        {
            result.Clear();
            StripImpl((char*)source.GetUnsafePtr(), source.Length, true, true, result);
        }
        /// <summary>
        /// strip char.IsWhiteSpece() charactors from left side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeList<char> Lstrip(this NativeList<char> source, Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            Lstrip(source, tmp);
            return tmp;
        }
        /// <summary>
        /// strip char.IsWhiteSpece() charactors from right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeList<char> Rstrip(this NativeList<char> source, Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            Rstrip(source, tmp);
            return tmp;
        }
        /// <summary>
        /// strip char.IsWhiteSpece() charactors from left and right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeList<char> Strip(this NativeList<char> source, Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            Strip(source, tmp);
            return tmp;
        }

        
        /// <summary>
        /// strip target charactor from left side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="result"></param>
        public unsafe static void Lstrip(this NativeList<char> source, char target, NativeList<char> result)
        {
            result.Clear();
            StripImpl((char*)source.GetUnsafePtr(), source.Length, target, true, false, result);
        }
        /// <summary>
        /// strip target charactor from right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="result"></param>
        public unsafe static void Rstrip(this NativeList<char> source, char target, NativeList<char> result)
        {
            result.Clear();
            StripImpl((char*)source.GetUnsafePtr(), source.Length, target, false, true, result);
        }
        /// <summary>
        /// strip target charactor from left and right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="result"></param>
        public unsafe static void Strip(this NativeList<char> source, char target, NativeList<char> result)
        {
            result.Clear();
            StripImpl((char*)source.GetUnsafePtr(), source.Length, target, true, true, result);
        }
        /// <summary>
        /// strip target charactor from left side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeList<char> Lstrip(this NativeList<char> source, char target, Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            Lstrip(source, target, tmp);
            return tmp;
        }
        /// <summary>
        /// strip target charactor from right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeList<char> Rstrip(this NativeList<char> source, char target, Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            Rstrip(source, target, tmp);
            return tmp;
        }
        /// <summary>
        /// strip target charactor from left and right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeList<char> Strip(this NativeList<char> source, char target, Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            Strip(source, target, tmp);
            return tmp;
        }


        /// <summary>
        /// strip target string from left side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="result"></param>
        public unsafe static void Lstrip(this NativeList<char> source, NativeList<char> target, NativeList<char> result)
        {
            result.Clear();
            StripImpl((char*)source.GetUnsafePtr(), source.Length,
                      (char*)target.GetUnsafePtr(), target.Length,
                      true, false, result);
        }
        /// <summary>
        /// strip target string from right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="result"></param>
        public unsafe static void Rstrip(this NativeList<char> source, NativeList<char> target, NativeList<char> result)
        {
            result.Clear();
            StripImpl((char*)source.GetUnsafePtr(), source.Length,
                      (char*)target.GetUnsafePtr(), target.Length,
                      false, true, result);
        }
        /// <summary>
        /// strip target string from left and right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="result"></param>
        public unsafe static void Strip(this NativeList<char> source, NativeList<char> target, NativeList<char> result)
        {
            result.Clear();
            StripImpl((char*)source.GetUnsafePtr(), source.Length,
                      (char*)target.GetUnsafePtr(), target.Length,
                      true, true, result);
        }
        /// <summary>
        /// strip target string from left side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeList<char> Lstrip(this NativeList<char> source, NativeList<char> target, Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            Lstrip(source, target, tmp);
            return tmp;
        }
        /// <summary>
        /// strip target string from right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeList<char> Rstrip(this NativeList<char> source, NativeList<char> target, Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            Rstrip(source, target, tmp);
            return tmp;
        }
        /// <summary>
        /// strip target string from left and right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeList<char> Strip(this NativeList<char> source, NativeList<char> target, Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            Strip(source, target, tmp);
            return tmp;
        }
        /// <summary>
        /// strip target string from left side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="result"></param>
        public unsafe static void Lstrip(this NativeList<char> source, IStringEntityBase target, NativeList<char> result)
        {
            result.Clear();
            StripImpl((char*)source.GetUnsafePtr(), source.Length,
                      (char*)target.GetUnsafePtr(), target.Length,
                      true, false, result);
        }
        /// <summary>
        /// strip target string from right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="result"></param>
        public unsafe static void Rstrip(this NativeList<char> source, IStringEntityBase target, NativeList<char> result)
        {
            result.Clear();
            StripImpl((char*)source.GetUnsafePtr(), source.Length,
                      (char*)target.GetUnsafePtr(), target.Length,
                      false, true, result);
        }
        /// <summary>
        /// strip target string from left and right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="result"></param>
        public unsafe static void Strip(this NativeList<char> source, IStringEntityBase target, NativeList<char> result)
        {
            result.Clear();
            StripImpl((char*)source.GetUnsafePtr(), source.Length,
                      (char*)target.GetUnsafePtr(), target.Length,
                      true, true, result);
        }
        /// <summary>
        /// strip target string from left side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeList<char> Lstrip(this NativeList<char> source, IStringEntityBase target, Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            Lstrip(source, target, tmp);
            return tmp;
        }
        /// <summary>
        /// strip target string from right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeList<char> Rstrip(this NativeList<char> source, IStringEntityBase target, Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            Rstrip(source, target, tmp);
            return tmp;
        }
        /// <summary>
        /// strip target string from left and right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeList<char> Strip(this NativeList<char> source, IStringEntityBase target, Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            Strip(source, target, tmp);
            return tmp;
        }



        private unsafe static T StripImpl<T>(T source, bool left, bool right) where T : IStringEntityBase
        {
            // L side
            int start = 0;
            if (left)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    if (!char.IsWhiteSpace(source[i]))
                    {
                        start = i - 1;
                        break;
                    }
                }
            }

            // R side
            int last = source.Length - 1;
            if (right)
            {
                for (int i = last; i >= 0; i--)
                {
                    if (!char.IsWhiteSpace(source[i]))
                    {
                        last = i - 1;
                        break;
                    }
                }
            }

            // make result
            int len = last - start + 1;
            if (len > 0)
            {
                return (T)source.Slice(start, len);
            }
            else
            {
                return (T)source.Slice(0,0);
            }
        }
        private unsafe static T StripImpl<T>(T source, char target, bool left, bool right) where T : IStringEntityBase
        {
            // L side
            int start = 0;
            if (left)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] != target)
                    {
                        start = i + 1;
                        break;
                    }
                }
            }

            // R side
            int last = source.Length - 1;
            if (right)
            {
                for (int i = last; i >= 0; i--)
                {
                    if (source[i] != target)
                    {
                        last = i - 1;
                        break;
                    }
                }
            }

            // make result
            int len = last - start + 1;
            if (len > 0)
            {
                return (T)source.Slice(start, len);
            }
            else
            {
                return (T)source.Slice(0, 0);
            }
        }
        private unsafe static T StripImpl<T>(T source,
                                             char* tgt_ptr, int tgt_len,
                                             bool left, bool right) where T : IStringEntityBase
        {
            // init
            int start = 0;
            int last = source.Length - tgt_len;

            // L side
            if (left)
            {
                for (int i = start; i < last; i++)
                {
                    bool match = true;
                    for (int j = 0; j < tgt_len; j++)
                    {
                        if (source[i + j] != tgt_ptr[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        start = i + tgt_len;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // R side
            if (right)
            {
                for (int i = last; i >= start; i--)
                {
                    bool match = true;
                    for (int j = 0; j < tgt_len; j++)
                    {
                        if (source[i + j] != tgt_ptr[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        last = i - tgt_len;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // make result
            int len = last - start + 1;
            if (len > 0)
            {
                return (T)source.Slice(start, len);
            }
            else
            {
                return (T)source.Slice(0, 0);
            }
        }

        /// <summary>
        /// strip char.IsWhiteSpece() charactor from left side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public unsafe static T Lstrip<T>(this T source) where T : IStringEntityBase
        {
            return StripImpl(source, true, false);
        }
        /// <summary>
        /// strip char.IsWhiteSpece() charactor from right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public unsafe static T Rstrip<T>(this T source) where T : IStringEntityBase
        {
            return StripImpl(source, false, true);
        }
        /// <summary>
        /// strip char.IsWhiteSpece() charactor from left and right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public unsafe static T Strip<T>(this T source) where T : IStringEntityBase
        {
            return StripImpl(source, true, true);
        }
        /// <summary>
        /// strip target charactor from left side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Lstrip<T>(this T source, char target) where T : IStringEntityBase
        {
            return StripImpl(source, target, true, false);
        }
        /// <summary>
        /// strip target charactor from right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Rstrip<T>(this T source, char target) where T : IStringEntityBase
        {
            return StripImpl(source, target, false, true);
        }
        /// <summary>
        /// strip target charactor from left and right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Strip<T>(this T source, char target) where T : IStringEntityBase
        {
            return StripImpl(source, target, true, true);
        }
        /// <summary>
        /// strip target string from left side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Lstrip<T>(this T source, NativeList<char> target) where T : IStringEntityBase
        {
            return StripImpl(source, (char*)target.GetUnsafePtr(), target.Length, true, false);
        }
        /// <summary>
        /// strip target string from right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Rstrip<T>(this T source, NativeList<char> target) where T : IStringEntityBase
        {
            return StripImpl(source, (char*)target.GetUnsafePtr(), target.Length, false, true);
        }
        /// <summary>
        /// strip target string from left and right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Strip<T>(this T source, NativeList<char> target) where T : IStringEntityBase
        {
            return StripImpl(source, (char*)target.GetUnsafePtr(), target.Length, true, true);
        }
        /// <summary>
        /// strip target string from left side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Lstrip<T>(this T source, IStringEntityBase target) where T : IStringEntityBase
        {
            return StripImpl(source, (char*)target.GetUnsafePtr(), target.Length, true, false);
        }
        /// <summary>
        /// strip target string from right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Rstrip<T>(this T source, IStringEntityBase target) where T : IStringEntityBase
        {
            return StripImpl(source, (char*)target.GetUnsafePtr(), target.Length, false, true);
        }
        /// <summary>
        /// strip target string from left and right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Strip<T>(this T source, IStringEntityBase target) where T : IStringEntityBase
        {
            return StripImpl(source, (char*)target.GetUnsafePtr(), target.Length, true, true);
        }
    }
}