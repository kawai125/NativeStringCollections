using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

using Unity.Collections;
using NativeStringCollections;

public class Test_NativeStringList : MonoBehaviour
{
    [SerializeField]
    public int seed;
    [SerializeField]
    public int factor_delim;

    [SerializeField]
    public int n_char;

    [SerializeField]
    public int n_RemoveAt;

    [SerializeField]
    public int n_RemoveRange;
    [SerializeField]
    public int len_RemoveRange;

    [SerializeField]
    public int n_numeric_string;


    private NativeStringList str_native;
    private List<string> str_list;

    private System.Random random;

    // Start is called before the first frame update
    void Start()
    {
        this.str_native = new NativeStringList(128, 32);
        this.str_list = new List<string>();

        this.random = new System.Random(seed);
    }

    private void OnDestroy()
    {
        this.str_native.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnClickCollectionTest()
    {
    //    this.GenerateRandomStrings();
        this.GenerateNumberedStrings();

        this.Test_Collection("gen 1");
        this.Test_RemoveAt("RemoveAt()");
        this.Test_Collection("after Test_RemoveAt()");

    //    this.GenerateRandomStrings();
        this.GenerateNumberedStrings();

        this.Test_Collection("gen 2");
        this.Test_RemoveRange("RemoveRange()");
        this.Test_Collection("after Test_RemoveRange()");
    }
    public void OnClickParseIntStringTest()
    {
        this.Test_ParseInt32_String();
        this.Test_ParseInt64_String();
    }
    public void OnClickParseFloatStringTest()
    {
        this.Test_ParseFloat32_String();
        this.Test_ParseFloat64_String();
    }
    public void OnClickParseHexStringTest()
    {
        this.Test_ParseHex_String();
    }
    public void OnClickReallocateTraceTest()
    {
        string str = "1234567890--@@";

        var NSL = new NativeStringList(64, 4, Allocator.TempJob);

        NSL.Add(str);
        StringEntity entity = NSL[0];

        bool effective_ref = true;

        for (int i=0; i<100; i++)
        {
            NSL.Add(str);
            try
            {
                Debug.Log("add: " + i.ToString() + ", entity: " + entity.ToString() + ", NSL [Size/Capacity] = [" + NSL.Size.ToString() + "/" + NSL.Capacity.ToString() + "]");
            }
            catch (InvalidOperationException e)
            {
                effective_ref = false;
                Debug.Log("the reallocation of NativeStringList is detected by StringEntity.");
                break;
            }
        }
        if (effective_ref)
        {
            Debug.LogError("the reallocation of NativeStringList was not detected. add the macro: 'NATIVE_STRING_COLLECTION_TRACE_REALLOCATION' for using this feature.");
        }

        NSL.Dispose();
    }

    void Test_Collection(string tag)
    {
        bool test_pass = true;
        Debug.Log(" == check collection consistency ==");
        if (tag.Length > 0) Debug.Log("  test tag = '" + tag + "' was begin.");
        Debug.Log("collection size: str_native.Length = " + str_native.Length + ", str_list.Count = " + str_list.Count);
        if (str_native.Length != str_list.Count)
        {
            test_pass = false;
            Debug.LogError("invalid size.");
        }

        string error_log_txt = "";

        int elem_len = math.min(str_native.Length, str_list.Count);
        for(int i=0; i<elem_len; i++)
        {
            StringEntity entity = str_native[i];
            string str_e = entity.ToString();
            char[] c_arr_e = entity.ToCharArray();

            ReadOnlyStringEntity entity_ro = entity.GetReadOnlyEntity();

            string str = str_list[i];
            char[] c_arr = str.ToCharArray();

            if (str_e != str)
            {
                test_pass = false;
                error_log_txt += "index = " + i + ", invalid element: str_e = " + str_e + ", str = " + str + "\n";
            }
            if (c_arr_e.ToString() != c_arr.ToString())
            {
                test_pass = false;
                error_log_txt += "index = " + i + ", invalid element: c_arr_e = " + (new string(c_arr_e)) + ", c_arr = " + (new string(c_arr)) + "\n";
            }

            if (!entity.Equals(str))
            {
                test_pass = false;
                error_log_txt += "index = " + i + ", invalid element: entity = " + entity.ToString() + ", str = " + str + "\n";
            }
            if (!entity.Equals(c_arr))
            {
                test_pass = false;
                error_log_txt += "index = " + i + ", failed to compare by char[]: entity = " + entity.ToString() + ", c_arr = " + (new string(c_arr)) + "\n";
            }
            if (str.Length == 1)
            {
                if (!entity.Equals(str[0]))
                {
                    test_pass = false;
                    error_log_txt += "index = " + i + ", failed to compare by char: entity = " + entity.ToString() + ", c = " + str[0] + "\n";
                }
            }

            if (!entity_ro.Equals(str))
            {
                test_pass = false;
                error_log_txt += "index = " + i + ", invalid element: read only entity = " + entity_ro.ToString() + ", str = " + str.ToString() + "\n";
            }
            if (!entity_ro.Equals(c_arr))
            {
                test_pass = false;
                error_log_txt += "index = " + i + ", failed to compare by char[]: read only entity = " + entity_ro.ToString() + ", c_arr = " + (new string(c_arr)) + "\n";
            }
            if (str.Length == 1)
            {
                if (!entity_ro.Equals(str[0]))
                {
                    test_pass = false;
                    error_log_txt += "index = " + i + ", failed to compare by char: read only entity = " + entity_ro.ToString() + ", c = " + str[0] + "\n";
                }
            }

        }
        if (error_log_txt.Length > 0) Debug.LogError(error_log_txt);

        if (test_pass)
        {
            Debug.Log("== test passed ==");
        }
        else
        {
            Debug.LogError("== test failed ==");
        }
        if (tag.Length > 0) Debug.Log("  test tag = '" + tag + "' was end.");
    }
    void Test_RemoveAt(string tag)
    {
        if (str_native.Length <= 0 || str_list.Count <= 0) return;
        if (n_RemoveAt <= 0) return;

        Debug.Log(" == check RemoveAt() function ==");
        if (tag.Length > 0) Debug.Log("  test tag = '" + tag + "' was begin.");
        if (str_native.Length != str_list.Count)
        {
            Debug.LogError("collection size: str_native.Length = " + str_native.Length.ToString() + ", str_list.Count = " + str_list.Count.ToString());
            return;
        }

        bool test_pass = true;
        int i_range = str_native.Length;
        int n_remove = math.min(n_RemoveAt, i_range);

        Debug.Log("str_native.Capacity = " + str_native.Capacity.ToString() + ", str_native.Size = " + str_native.Size.ToString()
                  + ", str_native.IndexCapacity = " + str_native.IndexCapacity.ToString() + ", str_native.Length = " + str_native.Length.ToString() + " (before remove)");

        for (int i=0; i<n_remove; i++)
        {
            int index = random.Next(0, i_range);

            StringEntity entity = str_native.At(index);
            ReadOnlyStringEntity entity_ro = entity.GetReadOnlyEntity();

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

        if (test_pass)
        {
            Debug.Log("== test passed ==");
        }
        else
        {
            Debug.LogError("== test failed ==");
        }
        if (tag.Length > 0) Debug.Log("  test tag = '" + tag + "' was end.");
    }
    void Test_RemoveRange(string tag)
    {
        if (str_native.Length <= 0 || str_list.Count <= 0) return;
        if (n_RemoveRange <= 0 || len_RemoveRange <= 0) return;

        Debug.Log(" == check RemoveRange() function ==");
        if (tag.Length > 0) Debug.Log("  test tag = '" + tag + "' was begin.");
        if (str_native.Length != str_list.Count)
        {
            Debug.LogError("collection size: str_native.Length = " + str_native.Length.ToString() + ", str_list.Count = " + str_list.Count.ToString());
            return;
        }

        bool test_pass = true;
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

        if (test_pass)
        {
            Debug.Log("== test passed ==");
        }
        else
        {
            Debug.LogError("== test failed ==");
        }
        if (tag.Length > 0) Debug.Log("  test tag = '" + tag + "' was end.");
    }

    void Test_ParseInt32_String()
    {
        Debug.Log("== Parse Int32 test ==");

        Debug.Log("boundary value check");
        bool test_pass = true;

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

        for (int i=0; i<str_list.Count; i++)
        {
            str_native.Add(str_list[i]);
        }

        for(int i=0; i<str_native.Length; i++)
        {
            var str = str_list[i];
            ReadOnlyStringEntity str_e = str_native[i];

            bool success = int.TryParse(str, out int value);
            bool success_e = str_e.TryParse(out int value_e);

            Debug.Log("parse str = '" + str + "', try = " + success.ToString() + ", value = " + value.ToString());

            if (success != success_e || value != value_e)
            {
                test_pass = false;
                Debug.LogError("failed to parse. string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                             + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() +"], "
                             + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "]");
            }
        }
        if (!test_pass)
        {
            Debug.LogError("the boundary value check was failure");
        }

        Debug.Log("random value check");
        test_pass = true;
        this.GenerateRandomIntStrings(n_numeric_string, 2, 10, 64);

        int int_count = 0;
        for(int i=0; i<n_numeric_string; i++)
        {
            string str = str_list[i];
            ReadOnlyStringEntity str_e = str_native[i];

            bool success = int.TryParse(str, out int value);
            bool success_e = str_e.TryParse(out int value_e);

            if (success != success_e || value != value_e)
            {
                test_pass = false;
                Debug.LogError("failed to parse. string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                             + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                             + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "]");
            }
            if (success) int_count++;
        }
        Debug.Log("parsed int count = " + int_count.ToString() + " / " + n_numeric_string.ToString());
        if (!test_pass)
        {
            Debug.LogError("the random value check was failure");
        }
    }
    void Test_ParseInt64_String()
    {
        Debug.Log("== Parse Int64 test ==");

        Debug.Log("boundary value check");
        bool test_pass = true;

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

            Debug.Log("parse str = '" + str + "', try = " + success.ToString() + ", value = " + value.ToString());

            if (success != success_e || value != value_e)
            {
                test_pass = false;
                Debug.LogError("failed to parse. string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                             + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                             + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "]");
            }
        }
        if (!test_pass)
        {
            Debug.LogError("the boundary value check was failure");
        }

        Debug.Log("random value check");
        test_pass = true;
        this.GenerateRandomIntStrings(n_numeric_string, 9, 19, 128);

        int long_count = 0;
        for (int i = 0; i < n_numeric_string; i++)
        {
            string str = str_list[i];
            ReadOnlyStringEntity str_e = str_native[i];

            bool success = long.TryParse(str, out long value);
            bool success_e = str_e.TryParse(out long value_e);

            if (success != success_e || value != value_e)
            {
                test_pass = false;
                Debug.LogError("failed to parse. string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                             + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                             + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "]");
            }
            if (success) long_count++;
        }
        Debug.Log("parsed long count = " + long_count.ToString() + " / " + n_numeric_string.ToString());
        if (!test_pass)
        {
            Debug.LogError("the random value check was failure");
        }
    }
    void Test_ParseFloat32_String()
    {
        Debug.Log("== Parse Float32 test ==");

        Debug.Log("boundary value check");
        bool test_pass = true;

        str_native.Clear();
        str_list.Clear();

        string in_range_lo = "-3.40281e+38";
        string in_range_hi = "3.40281E+38";
        string out_range_lo = "-3.40283e+38";
        string out_range_hi = "3.40283E+38";

        str_list.Add(in_range_lo.ToString());
        str_list.Add(in_range_hi.ToString());
        str_list.Add(out_range_lo.ToString());
        str_list.Add(out_range_hi.ToString());

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

        for (int i = 0; i < str_list.Count; i++)
        {
            str_native.Add(str_list[i]);
        }

        for (int i = 0; i < str_native.Length; i++)
        {
            var str = str_list[i];
            ReadOnlyStringEntity str_e = str_native[i];

            bool success = float.TryParse(str, out float value);
            bool success_e = str_e.TryParse(out float value_e);

            Debug.Log("parse str = '" + str + "', try = " + success.ToString() + ", value = " + value.ToString());

            if (!this.EqualsFloat(value, value_e, 1.0e-5f, out float rel_diff) || success != success_e)
            {
                test_pass = false;
                Debug.LogError("failed to parse. i = " + i.ToString() + " string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                             + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                             + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "], rel_diff = " + rel_diff.ToString());
            }
        }
        if (!test_pass)
        {
            Debug.LogError("the boundary value check was failure");
        }

        Debug.Log("random value check");
        //this.GenerateRandomIntStrings(n_numeric_string, 2, 10, 64);
        this.GenerateRandomFloatStrings(n_numeric_string, 3, 8, 39, 64);

        test_pass = true;
        int fail_count = 0;

        float max_rel_err = 0.0f;
        int max_err_id = 0;

        int int_count = 0;
        for (int i = 0; i < n_numeric_string; i++)
        {
            string str = str_list[i];
            ReadOnlyStringEntity str_e = str_native[i];

            bool success = float.TryParse(str, out float value);
            bool success_e = str_e.TryParse(out float value_e);

            if (!this.EqualsFloat(value, value_e, 1.0e-5f, out float rel_diff) || success != success_e)
            {
                test_pass = false;
                fail_count++;
                Debug.LogError("failed to parse i = " + i.ToString() + " string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                             + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                             + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "], rel_diff = " + rel_diff.ToString());
            }
            if (success) int_count++;

            if(max_rel_err < rel_diff)
            {
                max_rel_err = rel_diff;
                max_err_id = i;
            }
        }
        Debug.Log("parsed int count = " + int_count.ToString() + " / " + n_numeric_string.ToString());
        Debug.Log("max relative error = " + max_rel_err.ToString() + " at " + str_list[max_err_id]);
        if (!test_pass)
        {
            Debug.LogError("the random value check was failure, fail_count=" + fail_count.ToString());
        }
    }
    void Test_ParseFloat64_String()
    {
        Debug.Log("== Parse Float64 test ==");

        Debug.Log("boundary value check");
        bool test_pass = true;

        str_native.Clear();
        str_list.Clear();

        string in_range_lo = "-1.797693134862e+308";
        string in_range_hi = "1.797693134862E+308";
        string out_range_lo = "-1.797693134863e+308";
        string out_range_hi = "1.797693134863E+308";

        str_list.Add(in_range_lo.ToString());
        str_list.Add(in_range_hi.ToString());
        str_list.Add(out_range_lo.ToString());
        str_list.Add(out_range_hi.ToString());

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

            Debug.Log("parse str = '" + str + "', try = " + success.ToString() + ", value = " + value.ToString());

            if (!this.EqualsDouble(value, value_e, 1.0e-14, out double rel_diff) || success != success_e)
            {
                test_pass = false;
                Debug.LogError("failed to parse. i = " + i.ToString() + " string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                             + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                             + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "], rel_diff = " + rel_diff.ToString());
            }
        }
        if (!test_pass)
        {
            Debug.LogError("the boundary value check was failure");
        }

        Debug.Log("random value check");
        this.GenerateRandomFloatStrings(n_numeric_string, 2, 18, 309, 64);

        test_pass = true;
        int fail_count = 0;

        double max_rel_err = 0.0;
        int max_err_id = 0;

        int int_count = 0;
        for (int i = 0; i < n_numeric_string; i++)
        {
            string str = str_list[i];
            ReadOnlyStringEntity str_e = str_native[i];

            bool success = double.TryParse(str, out double value);
            bool success_e = str_e.TryParse(out double value_e);

            if (!this.EqualsDouble(value, value_e, 1.0e-14, out double rel_diff) || success != success_e)
            {
                test_pass = false;
                fail_count++;
                Debug.LogError("failed to parse i = " + i.ToString() + " string [str/entity] = [" + str + "/" + str_e.ToString() + "]"
                             + "bool [str/entity] = [" + success.ToString() + "/" + success_e.ToString() + "], "
                             + "value [str/entity] = [" + value.ToString() + "/" + value_e.ToString() + "], rel_diff = " + rel_diff.ToString());
            }
            if (success) int_count++;

            if (max_rel_err < rel_diff)
            {
                max_rel_err = rel_diff;
                max_err_id = i;
            }
        }
        Debug.Log("parsed int count = " + int_count.ToString() + " / " + n_numeric_string.ToString());
        Debug.Log("max relative error = " + max_rel_err.ToString() + " at " + str_list[max_err_id]);
        if (!test_pass)
        {
            Debug.LogError("the random value check was failure, fail_count=" + fail_count.ToString());
        }
    }
    void Test_ParseHex_String()
    {
        Debug.Log("== Parse Hex format string test ==");
        bool test_pass;

        NativeStringList str_native_big = new NativeStringList();
        NativeStringList str_native_lit = new NativeStringList();

        // int hex
        Debug.Log("  >>>> hex convertion test for Int32 >>>>");
        int[] int_list = new int[] { 0, 128, 512, 10000, -12345678, -2147483648, 2147483647 };

        str_native_big.Clear();
        str_native_lit.Clear();
        test_pass = true;

        for(int i=0; i<int_list.Length; i++)
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
            int value_ref = int_list[i/2];
            ReadOnlyStringEntity str_lit = str_native_lit[i];
            ReadOnlyStringEntity str_big = str_native_big[i];

            bool success_lit = str_lit.TryParseHex(out int value_lit, Endian.Little);
            bool success_big = str_big.TryParseHex(out int value_big);

            Debug.Log("parse str[big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                    + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                    + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]" );

            if ( (value_ref != value_lit || value_ref != value_big) || success_lit != success_big)
            {
                test_pass = false;
                Debug.LogError("failed to parse. i = " + i.ToString()
                    + " string [big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                    + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                    + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");
            }
        }
        if (!test_pass)
        {
            Debug.LogError("the hex decode test for int was failure");
        }

        // long hex
        Debug.Log("  >>>> hex convertion test for Int64 >>>>");
        long[] long_list = new long[] { 0, 128, 512, 10000, -12345678, -9223372036854775808, 9223372036854775807 };

        str_native_big.Clear();
        str_native_lit.Clear();
        test_pass = true;

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

            bool success_lit = str_lit.TryParseHex(out long value_lit, Endian.Little);
            bool success_big = str_big.TryParseHex(out long value_big);

            Debug.Log("parse str[big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                    + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                    + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");

            if ((value_ref != value_lit || value_ref != value_big) || success_lit != success_big)
            {
                test_pass = false;
                Debug.LogError("failed to parse. i = " + i.ToString()
                    + " string [big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                    + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                    + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");
            }
        }
        if (!test_pass)
        {
            Debug.LogError("the hex decode test for int was failure");
        }

        // float hex
        Debug.Log("  >>>> hex convertion test for float >>>>");
        float[] float_list = new float[] { 0.0f, 128.0f, 512.0f, 12345.67f, -12345678f, -3.40281e+38f, 3.40281E+38f };

        str_native_big.Clear();
        str_native_lit.Clear();
        test_pass = true;

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

            bool success_lit = str_lit.TryParseHex(out float value_lit, Endian.Little);
            bool success_big = str_big.TryParseHex(out float value_big);

            Debug.Log("parse str[big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                    + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                    + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");

            // must be bit-complete convertion in hex data format
            if ((value_ref != value_lit || value_ref != value_big) || success_lit != success_big)
            {
                test_pass = false;
                Debug.LogError("failed to parse. i = " + i.ToString()
                    + " string [big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                    + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                    + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");
            }
        }
        if (!test_pass)
        {
            Debug.LogError("the hex decode test for int was failure");
        }

        // double hex
        Debug.Log("  >>>> hex convertion test for double >>>>");
        double[] double_list = new double[]
        {
            0.0, 128.0, 512.0, 12345.67, -12345678,
            -9223372036854775808d, 9223372036854775807d,
            -3.40281e+38, 3.40281E+38,
            -1.797693134862e+308, 1.797693134862E+308
        };

        str_native_big.Clear();
        str_native_lit.Clear();
        test_pass = true;

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

            bool success_lit = str_lit.TryParseHex(out double value_lit, Endian.Little);
            bool success_big = str_big.TryParseHex(out double value_big);

            Debug.Log("parse str[big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                    + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                    + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");

            // must be bit-complete convertion in hex data format
            if ((value_ref != value_lit || value_ref != value_big) || success_lit != success_big)
            {
                test_pass = false;
                Debug.LogError("failed to parse. i = " + i.ToString()
                    + " string [big/little] = [" + str_big.ToString() + "/" + str_lit.ToString()
                    + "], try[big/little] = [" + success_big.ToString() + "/" + success_lit.ToString()
                    + "], value[ref/big/little] = [" + value_ref.ToString() + "/" + value_big.ToString() + "/" + value_lit.ToString() + "]");
            }
        }
        if (!test_pass)
        {
            Debug.LogError("the hex decode test for int was failure");
        }


        str_native_big.Dispose();
        str_native_lit.Dispose();
    }

    void GenerateNumberedStrings()
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
            for(int jj = 1; jj<n_term; jj++)
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
    void GenerateRandomStrings()
    {
        if (n_char <= 0) return;
        Debug.Log(" == generate random string ==");

        str_native.Clear();
        str_list.Clear();

        int char_count = 0;
        int line_count = 0;

        List<char> tmp = new List<char>();
        for(int i=0; i<n_char; i++)
        {
            int code = random.Next(32, 130 + math.min(factor_delim, 0));  // ASCII char code: [32~126]
            if (code == 127) code = 9;                                    // 127 ->  9 (\t)
            if (code == 128) code = 10;                                   // 128 -> 10 (\n)

            code = math.min(code, 130);
            if (code >= 129 && tmp.Count > 0)                             // (code >= 129) are split strings
            {
                var str = new string(tmp.ToArray());
                tmp.Clear();

                this.str_native.Add(str);
                this.str_list.Add(str);
                line_count++;
            }
            else
            {
                tmp.Add( (char)code );
                char_count++;
            }
        }
        if(tmp.Count > 0)
        {
            var str = new string(tmp.ToArray());
            tmp.Clear();

            this.str_native.Add(str);
            this.str_list.Add(str);
            line_count++;
        }
        this.str_native.Add("7");
        this.str_list.Add("7");
        char_count++;
        line_count++;
        Debug.Log(" generated: " + line_count.ToString() + " strings, " + char_count.ToString() + " charactors.");
    }
    void GenerateRandomIntStrings(int n_str, int most_significant_digit, int digit_len, int fail_factor)
    {
        if (most_significant_digit < 1 || digit_len <= 4 || fail_factor < 1)
        {
            Debug.LogError("cannot perform GenerateRandomIntStrings");
            return;
        }

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

            for(int jj=0; jj<len; jj++)
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
    void GenerateRandomFloatStrings(int n_str, int most_significant_digit, int digit_len, int exp_range, int fail_factor)
    {
        if (most_significant_digit < 1 || digit_len <= 4 || exp_range < 1 || fail_factor < 1)
        {
            Debug.LogError("cannot perform GenerateRandomIntStrings");
            return;
        }

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
                if(jj == dot_pos)
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
            if(random.Next(0, 2) == 0)
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
    void GenerateRandomHex_Strings(int n_str, int max_hex_len, bool add_header)
    {
        if (n_str <= 0) return;
        str_native.Clear();
        str_list.Clear();

        List<char> tmp = new List<char>();
        for(int i=0; i<n_numeric_string; i++)
        {
            tmp.Clear();
            while (true)
            {
                int code = random.Next(0, 17);  // Hex range + spliter
                if(code >= 16)
                {
                    code = -1;
                }
                else if(code >= 10)
                {
                    code += 'A';
                }
                else
                {
                    code += '0';
                }

                if(code == -1 && tmp.Count > 1)
                {
                    break;
                }
                else
                {
                    tmp.Add( (char)code );
                    if (tmp.Count >= max_hex_len) break;
                }
            }
            
            var str = tmp.ToArray().ToString();
            if (add_header) str = "0x" + str;

            this.str_native.Add(str);
            this.str_list.Add(str);
        }
    }

    string InsertFailChar(string tgt, int fail_factor, char[] exclude_list, out bool inserted)
    {
        inserted = false;
        char c = 'z';
        if (random.Next(0, fail_factor) == 0)
        {
            //char c = (char)random.Next(33, 127);  // ASCII char code: [32~126], 32 is space.
            int i_try = 0;
            while(i_try < 10000)
            {
                i_try++;
                bool is_ex_char = false;

                c = (char)random.Next(33, 127);  // ASCII char code: [32~126], 32 is space.
                foreach(char ex in exclude_list)
                {
                    if(c == ex)
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
    string ConvertEndian(string value)
    {
        if(value.Length < 8 || value.Length % 2 != 0)
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

    private bool EqualsFloat(float a, float b, float rel_err, out float rel_diff)
    {
        rel_diff = 0.0f;
        float diff = a - b;
        if (diff == 0.0f) return true;

        if(a != 0.0f)
        {
            rel_diff = diff / a;
        }
        else
        {
            rel_diff = diff / b;
        }
        if (math.abs(rel_diff) < rel_err) return true;
        return false;
    }
    private bool EqualsDouble(double a, double b, double rel_err, out double rel_diff)
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
        if (math.abs(rel_diff) < rel_err) return true;
        return false;
    }
}
