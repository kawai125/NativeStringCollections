using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Unity.Collections;

using NativeStringCollections;
using NativeStringCollections.Utility;
using Unity.Collections.LowLevel.Unsafe;

public class Test_String_SpliterAndStripper : MonoBehaviour
{
    // Start is called before the first frame update
    public void Start()
    {

    }

    // Update is called once per frame
    public void Update()
    {

    }

    public void OnClickStringSpliterTest()
    {
        string str_source = " 1234567890@@0987654321^^ 1234567\t890 # ";

        NativeList<char> NL_source = new NativeList<char>(Allocator.TempJob);
        foreach(char c in str_source)
        {
            NL_source.Add(c);
        }
        StringEntity SE_source = NL_source.ToStringEntity();

        var ref_list = new List<string>();
        var NSL_result = new NativeStringList(Allocator.TempJob);
        var NL_SE_result = new NativeList<StringEntity>(Allocator.TempJob);

        //-------------------------------------------------------------------------------
        // split by char.IsWhiteSpace()
        Debug.Log("== Test: split by char.IsWhiteSpace().");

        ref_list.Clear();
        ref_list.Add("1234567890@@0987654321^^");
        ref_list.Add("1234567");
        ref_list.Add("890");
        ref_list.Add("#");

        Debug.Log("  >> try NativeList<char>.Split(result) >>");
        NSL_result.Clear();
        NL_source.Split(NSL_result);
        this.CheckSpliterResult(NSL_result, ref_list);

        Debug.Log("  >> try StringEntity.Split(result) >>");
        NSL_result.Clear();
        SE_source.Split(NSL_result);
        this.CheckSpliterResult(NSL_result, ref_list);

        Debug.Log("  >> try StringEntity.Split(result) (result: NativeList<StringEntity>) >>");
        NL_SE_result.Clear();
        SE_source.Split(NL_SE_result);
        this.CheckSpliterResult(NL_SE_result, ref_list);

        //-------------------------------------------------------------------------------
        // split by single charactor
        Debug.Log("== Test: split by single charactor.");

        ref_list.Clear();
        ref_list.Add(" 1234567890");
        ref_list.Add("0987654321^^ 1234567\t890 # ");

        Debug.Log("  >> try NativeList<char>.Split(result) >>");
        NSL_result.Clear();
        NL_source.Split('@', NSL_result);
        this.CheckSpliterResult(NSL_result, ref_list);

        Debug.Log("  >> try StringEntity.Split(result) >>");
        NSL_result.Clear();
        SE_source.Split('@', NSL_result);
        this.CheckSpliterResult(NSL_result, ref_list);

        ref_list.Clear();
        ref_list.Add(" 12345");
        ref_list.Add("7890@@0987");
        ref_list.Add("54321^^ 12345");
        ref_list.Add("7\t890 # ");

        Debug.Log("  >> try NativeList<char>.Split(char, result) >>");
        NSL_result.Clear();
        NL_source.Split('6', NSL_result);
        this.CheckSpliterResult(NSL_result, ref_list);

        Debug.Log("  >> try StringEntity.Split(char, result) >>");
        NSL_result.Clear();
        SE_source.Split('6', NSL_result);
        this.CheckSpliterResult(NSL_result, ref_list);

        //-------------------------------------------------------------------------------
        // split by string
        Debug.Log("== Test: split by string.");

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
        this.CheckSpliterResult(NSL_result, ref_list);

        Debug.Log("  >> try StringEntity.Split(NativeList<char>, result) >>");
        NSL_result.Clear();
        SE_source.Split(NL_delim, NSL_result);
        this.CheckSpliterResult(NSL_result, ref_list);

        NL_delim.Dispose();

        //-------------------------------------------------------------------------------
        // split by struct StringStripper()
        Debug.Log("== Test: split by struct StringStripper().");

        StringSpliter spliter = new StringSpliter(Allocator.TempJob);
        spliter.AddDelim("@@");
        spliter.AddDelim("678");
        spliter.AddDelim("8");
        spliter.AddDelim("987");

        ref_list.Clear();
        ref_list.Add(" 12345");
        ref_list.Add("90");
        ref_list.Add("0");
        ref_list.Add("654321^^ 1234567\t");
        ref_list.Add("90 # ");

        Debug.Log("  >> try StringSpliter.Split(NativeList<char>, result) >>");
        NSL_result.Clear();
        spliter.Split(NL_source, NSL_result);
        this.CheckSpliterResult(NSL_result, ref_list);

        Debug.Log("  >> try StringSpliter.Split(StringEntity, result) >>");
        NSL_result.Clear();
        spliter.Split(SE_source, NSL_result);
        this.CheckSpliterResult(NSL_result, ref_list);


        spliter.Dispose();


        NL_source.Dispose();
        NSL_result.Dispose();
        NL_SE_result.Dispose();
    }
    public void OnClickStringStripperTest()
    {
        string str_source;
        string ref_Lstrip;
        string ref_Rstrip;
        string ref_Strip;

        var NL_source = new NativeList<char>(Allocator.TempJob);
        var NL_result = new NativeList<char>(Allocator.TempJob);

        StringEntity SE_source;
        StringEntity SE_result;

        //-------------------------------------------------------------------------------
        // strip by char.IsWhiteSpace()
        Debug.Log("== Test: strip by char.IsWhiteSpace().");

        str_source = "  \t \n something string\tsample \t\t";
        ref_Lstrip = "something string\tsample \t\t";
        ref_Rstrip = "  \t \n something string\tsample";
        ref_Strip = "something string\tsample";

        NL_source.Clear();
        foreach (char c in str_source) NL_source.Add(c);
        SE_source = NL_source.ToStringEntity();

        Debug.Log("  >> try NativeList<char>.Lstrip(result) >>");
        NL_source.Lstrip(NL_result);
        this.CheckStripperResult(NL_result, ref_Lstrip);

        Debug.Log("  >> try result = StringEntity.Lstrip() >>");
        SE_result = SE_source.Lstrip();
        this.CheckStripperResult(SE_result, ref_Lstrip);


        Debug.Log("  >> try NativeList<char>.Rstrip(result) >>");
        NL_source.Rstrip(NL_result);
        this.CheckStripperResult(NL_result, ref_Rstrip);

        Debug.Log("  >> try result = StringEntity.Rstrip() >>");
        SE_result = SE_source.Rstrip();
        this.CheckStripperResult(SE_result, ref_Rstrip);


        Debug.Log("  >> try NativeList<char>.Strip(result) >>");
        NL_source.Strip(NL_result);
        this.CheckStripperResult(NL_result, ref_Strip);

        Debug.Log("  >> try result = StringEntity.Strip() >>");
        SE_result = SE_source.Strip();
        this.CheckStripperResult(SE_result, ref_Strip);

        //-------------------------------------------------------------------------------
        // strip by single charactor
        Debug.Log("== Test: strip by single charactor.");

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
        this.CheckStripperResult(NL_result, ref_Lstrip);

        Debug.Log("  >> try result = StringEntity.Lstrip(char) >>");
        SE_result = SE_source.Lstrip(c_target);
        this.CheckStripperResult(SE_result, ref_Lstrip);


        Debug.Log("  >> try NativeList<char>.Rstrip(char, result) >>");
        NL_source.Rstrip(c_target, NL_result);
        this.CheckStripperResult(NL_result, ref_Rstrip);

        Debug.Log("  >> try result = StringEntity.Rstrip(char) >>");
        SE_result = SE_source.Rstrip(c_target);
        this.CheckStripperResult(SE_result, ref_Rstrip);


        Debug.Log("  >> try NativeList<char>.Strip(char, result) >>");
        NL_source.Strip(c_target, NL_result);
        this.CheckStripperResult(NL_result, ref_Strip);

        Debug.Log("  >> try result = StringEntity.Strip(char) >>");
        SE_result = SE_source.Strip(c_target);
        this.CheckStripperResult(SE_result, ref_Strip);

        //-------------------------------------------------------------------------------
        // strip by string
        Debug.Log("== Test: strip by string.");

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
        this.CheckStripperResult(NL_result, ref_Lstrip);

        Debug.Log("  >> try result = StringEntity.Lstrip(NativeList<char>) >>");
        SE_result = SE_source.Lstrip(NL_target);
        this.CheckStripperResult(SE_result, ref_Lstrip);

        Debug.Log("  >> try result = StringEntity.Lstrip(StringEntity) >>");
        SE_result = SE_source.Lstrip(SE_target);
        this.CheckStripperResult(SE_result, ref_Lstrip);


        Debug.Log("  >> try NativeList<char>.Rstrip(NativeList<char>, result) >>");
        NL_source.Rstrip(NL_target, NL_result);
        this.CheckStripperResult(NL_result, ref_Rstrip);

        Debug.Log("  >> try result = StringEntity.Rstrip(NativeList<char>) >>");
        SE_result = SE_source.Rstrip(NL_target);
        this.CheckStripperResult(SE_result, ref_Rstrip);

        Debug.Log("  >> try result = StringEntity.Rstrip(StringEntity) >>");
        SE_result = SE_source.Rstrip(SE_target);
        this.CheckStripperResult(SE_result, ref_Rstrip);


        Debug.Log("  >> try NativeList<char>.Strip(NativeList<char>, result) >>");
        NL_source.Strip(NL_target, NL_result);
        this.CheckStripperResult(NL_result, ref_Strip);

        Debug.Log("  >> try result = StringEntity.Strip(NativeList<char>) >>");
        SE_result = SE_source.Strip(NL_target);
        this.CheckStripperResult(SE_result, ref_Strip);

        Debug.Log("  >> try result = StringEntity.Strip(StringEntity) >>");
        SE_result = SE_source.Strip(SE_target);
        this.CheckStripperResult(SE_result, ref_Strip);

        NL_target.Dispose();


        NL_source.Dispose();
        NL_result.Dispose();
    }

    private bool CheckSpliterResult(NativeStringList result, List<string> ref_data)
    {
        var sb = new StringBuilder();

        bool check = true;
        if(result.Length != ref_data.Count)
        {
            sb.Append("    !! the element number was differ."
                      + " result: " + result.Length.ToString()
                      + ", ref: " + ref_data.Count.ToString() + "\n");
            check = false;
        }

        int len = Mathf.Max(result.Length, ref_data.Count);

        sb.Append("    elements [result/ref] = {\n");
        for(int i=0; i<len; i++)
        {
            bool local_check = true;
            if (i < result.Length && i < ref_data.Count)
            {
                if (result[i] != ref_data[i]) check = local_check = false;
            }

            sb.Append("   [ ");
            if(i < result.Length) sb.Append(result[i]);
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
    private bool CheckSpliterResult<T>(NativeList<T> result, List<string> ref_data) where T : unmanaged, IStringEntityBase, IEquatable<string>
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
                if ( !result[i].Equals(ref_data[i]) ) check = local_check = false;
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
    private unsafe bool CheckStripperResult(NativeList<char> result, string ref_data)
    {
        return this.CheckStripperResultImpl((char*)result.GetUnsafePtr(), result.Length, ref_data);
    }
    private unsafe bool CheckStripperResult(IStringEntityBase result, string ref_data)
    {
        return this.CheckStripperResultImpl((char*)result.GetUnsafePtr(), result.Length, ref_data);
    }
    private unsafe bool CheckStripperResultImpl(char* res_ptr, int res_len, string ref_data)
    {
        var sb = new StringBuilder();

        sb.Append("   -- compare elem [result/ref]={\n");

        bool check = true;

        int max_len = Mathf.Max(res_len, ref_data.Length);
        for(int i=0; i< max_len; i++)
        {
            char c = res_ptr[i];

            if(i >= res_len || i >= ref_data.Length)
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
