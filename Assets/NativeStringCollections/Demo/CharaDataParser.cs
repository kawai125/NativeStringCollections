using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using NativeStringCollections;


namespace NativeStringCollections.Demo
{
    // user defined text parser & data container
    public class CharaDataParser : ITextFileParser, IDisposable
    {
        public NativeList<int> IndexSeq;
        public NativeList<CharaData> Data;
        public int N;
        public int D;
        public float R;

        public int Lines;

        private double _dummy;

        private NativeStringList _name;

        private NativeStringList _tmp_name;
        private NativeList<ReadOnlyStringEntity> _str_list;

        private NativeStringList _mark_list;
        private ReadOnlyStringEntity _mark_comment;
        private ReadOnlyStringEntity _mark_tag, _mark_header, _mark_ext, _mark_ext_end, _mark_body;
        private ReadOnlyStringEntity _mark_n_total, _mark_d, _mark_r;

        private NativeBase64Decoder _b64_decoder;
        private NativeList<byte> _b64_decoded_bytes;

        private ReadMode _read_mode;

        private System.Diagnostics.Stopwatch _timer;

        private bool _allocated;


        public enum ReadMode
        {
            None,
            Header,
            ExtData,
            Body,

            Complete,

            HeaderError,
            Base64DataError,
            FormatError,
            PostProcError,
        }
        public ReadMode ParserState { get { return _read_mode; } }
        
        public void Init()
        {
            Data = new NativeList<CharaData>(Allocator.Persistent);
            IndexSeq = new NativeList<int>(Allocator.Persistent);

            _name = new NativeStringList(Allocator.Persistent);

            _tmp_name = new NativeStringList(Allocator.Persistent);
            _str_list = new NativeList<ReadOnlyStringEntity>(Allocator.Persistent);

            _mark_list = new NativeStringList(Allocator.Persistent);

            _mark_list.Add("#");

            _mark_list.Add("<@MARK>");
            _mark_list.Add("Header");
            _mark_list.Add("ExtData");
            _mark_list.Add("ExtDataEnd");
            _mark_list.Add("Body");

            _mark_list.Add("n_total");
            _mark_list.Add("d");
            _mark_list.Add("r");

            _mark_comment = _mark_list[0];

            _mark_tag = _mark_list[1];
            _mark_header = _mark_list[2];
            _mark_ext = _mark_list[3];
            _mark_ext_end = _mark_list[4];
            _mark_body = _mark_list[5];

            _mark_n_total = _mark_list[6];
            _mark_d = _mark_list[7];
            _mark_r = _mark_list[8];

            _b64_decoder = new NativeBase64Decoder(Allocator.Persistent);
            _b64_decoded_bytes = new NativeList<byte>(Allocator.Persistent);

            _timer = new System.Diagnostics.Stopwatch();
            _timer.Start();

            _allocated = true;

        }
        public void Clear()
        {
            Data.Clear();
            IndexSeq.Clear();
            N = 0;
            D = 0;
            R = 0;

            Lines = 0;

            _name.Clear();
            _tmp_name.Clear();
            _str_list.Clear();

            _b64_decoder.Clear();
            _b64_decoded_bytes.Clear();

            _read_mode = ReadMode.None;
        }
        public bool ParseLine(ReadOnlyStringEntity line)
        {
            this.Lines++;
            if (line.Length < 1) return true;

            line.Split('\t', _str_list);

            //--- check data block
            if (_str_list.Length >= 2 && _str_list[0] == _mark_tag)
            {
                if (_str_list[1] == _mark_header) _read_mode = ReadMode.Header;
                else if (_str_list[1] == _mark_ext) _read_mode = ReadMode.ExtData;
                else if (_str_list[1] == _mark_ext_end) _read_mode = ReadMode.None;
                else if (_str_list[1] == _mark_body)
                {
                    //--- check header info was read correctly or not
                    if (this.N <= 0 || this.D <= 0 || this.R < 0.0 || this.R >= 1.0)
                    {
                        _read_mode = ReadMode.HeaderError;
                        return false;
                    }
                    _read_mode = ReadMode.Body;
                }
                return true;
            }
            if (_read_mode == ReadMode.None) return true;

            //--- ignore comment line
            if (_str_list[0].Length >= 1 && _str_list[0].Slice(0, 1) == _mark_comment)
            {
                return true;
            }

            //--- store data
            if(_read_mode == ReadMode.Header)
            {
                bool success = true;
                if (_str_list[0] == _mark_n_total)
                {
                    success = _str_list[1].TryParse(out int n);
                    this.N = n;
                }
                else if (_str_list[0] == _mark_d)
                {
                    success = _str_list[1].TryParse(out int d);
                    this.D = d;
                }
                else if (_str_list[0] == _mark_r)
                {
                    success = _str_list[1].TryParse(out float r);
                    this.R = r;
                }
                if (!success)
                {
                    _read_mode = ReadMode.HeaderError;
                    return false;
                }
            }
            else if(_read_mode == ReadMode.ExtData)
            {
                if(_str_list.Length > 1)
                {
                    // must be 1 element in line
                    _read_mode = ReadMode.Base64DataError;
                    return false;
                }
                else if(_str_list.Length == 1)
                {
                    _b64_decoder.GetBytes(_b64_decoded_bytes, _str_list[0]);
                }
            }
            else if(_read_mode == ReadMode.Body)
            {
                if (_str_list.Length < 6) return true;

                var tmp = new CharaData();
                bool success = true;
                success = success && _str_list[0].TryParse(out tmp.ID);
                success = success && _str_list[2].TryParse(out tmp.HP);
                success = success && _str_list[3].TryParse(out tmp.MP);
                success = success && _str_list[4].TryParse(out tmp.Attack);
                success = success && _str_list[5].TryParse(out tmp.Defence);

                if (!success)
                {
                    _read_mode = ReadMode.FormatError;
                    return false;
                }

                _tmp_name.Add(_str_list[1]);
                tmp.Name = _tmp_name.Last;

                Data.Add(tmp);

                if(Data.Length > this.N)
                {
                    _read_mode = ReadMode.FormatError;
                    return false;
                }
            }

            return true;
        }
        public unsafe void PostReadProc()
        {
            //--- check reading was success or not
            if (_read_mode != ReadMode.Body) return;

            //--- store Base64 ext data
            int index_len = _b64_decoded_bytes.Length / UnsafeUtility.SizeOf<int>();
            if(_b64_decoded_bytes.Length % UnsafeUtility.SizeOf<int>() != 0)
            {
                _read_mode = ReadMode.Base64DataError;
                return;
            }
            IndexSeq.ResizeUninitialized(index_len);
            UnsafeUtility.MemCpy(IndexSeq.GetUnsafePtr(), _b64_decoded_bytes.GetUnsafePtr(), _b64_decoded_bytes.Length);
            _b64_decoded_bytes.Clear();

            //--- check Base64 ext data
            if(IndexSeq.Length != Data.Length)
            {
                _read_mode = ReadMode.PostProcError;

            //    var sb = new System.Text.StringBuilder();
            //    sb.Append("ERROR in PostReadProc().\n");
            //    sb.Append($"IndexSeq.Length = {IndexSeq.Length}\n");
            //    sb.Append($"Data.Length     = {Data.Length}\n");
            //    UnityEngine.Debug.LogError(sb.ToString());

                return;
            }
            for(int i=0; i<IndexSeq.Length; i++)
            {
                if(IndexSeq[i] != Data[i].ID)
                {
                    _read_mode = ReadMode.PostProcError;

                //    var sb = new System.Text.StringBuilder();
                //    sb.Append("ERROR in PostReadProc().\n");
                //    for(int j=0; j<IndexSeq.Length; j++)
                //    {
                //        sb.Append($"  Elems[{j}]: [IndexSeq[i] / Data[i].ID] = [{IndexSeq[j]} / {Data[j].ID}]");
                //        if (IndexSeq[j] != Data[j].ID) sb.Append(" -- differ.");
                //        sb.Append('\n');
                //    }
                //    UnityEngine.Debug.LogError(sb.ToString());

                    return;
                }
            }

            //--- sort read data
            Data.Sort();

            _name.Clear();
            foreach(var tmp in Data)
            {
                _name.Add(tmp.Name);
            }
            for(int i=0; i<Data.Length; i++)
            {
                //--- overwright CharaData.Name StringEntity to _name (sorted) from _tmp_name (random).
                var tmp = Data[i];
                tmp.Name = _name[i];
                Data[i] = tmp;
            }

            _read_mode = ReadMode.Complete;
        }
        public void UnLoad()
        {
            this.Clear();
        }
        public void Dispose()
        {
            if (_allocated)
            {
                Data.Dispose();
                IndexSeq.Dispose();

                _name.Dispose();

                _str_list.Dispose();
                _tmp_name.Dispose();
                _mark_list.Dispose();

                _b64_decoder.Dispose();
                _b64_decoded_bytes.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        ~CharaDataParser()
        {
            this.Dispose();
        }
    }
}

