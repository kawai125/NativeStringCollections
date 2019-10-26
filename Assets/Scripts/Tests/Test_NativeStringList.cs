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
    public void OnClickParseStringTest()
    {
        //--- parse int
        this.Test_ParseInt32_String();
        this.Test_ParseInt64_String();

        //--- parse float
        this.Test_ParseFloat32_String();
        this.Test_ParseFloat64_String();

        //--- parse hex
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

        str_list.Add(in_range_lo.ToString());
        str_list.Add(in_range_hi.ToString());
        str_list.Add(out_range_lo.ToString());
        str_list.Add(out_range_hi.ToString());

        for(int i=0; i<str_list.Count; i++)
        {
            str_native.Add(str_list[i]);
        }

        for(int i=0; i<str_native.Length; i++)
        {
            var str = str_list[i];
            ReadOnlyStringEntity str_e = str_native[i];

            bool success = int.TryParse(str, out int value);
            bool success_e = str_e.TryParse(out int value_e);

            Debug.Log("parse str = '" + str + "', try = " + success.ToString());

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

            Debug.Log("parse str = '" + str + "', try = " + success.ToString());

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

    }
    void Test_ParseFloat64_String()
    {

    }
    void Test_ParseHex_String()
    {

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
        if (digit_len <= 4 || fail_factor < 1)
        {
            Debug.LogError("cannot perform GenerateRandomIntStrings");
            return;
        }

        str_list.Clear();
        str_native.Clear();

        for (int ii = 0; ii < n_str; ii++)
        {
            string str = "";

            if(random.Next(0, 2) == 0)
            {
                str += '-';
            }

            str += random.Next(0, most_significant_digit + 1).ToString();

            int len = random.Next(digit_len - 4, digit_len);
            for(int jj=0; jj<len; jj++)
            {
                if (random.Next(0, fail_factor) == 0) 
                {
                    char c = (char)random.Next(33, 127);  // ASCII char code: [32~126], 32 is space.
                    str += c;
                }
                else
                {
                    str += random.Next(0, 10).ToString();
                }
            }

            str_list.Add(str);
            str_native.Add(str);
        }
    }
    void GenerateRandomFloatStrings(bool exp_camel_mark)
    {

    }
    void GenerateRandomHex_Strings(int max_hex_len, bool add_header)
    {
        if (n_numeric_string <= 0) return;
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
}
