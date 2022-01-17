using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

using NativeStringCollections;
using NativeStringCollections.Utility;
using NativeStringCollections.Impl;

namespace NativeStringCollections
{
    public static class FilePathUtility
    {
        /// <summary>
        /// sort file paths.
        /// </summary>
        static public void Sort<T>(T source, List<string> result)
            where T : IEnumerable<string>
        {
            var tmp_source = ConvertListStr(source, Allocator.TempJob);
            Sort(tmp_source, result);
            tmp_source.Dispose();
        }
        /// <summary>
        /// sort file paths.
        /// </summary>
        static public void Sort<T>(T source, NativeStringList result)
            where T : IEnumerable<string>
        {
            var tmp_source = ConvertListStr(source, Allocator.TempJob);
            Sort(tmp_source, result);
            tmp_source.Dispose();
        }
        /// <summary>
        /// sort file paths.
        /// </summary>
        static public void Sort(NativeStringList source, List<string> result)
        {
            var tmp_result = new NativeStringList(Allocator.TempJob);
            Sort(source, tmp_result);
            WriteBackListStr(tmp_result, result);
            tmp_result.Dispose();
        }
        /// <summary>
        /// sort file paths.
        /// </summary>
        static public void Sort(NativeStringList source, NativeStringList result)
        {
            var job = new SortJob(source, result, Allocator.TempJob);
            job.Run();
            job.Dispose();
        }

        /// <summary>
        /// sort file paths with filtering.
        /// </summary>
        static public void Sort<T>(T source, string target_pattern, List<string> result)
            where T : IEnumerable<string>
        {
            var tmp_source = ConvertListStr(source, Allocator.TempJob);
            Sort(tmp_source, target_pattern, result);
            tmp_source.Dispose();
        }
        /// <summary>
        /// sort file paths with filtering.
        /// </summary>
        static public void Sort<T>(T source, string target_pattern, NativeStringList result)
           where T : IEnumerable<string>
        {
            var tmp_source = ConvertListStr(source, Allocator.TempJob);
            Sort(tmp_source, target_pattern, result);
            tmp_source.Dispose();
        }
        /// <summary>
        /// sort file paths with filtering.
        /// </summary>
        static public void Sort<T>(T source, ReadOnlyStringEntity target_pattern, List<string> result)
           where T : IEnumerable<string>
        {
            var tmp_source = ConvertListStr(source, Allocator.TempJob);
            Sort(tmp_source, target_pattern, result);
            tmp_source.Dispose();
        }
        /// <summary>
        /// sort file paths with filtering.
        /// </summary>
        static public void Sort(NativeStringList source, string target_pattern, List<string> result)
        {
            var tmp_result = new NativeStringList(Allocator.TempJob);
            Sort(source, target_pattern, tmp_result);
            WriteBackListStr(tmp_result, result);
            tmp_result.Dispose();
        }
        /// <summary>
        /// sort file paths with filtering.
        /// </summary>
        static public void Sort<T>(T source, ReadOnlyStringEntity target_pattern, NativeStringList result)
          where T : IEnumerable<string>
        {
            var tmp_source = ConvertListStr(source, Allocator.TempJob);
            Sort(tmp_source, target_pattern, result);
            tmp_source.Dispose();
        }
        /// <summary>
        /// sort file paths with filtering.
        /// </summary>
        static public void Sort(NativeStringList source, string target_pattern, NativeStringList result)
        {
            var char_list = new NativeList<char>(Allocator.TempJob);
            char_list.Capacity = target_pattern.Length;
            foreach(var c in target_pattern)
            {
                char_list.Add(c);
            }
            Sort(source, char_list.ToStringEntity(), result);
            char_list.Dispose();
        }
        /// <summary>
        /// sort file paths with filtering.
        /// </summary>
        static public void Sort(NativeStringList source, ReadOnlyStringEntity target_pattern, List<string> result)
        {
            var tmp_result = new NativeStringList(Allocator.TempJob);
            Sort(source, target_pattern, tmp_result);
            WriteBackListStr(tmp_result, result);
            tmp_result.Dispose();
        }
        /// <summary>
        /// sort file paths with filtering.
        /// </summary>
        static public void Sort(NativeStringList source, ReadOnlyStringEntity target_pattern, NativeStringList result)
        {
            var job = new SortJob(source, target_pattern, result, Allocator.TempJob);
            job.Run();
            job.Dispose();
        }

        static private NativeStringList ConvertListStr<T>(T str_list, Allocator alloc)
            where T : IEnumerable<string>
        {
            var tmp = new NativeStringList(alloc);
            foreach (var str in str_list)
            {
                tmp.Add(str);
            }
            return tmp;
        }
        static private void WriteBackListStr(NativeStringList source, List<string> result)
        {
            result.Clear();
            for (int i = 0; i < source.Length; i++)
            {
                result.Add(source[i].ToString());
            }
        }

        internal enum WordType
        {
            None,
            String,
            Digit,
        }
        internal readonly struct WordBlock
        {
            public readonly long num;
            public readonly ReadOnlyStringEntity word;
            public readonly WordType type;

            public WordBlock(ReadOnlyStringEntity word, WordType type)
            {
                this.word = word;
                this.type = type;
                if (type == WordType.Digit)
                {
                    word.TryParse(out this.num);
                }
                else
                {
                    num = 0;
                }
            }
        }

        [BurstCompile]
        public struct SortJob : IJob, IDisposable
        {
            public NativeStringList source, result;
            public ReadOnlyStringEntity target_pattern;

            private bool WithFiltering;

            private NativeList<WordBlock> TgtWords, tmpWords;
            private NativeJaggedArray<WordBlock> SrcWordsList;

            private readonly struct DecodedPath
            {
                public readonly NativeJaggedArraySlice<WordBlock> path;
                public readonly int index;

                public DecodedPath(NativeJaggedArraySlice<WordBlock> path, int index)
                {
                    this.path = path;
                    this.index = index;
                }
            }

            private NativeList<DecodedPath> paths;
            private NativeList<DecodedPath> tmp_list;

            public SortJob(NativeStringList source, ReadOnlyStringEntity target_pattern, NativeStringList result, Allocator alloc)
            {
                this.source = source;
                this.target_pattern = target_pattern;
                this.result = result;

                WithFiltering = true;

                TgtWords = new NativeList<WordBlock>(alloc);
                tmpWords = new NativeList<WordBlock>(alloc);
                SrcWordsList = new NativeJaggedArray<WordBlock>(alloc);

                paths = new NativeList<DecodedPath>(alloc);
                tmp_list = new NativeList<DecodedPath>(alloc);
            }
            public SortJob(NativeStringList source, NativeStringList result, Allocator alloc)
            {
                this.source = source;
                this.target_pattern = source[0]; // dummy value
                this.result = result;

                WithFiltering = false;

                TgtWords = new NativeList<WordBlock>(alloc);
                tmpWords = new NativeList<WordBlock>(alloc);
                SrcWordsList = new NativeJaggedArray<WordBlock>(alloc);

                paths = new NativeList<DecodedPath>(alloc);
                tmp_list = new NativeList<DecodedPath>(alloc);
            }

            public void Dispose()
            {
                TgtWords.Dispose();
                tmpWords.Dispose();
                SrcWordsList.Dispose();

                paths.Dispose();
                tmp_list.Dispose();
            }

            public void Execute()
            {
                //--- split into string & digits
                SrcWordsList.Clear();
                for (int i = 0; i < source.Length; i++)
                {
                    SplitWords(source[i], tmpWords);
                    SrcWordsList.Add(tmpWords);
                }
                SplitWords(target_pattern, TgtWords);

                paths.Clear();
                if (WithFiltering)
                {
                    //--- extract same format paths
                    for (int i = 0; i < SrcWordsList.Length; i++)
                    {
                        if (CheckFormat(TgtWords, SrcWordsList[i]))
                        {
                            paths.Add(new DecodedPath(SrcWordsList[i], i));
                        }
                    }
                    //--- sort by digits
                    tmp_list.Capacity = paths.Length;
                    MergeSort(paths, tmp_list, new ComparatorDigitPart(), 0, paths.Length);
                }
                else
                {
                    for (int i = 0; i < SrcWordsList.Length; i++)
                    {
                        paths.Add(new DecodedPath(SrcWordsList[i], i));
                    }
                    //--- sort by natural
                    tmp_list.Capacity = paths.Length;
                    MergeSort(paths, tmp_list, new ComparatorNatural(), 0, paths.Length);
                }

                //--- build result
                result.Clear();
                for(int i=0; i<paths.Length; i++)
                {
                    int path_index = paths[i].index;
                    result.Add(source[path_index]);
                }
            }

            private static void SplitWords(ReadOnlyStringEntity source_str,
                                           NativeList<WordBlock> result)
            {
                result.Clear();
                if (source_str.Length < 1) return;

                int start = 0;
                WordType type = CheckCharType(source_str[0]);

                for (int i = 1; i < source_str.Length; i++)
                {
                    WordType next_type = CheckCharType(source_str[i]);
                    if (type != next_type)
                    {
                        var block = new WordBlock(source_str.Slice(start, i), type);
                        result.Add(block);
                        start = i;
                        type = next_type;
                    }
                }
                if (start != source_str.Length)
                {
                    result.Add(new WordBlock(source_str.Slice(start), type));
                }
            }
            private static WordType CheckCharType(Char16 c)
            {
                if (c.IsDigit(out int m)) return WordType.Digit;
                return WordType.String;
            }
            private static bool CheckFormat(NativeList<WordBlock> reference, NativeJaggedArraySlice<WordBlock> target)
            {
                if (reference.Length != target.Length) return false;

                for (int i = 0; i < reference.Length; i++)
                {
                    var word_src = reference[i];
                    var word_tgt = target[i];

                    if (word_src.type != word_tgt.type) return false;
                    if (word_src.type == WordType.None) return false;

                    if (word_src.type == WordType.String)
                    {
                        if (word_src.word != word_tgt.word) return false;
                    }
                }

                return true;
            }
            /// <summary>
            /// ref: https://qiita.com/drken/items/44c60118ab3703f7727f#5-%E3%83%9E%E3%83%BC%E3%82%B8%E3%82%BD%E3%83%BC%E3%83%88-on-log-n
            /// </summary>
            private static unsafe void MergeSort<T>(NativeList<DecodedPath> paths,
                                                    NativeList<DecodedPath> tmp_list,
                                                    T Comparator, int left, int right)
                where T : unmanaged, IWordComparator
            {
                int length = right - left;
                if (length == 1) return;
                int mid = left + length / 2;

                MergeSort(paths, tmp_list, Comparator, left, mid);
                MergeSort(paths, tmp_list, Comparator, mid, right);

                tmp_list.Clear();
                //--- left part
                for (int i = left; i < mid; i++) tmp_list.Add(paths[i]);
                //--- right part (flip order)
                for (int i = right - 1; i >= mid; i--) tmp_list.Add(paths[i]);

                //--- merge
                int itr_left = 0;
                int itr_right = length - 1;
                for(int i = left; i < right; i++)
                {
                    //--- pop left side
                    if(Comparator.Compare(tmp_list[itr_left].path, tmp_list[itr_right].path) <= 0)
                    {
                        paths[i] = tmp_list[itr_left];
                        itr_left++;
                    }
                    //--- pop right side
                    else
                    {
                        paths[i] = tmp_list[itr_right];
                        itr_right--;
                    }
                }
            }

            private interface IWordComparator
            {
                int Compare(NativeJaggedArraySlice<WordBlock> left,
                            NativeJaggedArraySlice<WordBlock> right);
            }
            /// <summary>
            /// use only to same format paths
            /// </summary>
            private struct ComparatorDigitPart : IWordComparator
            {
                public int Compare(NativeJaggedArraySlice<WordBlock> left,
                                   NativeJaggedArraySlice<WordBlock> right)
                {
                    int length = math.min(left.Length, right.Length);
                    for (int i = 0; i < length; i++)
                    {
                        var word_left = left[i];
                        var word_right = right[i];
                        if (word_left.type == WordType.Digit)
                        {
                            if (word_left.num < word_right.num) return -1;
                            if (word_left.num > word_right.num) return 1;
                        }
                    }
                    if (left.Length > right.Length) return -1;
                    if (left.Length < right.Length) return 1;
                    return 0;
                }
            }
            /// <summary>
            /// comparison policy ref: https://days-of-programming.blogspot.com/2018/04/blog-post.html
            /// </summary>
            private struct ComparatorNatural : IWordComparator
            {
                public int Compare(NativeJaggedArraySlice<WordBlock> left,
                                   NativeJaggedArraySlice<WordBlock> right)
                {
                    if (left.Length < 1 && right.Length < 1) return 0;
                    if (left.Length < 1) return -1;
                    if (right.Length < 1) return 1;

                    if(left[0].type != right[0].type)
                    {
                        if (left[0].type == WordType.Digit) return -1;
                        if (right[0].type == WordType.Digit) return 1;
                        return 0;
                    }

                    int length = math.min(left.Length, right.Length);
                    for (int i = 0; i < length; i++)
                    {
                        var word_left = left[i];
                        var word_right = right[i];
                        if (word_left.type == WordType.Digit)
                        {
                            if (word_left.num < word_right.num) return -1;
                            if (word_left.num > word_right.num) return 1;
                        }
                        else if(word_left.type == WordType.String)
                        {
                            int word_len = math.min(word_left.word.Length, word_right.word.Length);
                            for(int j=0; j<word_len; j++)
                            {
                                Char16 c_left = word_left.word[j];
                                Char16 c_right = word_right.word[j];
                                if (c_left < c_right) return -1;
                                if (c_left > c_right) return 1;
                            }
                            if (word_left.word.Length < word_right.word.Length) return -1;
                            if (word_left.word.Length > word_right.word.Length) return 1;
                        }
                    }
                    if (left.Length < right.Length) return -1;
                    if (left.Length > right.Length) return 1;
                    return 0;
                }
            }
        }
    }
}
