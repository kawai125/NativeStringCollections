using Unity.Collections;


namespace NativeStringCollections
{
    using NativeStringCollections.Utility;
    using NativeStringCollections.Impl;


    public static class StringStripperExt
    {

        private unsafe static T StripWhiteSpaceImpl<T>(in T source, bool left, bool right)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            // L side
            int start = 0;
            if (left)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    if (!char.IsWhiteSpace(source[i]))
                    {
                        start = i;
                        break;
                    }
                }
            }

            // R side
            int last = source.Length - 1;
            if (right)
            {
                for (int i = last; i >= start; i--)
                {
                    if (!char.IsWhiteSpace(source[i]))
                    {
                        last = i;
                        break;
                    }
                }
            }

            // make result
            int len = last - start + 1;
            if (len > 0)
            {
                int end = last + 1;
                return (T)source.Slice(start, end);
            }
            else
            {
                return (T)source.Slice(0,0);
            }
        }
        private unsafe static T StripCharImpl<T>(in T source, char target, bool left, bool right)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            // L side
            int start = 0;
            if (left)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] != target)
                    {
                        start = i;
                        break;
                    }
                }
            }

            // R side
            int last = source.Length - 1;
            if (right)
            {
                for (int i = last; i >= start; i--)
                {
                    if (source[i] != target)
                    {
                        last = i;
                        break;
                    }
                }
            }

            // make result
            int len = last - start + 1;
            if (len > 0)
            {
                int end = last + 1;
                return (T)source.Slice(start, end);
            }
            else
            {
                return (T)source.Slice(0, 0);
            }
        }
        private unsafe static T StripStringImpl<T>(in T source,
                                                   IJaggedArraySliceBase<char> target,
                                                   bool left, bool right)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            // L side
            int start = 0;
            if (left)
            {
                for (int i = 0; i <= source.Length - target.Length; i++)
                {
                    if (start > i) continue;

                    bool match = true;
                    for (int j = 0; j < target.Length; j++)
                    {
                        if (source[i + j] != target[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        start = i + target.Length;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // R side
            int last = source.Length - 1;
            if (right)
            {
                for (int i = source.Length - 1; i >= start; i--)
                {
                    if (last < i) continue;

                    bool match = true;
                    for (int j = 0; j < target.Length; j++)
                    {
                        if (source[i - j] != target[target.Length - 1 - j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        last = i - target.Length;
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
                int end = last + 1;
                return (T)source.Slice(start, end);
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
        public unsafe static T Lstrip<T>(this T source)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            return StripWhiteSpaceImpl(source, true, false);
        }
        /// <summary>
        /// strip char.IsWhiteSpece() charactor from right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public unsafe static T Rstrip<T>(this T source)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            return StripWhiteSpaceImpl(source, false, true);
        }
        /// <summary>
        /// strip char.IsWhiteSpece() charactor from left and right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public unsafe static T Strip<T>(this T source)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            return StripWhiteSpaceImpl(source, true, true);
        }
        /// <summary>
        /// strip target charactor from left side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Lstrip<T>(this T source, char target)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            return StripCharImpl(source, target, true, false);
        }
        /// <summary>
        /// strip target charactor from right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Rstrip<T>(this T source, char target)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            return StripCharImpl(source, target, false, true);
        }
        /// <summary>
        /// strip target charactor from left and right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Strip<T>(this T source, char target)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            return StripCharImpl(source, target, true, true);
        }
        /// <summary>
        /// strip target string from left side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Lstrip<T>(this T source, NativeList<char> target)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            return StripStringImpl(source, target.ToStringEntity(), true, false);
        }
        /// <summary>
        /// strip target string from right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Rstrip<T>(this T source, NativeList<char> target)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            return StripStringImpl(source, target.ToStringEntity(), false, true);
        }
        /// <summary>
        /// strip target string from left and right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Strip<T>(this T source, NativeList<char> target)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            return StripStringImpl(source, target.ToStringEntity(), true, true);
        }
        /// <summary>
        /// strip target string from left side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Lstrip<T>(this T source, IJaggedArraySliceBase<char> target)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            return StripStringImpl(source, target, true, false);
        }
        /// <summary>
        /// strip target string from right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Rstrip<T>(this T source, IJaggedArraySliceBase<char> target)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            return StripStringImpl(source, target, false, true);
        }
        /// <summary>
        /// strip target string from left and right side of source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public unsafe static T Strip<T>(this T source, IJaggedArraySliceBase<char> target)
            where T : IJaggedArraySliceBase<char>, ISlice<T>
        {
            return StripStringImpl(source, target, true, true);
        }





        /// <summary>
        /// strip char.IsWhiteSpece() charactors from left side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="result"></param>
        public unsafe static void Lstrip(this NativeList<char> source, NativeList<char> result)
        {
            result.Clear();
            var se = StripWhiteSpaceImpl(source.ToStringEntity(), true, false);
            result.AddRange((void*)se.GetUnsafePtr(), se.Length);
        }
        /// <summary>
        /// strip char.IsWhiteSpece() charactors from right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="result"></param>
        public unsafe static void Rstrip(this NativeList<char> source, NativeList<char> result)
        {
            result.Clear();
            var se = StripWhiteSpaceImpl(source.ToStringEntity(), false, true);
            result.AddRange((void*)se.GetUnsafePtr(), se.Length);
        }
        /// <summary>
        /// strip char.IsWhiteSpece() charactors from left and right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="result"></param>
        public unsafe static void Strip(this NativeList<char> source, NativeList<char> result)
        {
            result.Clear();
            var se = StripWhiteSpaceImpl(source.ToStringEntity(), true, true);
            result.AddRange((void*)se.GetUnsafePtr(), se.Length);
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
            var se = StripCharImpl(source.ToStringEntity(), target, true, false);
            result.AddRange((void*)se.GetUnsafePtr(), se.Length);
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
            var se = StripCharImpl(source.ToStringEntity(), target, false, true);
            result.AddRange((void*)se.GetUnsafePtr(), se.Length);
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
            var se = StripCharImpl(source.ToStringEntity(), target, true, true);
            result.AddRange((void*)se.GetUnsafePtr(), se.Length);
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
            var se = StripStringImpl(source.ToStringEntity(), target.ToStringEntity(), true, false);
            result.AddRange((void*)se.GetUnsafePtr(), se.Length);
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
            var se = StripStringImpl(source.ToStringEntity(), target.ToStringEntity(), false, true);
            result.AddRange((void*)se.GetUnsafePtr(), se.Length);
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
            var se = StripStringImpl(source.ToStringEntity(), target.ToStringEntity(), true, true);
            result.AddRange((void*)se.GetUnsafePtr(), se.Length);
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
        public unsafe static void Lstrip(this NativeList<char> source, IJaggedArraySliceBase<char> target, NativeList<char> result)
        {
            result.Clear();
            var se = StripStringImpl(source.ToStringEntity(), target, true, false);
            result.AddRange((void*)se.GetUnsafePtr(), se.Length);
        }
        /// <summary>
        /// strip target string from right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="result"></param>
        public unsafe static void Rstrip(this NativeList<char> source, IJaggedArraySliceBase<char> target, NativeList<char> result)
        {
            result.Clear();
            var se = StripStringImpl(source.ToStringEntity(), target, false, true);
            result.AddRange((void*)se.GetUnsafePtr(), se.Length);
        }
        /// <summary>
        /// strip target string from left and right side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="result"></param>
        public unsafe static void Strip(this NativeList<char> source, IJaggedArraySliceBase<char> target, NativeList<char> result)
        {
            result.Clear();
            var se = StripStringImpl(source.ToStringEntity(), target, true, true);
            result.AddRange((void*)se.GetUnsafePtr(), se.Length);
        }
        /// <summary>
        /// strip target string from left side.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="alloc"></param>
        /// <returns></returns>
        public static NativeList<char> Lstrip(this NativeList<char> source, IJaggedArraySliceBase<char> target, Allocator alloc)
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
        public static NativeList<char> Rstrip(this NativeList<char> source, IJaggedArraySliceBase<char> target, Allocator alloc)
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
        public static NativeList<char> Strip(this NativeList<char> source, IJaggedArraySliceBase<char> target, Allocator alloc)
        {
            var tmp = new NativeList<char>(alloc);
            Strip(source, target, tmp);
            return tmp;
        }
    }
}