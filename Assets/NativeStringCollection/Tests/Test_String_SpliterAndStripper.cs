using System.Text;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Unity.Collections;

using NativeStringCollections;
using NativeStringCollections.Utility;

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

        NativeList<char> NL_source = new NativeList<char>(Allocator.Persistent);
        foreach(char c in str_source)
        {
            NL_source.Add(c);
        }
        StringEntity SE_source = NL_source.ToStringEntity();

        var ref_list = new List<string>();
        NativeStringList NSL_result = new NativeStringList(Allocator.Persistent);

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

        NativeList<char> NL_delim = new NativeList<char>(Allocator.Persistent);
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

        StringSpliter spliter = new StringSpliter(Allocator.Persistent);
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
    }
    public void OnClickStringStripperTest()
    {

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

            sb.Append("[ ");
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
    private bool CheckStripperResult(IEnumerable<char> result, string ref_data)
    {
        return true;
    }
}
