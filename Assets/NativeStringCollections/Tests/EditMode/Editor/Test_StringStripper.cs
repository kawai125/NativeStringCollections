using System.Text;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using NativeStringCollections;
using NativeStringCollections.Utility;

namespace Tests
{
    public class Test_StringStripper
    {
        private string str_source;
        private string ref_Lstrip;
        private string ref_Rstrip;
        private string ref_Strip;

        private NativeList<char> NL_source;
        private NativeList<char> NL_result;

        private StringEntity SE_source;
        private StringEntity SE_result;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            NL_source = new NativeList<char>(Allocator.TempJob);
            NL_result = new NativeList<char>(Allocator.TempJob);
        }
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            NL_source.Dispose();
            NL_result.Dispose();
        }

        // A Test behaves as an ordinary method
        [Test]
        public void StripByCharIsWhiteSpace()
        {
            str_source = "  \t \n something string\tsample \t\t";
            ref_Lstrip = "something string\tsample \t\t";
            ref_Rstrip = "  \t \n something string\tsample";
            ref_Strip = "something string\tsample";

            NL_source.Clear();
            foreach (char c in str_source) NL_source.Add(c);
            SE_source = NL_source.ToStringEntity();

            Debug.Log("  >> try NativeList<char>.Lstrip(result) >>");
            NL_source.Lstrip(NL_result);
            Assert.IsTrue(this.CheckStripperResult(NL_result, ref_Lstrip));

            Debug.Log("  >> try result = StringEntity.Lstrip() >>");
            SE_result = SE_source.Lstrip();
            Assert.IsTrue(this.CheckStripperResult(SE_result, ref_Lstrip));


            Debug.Log("  >> try NativeList<char>.Rstrip(result) >>");
            NL_source.Rstrip(NL_result);
            Assert.IsTrue(this.CheckStripperResult(NL_result, ref_Rstrip));

            Debug.Log("  >> try result = StringEntity.Rstrip() >>");
            SE_result = SE_source.Rstrip();
            Assert.IsTrue(this.CheckStripperResult(SE_result, ref_Rstrip));


            Debug.Log("  >> try NativeList<char>.Strip(result) >>");
            NL_source.Strip(NL_result);
            Assert.IsTrue(this.CheckStripperResult(NL_result, ref_Strip));

            Debug.Log("  >> try result = StringEntity.Strip() >>");
            SE_result = SE_source.Strip();
            Assert.IsTrue(this.CheckStripperResult(SE_result, ref_Strip));
        }
        [Test]
        public void StripBySingleChar()
        {
            str_source = "@@%%@ test\tstring @+<>@@@";
            ref_Lstrip = "%%@ test\tstring @+<>@@@";
            ref_Rstrip = "@@%%@ test\tstring @+<>";
            ref_Strip = "%%@ test\tstring @+<>";

            char c_target = '@';

            NL_source.Clear();
            foreach (char c in str_source) NL_source.Add(c);
            SE_source = NL_source.ToStringEntity();

            Debug.Log("  >> try NativeList<char>.Lstrip(char, result) >>");
            NL_source.Lstrip(c_target, NL_result);
            Assert.IsTrue(this.CheckStripperResult(NL_result, ref_Lstrip));

            Debug.Log("  >> try result = StringEntity.Lstrip(char) >>");
            SE_result = SE_source.Lstrip(c_target);
            Assert.IsTrue(this.CheckStripperResult(SE_result, ref_Lstrip));


            Debug.Log("  >> try NativeList<char>.Rstrip(char, result) >>");
            NL_source.Rstrip(c_target, NL_result);
            Assert.IsTrue(this.CheckStripperResult(NL_result, ref_Rstrip));

            Debug.Log("  >> try result = StringEntity.Rstrip(char) >>");
            SE_result = SE_source.Rstrip(c_target);
            Assert.IsTrue(this.CheckStripperResult(SE_result, ref_Rstrip));


            Debug.Log("  >> try NativeList<char>.Strip(char, result) >>");
            NL_source.Strip(c_target, NL_result);
            Assert.IsTrue(this.CheckStripperResult(NL_result, ref_Strip));

            Debug.Log("  >> try result = StringEntity.Strip(char) >>");
            SE_result = SE_source.Strip(c_target);
            Assert.IsTrue(this.CheckStripperResult(SE_result, ref_Strip));
        }
        [Test]
        public void StripByString()
        {
            str_source = "StripperStripperStr$$#ipper test\tstring Stri@%_<pperStripperStripper";
            ref_Lstrip = "Str$$#ipper test\tstring Stri@%_<pperStripperStripper";
            ref_Rstrip = "StripperStripperStr$$#ipper test\tstring Stri@%_<pper";
            ref_Strip = "Str$$#ipper test\tstring Stri@%_<pper";

            string str_target = "Stripper";

            NL_source.Clear();
            foreach (char c in str_source) NL_source.Add(c);
            SE_source = NL_source.ToStringEntity();

            var NL_target = new NativeList<char>(Allocator.TempJob);
            foreach (char c in str_target) NL_target.Add(c);
            var SE_target = NL_target.ToStringEntity();

            Debug.Log("  >> try NativeList<char>.Lstrip(NativeList<char>, result) >>");
            NL_source.Lstrip(NL_target, NL_result);
            Assert.IsTrue(this.CheckStripperResult(NL_result, ref_Lstrip));

            Debug.Log("  >> try result = StringEntity.Lstrip(NativeList<char>) >>");
            SE_result = SE_source.Lstrip(NL_target);
            Assert.IsTrue(this.CheckStripperResult(SE_result, ref_Lstrip));

            Debug.Log("  >> try result = StringEntity.Lstrip(StringEntity) >>");
            SE_result = SE_source.Lstrip(SE_target);
            Assert.IsTrue(this.CheckStripperResult(SE_result, ref_Lstrip));


            Debug.Log("  >> try NativeList<char>.Rstrip(NativeList<char>, result) >>");
            NL_source.Rstrip(NL_target, NL_result);
            Assert.IsTrue(this.CheckStripperResult(NL_result, ref_Rstrip));

            Debug.Log("  >> try result = StringEntity.Rstrip(NativeList<char>) >>");
            SE_result = SE_source.Rstrip(NL_target);
            Assert.IsTrue(this.CheckStripperResult(SE_result, ref_Rstrip));

            Debug.Log("  >> try result = StringEntity.Rstrip(StringEntity) >>");
            SE_result = SE_source.Rstrip(SE_target);
            Assert.IsTrue(this.CheckStripperResult(SE_result, ref_Rstrip));


            Debug.Log("  >> try NativeList<char>.Strip(NativeList<char>, result) >>");
            NL_source.Strip(NL_target, NL_result);
            Assert.IsTrue(this.CheckStripperResult(NL_result, ref_Strip));

            Debug.Log("  >> try result = StringEntity.Strip(NativeList<char>) >>");
            SE_result = SE_source.Strip(NL_target);
            Assert.IsTrue(this.CheckStripperResult(SE_result, ref_Strip));

            Debug.Log("  >> try result = StringEntity.Strip(StringEntity) >>");
            SE_result = SE_source.Strip(SE_target);
            Assert.IsTrue(this.CheckStripperResult(SE_result, ref_Strip));

            NL_target.Dispose();
        }

        // helper functions
        private unsafe bool CheckStripperResult(NativeList<char> result, string ref_data)
        {
            return this.CheckStripperResultImpl((char*)result.GetUnsafePtr(), result.Length, ref_data);
        }
        private unsafe bool CheckStripperResult(IJaggedArraySliceBase<Char16> result, string ref_data)
        {
            return this.CheckStripperResultImpl((char*)result.GetUnsafePtr(), result.Length, ref_data);
        }
        private unsafe bool CheckStripperResultImpl(char* res_ptr, int res_len, string ref_data)
        {
            var sb = new StringBuilder();

            sb.Append("   -- compare elem [result/ref]={\n");

            bool check = true;

            int max_len = Mathf.Max(res_len, ref_data.Length);
            for (int i = 0; i < max_len; i++)
            {
                char c = res_ptr[i];

                if (i >= res_len || i >= ref_data.Length)
                {
                    check = false;
                    if (i >= ref_data.Length)
                    {
                        sb.Append("   [ " + c + " / ]  -- differ (ref is empty)\n");
                    }
                    else
                    {
                        sb.Append("   [ / " + ref_data[i] + " ]  -- differ (result is empty)\n");
                    }
                }
                else
                {
                    sb.Append("   [ " + c + " / " + ref_data[i] + "]");
                    if (c != ref_data[i])
                    {
                        check = false;
                        sb.Append("   -- differ\n");
                    }
                    else
                    {
                        sb.Append("\n");
                    }
                }
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
