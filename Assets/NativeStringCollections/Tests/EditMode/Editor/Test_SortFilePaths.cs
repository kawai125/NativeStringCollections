using System.Text;
using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;
using UnityEngine;

using NativeStringCollections;

namespace Tests
{
    public class Test_SortFilePaths
    {
        string[] source_paths = { "file_8_sample_011.dat",
                                  "file_012_sample_11.dat",
                                  "file_008_sample_2.dat",
                                  "file_44_sample_11.dat",
                                  "data777_pattern2.csv",
                                  "data00777_pattern2.csv",
                                  "data777_pattern2.csvext",
                                  "data777_pattern2.csv2",
                                  "02file_10_sample_00.dat",
                                  "02file_4_sample_00.dat" };

        [Test]
        public void CheckSortNatural()
        {
            var result = new List<string>();
            FilePathUtility.Sort(source_paths, result);

            var sb = new StringBuilder();
            sb.Append("sorted path = [\n");
            foreach(var path in result)
            {
                sb.Append($"  {path}\n");
            }
            sb.Append("]\n");
            Debug.Log(sb);

            Assert.IsTrue(CheckEquals(result,
                new string[] { "02file_4_sample_00.dat",
                               "02file_10_sample_00.dat",
                               "data777_pattern2.csv" ,
                               "data00777_pattern2.csv",
                               "data777_pattern2.csv2",
                               "data777_pattern2.csvext",
                               "file_008_sample_2.dat" ,
                               "file_8_sample_011.dat",
                               "file_012_sample_11.dat",
                               "file_44_sample_11.dat" }));
        }

        [Test]
        public void CheckSortWithFiltering()
        {
            // digits block is the place holder.
            CheckSortWithFilter("file_00_sample_00.dat",
                new string[] { "file_008_sample_2.dat" ,
                               "file_8_sample_011.dat",
                               "file_012_sample_11.dat",
                               "file_44_sample_11.dat" });
            CheckSortWithFilter("data77_pattern77.csv",
                new string[] { "data777_pattern2.csv" ,
                               "data00777_pattern2.csv" });
            CheckSortWithFilter("22file_22_sample_22.dat",
                new string[] { "02file_4_sample_00.dat",
                               "02file_10_sample_00.dat" });
        }
        private void CheckSortWithFilter(string filter, string[] reference)
        {
            var result = new List<string>();
            FilePathUtility.Sort(source_paths, filter, result);

            var sb = new StringBuilder();
            sb.Append($"filter = {filter}\n");
            sb.Append("sorted path = [\n");
            foreach (var path in result)
            {
                sb.Append($"  {path}\n");
            }
            sb.Append("]\n");
            Debug.Log(sb);

            Assert.IsTrue(CheckEquals(result, reference));
        }

        private bool CheckEquals(List<string> target, string[] reference)
        {
            if (target.Count != reference.Length) return false;

            for(int i=0; i<target.Count; i++)
            {
                if (target[i] != reference[i]) return false;
            }

            return true;
        }
    }
}
