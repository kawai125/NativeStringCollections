using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using Unity.Collections;

using NativeStringCollections;
using NativeStringCollections.Utility;

namespace Tests
{
    public class Test_StringSplitter
    {
        private string str_source;
        private NativeList<char> NL_source;
        private StringEntity SE_source;

        private List<string> ref_list;

        private NativeStringList NSL_result;
        private NativeList<StringEntity> NL_SE_result;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            this.str_source = " 1234567890@@0987654321^^ 1234567\t890 # ";

            this.NL_source = new NativeList<char>(Allocator.TempJob);
            foreach (char c in str_source)
            {
                NL_source.Add(c);
            }
            this.SE_source = NL_source.ToStringEntity();

            this.ref_list = new List<string>();
            this.NSL_result = new NativeStringList(Allocator.TempJob);
            this.NL_SE_result = new NativeList<StringEntity>(Allocator.TempJob);
        }
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            this.NL_source.Dispose();
            this.NSL_result.Dispose();
            this.NL_SE_result.Dispose();
        }

        // A Test behaves as an ordinary method
        [Test]
        public void SplitByCharIsWhiteSpace()
        {
            // split by char.IsWhiteSpace()
            ref_list.Clear();
            ref_list.Add("1234567890@@0987654321^^");
            ref_list.Add("1234567");
            ref_list.Add("890");
            ref_list.Add("#");

            Debug.Log("  >> try NativeList<char>.Split(result) >>");
            NSL_result.Clear();
            NL_source.Split(NSL_result);
            Assert.IsTrue(this.CheckSplitterResult(NSL_result, ref_list));

            Debug.Log("  >> try StringEntity.Split(result) >>");
            NSL_result.Clear();
            SE_source.Split(NSL_result);
            Assert.IsTrue(this.CheckSplitterResult(NSL_result, ref_list));

            Debug.Log("  >> try StringEntity.Split(result) (result: NativeList<StringEntity>) >>");
            NL_SE_result.Clear();
            SE_source.Split(NL_SE_result);
            Assert.IsTrue(this.CheckSplitterResult(NL_SE_result, ref_list));
        }
        [Test]
        public void SplitBySingleChar()
        {
            ref_list.Clear();
            ref_list.Add(" 1234567890");
            ref_list.Add("0987654321^^ 1234567\t890 # ");

            Debug.Log("  >> try NativeList<char>.Split(result) >>");
            NSL_result.Clear();
            NL_source.Split('@', NSL_result);
            Assert.IsTrue(this.CheckSplitterResult(NSL_result, ref_list));

            Debug.Log("  >> try StringEntity.Split(result) >>");
            NSL_result.Clear();
            SE_source.Split('@', NSL_result);
            Assert.IsTrue(this.CheckSplitterResult(NSL_result, ref_list));

            ref_list.Clear();
            ref_list.Add(" 12345");
            ref_list.Add("7890@@0987");
            ref_list.Add("54321^^ 12345");
            ref_list.Add("7\t890 # ");

            Debug.Log("  >> try NativeList<char>.Split(char, result) >>");
            NSL_result.Clear();
            NL_source.Split('6', NSL_result);
            Assert.IsTrue(this.CheckSplitterResult(NSL_result, ref_list));

            Debug.Log("  >> try StringEntity.Split(char, result) >>");
            NSL_result.Clear();
            SE_source.Split('6', NSL_result);
            Assert.IsTrue(this.CheckSplitterResult(NSL_result, ref_list));
        }
        [Test]
        public void SplitByString()
        {
            NativeList<char> NL_delim = new NativeList<char>(Allocator.TempJob);
            string str_delim = "345";
            foreach (char c in str_delim)
            {
                NL_delim.Add(c);
            }

            ref_list.Clear();
            ref_list.Add(" 12");
            ref_list.Add("67890@@0987654321^^ 12");
            ref_list.Add("67\t890 # ");

            Debug.Log("  >> try NativeList<char>.Split(NativeList<char>, result) >>");
            NSL_result.Clear();
            NL_source.Split(NL_delim, NSL_result);
            Assert.IsTrue(this.CheckSplitterResult(NSL_result, ref_list));

            Debug.Log("  >> try StringEntity.Split(NativeList<char>, result) >>");
            NSL_result.Clear();
            SE_source.Split(NL_delim, NSL_result);
            Assert.IsTrue(this.CheckSplitterResult(NSL_result, ref_list));

            NL_delim.Dispose();
        }
        [Test]
        public void SplitByStringSplitter()
        {
            StringSplitter splitter = new StringSplitter(Allocator.TempJob);
            splitter.AddDelim("@@");
            splitter.AddDelim("678");
            splitter.AddDelim("8");
            splitter.AddDelim("987");

            ref_list.Clear();
            ref_list.Add(" 12345");
            ref_list.Add("90");
            ref_list.Add("0");
            ref_list.Add("654321^^ 1234567\t");
            ref_list.Add("90 # ");

            Debug.Log("  >> try StringSplitter.Split(NativeList<char>, result) >>");
            NSL_result.Clear();
            splitter.Split(NL_source, NSL_result);
            Assert.IsTrue(this.CheckSplitterResult(NSL_result, ref_list));

            Debug.Log("  >> try StringSplitter.Split(StringEntity, result) >>");
            NSL_result.Clear();
            splitter.Split(SE_source, NSL_result);
            Assert.IsTrue(this.CheckSplitterResult(NSL_result, ref_list));


            splitter.Dispose();
        }

        // helper f unctions
        private bool CheckSplitterResult(NativeStringList result, List<string> ref_data)
        {
            var sb = new StringBuilder();

            bool check = true;
            if (result.Length != ref_data.Count)
            {
                sb.Append("    !! the element number was differ."
                          + " result: " + result.Length.ToString()
                          + ", ref: " + ref_data.Count.ToString() + "\n");
                check = false;
            }

            int len = Mathf.Max(result.Length, ref_data.Count);

            sb.Append("    elements [result/ref] = {\n");
            for (int i = 0; i < len; i++)
            {
                bool local_check = true;
                if (i < result.Length && i < ref_data.Count)
                {
                    if (result[i] != ref_data[i]) check = local_check = false;
                }

                sb.Append("   [ ");
                if (i < result.Length) sb.Append(result[i]);
                sb.Append(" / ");
                if (i < ref_data.Count) sb.Append(ref_data[i]);
                sb.Append(" ]");
                if (!local_check || i >= result.Length || i >= ref_data.Count) sb.Append("  - differ.");
                sb.Append("\n");
            }
            sb.Append("}\n");

            if (check)
            {
                Debug.Log(sb.ToString());
            }
            else
            {
                Debug.LogWarning(sb.ToString());
            }

            return check;
        }
        private bool CheckSplitterResult<T>(NativeList<T> result, List<string> ref_data) where T : unmanaged, IStringEntityBase, IEquatable<string>
        {
            var sb = new StringBuilder();

            bool check = true;
            if (result.Length != ref_data.Count)
            {
                sb.Append("    !! the element number was differ."
                          + " result: " + result.Length.ToString()
                          + ", ref: " + ref_data.Count.ToString() + "\n");
                check = false;
            }

            int len = Mathf.Max(result.Length, ref_data.Count);

            sb.Append("    elements [result/ref] = {\n");
            for (int i = 0; i < len; i++)
            {
                bool local_check = true;
                if (i < result.Length && i < ref_data.Count)
                {
                    if (!result[i].Equals(ref_data[i])) check = local_check = false;
                }

                sb.Append("   [ ");
                if (i < result.Length) sb.Append(result[i]);
                sb.Append(" / ");
                if (i < ref_data.Count) sb.Append(ref_data[i]);
                sb.Append(" ]");
                if (!local_check || i >= result.Length || i >= ref_data.Count) sb.Append("  - differ.");
                sb.Append("\n");
            }
            sb.Append("}\n");

            if (check)
            {
                Debug.Log(sb.ToString());
            }
            else
            {
                Debug.LogWarning(sb.ToString());
            }

            return check;
        }
    }
}
