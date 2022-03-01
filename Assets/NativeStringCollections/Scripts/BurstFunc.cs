using System;

using UnityEngine;

using Unity.Collections;
using Unity.Burst;

namespace NativeStringCollections
{
    using NativeStringCollections.Utility;
    using NativeStringCollections.Impl;

    public unsafe static class BurstFunc
    {

        // public interface

        /// <summary>
        /// Try to parse StringEntity to bool. Cannot accept whitespaces (this is differ from official C# bool.TryParse()).
        /// This function calls the delegate of BurstCompiler.CompileFunctionPointer().
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static bool TryParse<T>(T source, out bool result)
            where T : IParseExt
        {
            TryParseBurstCompile._tryParseBoolDelegate((Char16*)source.GetUnsafePtr(),
                                                       source.Length,
                                                       out bool success, out result);
            return success;
        }
        /// <summary>
        /// Try to parse StringEntity to Int32. Cannot accept whitespaces and hex format (this is differ from official C# int.TryParse()). 
        /// Use TryParseHex(out T) for hex data.
        /// This function calls the delegate of BurstCompiler.CompileFunctionPointer().
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static bool TryParse<T>(T source, out int result)
            where T : IParseExt
        {
            TryParseBurstCompile._tryParseInt32Delegate((Char16*)source.GetUnsafePtr(),
                                                        source.Length,
                                                        out bool success, out result);
            return success;
        }
        /// <summary>
        /// Try to parse StringEntity to Int64. Cannot accept whitespaces and hex format (this is differ from official C# int.TryParse()). 
        /// Use TryParseHex(out T) for hex data.
        /// This function calls the delegate of BurstCompiler.CompileFunctionPointer().
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static bool TryParse<T>(T source, out long result)
            where T : IParseExt
        {
            TryParseBurstCompile._tryParseInt64Delegate((Char16*)source.GetUnsafePtr(),
                                                        source.Length,
                                                        out bool success, out result);
            return success;
        }
        /// <summary>
        /// Try to parse StringEntity to float.
        /// Cannot accept whitespaces, comma insertion, and hex format (these are differ from official C# float.TryParse()).
        /// Use TryParseHex(out T) for hex data.
        /// This function calls the delegate of BurstCompiler.CompileFunctionPointer().
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static bool TryParse<T>(T source, out float result)
            where T : IParseExt
        {
            result = 0.0f;

            TryParseBurstCompile._tryParseFloat64Delegate((Char16*)source.GetUnsafePtr(),
                                                          source.Length,
                                                          out bool success, out double tmp);
            if (!success) return false;

            float f_cast = (float)tmp;
            if (float.IsInfinity(f_cast)) return false;

            result = f_cast;
            return true;
        }
        /// <summary>
        /// Try to parse StringEntity to double.
        /// Cannot accept whitespaces, comma insertion, and hex format (these are differ from official C# float.TryParse()).
        /// Use TryParseHex(out T) for hex data.
        /// This function calls the delegate of BurstCompiler.CompileFunctionPointer().
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static bool TryParse<T>(T source, out double result)
            where T : IParseExt
        {
            TryParseBurstCompile._tryParseFloat64Delegate((Char16*)source.GetUnsafePtr(),
                                                          source.Length,
                                                          out bool success, out result);
            return success;
        }

        /// <summary>
        /// This function calls the delegate of BurstCompiler.CompileFunctionPointer().
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <param name="endian"></param>
        /// <returns></returns>
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static bool TryParseHex<T>(T source, out int result, Endian endian = Endian.Little)
            where T :IParseExt
        {
            TryParseBurstCompile._tryParseHex32Delegate((Char16*)source.GetUnsafePtr(),
                                                        source.Length,
                                                        out bool success, out uint buff, endian);
            if (success)
            {
                result = *(int*)&buff;
            }
            else
            {
                result = 0;
            }
            return success;
        }
        /// <summary>
        /// This function calls the delegate of BurstCompiler.CompileFunctionPointer().
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <param name="endian"></param>
        /// <returns></returns>
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static bool TryParseHex<T>(T source, out long result, Endian endian = Endian.Little)
            where T : IParseExt
        {
            TryParseBurstCompile._tryParseHex64Delegate((Char16*)source.GetUnsafePtr(),
                                                        source.Length,
                                                        out bool success, out ulong buff, endian);
            if (success)
            {
                result = *(long*)&buff;
            }
            else
            {
                result = 0;
            }
            return success;
        }
        /// <summary>
        /// This function calls the delegate of BurstCompiler.CompileFunctionPointer().
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <param name="endian"></param>
        /// <returns></returns>
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static bool TryParseHex<T>(T source, out float result, Endian endian = Endian.Little)
            where T : IParseExt
        {
            TryParseBurstCompile._tryParseHex32Delegate((Char16*)source.GetUnsafePtr(),
                                                        source.Length,
                                                        out bool success, out uint buff, endian);
            if (success)
            {
                result = *(float*)&buff;
            }
            else
            {
                result = 0;
            }
            return success;
        }
        /// <summary>
        /// This function calls the delegate of BurstCompiler.CompileFunctionPointer().
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="result"></param>
        /// <param name="endian"></param>
        /// <returns></returns>
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static bool TryParseHex<T>(T source, out double result, Endian endian = Endian.Little)
            where T : IParseExt
        {
            TryParseBurstCompile._tryParseHex64Delegate((Char16*)source.GetUnsafePtr(),
                                                        source.Length,
                                                        out bool success, out ulong buff, endian);
            if (success)
            {
                result = *(double*)&buff;
            }
            else
            {
                result = 0;
            }
            return success;
        }


        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static void Split(ReadOnlyStringEntity source,
                                 NativeList<ReadOnlyStringEntity> result)
        {
            var tmp = result.GetUnsafeRef();
            SplitBurstCompile._splitWhiteSpaceDelegate(ref source, ref tmp);
        }
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static void Split(ReadOnlyStringEntity source, Char16 delim,
                                 NativeList<ReadOnlyStringEntity> result)
        {
            var tmp = result.GetUnsafeRef();
            SplitBurstCompile._splitCharDelegate(ref source, ref delim, ref tmp);
        }
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static void Split(ReadOnlyStringEntity source, ReadOnlyStringEntity delim,
                                 NativeList<ReadOnlyStringEntity> result)
        {
            var tmp = result.GetUnsafeRef();
            SplitBurstCompile._splitStringDelegate(ref source, ref delim, ref tmp);
        }


        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static ReadOnlyStringEntity Strip(ReadOnlyStringEntity source)
        {
            StripBurstCompile._stripWhiteSpaceDelegate(ref source, true, true,
                                                       out ReadOnlyStringEntity result);
            return result;
        }
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static ReadOnlyStringEntity Lstrip(ReadOnlyStringEntity source)
        {
            StripBurstCompile._stripWhiteSpaceDelegate(ref source, true, false,
                                                       out ReadOnlyStringEntity result);
            return result;
        }
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static ReadOnlyStringEntity Rstrip(ReadOnlyStringEntity source)
        {
            StripBurstCompile._stripWhiteSpaceDelegate(ref source, false, true,
                                                       out ReadOnlyStringEntity result);
            return result;
        }

        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static ReadOnlyStringEntity Strip(ReadOnlyStringEntity source, Char16 target)
        {
            StripBurstCompile._stripCharDelegate(ref source,
                                                 ref target,
                                                 true, true,
                                                 out ReadOnlyStringEntity result);
            return result;
        }
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static ReadOnlyStringEntity Lstrip(ReadOnlyStringEntity source, Char16 target)
        {
            StripBurstCompile._stripCharDelegate(ref source,
                                                 ref target,
                                                 true, false,
                                                 out ReadOnlyStringEntity result);
            return result;
        }
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static ReadOnlyStringEntity Rstrip(ReadOnlyStringEntity source, Char16 target)
        {
            StripBurstCompile._stripCharDelegate(ref source,
                                                 ref target,
                                                 false, true,
                                                 out ReadOnlyStringEntity result);
            return result;
        }

        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static ReadOnlyStringEntity Strip(ReadOnlyStringEntity source, ReadOnlyStringEntity target)
        {
            StripBurstCompile._stripStringDelegate(ref source,
                                                   ref target,
                                                   true, true,
                                                   out ReadOnlyStringEntity result);
            return result;
        }
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static ReadOnlyStringEntity Lstrip(ReadOnlyStringEntity source, ReadOnlyStringEntity target)
        {
            StripBurstCompile._stripStringDelegate(ref source,
                                                   ref target,
                                                   true, false,
                                                   out ReadOnlyStringEntity result);
            return result;
        }
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static ReadOnlyStringEntity Rstrip(ReadOnlyStringEntity source, ReadOnlyStringEntity target)
        {
            StripBurstCompile._stripStringDelegate(ref source,
                                                   ref target,
                                                   false, true,
                                                   out ReadOnlyStringEntity result);
            return result;
        }


        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static void GetChars(NativeBase64Encoder encoder,
                                    NativeList<Char16> chars,
                                    byte* ptr, int len, bool splitData = false)
        {
            var enc = encoder.GetUnsafeRef();
            var res = chars.GetUnsafeRef();
            Base64EncodingBurstCompile._getCharsDelegate(ref enc, ref res, ptr, len, splitData);
        }
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static void Terminate(NativeBase64Encoder encoder,
                                     NativeList<Char16> chars)
        {
            var enc = encoder.GetUnsafeRef();
            var res = chars.GetUnsafeRef();
            Base64EncodingBurstCompile._terminateDelegate(ref enc, ref res);
        }
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static bool GetBytes(NativeBase64Decoder decoder,
                                    NativeList<byte> bytes,
                                    Char16* ptr, int len)
        {
            var dec = decoder.GetUnsafeRef();
            var res = bytes.GetUnsafeRef();
            Base64EncodingBurstCompile._getBytesDelegate(ref dec, ref res, ptr, len, out bool success);
            return success;
        }
        [Obsolete("small delegate of Burst function pointers has no benefit.")]
        public static bool GetBytes<T>(NativeBase64Decoder decoder,
                                       NativeList<byte> bytes,
                                       T slice)
            where T : IJaggedArraySliceBase<Char16>
        {
            var dec = decoder.GetUnsafeRef();
            var res = bytes.GetUnsafeRef();
            Base64EncodingBurstCompile._getBytesDelegate(ref dec, ref res,
                                                         (Char16*)slice.GetUnsafePtr(), slice.Length,
                                                         out bool success);
            return success;
        }
    }

    namespace Impl
    {
        // delegates

        [BurstCompile]
        internal unsafe static class TryParseBurstCompile
        {
            internal delegate void TryParseBoolDelegate(Char16* ptr_source, int length,
                                                        out bool success, out bool result);
            internal static TryParseBoolDelegate _tryParseBoolDelegate;

            internal delegate void TryParseInt32Delegate(Char16* ptr_source, int length,
                                                         out bool success, out int result);
            internal static TryParseInt32Delegate _tryParseInt32Delegate;

            internal delegate void TryParseInt64Delegate(Char16* ptr_source, int length,
                                                         out bool success, out long result);
            internal static TryParseInt64Delegate _tryParseInt64Delegate;

            internal delegate void TryParseFloat64Delegate(Char16* ptr_source, int length,
                                                           out bool success, out double result);
            internal static TryParseFloat64Delegate _tryParseFloat64Delegate;

            internal delegate void TryParseHex32Delegate(Char16* ptr_source, int length,
                                                         out bool success, out uint result, Endian endian);
            internal static TryParseHex32Delegate _tryParseHex32Delegate;

            internal delegate void TryParseHex64Delegate(Char16* ptr_source, int length,
                                                         out bool success, out ulong result, Endian endian);
            internal static TryParseHex64Delegate _tryParseHex64Delegate;


            [RuntimeInitializeOnLoadMethod]
            public static void Initialize()
            {
                // for TryParse() func
                _tryParseBoolDelegate = BurstCompiler.
                    CompileFunctionPointer<TryParseBoolDelegate>(TryParseBoolBurst).Invoke;

                _tryParseInt32Delegate = BurstCompiler.
                    CompileFunctionPointer<TryParseInt32Delegate>(TryParseInt32Burst).Invoke;

                _tryParseInt64Delegate = BurstCompiler.
                    CompileFunctionPointer<TryParseInt64Delegate>(TryParseInt64Burst).Invoke;

                _tryParseFloat64Delegate = BurstCompiler.
                    CompileFunctionPointer<TryParseFloat64Delegate>(TryParseFloat64Burst).Invoke;

                _tryParseHex32Delegate = BurstCompiler.
                    CompileFunctionPointer<TryParseHex32Delegate>(TryParseHex32Burst).Invoke;

                _tryParseHex64Delegate = BurstCompiler.
                    CompileFunctionPointer<TryParseHex64Delegate>(TryParseHex64Burst).Invoke;
            }


            // BurstCompile entry point
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(TryParseBoolDelegate))]
            private static void TryParseBoolBurst(Char16* ptr_source, int length,
                                                  out bool success, out bool result)
            {
                StringParserExt.TryParseBoolImpl(ptr_source, length, out success, out result);
            }
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(TryParseInt32Delegate))]
            private static void TryParseInt32Burst(Char16* ptr_source, int length,
                                                   out bool success, out int result)
            {
                StringParserExt.TryParseInt32Impl(ptr_source, length, out success, out result);
            }
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(TryParseInt64Delegate))]
            private static void TryParseInt64Burst(Char16* ptr_source, int length,
                                                   out bool success, out long result)
            {
                StringParserExt.TryParseInt64Impl(ptr_source, length, out success, out result);
            }
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(TryParseFloat64Delegate))]
            private static void TryParseFloat64Burst(Char16* ptr_source, int length,
                                                     out bool success, out double result)
            {
                StringParserExt.TryParseFloat64Impl(ptr_source, length, out success, out result);
            }
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(TryParseHex32Delegate))]
            private static void TryParseHex32Burst(Char16* ptr_source, int length,
                                                   out bool success, out uint result,
                                                   Endian endian)
            {
                StringParserExt.TryParseHex32Impl(ptr_source, length, out success, out result, endian);
            }
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(TryParseHex64Delegate))]
            private static void TryParseHex64Burst(Char16* ptr_source, int length,
                                                   out bool success, out ulong result,
                                                   Endian endian)
            {
                StringParserExt.TryParseHex64Impl(ptr_source, length, out success, out result, endian);
            }
        }

        [BurstCompile]
        internal unsafe static class SplitBurstCompile
        {
            internal delegate void SplitWhiteSpaceDelegate(ref ReadOnlyStringEntity source,
                                                           ref UnsafeRefToNativeList<ReadOnlyStringEntity> result);
            internal static SplitWhiteSpaceDelegate _splitWhiteSpaceDelegate;

            internal delegate void SplitCharDelegate(ref ReadOnlyStringEntity source,
                                                     ref Char16 delim,
                                                     ref UnsafeRefToNativeList<ReadOnlyStringEntity> result);
            internal static SplitCharDelegate _splitCharDelegate;

            internal delegate void SplitStringDelegate(ref ReadOnlyStringEntity source,
                                                       ref ReadOnlyStringEntity delim,
                                                       ref UnsafeRefToNativeList<ReadOnlyStringEntity> result);
            internal static SplitStringDelegate _splitStringDelegate;


            [RuntimeInitializeOnLoadMethod]
            public static void Initialize()
            {
                _splitWhiteSpaceDelegate = BurstCompiler.
                    CompileFunctionPointer<SplitWhiteSpaceDelegate>(SplitWhiteSpaceBurst).Invoke;

                _splitCharDelegate = BurstCompiler.
                    CompileFunctionPointer<SplitCharDelegate>(SplitCharBurst).Invoke;

                _splitStringDelegate = BurstCompiler.
                    CompileFunctionPointer<SplitStringDelegate>(SplitStringBurst).Invoke;
            }

            // BurstCompile entry point
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(SplitWhiteSpaceDelegate))]
            private static void SplitWhiteSpaceBurst(ref ReadOnlyStringEntity source,
                                                     ref UnsafeRefToNativeList<ReadOnlyStringEntity> result)
            {
                source.Split(result);
            }
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(SplitCharDelegate))]
            private static void SplitCharBurst(ref ReadOnlyStringEntity source,
                                               ref Char16 delim,
                                               ref UnsafeRefToNativeList<ReadOnlyStringEntity> result)
            {
                source.Split(delim, result);
            }
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(SplitStringDelegate))]
            private static void SplitStringBurst(ref ReadOnlyStringEntity source,
                                                 ref ReadOnlyStringEntity delim,
                                                 ref UnsafeRefToNativeList<ReadOnlyStringEntity> result)
            {
                source.Split(delim, result);
            }
        }

        [BurstCompile]
        internal unsafe static class StripBurstCompile
        {
            internal delegate void StripWhiteSpaceDelegate(ref ReadOnlyStringEntity source,
                                                           bool left, bool right,
                                                           out ReadOnlyStringEntity result);
            internal static StripWhiteSpaceDelegate _stripWhiteSpaceDelegate;

            internal delegate void StripCharDelegate(ref ReadOnlyStringEntity source,
                                                     ref Char16 target,
                                                     bool left, bool right,
                                                     out ReadOnlyStringEntity result);
            internal static StripCharDelegate _stripCharDelegate;

            internal delegate void StripStringDelegate(ref ReadOnlyStringEntity source,
                                                       ref ReadOnlyStringEntity target,
                                                       bool left, bool right,
                                                       out ReadOnlyStringEntity result);
            internal static StripStringDelegate _stripStringDelegate;

            [RuntimeInitializeOnLoadMethod]
            public static void Initialize()
            {
                _stripWhiteSpaceDelegate = BurstCompiler.
                    CompileFunctionPointer<StripWhiteSpaceDelegate>(StripWhiteSpaceBurst).Invoke;

                _stripCharDelegate = BurstCompiler.
                    CompileFunctionPointer<StripCharDelegate>(StripCharBurst).Invoke;

                _stripStringDelegate = BurstCompiler.
                    CompileFunctionPointer<StripStringDelegate>(StripStringBurst).Invoke;
            }

            // BurstCompile entry point
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(StripWhiteSpaceDelegate))]
            private static void StripWhiteSpaceBurst(ref ReadOnlyStringEntity source,
                                                     bool left, bool right,
                                                     out ReadOnlyStringEntity result)
            {
                result = StringStripperExt.StripWhiteSpaceImpl(source, left, right);
            }
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(StripCharDelegate))]
            private static void StripCharBurst(ref ReadOnlyStringEntity source,
                                               ref Char16 target,
                                               bool left, bool right,
                                               out ReadOnlyStringEntity result)
            {
                result = StringStripperExt.StripCharImpl(source, target, left, right);
            }
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(StripStringDelegate))]
            private static void StripStringBurst(ref ReadOnlyStringEntity source,
                                                 ref ReadOnlyStringEntity target,
                                                 bool left, bool right,
                                                 out ReadOnlyStringEntity result)
            {
                result = StringStripperExt.StripStringImpl(source, target, left, right);
            }
        }

        [BurstCompile]
        internal unsafe static class Base64EncodingBurstCompile
        {
            internal delegate void GetCharsDelegate(ref UnsafeRefToNativeBase64Encoder encoder,
                                                    ref UnsafeRefToNativeList<Char16> chars,
                                                    byte* ptr_byte, int len_byte,
                                                    bool splitData);
            internal static GetCharsDelegate _getCharsDelegate;

            internal delegate void TerminateDelegate(ref UnsafeRefToNativeBase64Encoder encoder,
                                                     ref UnsafeRefToNativeList<Char16> chars);
            internal static TerminateDelegate _terminateDelegate;

            internal delegate void GetBytesDelegate(ref UnsafeRefToNativeBase64Decoder decoder,
                                                    ref UnsafeRefToNativeList<byte> bytes,
                                                    Char16* ptr_chars, int len_chars,
                                                    out bool success);
            internal static GetBytesDelegate _getBytesDelegate;


            [RuntimeInitializeOnLoadMethod]
            public static void Initialize()
            {
                _getCharsDelegate = BurstCompiler.
                    CompileFunctionPointer<GetCharsDelegate>(GetCharsBurst).Invoke;

                _terminateDelegate = BurstCompiler.
                    CompileFunctionPointer<TerminateDelegate>(TerminateBurst).Invoke;

                _getBytesDelegate = BurstCompiler.
                    CompileFunctionPointer<GetBytesDelegate>(GetBytesBurst).Invoke;
            }


            // BurstCompile entry point
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(GetCharsDelegate))]
            private static void GetCharsBurst(ref UnsafeRefToNativeBase64Encoder encoder,
                                              ref UnsafeRefToNativeList<Char16> chars,
                                              byte* ptr_byte, int len_byte,
                                              bool splitData)
            {
                encoder.GetChars(chars, ptr_byte, len_byte, splitData);
            }
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(TerminateDelegate))]
            private static void TerminateBurst(ref UnsafeRefToNativeBase64Encoder encoder,
                                               ref UnsafeRefToNativeList<Char16> chars)
            {
                encoder.Terminate(chars);
            }
            [BurstCompile]
            [AOT.MonoPInvokeCallback(typeof(GetBytesDelegate))]
            private static void GetBytesBurst(ref UnsafeRefToNativeBase64Decoder decoder,
                                              ref UnsafeRefToNativeList<byte> bytes,
                                              Char16* ptr_chars, int len_chars,
                                              out bool success)
            {
                success = decoder.GetBytes(bytes, ptr_chars, len_chars);
            }
        }
    }
}


