# NativeStringCollections

## Introduction
The toolset to parse generic text files using C# JobSystem on Unity.

## Environment
This library was tested in the below system.

- software
  - Windows10 20H2 19042.804
  - Unity 2019.4.20f1
    - Collections 0.9.0-preview.6


## Demo scene

- single file & single data user demo:  
`/Assets/NativeStringCollections/Demo/Scenes/Demo_ReadingLargeFile.unity`

- multiple files & multiple data users demo:  
`/Assets/NativeStringCollections/Demo/Scenes/Demo_AsyncMultiFileManagement.unity`

## API

- namespace

  All implementations are written in the namespace `NativeStringCollections`

- job scheduler

```C#
class AsyncTextFileReader<T>  // for single file
class AsyncTextFileLoader<T>  // for multi files and users

where T : class, ITextFileParser, new()
```

These classes can accept `class System.Text.Encoding` to decode byte stream into chars.

- string like NativeContainer

```C#
struct NativeStringList
struct StringEntity
struct ReadOnlyStringEntity
```

The `NativeStringList` is a jagged array container similar to `List<string>`, using `NativeList<char>` internally.  
`StringEntity` and `ReadOnlyStringEntity` are the slice view of `NativeStringList`.

**Note:** Because of reallocation of internal buffer, after calling `NativeStringList.Add()` function makes `StringEntity` to invalid reference.  
(The tracer system is also implemented on the macro "ENABLE_UNITY_COLLECTIONS_CHECKS".)

- parse functions

```C#
bool StringEntity.TryParse(out T value)
bool StringEntity.TryParseHex(out T value)

where T : int32, int64, float32, or float64
```

The conversion accuracy compared with `System.T.Parse()` is shown in below:

|type|error|
|:--|:--|
|int32, int64, and float32| no differ |
|float64| < 1.0e-15 |
|(Hex input)|no differ|

tested in thousands random strings.

(see `/Assets/NativeStringCollections/Tests/EditMode/Editor/Test_StringParser.cs` for more detail)

The converters between Base64 encoded string and byte stream are also available.

```C#
struct NativeBase64Encoder
struct NativeBase64Decoder
```

- manipulation functions

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

## Limitation
When Loading 2 files or more in same timing using `AsyncTextFileLoader<T>`, that causes laoding files with large delay.  
N files loading may be slown as N times slower. (total time for parse file is same to when running only 1 job.)  
I cannot found the cause of this delay.  
Is it memory transfer bound ?
