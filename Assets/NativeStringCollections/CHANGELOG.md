# Changelog

## [1.2.6] - 2022-09-14
- Fixed for Unity 2021 LTS.

## [1.2.5] - 2022-04-06
- Fixed `StringEntity.AsArray()` and `NativeJaggedArraySlice.AsArray()` .

## [1.2.4] - 2022-04-03
- Fixed `StringEntity.GetHashCode()` .

## [1.2.3] - 2022-03-08
- Add comment and restructure files.
- Add error check in AsyncTextFileLoader<T> .

## [1.2.2] - 2022-03-02
- Removed macros in `csFastFloat` .
- Removed unused part (ASCII/UTF-8 implementation) of `csFastFloat` .
- Removed `IParseExt` and use `IJaggedArraySliceBase<Char16>` .
- Changed folder structure.

## [1.2.1] - 2022-03-02
- Fixed the test code `Test_StringParser.cs` .
- Update `README` .

## [1.2.0] - 2022-03-01
- Modified `StringEntity.TryParse()` are updated as fast implementations.
- Changed `Define.DefaultDecodeBlock`.

## [1.1.4] - 2022-03-01
- Fixed `AsyncTextFileReader<T>.Dispose()`, `AsyncTextFileReader<T>.Complete()` .
- Added `AsyncTextFileLoader<T>.Complete()` .
- Refactored `NativeJaggedArray<T>.IndexOf()` , `NativeJaggedArraySlice<T>.IndexOf()` .
- Added `NativeJaggedArray<T>.LastIndexOf()` , `NativeJaggedArraySlice<T>.LastIndexOf()` .

## [1.1.3] - 2022-02-22
- Fixed `NativeJaggedArray.IndexOf()` . boxing was removed.

## [1.1.2] - 2022-02-18
- Patching for `Unity.Collections` 0.17 and 1.1 on Unity 2020.3.

## [1.1.1] - 2022-01-17
- Refactored `NativeJaggedArray<T>` and `NativeJaggedArraySlice<T>`. now the `IEquatable<T>` is not required for them.

## [1.1.0] - 2022-01-17
- Added `FilePathUtility.Sort()` for sorting file paths.

## [1.0.0] - 2021-04-25
### This is the first release of *NativeStringCollections* .
- Added support for Unity Package Manager.
