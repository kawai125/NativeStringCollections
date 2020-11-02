using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;

using TMPro;

using NativeStringCollections;


namespace NativeStringCollections.Test
{
    public class Test_NativeTextStreamReader : MonoBehaviour
    {
        private string SampleDataPath;


        [SerializeField]
        public string TestSeq = "";
        [SerializeField]
        public string LineFactor = "\n";  // set "\n", "\r", or "\r\n"

        private List<Encoding> _encodings;
        private int _tgtEncoder;

        [SerializeField]
        public TMP_Dropdown _dropdownEncoding;

        private NativeList<char> _readData;


        public int GetEncoderNum() { return _encodings.Count; }
        public void SelectEncoder(int index)
        {
            if (0 < index && index < _encodings.Count)
            {
                _tgtEncoder = index;
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }
        }


        // Start is called before the first frame update
        void Start()
        {
            SampleDataPath = Application.dataPath + "/../sample_short.csv";

            // set default test sequence
            // ref: https://pierre3.hatenablog.com/entry/2014/04/07/000222
            var sb = new StringBuilder();
            sb.Append("No., Name, Price, Comment");
            sb.Append(LineFactor);
            sb.Append("001, りんご, 98円, 青森産");
            sb.Append(LineFactor);
            sb.Append("002, バナナ, 120円, \"    とっても!");
            sb.Append(LineFactor);
            sb.Append("お,い,し,い,よ!\"");
            sb.Append(LineFactor);
            //sb.Append("");
            sb.Append(LineFactor);
            sb.Append("004, \"うまい棒\"\"めんたい\"\"\", 10円,");
            sb.Append(LineFactor);
            sb.Append("005, バナメイ海老, 800円, \"300ｇ");
            sb.Append(LineFactor);
            //sb.Append("");
            sb.Append(LineFactor);
            sb.Append("エビチリに\"");
            sb.Append(LineFactor);
            //sb.Append("");
            sb.Append(LineFactor);

            TestSeq = sb.ToString();

            // test encodings
            _encodings = new List<Encoding>();
            _encodings.Add(Encoding.UTF8);
            _encodings.Add(Encoding.UTF32);
            _encodings.Add(Encoding.Unicode);
            //_encodings.Add(Encoding.ASCII);  // cannot decode japanese test.

            _encodings.Add(Encoding.GetEncoding("shift_jis"));
            _encodings.Add(Encoding.GetEncoding("euc-jp"));
            _encodings.Add(Encoding.GetEncoding("iso-2022-jp"));

            if (_dropdownEncoding)
            {
                _dropdownEncoding.ClearOptions();
                var drop_menu = new List<string>();

                drop_menu.Add("UTF8");
                drop_menu.Add("UTF32");
                drop_menu.Add("Unicode");
                //drop_menu.Add("ASCII");

                drop_menu.Add("shift_jis");
                drop_menu.Add("euc-jp");
                drop_menu.Add("iso-2022-jp");

                _dropdownEncoding.AddOptions(drop_menu);
                _dropdownEncoding.value = 0;
            }

            _tgtEncoder = 0;

            _readData = new NativeList<char>(0, Allocator.Persistent);
        }
        private void OnDestroy()
        {
            _readData.Dispose();
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void OnClickIOTest()
        {
            _tgtEncoder = _dropdownEncoding.value;

            Debug.Log("=== Text file IO test ===");
            Debug.Log("  Encoding = " + _encodings[_tgtEncoder].ToString());

            // write sequence
            using (StreamWriter writer = new StreamWriter(SampleDataPath, false, _encodings[_tgtEncoder]))
            {
                writer.Write(TestSeq);
            }

            // read sequence
            using (NativeTextStreamReader reader = new NativeTextStreamReader(Allocator.TempJob))
            {
                var comp_report = new List<string>();
                bool check = true;

                var sb = new StringBuilder();
                int call_count = 0;

                // by ReadToEnd()
                Debug.Log("  >> ReadToEnd() test >>");
                _readData.Clear();
                reader.Init(SampleDataPath, _encodings[_tgtEncoder]);
                reader.ReadToEnd(_readData);

                //--- check result
                check = check && this.EqualString(TestSeq, _readData, comp_report);
                if (comp_report.Count > 0)
                {
                    for (int i = 0; i < comp_report.Count; i++)
                    {
                        Debug.LogWarning("  !! " + comp_report[i]);
                    }
                }

                // by ReadBuffer()
                Debug.Log("  >> ReadBuffer() test >>");
                _readData.Clear();
                reader.Init(SampleDataPath, _encodings[_tgtEncoder]);
                call_count = 0;
                while (!reader.EndOfStream)
                {
                    reader.ReadBuffer(_readData);

                    sb.Clear();
                    sb.Append("    call count = " + call_count.ToString() + "\n");
                    sb.Append("    _readData = [\n");
                    foreach (char c in _readData)
                    {
                        sb.Append(" " + c);
                    }
                    sb.Append(" ]");
                    Debug.Log(sb.ToString());

                    call_count++;
                }

                //--- check result
                check = check && this.EqualString(TestSeq, _readData, comp_report);
                if (comp_report.Count > 0)
                {
                    for (int i = 0; i < comp_report.Count; i++)
                    {
                        Debug.LogWarning("  !! " + comp_report[i]);
                    }
                }

                // by ReadLine()
                Debug.Log("  >> ReadLine() test >>");
                _readData.Clear();
                reader.Init(SampleDataPath, _encodings[_tgtEncoder]);  // reusable internal buffer by calling Init().
                call_count = 0;
                while (!reader.EndOfStream)
                {
                    reader.ReadLine(_readData);
                    for (int i = 0; i < LineFactor.Length; i++)
                    {
                        _readData.Add(LineFactor[i]);
                    }

                    sb.Clear();
                    sb.Append("    call count = " + call_count.ToString() + "\n");
                    sb.Append("    _readData = [\n");
                    foreach(char c in _readData)
                    {
                        sb.Append(" " + c);
                    }
                    sb.Append(" ]");
                    Debug.Log(sb.ToString());

                    call_count++;
                }

                //--- check result
                check = check && this.EqualString(TestSeq, _readData, comp_report);
                if (comp_report.Count > 0)
                {
                    for (int i = 0; i < comp_report.Count; i++)
                    {
                        Debug.LogWarning("  !! " + comp_report[i]);
                    }
                }

                if (check)
                {
                    Debug.Log("=== test passed ===");
                }
                else
                {
                    Debug.Log("!!! test failure !!!");
                }
            }
        }

        private bool EqualString(string ref_data, NativeList<char> data, List<string> log)
        {
            log.Clear();
            var sb = new StringBuilder();

            if (ref_data.Length != data.Length)
            {
                log.Add("  !! data length was wrong. [ref/test]=[" + ref_data.Length + "/" + data.Length + "]");
                sb.Clear();
                sb.Append("      ref=[\n");
                foreach (char c in ref_data)
                {
                    sb.Append(c);
                }
                sb.Append("]\n");
                sb.Append("      data=[\n");
                foreach (char c in data)
                {
                    sb.Append(c);
                }
                sb.Append("]\n");
                sb.Append("  code: [ref/data]=  ('-1' means empty.)\n");
                int len = Math.Max(ref_data.Length, data.Length);
                for(int i=0; i<len; i++)
                {
                    int c_ref = -1, c_d = -1;
                    if (i < ref_data.Length) c_ref = ref_data[i];
                    if (i < data.Length) c_d = data[i];
                    sb.Append("  [" + ((int)c_ref).ToString() + "/" + ((int)c_d).ToString() + "]\n");
                }
                log.Add(sb.ToString());
                return false;
            }

            for (int i = 0; i < ref_data.Length; i++)
            {
                if (ref_data[i] != data[i])
                {
                    sb.Clear();
                    sb.Append("  !! data[" + i.ToString() + "] was difer.");
                    sb.Append(". [ref/test]=[" + ref_data[i] + "/" + data[i] + "]");
                    sb.Append(", code=[" + ((int)ref_data[i]).ToString() + "/" + ((int)data[i]).ToString() + "]");
                    log.Add(sb.ToString());
                }
            }

            if (log.Count > 0) return false;
            return true;
        }
    }
}


