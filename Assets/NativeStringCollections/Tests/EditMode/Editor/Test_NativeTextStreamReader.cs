using System;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using Unity.Collections;

using NativeStringCollections;

namespace Tests
{
    public class Test_NativeTextStreamReader
    {
        private string SampleDataPath;

        private string TestSeq = "";
        private string LineFactor = "\n";  // set "\n", "\r", or "\r\n"

        private NativeList<char> _readData;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            SampleDataPath = Application.dataPath + "/../Assets/NativeStringCollections/Tests/sample_short.csv";

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

            _readData = new NativeList<char>(0, Allocator.Persistent);
        }
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _readData.Dispose();
        }

        // A Test behaves as an ordinary method
        [Test]
        public void FileReadTest_UTF8()
        {
            this.FileReadTestImpl(Encoding.UTF8);
        }
        [Test]
        public void FileReadTest_UTF32()
        {
            this.FileReadTestImpl(Encoding.UTF32);
        }
        [Test]
        public void FileReadTest_Unicode()
        {
            this.FileReadTestImpl(Encoding.Unicode);
        }
        [Test]
        public void FileReadTest_ShiftJIS()
        {
            this.FileReadTestImpl(Encoding.GetEncoding("shift_jis"));
        }
        [Test]
        public void FileReadTest_EUC_JP()
        {
            this.FileReadTestImpl(Encoding.GetEncoding("euc-jp"));
        }
        [Test]
        public void FileReadTest_ISO2022JP()
        {
            this.FileReadTestImpl(Encoding.GetEncoding("iso-2022-jp"));
        }

        private void FileReadTestImpl(Encoding encoding)
        {
            // write sequence
            using (StreamWriter writer = new StreamWriter(SampleDataPath, false, encoding))
            {
                writer.Write(TestSeq);
            }

            // read sequence
            using (NativeTextStreamReader reader = new NativeTextStreamReader(Allocator.TempJob))
            {
                var comp_report = new List<string>();

                var sb = new StringBuilder();
                int call_count = 0;

                // by ReadToEnd()
                Debug.Log("  >> ReadToEnd() test >>");
                _readData.Clear();
                reader.Init(SampleDataPath, encoding);
                reader.ReadToEnd(_readData);

                //--- check result
                Assert.IsTrue(this.EqualString(TestSeq, _readData, comp_report));
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
                reader.Init(SampleDataPath, encoding);
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
                Assert.IsTrue(this.EqualString(TestSeq, _readData, comp_report));
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
                reader.Init(SampleDataPath, encoding);  // reusable internal buffer by calling Init().
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
                    foreach (char c in _readData)
                    {
                        sb.Append(" " + c);
                    }
                    sb.Append(" ]");
                    Debug.Log(sb.ToString());

                    call_count++;
                }

                //--- check result
                Assert.IsTrue(this.EqualString(TestSeq, _readData, comp_report));
                if (comp_report.Count > 0)
                {
                    for (int i = 0; i < comp_report.Count; i++)
                    {
                        Debug.LogWarning("  !! " + comp_report[i]);
                    }
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
                for (int i = 0; i < len; i++)
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
