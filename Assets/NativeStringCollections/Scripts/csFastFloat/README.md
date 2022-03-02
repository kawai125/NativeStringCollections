# csFastFloat - modified

## Introduction
this is modified library of csFastFloat for NativeStringCollections.StringEntity and Char16 interfaces.

## Original
[csFastFloat](https://github.com/CarlVerret/csFastFloat)

## Modified Points

### ◇ Behaviour
  - When found nun-numeric char after some numeric input.  
    Return parsed input (Original) -> Fail to parse (Modified)  

    The `TryParse()` should judge the input string is numeric or not.  
    Other APIs, such as `IndexOf()` and `Split()`, are responsible for searching and splitting strings.

  - Delete compatibility of `System.Globalization.NumberStyles` .  
      The `NumberStyles` is an enum, however, the API `NumberStyles.HasFlag()` was implemented as class.

  - Delete ASCII/UTF-8 implementation.  
    This variant accepts UTF-16 string only.


### ◇ Implement
  - struct `char` -> struct `Char16`
  - `ReadOnlySpan<T>` -> `static readonly T[]`
  - `string` literal -> `static readonly Char16[]`
  - `Debug.Asert()` -> `[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")] static void CheckFunc()`


  These implementation changes are for compatibility with Burst Compiler.
