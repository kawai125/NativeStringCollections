using System;
using System.Collections.Generic;
using UnityEngine;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

using NativeStringCollections;
using NativeStringCollections.Utility;

namespace NativeStringCollections.Demo
{
    // user defined text parser & data container
    public class CharaDataParser : ITextFileParser, IDisposable
    {
        public NativeList<int> IndexSeq;

        // this data packing makes easier to pass data between (class CharaDataParser) <-> (Burst compiled func).
        public struct DataPack
        {
            public int Lines;

            public int N, D;
            public float R;

            public ReadMode read_mode;

            public ReadOnlyStringEntity mark_tag,
                                        mark_comment,
                                        mark_header,
                                        mark_body,
                                        mark_ext,
                                        mark_ext_end;
            public ReadOnlyStringEntity mark_n_total,
                                        mark_d,
                                        mark_r;

            // unsafe references to pass container into Burst
            public UnsafeRefToNativeList<ReadOnlyStringEntity> str_list;

            public UnsafeRefToNativeBase64Decoder b64_decoder;
            public UnsafeRefToNativeList<byte> b64_decoded_bytes;

            public UnsafeRefToNativeStringList tmp_name;
            public UnsafeRefToNativeStringList name;

            public UnsafeRefToNativeList<int> IndexSeq;
            public UnsafeRefToNativeList<CharaData> Data;
        }
        private DataPack pack;

        public int Lines { get { return pack.Lines; } }
        public int N { get { return pack.N; } }
        public int D { get { return pack.D; } }
        public float R { get { return pack.R; } }

        public NativeList<CharaData> Data;

        public bool EnableBurst;
        public bool EnableBurstFunc;  // use BurstFunc.TryParse() or not.

        private int _data_size;

        private NativeStringList _name;

        private NativeStringList _tmp_name;
        private NativeList<ReadOnlyStringEntity> _str_list;

        private NativeStringList _mark_list;

        private NativeBase64Decoder _b64_decoder;
        private NativeList<byte> _b64_decoded_bytes;

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
        public ReadMode ParserState { get { return pack.read_mode; } }
        
        public void Init()
        {
            EnableBurst = true;
            EnableBurstFunc = true;

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

            pack.mark_comment = _mark_list[0];

            pack.mark_tag = _mark_list[1];
            pack.mark_header = _mark_list[2];
            pack.mark_ext = _mark_list[3];
            pack.mark_ext_end = _mark_list[4];
            pack.mark_body = _mark_list[5];

            pack.mark_n_total = _mark_list[6];
            pack.mark_d = _mark_list[7];
            pack.mark_r = _mark_list[8];

            _b64_decoder = new NativeBase64Decoder(Allocator.Persistent);
            _b64_decoded_bytes = new NativeList<byte>(Allocator.Persistent);

            // initialize references
            pack.str_list = _str_list.GetUnsafeRef();
            pack.b64_decoder = _b64_decoder.GetUnsafeRef();
            pack.b64_decoded_bytes = _b64_decoded_bytes.GetUnsafeRef();
            pack.tmp_name = _tmp_name.GetUnsafeRef();
            pack.IndexSeq = IndexSeq.GetUnsafeRef();
            pack.Data = Data.GetUnsafeRef();
            pack.name = _name.GetUnsafeRef();

            _data_size = 0;

            _timer = new System.Diagnostics.Stopwatch();
            _timer.Start();

            _allocated = true;

        }
        public void Clear()
        {
            Data.Clear();
            IndexSeq.Clear();
            pack.N = 0;
            pack.D = 0;
            pack.R = 0;

            pack.Lines = 0;

            _name.Clear();
            _tmp_name.Clear();
            _str_list.Clear();

            _b64_decoder.Clear();
            _b64_decoded_bytes.Clear();

            // pre-reallocating for after 2nd loadings
            if(_data_size > 0)
            {
                Data.Capacity = _data_size;
                IndexSeq.Capacity = _data_size;

                _name.Capacity = 8 * _data_size;              // estimate 8 charactors for name of 1 CharaData
                _tmp_name.Capacity = 8 * _data_size;
                _name.IndexCapacity = _data_size;
                _tmp_name.Capacity = _data_size;

                _b64_decoded_bytes.Capacity = 4 * _data_size; // convrt to Int32 array
            }

            pack.read_mode = ReadMode.None;
        }
        public bool ParseLines(NativeStringList lines)
        {
            if (EnableBurst)
            {
                var lines_input = lines.GetUnsafeRef();
                ParserFuncBurst.ParseLines(ref pack, ref lines_input, out bool success);
                return success;
            }
            else
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    bool success = ParseLineImpl(lines[i]);
                    if (!success) return false;
                }
            }
            return true;
        }
        private bool ParseLineImpl(ReadOnlyStringEntity line)
        {
            pack.Lines++;
            if (line.Length < 1) return true;

            if (EnableBurstFunc)
            {
                BurstFunc.Split(line, '\t', _str_list);
            }
            else
            {
                line.Split('\t', _str_list);
            }

            //--- check data block
            if (_str_list.Length >= 2 && _str_list[0] == pack.mark_tag)
            {
                if (_str_list[1] == pack.mark_header) pack.read_mode = ReadMode.Header;
                else if (_str_list[1] == pack.mark_ext) pack.read_mode = ReadMode.ExtData;
                else if (_str_list[1] == pack.mark_ext_end) pack.read_mode = ReadMode.None;
                else if (_str_list[1] == pack.mark_body)
                {
                    //--- check header info was read correctly or not
                    if (pack.N <= 0 || pack.D <= 0 || pack.R < 0.0 || pack.R >= 1.0)
                    {
                        pack.read_mode = ReadMode.HeaderError;
                        return false;
                    }
                    pack.read_mode = ReadMode.Body;
                }
                return true;
            }
            if (pack.read_mode == ReadMode.None) return true;

            //--- ignore comment line
            if (_str_list[0].Length >= 1 && _str_list[0].Slice(0, 1) == pack.mark_comment)
            {
                return true;
            }

            //--- store data
            if(pack.read_mode == ReadMode.Header)
            {
                bool success = true;
                if (EnableBurstFunc)
                {
                    // using BurstCompiler applied TryParse
                    if (_str_list[0] == pack.mark_n_total) { success = BurstFunc.TryParse(_str_list[1], out pack.N); }
                    else if (_str_list[0] == pack.mark_d) { success = BurstFunc.TryParse(_str_list[1], out pack.D); }
                    else if (_str_list[0] == pack.mark_r) { success = BurstFunc.TryParse(_str_list[1], out pack.R); }
                }
                else
                {
                    // using normal TryParse()
                    if (_str_list[0] == pack.mark_n_total) { success = _str_list[1].TryParse(out pack.N); }
                    else if (_str_list[0] == pack.mark_d) { success = _str_list[1].TryParse(out pack.D); }
                    else if (_str_list[0] == pack.mark_r) { success = _str_list[1].TryParse(out pack.R); }
                }
                
                if (!success)
                {
                    pack.read_mode = ReadMode.HeaderError;
                    return false;
                }
            }
            else if(pack.read_mode == ReadMode.ExtData)
            {
                if(_str_list.Length > 1)
                {
                    // must be 1 element in line
                    pack.read_mode = ReadMode.Base64DataError;
                    return false;
                }
                else if(_str_list.Length == 1)
                {
                    bool success = true;
                    if (EnableBurst)
                    {
                        // using BurstCompiler applied GetChars()
                        success = BurstFunc.GetBytes(_b64_decoder, _b64_decoded_bytes, _str_list[0]);
                    }
                    else
                    {
                        // using normal GetChars()
                        success = _b64_decoder.GetBytes(_b64_decoded_bytes, _str_list[0]);
                    }
                    
                    if (!success)
                    {
                        pack.read_mode = ReadMode.Base64DataError;
                        return false;
                    }
                }
            }
            else if(pack.read_mode == ReadMode.Body)
            {
                if (_str_list.Length < 6) return true;

                var tmp = new CharaData();
                bool success = true;
                if (EnableBurstFunc)
                {
                    // using BurstCompiler applied TryParse
                    success = success && BurstFunc.TryParse(_str_list[0], out tmp.ID);
                    success = success && BurstFunc.TryParse(_str_list[2], out tmp.HP);
                    success = success && BurstFunc.TryParse(_str_list[3], out tmp.MP);
                    success = success && BurstFunc.TryParse(_str_list[4], out tmp.Attack);
                    success = success && BurstFunc.TryParse(_str_list[5], out tmp.Defence);
                }
                else
                {
                    // using normal TryParse()
                    success = success && _str_list[0].TryParse(out tmp.ID);
                    success = success && _str_list[2].TryParse(out tmp.HP);
                    success = success && _str_list[3].TryParse(out tmp.MP);
                    success = success && _str_list[4].TryParse(out tmp.Attack);
                    success = success && _str_list[5].TryParse(out tmp.Defence);
                }

                if (!success)
                {
                    pack.read_mode = ReadMode.FormatError;
                    return false;
                }

                _tmp_name.Add(_str_list[1]);
                tmp.Name = _tmp_name.Last;

                Data.Add(tmp);

                if(Data.Length > pack.N)
                {
                    pack.read_mode = ReadMode.FormatError;
                    return false;
                }
            }

            return true;
        }

        public unsafe void PostReadProc()
        {
            // switch to Burst version PostReadProc()
            if (EnableBurst)
            {
                ParserFuncBurst.PostReadProc(ref pack);
                return;
            }

            // below is normal implementation of PostReadProc()



            //--- check reading was success or not
            if (pack.read_mode != ReadMode.Body) return;

            //--- store Base64 ext data
            int index_len = _b64_decoded_bytes.Length / UnsafeUtility.SizeOf<int>();
            if(_b64_decoded_bytes.Length % UnsafeUtility.SizeOf<int>() != 0)
            {
                pack.read_mode = ReadMode.Base64DataError;
                return;
            }
            IndexSeq.ResizeUninitialized(index_len);
            UnsafeUtility.MemCpy(IndexSeq.GetUnsafePtr(), _b64_decoded_bytes.GetUnsafePtr(), _b64_decoded_bytes.Length);
            _b64_decoded_bytes.Clear();

            //--- check Base64 ext data
            if(IndexSeq.Length != Data.Length)
            {
                pack.read_mode = ReadMode.PostProcError;

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
                    pack.read_mode = ReadMode.PostProcError;

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
            //--- overwright CharaData.Name StringEntity to _name from _tmp_name.
            //    (the reference of CharaData.Name maybe became invalid because of reallocating in _tmp.)
            _name.Clear();
            for(int i=0; i<_tmp_name.Length; i++)
            {
                _name.Add( _tmp_name[i]);
                var chara = Data[i];
                chara.Name = _name.Last;
                Data[i] = chara;
            }

            pack.read_mode = ReadMode.Complete;
            return;
        }
        public void UnLoad()
        {
            _data_size = Data.Length;
            this.Clear();

            //--- shrink data buffers
            Data.Capacity = 8;
            IndexSeq.Capacity = 8;

            _name.Capacity = 8;
            _name.IndexCapacity = 4;
            _tmp_name.Capacity = 8;
            _tmp_name.IndexCapacity = 4;
            _b64_decoded_bytes.Capacity = 8;
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

                _allocated = false;
            }
        }

        ~CharaDataParser()
        {
            this.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    [BurstCompile]
    public unsafe static class ParserFuncBurst
    {

        private delegate void ParseLinesDelegate(ref CharaDataParser.DataPack pack,
                                                 ref UnsafeRefToNativeStringList lines,
                                                 out bool success);
        private static ParseLinesDelegate _parseLinesDelegate;

        private delegate void PostReadProcDelegate(ref CharaDataParser.DataPack pack);
        private static PostReadProcDelegate _postReadProcDelegate;

        [RuntimeInitializeOnLoadMethod]
        public static void Initialize()
        {
            _parseLinesDelegate = BurstCompiler.
                CompileFunctionPointer<ParseLinesDelegate>(ParseLinesBurstEntry).Invoke;

            _postReadProcDelegate = BurstCompiler.
                CompileFunctionPointer<PostReadProcDelegate>(PostReadProcBurstEntry).Invoke;
        }

        public static void ParseLines(ref CharaDataParser.DataPack pack,
                                      ref UnsafeRefToNativeStringList lines,
                                      out bool success)
        {
            _parseLinesDelegate(ref pack, ref lines, out success);
        }

        public static void PostReadProc(ref CharaDataParser.DataPack pack)
        {
            _postReadProcDelegate(ref pack);
        }

        // entry point to BurstCompile func
        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(ParseLinesDelegate))]
        private static void ParseLinesBurstEntry(ref CharaDataParser.DataPack pack,
                                                 ref UnsafeRefToNativeStringList lines,
                                                 out bool success)
        {
            success = true;
            for(int i=0; i<lines.Length; i++)
            {
                success = ParseLineImpl(ref pack, lines[i]);
                if (!success) return;
            }
        }
        private static bool ParseLineImpl(ref CharaDataParser.DataPack pack, ReadOnlyStringEntity line)
        {
            pack.Lines++;
            if (line.Length < 1) return true;

            line.Split('\t', pack.str_list);

            //--- check data block
            if (pack.str_list.Length >= 2 && pack.str_list[0] == pack.mark_tag)
            {
                if (pack.str_list[1] == pack.mark_header) pack.read_mode = CharaDataParser.ReadMode.Header;
                else if (pack.str_list[1] == pack.mark_ext) pack.read_mode = CharaDataParser.ReadMode.ExtData;
                else if (pack.str_list[1] == pack.mark_ext_end) pack.read_mode = CharaDataParser.ReadMode.None;
                else if (pack.str_list[1] == pack.mark_body)
                {
                    //--- check header info was read correctly or not
                    if (pack.N <= 0 || pack.D <= 0 || pack.R < 0.0 || pack.R >= 1.0)
                    {
                        pack.read_mode = CharaDataParser.ReadMode.HeaderError;
                        return false;
                    }
                    pack.read_mode = CharaDataParser.ReadMode.Body;
                }
                return true;
            }
            if (pack.read_mode == CharaDataParser.ReadMode.None) return true;

            //--- ignore comment line
            if (pack.str_list[0].Length >= 1 && pack.str_list[0].Slice(0, 1) == pack.mark_comment)
            {
                return true;
            }

            //--- store data
            if (pack.read_mode == CharaDataParser.ReadMode.Header)
            {
                bool success = true;
                // using normal TryParse() for large scope optimization of BurstCompile
                if (pack.str_list[0] == pack.mark_n_total) { success = pack.str_list[1].TryParse(out pack.N); }
                else if (pack.str_list[0] == pack.mark_d) { success = pack.str_list[1].TryParse(out pack.D); }
                else if (pack.str_list[0] == pack.mark_r) { success = pack.str_list[1].TryParse(out pack.R); }

                if (!success)
                {
                    pack.read_mode = CharaDataParser.ReadMode.HeaderError;
                    return false;
                }
            }
            else if (pack.read_mode == CharaDataParser.ReadMode.ExtData)
            {
                if (pack.str_list.Length > 1)
                {
                    // must be 1 element in line
                    pack.read_mode = CharaDataParser.ReadMode.Base64DataError;
                    return false;
                }
                else if (pack.str_list.Length == 1)
                {
                    bool success = pack.b64_decoder.GetBytes(pack.b64_decoded_bytes, pack.str_list[0]);
                    if (!success)
                    {
                        pack.read_mode = CharaDataParser.ReadMode.Base64DataError;
                        return false;
                    }
                }
            }
            else if (pack.read_mode == CharaDataParser.ReadMode.Body)
            {
                if (pack.str_list.Length < 6) return true;

                var tmp = new CharaData();
                bool success = true;
                // using normal TryParse() for large scope optimization of BurstCompile
                success = success && pack.str_list[0].TryParse(out tmp.ID);
                success = success && pack.str_list[2].TryParse(out tmp.HP);
                success = success && pack.str_list[3].TryParse(out tmp.MP);
                success = success && pack.str_list[4].TryParse(out tmp.Attack);
                success = success && pack.str_list[5].TryParse(out tmp.Defence);

                if (!success)
                {
                    pack.read_mode = CharaDataParser.ReadMode.FormatError;
                    return false;
                }

                pack.tmp_name.Add(pack.str_list[1]);
                tmp.Name = pack.tmp_name.Last;

                pack.Data.Add(tmp);

                if (pack.Data.Length > pack.N)
                {
                    pack.read_mode = CharaDataParser.ReadMode.FormatError;
                    return false;
                }
            }

            return true;
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(PostReadProcDelegate))]
        private static void PostReadProcBurstEntry(ref CharaDataParser.DataPack pack)
        {
            //--- check reading was success or not
            if (pack.read_mode != CharaDataParser.ReadMode.Body) return;

            //--- store Base64 ext data
            int index_len = pack.b64_decoded_bytes.Length / UnsafeUtility.SizeOf<int>();
            if (pack.b64_decoded_bytes.Length % UnsafeUtility.SizeOf<int>() != 0)
            {
                pack.read_mode = CharaDataParser.ReadMode.Base64DataError;
                return;
            }
            pack.IndexSeq.ResizeUninitialized(index_len);
            UnsafeUtility.MemCpy(pack.IndexSeq.GetUnsafePtr(),
                                 pack.b64_decoded_bytes.GetUnsafePtr(),
                                 pack.b64_decoded_bytes.Length);
            pack.b64_decoded_bytes.Clear();

            //--- check Base64 ext data
            if (pack.IndexSeq.Length != pack.Data.Length)
            {
                pack.read_mode = CharaDataParser.ReadMode.PostProcError;

                //    var sb = new System.Text.StringBuilder();
                //    sb.Append("ERROR in PostReadProc().\n");
                //    sb.Append($"IndexSeq.Length = {IndexSeq.Length}\n");
                //    sb.Append($"Data.Length     = {Data.Length}\n");
                //    UnityEngine.Debug.LogError(sb.ToString());

                return;
            }
            for (int i = 0; i < pack.IndexSeq.Length; i++)
            {
                if (pack.IndexSeq[i] != pack.Data[i].ID)
                {
                    pack.read_mode = CharaDataParser.ReadMode.PostProcError;

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
            //--- overwright CharaData.Name StringEntity to _name from _tmp_name.
            //    (the reference of CharaData.Name maybe became invalid because of reallocating in _tmp.)
            pack.name.Clear();
            for (int i = 0; i < pack.tmp_name.Length; i++)
            {
                pack.name.Add(pack.tmp_name[i]);
                var chara = pack.Data[i];
                chara.Name = pack.name.Last;
                pack.Data[i] = chara;
            }

            pack.read_mode = CharaDataParser.ReadMode.Complete;
            return;
        }
    }
}

