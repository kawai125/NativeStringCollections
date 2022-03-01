using System;
using System.Text;
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
    public class Test_StringParser
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

        [Test]
        public void CheckParseInt32_Boundary()
        {
            // boundary value check
            str_native.Clear();
            str_list.Clear();

            string in_range_lo = "-2147483648";
            string in_range_hi = "2147483647";
            string out_range_lo = "-2147483649";
            string out_range_hi = "2147483648";
            string out_sp_1 = "4400000000";  // overflow to plus region

            str_list.Add(in_range_lo.ToString());
            str_list.Add(in_range_hi.ToString());
            str_list.Add(out_range_lo.ToString());
            str_list.Add(out_range_hi.ToString());
            str_list.Add(out_sp_1.ToString());

            for (int i = 0; i < str_list.Count; i++)
            {
                str_native.Add(str_list[i]);
            }

            for (int i = 0; i < str_native.Length; i++)
            {
                var str = str_list[i];
                ReadOnlyStringEntity str_e = str_native[i];

                bool success = int.TryParse(str, out int value);
                bool success_e = str_e.TryParse(out int value_e);

                Assert.AreEqual(success, success_e);
                Assert.AreEqual(value, value_e);

                if (success != success_e || value != value_e)
                {
                    Debug.LogError("failed to parse. string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                                 + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                                 + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "]");
                }
            }
        }
        [Test]
        public void CheckParseInt64_Boundary()
        {
            // boundary value check
            str_native.Clear();
            str_list.Clear();

            string in_range_lo = "-9223372036854775808";
            string in_range_hi = "9223372036854775807";
            string out_range_lo = "-9223372036854775809";
            string out_range_hi = "9223372036854775808";

            str_list.Add(in_range_lo.ToString());
            str_list.Add(in_range_hi.ToString());
            str_list.Add(out_range_lo.ToString());
            str_list.Add(out_range_hi.ToString());

            for (int i = 0; i < str_list.Count; i++)
            {
                str_native.Add(str_list[i]);
            }

            for (int i = 0; i < str_native.Length; i++)
            {
                var str = str_list[i];
                ReadOnlyStringEntity str_e = str_native[i];

                bool success = long.TryParse(str, out long value);
                bool success_e = str_e.TryParse(out long value_e);

                Assert.AreEqual(success, success_e);
                Assert.AreEqual(value, value_e);

                if (success != success_e || value != value_e)
                {
                    Debug.LogError("failed to parse. string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                                 + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                                 + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "]");
                }
            }
        }
        [Test]
        public void CheckParseFloat32_Boundary()
        {
            // boundary value check
            str_native.Clear();
            str_list.Clear();

            string in_range_lo = "-3.40281e+38";
            string in_range_hi = "3.40281E+38";
#if DISABLE_CS_FAST_FLOAT
            string out_range_lo = "-3.40283e+38";  // standard float.TryParse() gives "0" and return false.
            string out_range_hi = "3.40283E+38";   // csFastFloat.TryParse() gives "infinity" and return true.
#endif

            str_list.Add(in_range_lo.ToString());
            str_list.Add(in_range_hi.ToString());

#if DISABLE_CS_FAST_FLOAT
            str_list.Add(out_range_lo.ToString());
            str_list.Add(out_range_hi.ToString());
#endif

            str_list.Add("1.401298e-45");  // float.Elipson
            str_list.Add("-1.401298e-45");

            str_list.Add("1.401297e-45");
            str_list.Add("-1.401297e-45");

            for (int i = 0; i < str_list.Count; i++)
            {
                str_native.Add(str_list[i]);
            }

            for (int i = 0; i < str_native.Length; i++)
            {
                var str = str_list[i];
                ReadOnlyStringEntity str_e = str_native[i];

                //Debug.Log($"@ parse target: {str_e}");

                bool success = float.TryParse(str, out float value);
                bool success_e = str_e.TryParse(out float value_e);

                Assert.AreEqual(success, success_e);

                bool check_value = this.EqualsFloat(value, value_e, 1.0e-5f, out float rel_diff);
                if (!check_value || success != success_e)
                {
                    Debug.LogError("failed to parse. i = " + i.ToString() + " string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                                 + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                                 + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "], rel_diff = " + rel_diff.ToString());
                }
                Assert.IsTrue(check_value);
            }
        }
        [Test]
        public void CheckParseFloat32_Typical()
        {
            // typical format sample
            str_native.Clear();
            str_list.Clear();

            str_list.Add("12345678987654321");
            str_list.Add("-12345678987654321");
            str_list.Add("123456789.87654321");
            str_list.Add("-123456789.87654321");
            str_list.Add("12345678987654321e-5");
            str_list.Add("-12345678987654321E-5");
            str_list.Add("123456789.87654321e-5");
            str_list.Add("-123456789.87654321E-5");
            str_list.Add("000.00123456789e-5");
            str_list.Add("-000.00123456789E-5");

            str_list.Add("1.11111111e-60");
            str_list.Add("-1.11111111e-60");

            str_list.Add("4E+08");
            str_list.Add("3E+08");
            str_list.Add("0E+0");
            str_list.Add("000E-00");
            str_list.Add("321.98e22");

#if DISABLE_CS_FAST_FLOAT
        //    str_list.Add("123E+00000000000000000000221");    // standard float.TryParse() gives "0" and return false.
        //    str_list.Add("123E+00000000000000000000221777"); // csFastFloat.TryParse() gives "infinity" and return true.
#endif
            str_list.Add("123E-00000000000000000000221");
            str_list.Add("123E-00000000000000000000221777");


            str_list.Add("e");
            str_list.Add(".01234e+2");
            str_list.Add("6633.11e+");
            str_list.Add("6633.11e-+-4");

#if DISABLE_CS_FAST_FLOAT
            str_list.Add("123E+0000000000Kg00000000022");  // csFastFloat treats as "123E+0000000000". the part of "Kg00000000022" was ignored.
#endif
            str_list.Add("123E+Kg00000000022");

            for (int i = 0; i < str_list.Count; i++)
            {
                str_native.Add(str_list[i]);
            }

            for (int i = 0; i < str_native.Length; i++)
            {
                var str = str_list[i];
                ReadOnlyStringEntity str_e = str_native[i];

                //Debug.Log($"@ parse target: {str_e}");

                bool success = float.TryParse(str, out float value);
                bool success_e = str_e.TryParse(out float value_e);

                Assert.AreEqual(success, success_e);

                bool check_value = this.EqualsFloat(value, value_e, 1.0e-5f, out float rel_diff);
                if (!check_value || success != success_e)
                {
                    Debug.LogError("failed to parse. i = " + i.ToString() + " string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                                 + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                                 + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "], rel_diff = " + rel_diff.ToString());
                }
                Assert.IsTrue(check_value);
            }
        }
        [Test]
        public void CheckParseFloat64_Boundary()
        {
            // boundary value check
            str_native.Clear();
            str_list.Clear();

            string in_range_lo = "-1.797693134862e+308";
            string in_range_hi = "1.797693134862E+308";
#if DISABLE_CS_FAST_FLOAT
            string out_range_lo = "-1.797693134863e+308"; // standard double.TryParse() gives "0" and return false.
            string out_range_hi = "1.797693134863E+308";  // csFastFloat.TryParse() gives "infinity" and return true.
#endif

            str_list.Add(in_range_lo.ToString());
            str_list.Add(in_range_hi.ToString());
#if DISABLE_CS_FAST_FLOAT
            str_list.Add(out_range_lo.ToString());
            str_list.Add(out_range_hi.ToString());
#endif

            // this implementation don't aim perfect accuracy for IEEE754 convertion.
            //   Epsilon = 4.94065645841247e-324
            str_list.Add( "1e-323");
            str_list.Add("-1e-323");
            str_list.Add( "1e-325");
            str_list.Add("-1e-325");

            str_list.Add("00924731130.63782E+299");

            for (int i = 0; i < str_list.Count; i++)
            {
                str_native.Add(str_list[i]);
            }

            for (int i = 0; i < str_native.Length; i++)
            {
                var str = str_list[i];
                ReadOnlyStringEntity str_e = str_native[i];

                bool success = double.TryParse(str, out double value);
                bool success_e = str_e.TryParse(out double value_e);

                Assert.AreEqual(success, success_e);

                bool check_value = this.EqualsDouble(value, value_e, 1.0e-14, out double rel_diff);
                if (!check_value || success != success_e)
                {
                    Debug.LogError("failed to parse. i = " + i.ToString() + " string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                                 + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                                 + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "], rel_diff = " + rel_diff.ToString());
                }
                Assert.IsTrue(check_value);
            }
        }

        [TestCase(4096)]
        public void CheckParseInt32(int n_numeric_string)
        {
            // random value check
            this.GenerateRandomIntStrings(n_numeric_string, 2, 10, 64);

            int int_count = 0;
            for (int i = 0; i < n_numeric_string; i++)
            {
                string str = str_list[i];
                ReadOnlyStringEntity str_e = str_native[i];

                bool success = int.TryParse(str, out int value);
                bool success_e = str_e.TryParse(out int value_e);

                Assert.AreEqual(success, success_e);
                Assert.AreEqual(value, value_e);

                if (success != success_e || value != value_e)
                {
                    Debug.LogError("failed to parse. string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                                 + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                                 + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "]");
                }
                if (success) int_count++;
            }
            Debug.Log("parsed int count = " + int_count.ToString() + " / " + n_numeric_string.ToString());
        }
        [TestCase(4096)]
        public void CheckParseInt64(int n_numeric_string)
        {
            // random value check
            this.GenerateRandomIntStrings(n_numeric_string, 9, 19, 128);

            int long_count = 0;
            for (int i = 0; i < n_numeric_string; i++)
            {
                string str = str_list[i];
                ReadOnlyStringEntity str_e = str_native[i];

                bool success = long.TryParse(str, out long value);
                bool success_e = str_e.TryParse(out long value_e);

                Assert.AreEqual(success, success_e);
                Assert.AreEqual(value, value_e);

                if (success != success_e || value != value_e)
                {
                    Debug.LogError("failed to parse. string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                                 + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                                 + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "]");
                }
                if (success) long_count++;
            }
            Debug.Log("parsed long count = " + long_count.ToString() + " / " + n_numeric_string.ToString());
        }
        [TestCase(10240)]
        public void CheckParseFloat32(int n_numeric_string)
        {
            this.GenerateRandomFloatStrings(n_numeric_string, 3, 8, 39, 64);

            int fail_count = 0;

            float max_rel_err = 0.0f;
            int max_err_id = 0;

            int float_count = 0;
            for (int i = 0; i < n_numeric_string; i++)
            {
                string str = str_list[i];
                ReadOnlyStringEntity str_e = str_native[i];

                bool success = float.TryParse(str, out float value);
                bool success_e = str_e.TryParse(out float value_e);

                bool check_value = this.EqualsFloat(value, value_e, 1.0e-5f, out float rel_diff);

                if (!check_value || success != success_e)
                {
#if DISABLE_CS_FAST_FLOAT
#else
                    if (!success && success_e && (float.IsPositiveInfinity(value_e) || float.IsNegativeInfinity(value_e)))
                    {
                        // standard C# parser cannot parse value as infinit.
                        check_value = true;
                        break;
                    }
#endif

                    fail_count++;
                    Debug.LogError("failed to parse i = " + i.ToString() + " string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                                 + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                                 + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "], rel_diff = " + rel_diff.ToString());
                }
                Assert.IsTrue(check_value);

                if (success) float_count++;

                if (max_rel_err < rel_diff)
                {
                    max_rel_err = rel_diff;
                    max_err_id = i;
                }
            }
            Debug.Log("parsed float count = " + float_count.ToString() + " / " + n_numeric_string.ToString());
            Debug.Log("max relative error = " + max_rel_err.ToString() + " at " + str_list[max_err_id]);
        }
        [TestCase(10240)]
        public void CheckParseFloat64(int n_numeric_string)
        {
            this.GenerateRandomFloatStrings(n_numeric_string, 2, 18, 309, 64);

            double max_rel_err = 0.0;
            int max_err_id = 0;

            int double_count = 0;
            for (int i = 0; i < n_numeric_string; i++)
            {
                string str = str_list[i];
                ReadOnlyStringEntity str_e = str_native[i];

                bool success = double.TryParse(str, out double value);
                bool success_e = str_e.TryParse(out double value_e);

                //Debug.Log($"@ parse target: {str_e}");

                bool check_value = this.EqualsDouble(value, value_e, 1.0e-14, out double rel_diff);
                if (!check_value || success != success_e)
                {
#if DISABLE_CS_FAST_FLOAT
#else
                    if(!success && success_e && (double.IsPositiveInfinity(value_e) || double.IsNegativeInfinity(value_e)))
                    {
                        // standard C# parser cannot parse value as infinit.
                        check_value = true;
                        break;
                    }
#endif
                    Debug.LogError("failed to parse i = " + i.ToString() + " string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                                 + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                                 + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "], rel_diff = " + rel_diff.ToString());
                }
                Assert.IsTrue(check_value);

                if (success) double_count++;

                if (max_rel_err < rel_diff)
                {
                    max_rel_err = rel_diff;
                    max_err_id = i;
                }
            }
            Debug.Log("parsed double count = " + double_count.ToString() + " / " + n_numeric_string.ToString());
            Debug.Log("max relative error = " + max_rel_err.ToString() + " at " + str_list[max_err_id]);
        }

        [Test]
        public void CheckParseHex()
        {
            var str_native_big = new NativeStringList(Allocator.Temp);
            var str_native_lit = new NativeStringList(Allocator.Temp);

            this.CheckParseHexInt32(str_native_big, str_native_lit);
            this.CheckParseHexInt64(str_native_big, str_native_lit);
            this.CheckParseHexFloat32(str_native_big, str_native_lit);
            this.CheckParseHexFloat64(str_native_big, str_native_lit);

            str_native_big.Dispose();
            str_native_lit.Dispose();
        }
        private void CheckParseHexInt32(NativeStringList str_native_big, NativeStringList str_native_lit)
        {
            // int hex
            int[] int_list = new int[] { 0, 128, 512, 10000, -12345678, -2147483648, 2147483647 };

            str_native_big.Clear();
            str_native_lit.Clear();

            for (int i = 0; i < int_list.Length; i++)
            {
                int int_v = int_list[i];
                byte[] bytes = BitConverter.GetBytes(int_v);
                string str = BitConverter.ToString(bytes).Replace("-", "");  // BitConverter returns little endian code on x86.
                string str_0x = "0x" + str;

                str_native_lit.Add(str);
                str_native_lit.Add(str_0x);
                str_native_big.Add(this.ConvertEndian(str));
                str_native_big.Add(this.ConvertEndian(str_0x));
            }
            for (int i = 0; i < str_native_lit.Length; i++)
            {
                int value_ref = int_list[i / 2];  // stored 2x elems as [hex data] and 0x[hex data]
                ReadOnlyStringEntity str_lit = str_native_lit[i];
                ReadOnlyStringEntity str_big = str_native_big[i];

                bool success_lit = str_lit.TryParseHex(out int value_lit);
                bool success_big = str_big.TryParseHex(out int value_big, Endian.Big);

                Debug.Log("parse str[big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                        + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                        + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");

                Assert.IsTrue(success_lit);
                Assert.IsTrue(success_big);
                Assert.AreEqual(value_ref, value_lit);
                Assert.AreEqual(value_ref, value_big);
                if ((value_ref != value_lit || value_ref != value_big) || !success_lit || !success_big)
                {
                    Debug.LogError("failed to parse. i = " + i.ToString()
                        + " string [big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                        + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                        + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");
                }
            }
        }
        private void CheckParseHexInt64(NativeStringList str_native_big, NativeStringList str_native_lit)
        {
            // long hex
            long[] long_list = new long[] { 0, 128, 512, 10000, -12345678, -9223372036854775808, 9223372036854775807 };

            str_native_big.Clear();
            str_native_lit.Clear();

            for (int i = 0; i < long_list.Length; i++)
            {
                long long_v = long_list[i];
                byte[] bytes = BitConverter.GetBytes(long_v);
                string str = BitConverter.ToString(bytes).Replace("-", "");  // BitConverter returns little endian code on x86.
                string str_0x = "0x" + str;

                str_native_lit.Add(str);
                str_native_lit.Add(str_0x);
                str_native_big.Add(this.ConvertEndian(str));
                str_native_big.Add(this.ConvertEndian(str_0x));
            }
            for (int i = 0; i < str_native_lit.Length; i++)
            {
                long value_ref = long_list[i / 2];
                ReadOnlyStringEntity str_lit = str_native_lit[i];
                ReadOnlyStringEntity str_big = str_native_big[i];

                bool success_lit = str_lit.TryParseHex(out long value_lit);
                bool success_big = str_big.TryParseHex(out long value_big, Endian.Big);

                Debug.Log("parse str[big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                        + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                        + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");

                Assert.IsTrue(success_lit);
                Assert.IsTrue(success_big);
                Assert.AreEqual(value_ref, value_lit);
                Assert.AreEqual(value_ref, value_big);
                if ((value_ref != value_lit || value_ref != value_big) || !success_lit || !success_big)
                {
                    Debug.LogError("failed to parse. i = " + i.ToString()
                        + " string [big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                        + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                        + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");
                }
            }
        }
        private void CheckParseHexFloat32(NativeStringList str_native_big, NativeStringList str_native_lit)
        {
            // float hex
            float[] float_list = new float[] { 0.0f, 128.0f, 512.0f, 12345.67f, -12345678f, -3.40281e+38f, 3.40281E+38f };

            str_native_big.Clear();
            str_native_lit.Clear();

            for (int i = 0; i < float_list.Length; i++)
            {
                float float_v = float_list[i];
                byte[] bytes = BitConverter.GetBytes(float_v);
                string str = BitConverter.ToString(bytes).Replace("-", "");  // BitConverter returns little endian code on x86.
                string str_0x = "0x" + str;

                str_native_lit.Add(str);
                str_native_lit.Add(str_0x);
                str_native_big.Add(this.ConvertEndian(str));
                str_native_big.Add(this.ConvertEndian(str_0x));
            }
            for (int i = 0; i < str_native_lit.Length; i++)
            {
                float value_ref = float_list[i / 2];
                ReadOnlyStringEntity str_lit = str_native_lit[i];
                ReadOnlyStringEntity str_big = str_native_big[i];

                bool success_lit = str_lit.TryParseHex(out float value_lit);
                bool success_big = str_big.TryParseHex(out float value_big, Endian.Big);

                Debug.Log("parse str[big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                        + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                        + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");

                Assert.IsTrue(success_lit);
                Assert.IsTrue(success_big);
                Assert.AreEqual(value_ref, value_lit);
                Assert.AreEqual(value_ref, value_big);
                // must be bit-complete convertion in hex data format
                if ((value_ref != value_lit || value_ref != value_big) || !success_lit || !success_big)
                {
                    Debug.LogError("failed to parse. i = " + i.ToString()
                        + " string [big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                        + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                        + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");
                }
            }
        }
        private void CheckParseHexFloat64(NativeStringList str_native_big, NativeStringList str_native_lit)
        {
            // double hex
            double[] double_list = new double[]
            {
            0.0, 128.0, 512.0, 12345.67, -12345678,
            -9223372036854775808d, 9223372036854775807d,
            -3.40281e+38, 3.40281E+38,
            -1.797693134862e+308, 1.797693134862E+308
            };

            str_native_big.Clear();
            str_native_lit.Clear();

            for (int i = 0; i < double_list.Length; i++)
            {
                double double_v = double_list[i];
                byte[] bytes = BitConverter.GetBytes(double_v);
                string str = BitConverter.ToString(bytes).Replace("-", "");  // BitConverter returns little endian code on x86.
                string str_0x = "0x" + str;

                str_native_lit.Add(str);
                str_native_lit.Add(str_0x);
                str_native_big.Add(this.ConvertEndian(str));
                str_native_big.Add(this.ConvertEndian(str_0x));
            }
            for (int i = 0; i < str_native_lit.Length; i++)
            {
                double value_ref = double_list[i / 2];
                ReadOnlyStringEntity str_lit = str_native_lit[i];
                ReadOnlyStringEntity str_big = str_native_big[i];

                bool success_lit = str_lit.TryParseHex(out double value_lit);
                bool success_big = str_big.TryParseHex(out double value_big, Endian.Big);

                Debug.Log("parse str[big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                        + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                        + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");

                Assert.IsTrue(success_lit);
                Assert.IsTrue(success_big);
                Assert.AreEqual(value_ref, value_lit);
                Assert.AreEqual(value_ref, value_big);
                // must be bit-complete convertion in hex data format
                if ((value_ref != value_lit || value_ref != value_big) || !success_lit || !success_big)
                {
                    Debug.LogError("failed to parse. i = " + i.ToString()
                        + " string [big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                        + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                        + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");
                }
            }
        }

        // helper functions
        private void GenerateRandomIntStrings(int n_str, int most_significant_digit, int digit_len, int fail_factor)
        {
            Assert.IsTrue(most_significant_digit >= 0);
            Assert.IsTrue(digit_len > 4);
            Assert.IsTrue(fail_factor >= 1);

            str_list.Clear();
            str_native.Clear();

            int insert_count = 0;
            bool x_inserted = false;

            char[] exclude_c_list = new char[] { ' ' };

            for (int ii = 0; ii < n_str; ii++)
            {
                insert_count = 0;
                string str = "";

                str = this.InsertFailChar(str, fail_factor, exclude_c_list, out x_inserted);
                if (x_inserted) insert_count++;

                if (random.Next(0, 2) == 0)
                {
                    str += '-';
                }

                str = this.InsertFailChar(str, fail_factor, exclude_c_list, out x_inserted);
                if (x_inserted) insert_count++;

                str += random.Next(0, most_significant_digit + 1).ToString();

                int len = random.Next(digit_len - 4, digit_len);

                for (int jj = 0; jj < len; jj++)
                {
                    str = this.InsertFailChar(str, fail_factor, exclude_c_list, out x_inserted);
                    if (x_inserted) insert_count++;

                    str += random.Next(0, 10).ToString();
                }

                str = this.InsertFailChar(str, fail_factor, exclude_c_list, out x_inserted);
                if (x_inserted) insert_count++;

                if (insert_count + len > digit_len)
                {
                    // remake
                    ii--;
                }
                else
                {
                    str_list.Add(str);
                    str_native.Add(str);
                }
            }
        }
        private void GenerateRandomFloatStrings(int n_str, int most_significant_digit, int digit_len, int exp_range, int fail_factor)
        {
            Assert.IsTrue(most_significant_digit >= 1);
            Assert.IsTrue(digit_len > 4);
            Assert.IsTrue(exp_range >= 1);
            Assert.IsTrue(fail_factor >= 1);

            str_list.Clear();
            str_native.Clear();

            int insert_count = 0;
            bool x_inserted = false;

            char[] exclude_c_list = new char[] { ' ', ',' };

            for (int ii = 0; ii < n_str; ii++)
            {
                insert_count = 0;
                string str = "";

                str = this.InsertFailChar(str, fail_factor, exclude_c_list, out x_inserted);
                if (x_inserted) insert_count++;

                if (random.Next(0, 2) == 0)
                {
                    str += '-';
                }

                str = this.InsertFailChar(str, fail_factor, exclude_c_list, out x_inserted);
                if (x_inserted) insert_count++;

                str += random.Next(0, most_significant_digit + 1).ToString();

                int len = random.Next(digit_len - 4, digit_len) + 1;
                int dot_pos = random.Next(1, len);

                // mantissa part
                for (int jj = 0; jj < len; jj++)
                {
                    if (jj == dot_pos)
                    {
                        str += ".";
                    }
                    else
                    {
                        str += random.Next(0, 10).ToString();
                    }

                    str = this.InsertFailChar(str, fail_factor, exclude_c_list, out x_inserted);
                    if (x_inserted) insert_count++;
                }

                // exp part
                if (random.Next(0, 2) == 0)
                {
                    str += 'e';
                }
                else
                {
                    str += 'E';
                }

                str = this.InsertFailChar(str, fail_factor, exclude_c_list, out x_inserted);

                if (random.Next(0, 2) == 0)
                {
                    str += '-';
                }
                else
                {
                    str += '+';
                }

                str = this.InsertFailChar(str, fail_factor, exclude_c_list, out x_inserted);

                str += random.Next(0, exp_range).ToString("D3");

                str = this.InsertFailChar(str, fail_factor, exclude_c_list, out x_inserted);

                if (insert_count + len > digit_len)
                {
                    // remake
                    ii--;
                }
                else
                {
                    str_list.Add(str);
                    str_native.Add(str);
                }
            }
        }

        private string InsertFailChar(string tgt, int fail_factor, char[] exclude_list, out bool inserted)
        {
            inserted = false;
            char c = 'z';
            if (random.Next(0, fail_factor) == 0)
            {
                //char c = (char)random.Next(33, 127);  // ASCII char code: [32~126], 32 is space.
                int i_try = 0;
                while (i_try < 10000)
                {
                    i_try++;
                    bool is_ex_char = false;

                    c = (char)random.Next(33, 127);  // ASCII char code: [32~126], 32 is space.
                    foreach (char ex in exclude_list)
                    {
                        if (c == ex)
                        {
                            is_ex_char = true;
                            break;
                        }
                    }

                    if (!is_ex_char) break;
                }

                inserted = true;
                return tgt += c;
            }
            return tgt;
        }


        private bool EqualsFloat(float a, float b, float rel_err_tolerance, out float rel_diff)
        {
            rel_diff = 0.0f;
            float diff = a - b;
            if (diff == 0.0f) return true;

            if (a != 0.0f)
            {
                rel_diff = diff / a;
            }
            else
            {
                rel_diff = diff / b;
            }
            if (math.abs(rel_diff) < rel_err_tolerance) return true;
            return false;
        }
        private bool EqualsDouble(double a, double b, double rel_err_tolerance, out double rel_diff)
        {
            rel_diff = 0.0;
            double diff = a - b;
            if (diff == 0.0) return true;

            if (a != 0.0)
            {
                rel_diff = diff / a;
            }
            else
            {
                rel_diff = diff / b;
            }
            if (math.abs(rel_diff) < rel_err_tolerance) return true;
            return false;
        }

        private string ConvertEndian(string value)
        {
            if (value.Length < 8 || value.Length % 2 != 0)
            {
                throw new InvalidOperationException("this is not hex value. value = " + value);
            }

            int i_start = 0;
            if (value[0] == '0' && value[1] == 'x') i_start = 2;

            string result = "";
            if (i_start != 0) result = "0x";

            int n_byte = value.Length / 2;
            int j_last = i_start / 2;
            for (int i = n_byte - 1; i >= j_last; i--)
            {
                char c0 = value[2 * i];
                char c1 = value[2 * i + 1];

                result += c0;
                result += c1;
            }

            return result;
        }
    }
}
