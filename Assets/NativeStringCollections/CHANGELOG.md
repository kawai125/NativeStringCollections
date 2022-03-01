# Changelog

## [1.2.0] - 2022-03-01
- Modified `StringEntity.TryParse()` are updated as fast implementations.
- Changed `Define.DefaultDecodeBlock` is changed.

## [1.1.4] - 2022-03-01
- Fixed `AsyncTextFileReader<T>.Dispose()`, `AsyncTextFileReader<T>.Complete()`.
- Added `AsyncTextFileLoader<T>.Complete()`.
- Refactored `NativeJaggedArray<T>.IndexOf()`, `NativeJaggedArraySlice<T>.IndexOf()`.
- Added `NativeJaggedArray<T>.LastIndexOf()`, `NativeJaggedArraySlice<T>.LastIndexOf()`.

## [1.1.3] - 2022-02-22
- Fixed `NativeJaggedArray.IndexOf()`. boxing was removed.

## [1.1.2] - 2022-02-18
- Patching for `Unity.Collections` 0.17 and 1.1 on Unity 2020.3.

## [1.1.1] - 2022-01-17
- Refactored `NativeJaggedArray<T>` and `NativeJaggedArraySlice<T>`. now the `IEquatable<T>` is not required for them.

## [1.1.0] - 2022-01-17
- Added `FilePathUtility.Sort()` for sorting file paths.

## [1.0.0] - 2021-04-25
### This is the first release of *NativeStringCollections*.
- Added support for Unity Package Manager.
