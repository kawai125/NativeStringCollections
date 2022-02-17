# NativeStringCollections

## Introduction
The toolset to parse generic text files using C# JobSystem and Burst on Unity.

解説記事(日本語)はこちら:  
  [JobSystem編](https://qiita.com/kawai125/items/13390f25700dd89c0f2e)  
  [Burst編](https://qiita.com/kawai125/items/540dd8e5d2b4c7c1fa3b)

## Environment
This library was tested in the below system.

- Unity 2019.4.35f1
  - Collections 0.9.0-preview.6
  - Burst 1.4.11


- Unity 2020.3.25f1
  - Collections 0.17.0 or 1.1
  - Burst 1.6.3

## Git Path for Unity Package Manager
[https://github.com/kawai125/NativeStringCollections.git?path=Assets/NativeStringCollections](https://github.com/kawai125/NativeStringCollections.git?path=Assets/NativeStringCollections)

## Demo scene

- single file & single data user demo:  
`/Assets/NativeStringCollections/Samples/Demo/Scenes/Demo_ReadingLargeFile.unity`

- multiple files & multiple data users demo:  
`/Assets/NativeStringCollections/Samples/Demo/Scenes/Demo_AsyncMultiFileManagement.unity`

## Performance

Target file: 500k charactors (with comment lines and Base64 encoded external data, total 590k lines & 37MB size).  
  The sample file generator was implemented in demo scenes.

Measured environment:
  - Windows10
  - Ryzen5 3600X
  - GTX 1070
  - NVMe SSD (PCIe Gen3 x4)

(1) Single file loading performance:

|condition|Time [ms]|
|:-|-:|
|C# standard: `File.ReadAllLines()`|710 ~ 850|
|`ITextFileParser` (without Burst)|460 ~ 480|
|`ITextFileParser` (with Burst)|240 ~ 250|

(2) Parallel file loading performance:

|# of Parallel Jobs|Time [ms] with Burst|Time [ms] without Burst|
|-:|-:|-:|
|1|240 ~ 250|460 ~ 480|
|2|245 ~ 270|460 ~ 500|
|3|245 ~ 275|470 ~ 540|
|4|255 ~ 290|500 ~ 580|
|6|270 ~ 350|550 ~ 650|
|8|300 ~ 390|600 ~ 700|

## Usage

```C#
using NativeStringCollections;

public class TextData : ITextFileParser
{
    NativeList<DataElement> Data;

    public void Init()
    {
        /* initialize class. called at once after new(). */
        /* managed types can be used here because this function called in main thread. */
    }

    public void Clear()
    {
        /* preparing to parse. called at once before start calling ParseLines(). */
    }

    public bool ParseLines(NativeStringList lines)
    {
        for(int i=0; i<lines.Length; i++)
        {
            var line = lines[i];
            /* parse line. return true to read next block. */
            /* if you returned false, calling ParseLines() will be stopped and jump to PostReadProc(). */
        }
    }

    public void PostReadProc()
    {
        /* post reading process. called at once after calling ParseLines(). */
    }

    public void UnLoad()
    {
        /* write somethong to do for unloading data. */
    }
}

public class Hoge : MonoBehaviour
{
    AsyncTextFileReader<TextData> reader;

    void Start() { reader = new AsyncTextFileReader<TextData>(Allocator.Persistent); }

    void OnClickLoadFile()
    {
        // ordering to Load file. (give Encoding if necessarily)
        reader.Encoding = Encoding.UTF8;
        reader.LoadFile(path);
    }

    void Update()
    {
        // it can display progress. (Read, Length field is int by BlockSize unit)
        var info = reader.GetState
        float progress = (float)info.Read / info.Length;

        // call Complete() when the job finished.
        if(reader.JobState == ReadJobState.WaitForCallingComplete)
        {
            reader.Complete();

            // it can display the elapsed time [ms] for loading file.
            double delay = reader.GetState.Delay;
            Debug.Log($" file loading completed. time = {delay.ToString("F2")} [ms].");

            // something to do with loaded data.
            var data = reader.Data;
        }
    }

    void OnDestroy()
    {
        // calling Dispose() of data class. there are no ordering to dispose reader and loaded data.
        var data = reader.Data;  
        reader.Dispose();

        data.Dispose();
    }
}
```

More detailed sample for `ITextFileParser.ParseLines()` is shown in below.

```C#
using NativeStringCollections;

public class TextData : ITextFileParser
{
    public NativeList<DataElement> Data;

    private NativeStringList mark_list;
    private StringEntity check_mark;

    // if you want to use string in parse process,
    // add string into NativeStringList or NativeList<Char16> in Init().
    public void Init()
    {
        Data = new NativeList<DataElement>(Allocator.Persistent);
        mark_list = new NativeStringList(Allocator.Persistent);

        mark_list.Add("STRONG");
        mark_list.Add("Normal")

        // to pick StringEntity from NativeStringList, it must be after all data were inputed into NativeStringList.
        // (or set Capacity enough to large to contain all elements at first.)
        // if you access StringEntity after buffer reallocating in NativeStringList, it causes crash by invalid memory access.
        check_mark = mark_list[0];
    }

    // LF and CR were parsed and the results were input into NativeStringList.
    // use it as similar to List<string>.
    public bool ParseLines(NativeStringList lines)
    {
        bool continueRead = true;
        for(int i=0; i<lines.Length; i++)
        {
            var line = lines[i];
            continueRead = this.ParseLineImpl(line);
            if(!continueRead) return false;  // abort to read
        }
        return true;
    }
    private bool ParseLineImpl(ReadOnlyStringEntity line)
    {
        // this list recieves the result of StringEntity.Split().
        var str_list = new NativeList<ReadOnlyStringEntity>(Allocator.Temp);

        // in the case of input line = "CharaName_STRONG,11,64,15.7,1.295e+3" as CSV,
        // you can parse as shown in below.
        line.split(',', str_list);

        var name = str_list[0];

        bool success = true;
        success = success && str_list[1].TryParse(out long ID);
        success = success && str_list[2].TryParse(out int HP);
        success = success && str_list[3].TryParse(out float Attack);
        success = success && str_list[4].TryParse(out double Speed);

        int mark_index = name.IndexOf(check_mark);  // search "STRONG" in `name`
        if(mark_index >= 0)
        {
            /* specified treat for "STRONG" charactor. */
        }

        str_list.Dispose()

        // check to parse the line correctly or not.
        if(!success)
            return false;  // failed to parse. abort.

        Data.Add(new DataElement(ID, HP, Attack, Speed));
        return true;  // success to parse. go next line.
    }
}
```

## Usage for Burst optimization

In this library, the UInt16 based struct `Char16` is used instead of `System.Char` .  
Thus, you can use [Burst function pointers](https://docs.unity3d.com/Packages/com.unity.burst@1.4/manual/docs/AdvancedUsages.html#function-pointers) to optimize your `ITextFileParser` class.

If you use Burst 1.6 or later, you can use [Burst direct-call.](https://docs.unity3d.com/Packages/com.unity.burst@1.6/manual/docs/CSharpLanguageSupport_Lang.html#directly-calling-burst-compiled-code)  
It is easier to write code then Burst function pointers and has same features.

(see `/Assets/NativeStringCollections/Samples/Scripts/CharaDataParser.cs` for sample.)

## API

### ▽Namespace

  All implementations are written in the namespace `NativeStringCollections`

### ▽Job scheduler

```C#
class AsyncTextFileReader<T>  // for single file
class AsyncTextFileLoader<T>  // for multi files and users

where T : class, ITextFileParser, new()
```

These classes can accept `class System.Text.Encoding` to decode byte stream into chars.

The `ITextFileParser` is defined as below.

```C#
namespace NativeStringCollections
{
    public interface ITextFileParser
    {
        /// <summary>
        /// called once at the first in main thread (you can use managed object in this function).
        /// </summary>
        void Init();

        /// <summary>
        /// called every time at first on reading file.
        /// </summary>
        void Clear();

        /// <summary>
        /// when you returned 'false', the AsyncTextFileLoader discontinue calling the 'ParseLines()'
        /// and jump to calling 'PostReadProc()'.
        /// </summary>
        /// <param name="lines"></param>
        /// <returns>continue reading lines or not.</returns>
        bool ParseLines(NativeStringList lines);

        /// <summary>
        /// called every time at last on reading file.
        /// </summary>
        void PostReadProc();

        /// <summary>
        /// called when the AsyncTextFileLoader.UnLoadFile(index) function was called.
        /// </summary>
        void UnLoad();
    }
}
```

### ▽String like NativeContainer

```C#
struct NativeStringList
struct StringEntity
struct ReadOnlyStringEntity
```

The `NativeStringList` is a jagged array container similar to `List<string>`, using `NativeList<Char16>` internally.  
`StringEntity` and `ReadOnlyStringEntity` are the slice view of `NativeStringList`.

**Note:** Because of reallocation of internal buffer, calling `NativeStringList.Add()` function makes `StringEntity` to invalid reference.  
(The tracer system is also implemented on the macro "ENABLE_UNITY_COLLECTIONS_CHECKS".)

### ▽Parse functions

```C#
bool StringEntity.TryParse(out T value)
bool StringEntity.TryParseHex(out T value)

where T : int32, int64, float32, or float64
```

The conversion accuracy compared with `System.T.Parse()` is shown in below:

|type|relative error|
|:--|:--|
|int32, int64, and float32| no differ |
|float64| < 1.0e-15 |
|(Hex input)|no differ|

Tested in thousands random strings.

(see `/Assets/NativeStringCollections/Tests/EditMode/Editor/Test_StringParser.cs` for more details.)

The converters between Base64 encoded string and byte stream are also available.

```C#
struct NativeBase64Encoder
struct NativeBase64Decoder
```

### ▽manipulation functions

```C#
// Split()
var split_result = new NativeList<StringEntity>(Allocator.Temp);
StringEntity.Split(delim, split_result);  // delim: a char or StringEntity.
StringEntity.Split(split_result);         // split by Char.IsWhiteSpace()

// Strip(), Lstrip() and Rstrip()
StringEntity strip_result = StringEntity.Strip(delim);  //  delim: a char or StringEntity.
StringEntity strip_result = StringEntity.Strip();       //  strip for Char.IsWhiteSpace() in both side.

// Slice()
StringEntity slice_result = StringEntity.Slice(begin, end);
```

These modification functions are available.
These functions generate `StringEntity` as new slice.

### ▽Utility for using Burst Function Pointers

In Burst Function Pointers, typical NativeContainer cannot be used because their safety system is implemented with managed object.  
To workaround this probrem, UnsafeReference utility structs and functions are provided.

```C#
using NativeStringCollections;
using NativeStringCollections.Utility;

// for container
UnsafeRefToNativeList<T>
    ref_to_native_list = NativeList<T>.GetUnsafeRef();
UnsafeRefToNativeStringList
    ref_to_native_string_list = NativeStringList.GetUnsafeRef();
UnsafeRefToNativeJaggedList<T>
    ref_to_native_jagged_list = NativeJaggedList<T>.GetUnsafeRef();

// for Base64 converter
UnsafeRefToNativeBase64Encoder
    ref_to_base64_encoder = NativeBase64Encoder.GetUnsafeRef();
UnsafeRefToNativeBase64Decoder
    ref_to_base64_decoder = NativeBase64Decoder.GetUnsafeRef();
```

Unfortunately, the other than `NativeList<T>` has no accessor to internal unsafe container.  
If you want to use these container such as `NativeHashMap<Tkey, TValue>` or `NativeQueue<T>`,  
you have to use compatible unsafe container such as `UnsafeHashMap<Tkey, TValue>` or `UnsafeRingQueue<T>` and copy data before/after calling Burst function pointers.

### ▽Utility for Sorting file paths
The sort function which treats a digits part as an integer and sorts naturally is provided.

```C#
string[] paths = { /* paths */ };
var sorted_paths = new List<string>();

// sort file paths naturally
FilePathUtility.Sort(paths, sorted_paths);

// sort & filtering paths
string filter = directly + "/file_00.dat";   // digit part "00" is treated as a place holder for integer value.
FilePathUtility.Sort(paths, filter, sorted_paths);
```

(see `/Assets/NativeStringCollections/Tests/EditMode/Editor/Test_SortFilePaths.cs` for more details.)

### ▽Debug mode

```C#
var reader = new AsyncTextFileReader<T>(Allocator.Persistent);
var loader = new AsyncTextFileLoader<T>(Allocator.Persistent);

reader.LoadFileInMainThread(path);

loader.AddFile(new List<string>{path_1, path_2, path_3});
loader.LoadFileInMainThread(0);  // index = 0: load path_1.
```

When use the function `LoadFileInMainThread()`, all functions are processed in the main thread.

In this condition, managed objects such as `(obj).ToString()`, `StringBuilder`, and `Debug.Log()` can be used
in `Clear()`, `ParseLine()`, `PostReadProc()`, and `UnLoad()` functions of `ITextFileParser`.
