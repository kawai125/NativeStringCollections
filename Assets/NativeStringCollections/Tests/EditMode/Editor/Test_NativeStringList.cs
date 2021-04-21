using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

using UnityEngine;
using UnityEngine.TestTools;
using Unity.Collections;
using Unity.Mathematics;

using NativeStringCollections;

namespace Tests
{
    public class Test_NativeStringList
    {
        private int seed;
        private System.Random random;

        private NativeStringList str_native;
        private List<string> str_list;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            this.seed = 123456;
            this.random = new System.Random(this.seed);

            this.str_native = new NativeStringList(Allocator.Persistent);
            this.str_list = new List<string>();
        }
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            this.str_native.Dispose();
        }

        // A Test behaves as an ordinary method
        [Test]
        public void CheckAPI_RemoveAt()
        {
            this.GenCollectionTestString(400);
            this.CheckCollections();
            this.ApplyRemoveAt(28);
            this.CheckCollections();
        }
        [Test]
        public void CheckAPI_RemoveRange()
        {
            this.GenCollectionTestString(400);
            this.CheckCollections();
            this.ApplyRemoveRange(7, 4);
            this.CheckCollections();
        }
        [Test]
        public void CheckAPI_ReallocateTracer()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            string str = "1234567890--@@";

            var NSL = new NativeStringList(Allocator.TempJob);

            NSL.Add(str);
            StringEntity entity = NSL[0];

            bool effective_ref = true;
            for (int i = 0; i < 100; i++)
            {
                NSL.Add(str);
                try
                {
                    Debug.Log($"add: {i}, entity: {entity.ToString()}, NSL [Size/Capacity] = [{NSL.Size}/{NSL.Capacity}]");
                }
                catch (InvalidOperationException e)
                {
                    effective_ref = false;
                    Debug.Log("the reallocation of NativeStringList is detected by StringEntity. exception: " + e.Message);
                    break;
                }
            }
            Assert.IsFalse(effective_ref);

            NSL.Dispose();
#else
            Debug.Log("this feature will be enabled when the macro 'ENABLE_UNITY_COLLECTIONS_CHECKS' is defined.");
            Debug.Log("it is not tested in current setting.");
#endif
        }

        [Test]
        public void CheckAPI_StringEntity_IndexOf()
        {
            string str = "1234567890--@@G1234567890--@@@xxmhr1234567890--@@";

            var NSL = new NativeStringList(Allocator.TempJob);
            NSL.Add(str);

            StringEntity entity = NSL[0];

            // StringEntity.IndexOf(Char16)
            Assert.AreEqual(entity.IndexOf('6'), 5);
            Assert.AreEqual(entity.IndexOf('6', 10), 20);
            Assert.AreEqual(entity.IndexOf('x'), 30);
            Assert.AreEqual(entity.IndexOf('W'), -1);  // not found

            // StringEntity.IndexOf(string) (= same implementation of IndexOf(StringEntity))
            Assert.AreEqual(entity.IndexOf("@@x"), 28);
            Assert.AreEqual(entity.IndexOf("890-"), 7);
            Assert.AreEqual(entity.IndexOf("890-", 12), 22);
            Assert.AreEqual(entity.IndexOf("99"), -1);  // not found

            NSL.Dispose();
        }


        // helper functions
        private void GenCollectionTestString(int n_char)
        {
            if (n_char <= 0) return;
            Debug.Log(" == generate numbered string ==");

            str_native.Clear();
            str_list.Clear();

            int char_count = 0;
            int line_count = 0;

            int n_lines = n_char / 10;
            for (int i = 0; i < n_lines; i++)
            {
                int n_term = random.Next(3, 7);
                string word = i.ToString() + '_';

                string str = "";
                for (int jj = 1; jj < n_term; jj++)
                {
                    str += word;
                }

                str_native.Add(str);
                str_list.Add(str);

                char_count++;
                line_count++;
            }
            Debug.Log(" generated: " + line_count.ToString() + " strings, " + char_count.ToString() + " charactors.");
        }
        private void CheckCollections()
        {
            Assert.AreEqual(str_native.Length, str_list.Count);

            int elem_len = math.min(str_native.Length, str_list.Count);
            for (int i = 0; i < elem_len; i++)
            {
                StringEntity entity = str_native[i];
                string str_e = entity.ToString();
                char[] c_arr_e = entity.ToArray();

                ReadOnlyStringEntity entity_ro = entity.GetReadOnly();

                string str = str_list[i];
                char[] c_arr = str.ToCharArray();

                Assert.AreEqual(str_e, str);
                Assert.AreEqual(c_arr_e.ToString(), c_arr.ToString());

                Assert.IsTrue(entity.Equals(str));
                Assert.IsTrue(entity.Equals(c_arr));
                if (str.Length == 1)
                {
                    Assert.IsTrue(entity.Equals(str[0]));
                }

                Assert.IsTrue(entity_ro.Equals(str));
                Assert.IsTrue(entity_ro.Equals(c_arr));
                if (str.Length == 1)
                {
                    Assert.IsTrue(entity_ro.Equals(str[0]));
                }

            }
        }
        private void ApplyRemoveAt(int n_RemoveAt)
        {
            if (str_native.Length <= 0 || str_list.Count <= 0) return;
            if (n_RemoveAt <= 0) return;

            Assert.AreEqual(str_native.Length, str_list.Count);

            int i_range = str_native.Length;
            int n_remove = math.min(n_RemoveAt, i_range);

            Debug.Log("str_native.Capacity = " + str_native.Capacity.ToString() + ", str_native.Size = " + str_native.Size.ToString()
                      + ", str_native.IndexCapacity = " + str_native.IndexCapacity.ToString() + ", str_native.Length = " + str_native.Length.ToString() + " (before remove)");

            for (int i = 0; i < n_remove; i++)
            {
                int index = random.Next(0, i_range);

                StringEntity entity = str_native.At(index);
                ReadOnlyStringEntity entity_ro = entity.GetReadOnly();

                //Debug.Log("delete: index = " + index.ToString());

                str_native.RemoveAt(index);
                str_list.RemoveAt(index);

                i_range--;
            }


            Debug.Log("str_native.Capacity = " + str_native.Capacity.ToString() + ", str_native.Size = " + str_native.Size.ToString()
                      + ", str_native.IndexCapacity = " + str_native.IndexCapacity.ToString() + ", str_native.Length = " + str_native.Length.ToString() + " (after remove)");
            str_native.ReAdjustment();
            Debug.Log("str_native.Capacity = " + str_native.Capacity.ToString() + ", str_native.Size = " + str_native.Size.ToString()
                      + ", str_native.IndexCapacity = " + str_native.IndexCapacity.ToString() + ", str_native.Length = " + str_native.Length.ToString() + " (after call 'ReAdjustment()')");
            str_native.ShrinkToFit();
            Debug.Log("str_native.Capacity = " + str_native.Capacity.ToString() + ", str_native.Size = " + str_native.Size.ToString()
                      + ", str_native.IndexCapacity = " + str_native.IndexCapacity.ToString() + ", str_native.Length = " + str_native.Length.ToString() + " (after call 'ShrinkToFit()')");
        }
        private void ApplyRemoveRange(int n_RemoveRange, int len_RemoveRange)
        {
            if (str_native.Length <= 0 || str_list.Count <= 0) return;
            if (n_RemoveRange <= 0 || len_RemoveRange <= 0) return;

            Assert.AreEqual(str_native.Length, str_list.Count);

            int i_range = str_native.Length;
            int n_remove = math.min(n_RemoveRange, i_range / len_RemoveRange);

            Debug.Log("str_native.Capacity = " + str_native.Capacity.ToString() + ", str_native.Size = " + str_native.Size.ToString() + " (before remove)");
            for (int i = 0; i < n_remove; i++)
            {
                int index = random.Next(0, i_range - len_RemoveRange);

                str_native.RemoveRange(index, len_RemoveRange);
                str_list.RemoveRange(index, len_RemoveRange);

                i_range -= len_RemoveRange;
            }


            Debug.Log("str_native.Capacity = " + str_native.Capacity.ToString() + ", str_native.Size = " + str_native.Size.ToString()
                      + ", str_native.IndexCapacity = " + str_native.IndexCapacity.ToString() + ", str_native.Length = " + str_native.Length.ToString() + " (after remove)");
            str_native.ReAdjustment();
            Debug.Log("str_native.Capacity = " + str_native.Capacity.ToString() + ", str_native.Size = " + str_native.Size.ToString()
                      + ", str_native.IndexCapacity = " + str_native.IndexCapacity.ToString() + ", str_native.Length = " + str_native.Length.ToString() + " (after call 'ReAdjustment()')");
            str_native.ShrinkToFit();
            Debug.Log("str_native.Capacity = " + str_native.Capacity.ToString() + ", str_native.Size = " + str_native.Size.ToString()
                      + ", str_native.IndexCapacity = " + str_native.IndexCapacity.ToString() + ", str_native.Length = " + str_native.Length.ToString() + " (after call 'ShrinkToFit()')");
        }
    }
}
