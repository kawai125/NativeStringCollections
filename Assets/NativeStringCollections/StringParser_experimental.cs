using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Mathematics;

using Unity.Collections;
//using Unity.IO.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;


namespace NativeStringCollections
{
    using NativeStringCollections.Impl;

    public class StringParser : IDisposable, IEnumerable<LineEntity>
    {
        private NativeStringList buffer;
        private NativeList<ReadOnlyStringEntity> entityList;

        private NativeList<LineIndex> lineIndexList;

        private NativeStringList elemDelim;
        private NativeList<char> inputBuffer;
        private int now_pos;

        private bool allocated;

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {

            }
            if (this.allocated)
            {
                this.buffer.Dispose();
                this.entityList.Dispose();

                this.lineIndexList.Dispose();

                this.elemDelim.Dispose();
                this.inputBuffer.Dispose();

                this.allocated = false;
            }
        }

        private void Init()
        {
            this.buffer = new NativeStringList(Allocator.Persistent);
            this.entityList = new NativeList<ReadOnlyStringEntity>(Allocator.Persistent);

            this.lineIndexList = new NativeList<LineIndex>(Allocator.Persistent);

            this.elemDelim = new NativeStringList(Allocator.Persistent);
            this.inputBuffer = new NativeList<char>(Allocator.Persistent);
            this.now_pos = -1;

            this.allocated = true;
        }

        public StringParser()
        {
            this.Init();
        }
        ~StringParser()
        {
            this.Dispose(false);
        }

        public StringParser(string file_path, System.Text.Encoding encoding, IReadOnlyList<string> str_delims)
        {
            this.Init();
            this.Delim = str_delims;
            this.ReadFile(file_path, encoding);
        }

        public void ReadFile(string file_path, System.Text.Encoding encoding)
        {
            this.String = System.IO.File.ReadAllText(file_path, encoding);
        }

        public string String
        {
            set
            {
                Profiler.BeginSample(">>> load file into NativeStringList");

                this.ParseAllText(value);

                Profiler.EndSample();
            }
        }

        public IReadOnlyList<string> Delim
        {
            set
            {
                this.elemDelim.Clear();
                foreach (string delim in value)
                {
                    if(delim == null)
                    {
                        string str = "";
                        str += (char)0;
                        this.elemDelim.Add( str );
                    }
                    else if(delim.Length > 0)
                    {
                        this.elemDelim.Add(delim);
                    }
                }
            }
            get
            {
                var ret = new List<string>();
                foreach(var itr in this.elemDelim)
                {
                    ret.Add(itr.ToString());
                }
                return ret;
            }
        }

        public bool EndOfStream
        {
            get { return (this.now_pos < 0); }
        }

        //--- line enrity access
        public int Length { get { return this.lineIndexList.Length; } }

        unsafe public LineEntity this[int index]
        {
            get
            {
                var lineIndex = this.lineIndexList[index];
                return new LineEntity((ReadOnlyStringEntity*)this.entityList.GetUnsafeReadOnlyPtr(), lineIndex.Start, lineIndex.Length);
            }
        }
        public LineEntity At(int i)
        {
            if (i < 0 || this.Length <= i)
            {
                throw new IndexOutOfRangeException("index = " + i.ToString() + ", must be in range of [0~" + (this.Length - 1).ToString() + "].");
            }
            return this[i];
        }
        public IEnumerator<LineEntity> GetEnumerator()
        {
            for (int i = 0; i < this.Length; i++)
                yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        //--- elem direct access
        public int ElemLength { get { return this.buffer.Length; } }
        public ReadOnlyStringEntity GetStringEntity(int index)
        {
            return this.buffer[index];
        }
        public string SubString(int index)
        {
            return this.buffer.SubString(index);
        }

        //unsafe public void ParseLine()
        //{
        //    if (this.EndOfStream)
        //    {
        //        throw new IndexOutOfRangeException("already reach to end of string.");
        //    }
        //
        //    this.buffer.Clear();
        //    this.entityList.Clear();
        //    this.lineIndexList.Clear();
        //
        //    this.ParseLineImpl();
        //}

        public void ParseAllText(string value)
        {
            this.buffer.Clear();
            this.lineIndexList.Clear();
            this.now_pos = 0;

            while (!this.EndOfStream)
            {
                this.ParseLineStringImpl(value);
            }

            // update entity list
            this.entityList.Clear();
            for (int i=0; i<this.buffer.Length; i++)
            {
                this.entityList.Add(this.buffer[i]);
            }
        }

        unsafe private void ParseLineStringImpl(string value)
        {
            int line_start = math.max(this.buffer.Length - 1, 0);  // count by entity
            int line_elem = 0;

            int i_char = this.now_pos;

            this.inputBuffer.Clear();
            while (i_char < value.Length)
            {
                if (this.IsLineEnd(value, i_char, out int next_line_start))
                {
                    i_char = next_line_start;
                    if (this.inputBuffer.Length > 0)
                    {
                        this.buffer.Add( (char*)this.inputBuffer.GetUnsafePtr(), this.inputBuffer.Length );
                        this.inputBuffer.Clear();
                        line_elem++;
                    }

                    this.lineIndexList.Add(new LineIndex(line_start, line_elem));
                    break;
                }
                else if (this.IsDelim(value, i_char, out int next_start))
                {
                    if (this.inputBuffer.Length > 0)
                    {
                        this.buffer.Add((char*)this.inputBuffer.GetUnsafePtr(), this.inputBuffer.Length);
                        this.inputBuffer.Clear();
                        line_elem++;
                    }
                    i_char = next_start;
                }
                else
                {
                    inputBuffer.Add(value[i_char]);
                }
            }
            this.now_pos = i_char;
        }

        private bool IsLineEnd(string str, int i, out int next_start)
        {
            next_start = -1;
            int left_len = str.Length - i;

            // end of file
            if (left_len == 0)
            {
                next_start = -1;
                return true;
            }

            bool ret = false;
            char c = str[i];
            if (c == '\n' ||
                c == '\r')
            {
                ret = true;
                next_start = i + 1;

                if (left_len >= 2)
                {
                    if (c == '\r' & str[i + 1] == '\n')
                    {
                        next_start = i + 2;
                        if (left_len == 2) next_start = -1;
                    }
                }
                else
                {
                    // left_len <= 1, reach to end.
                    next_start = -1;
                }
            }

            return ret;
        }

        private bool IsDelim(string str, int index, out int next_start)
        {
            bool ret = false;
            next_start = -1;

            long left_len = str.Length - index;
            int match_span = 0;

            foreach (ReadOnlyStringEntity delim in this.elemDelim)
            {
                if (delim.Length > match_span && delim.Length <= left_len)
                {
                    if (delim.Length == 1 && delim[0] == 0)  // "null" delim
                    {
                        ret = Char.IsWhiteSpace(str[index]);
                        if (ret)
                        {
                            match_span = 1;
                            int j = 1;
                            while (Char.IsWhiteSpace(str[index + j]))
                            {
                                next_start = index + j;
                                j++;
                                match_span++;
                            }
                        }
                    }
                    else
                    {
                        int m = 0;
                        for (int j = 0; j < delim.Length; j++)
                        {
                            if (str[index + j] == delim[j])
                            {
                                m++;
                            }
                            else
                            {
                                break;
                            }
                        }
                        if (m == delim.Length)
                        {
                            ret = true;
                            match_span = m;
                        }
                    }
                }
            }

            next_start = index + match_span;
            return ret;
        }
    }

    public readonly unsafe struct LineEntity : IEnumerable<ReadOnlyStringEntity>
    {
        private readonly ReadOnlyStringEntity* entity_list_ptr;
        private readonly int start;
        private readonly int len;

        public int Start { get { return this.start; } }
        public int Length { get { return this.len; } }

        public int End { get { return this.Start + this.Length; } }

        public LineEntity(ReadOnlyStringEntity* entity_list_ptr, int start, int len)
        {
            this.entity_list_ptr = entity_list_ptr;
            this.start = start;
            this.len = len;
        }

        public ReadOnlyStringEntity this[int i]
        {
            get
            {
                var entity = *(this.entity_list_ptr + this.Start + i);
                return entity;
            }
        }
        public ReadOnlyStringEntity At(int i)
        {
            if (i < 0 || this.Length <= i)
            {
                throw new IndexOutOfRangeException("index = " + i.ToString() + ", must be in range of [0~" + (this.Length - 1).ToString() + "].");
            }
            return this[i];
        }

        public IEnumerator<ReadOnlyStringEntity> GetEnumerator()
        {
            for (int i = 0; i < this.Length; i++)
                yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    namespace Impl
    {
        public struct LineIndex
        {
            public int Start { get; private set; }
            public int Length { get; private set; }

            public int End { get { return this.Start + this.Length + 1; } }

            public LineIndex(int st, int len)
            {
                this.Start = st;
                this.Length = len;
            }
        }
    }
}