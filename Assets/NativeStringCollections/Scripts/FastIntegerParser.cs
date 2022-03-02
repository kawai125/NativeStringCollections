using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using NativeStringCollections;

// ref: https://codereview.stackexchange.com/questions/200935/custom-integer-parser-optimized-for-performance
//      Pieter Witvoet's implemet.

namespace NativeStringCollections.Impl
{
    internal static class FastIntegerParser
    {
        internal static unsafe bool TryParse<T>(T source, out int result)
            where T : IJaggedArraySliceBase<Char16>
        {
            const int max_digits_len = 10;

            var length = source.Length;
            var ptr = (Char16*)source.GetUnsafePtr();

            if(length == 0)
            {
                result = default(int);
                return false;
            }

            var isNegative = ptr[0] == UTF16CodeSet.code_minus;
            var offset = isNegative ? 1 : 0;

            // It's faster to not operate directly on 'out' parameters:
            int value = 0;
            for (int i = offset; i < length; i++)
            {
                var c = ptr[i];
                if (c < UTF16CodeSet.code_0 || c > UTF16CodeSet.code_9)
                {
                    result = default(int);
                    return false;
                }
                else
                {
                    value = (value * 10) + (c - UTF16CodeSet.code_0);
                }
            }

            // Inputs with 10 digits or more might not fit in an integer, so they'll require additional checks:
            if (length - offset >= max_digits_len)
            {
                // Overflow/length checks should ignore leading zeroes:
                var meaningfulDigits = length - offset;
                for (int i = offset; i < length && ptr[i] == UTF16CodeSet.code_0; i++)
                    meaningfulDigits -= 1;

                if (meaningfulDigits > max_digits_len)
                {
                    // Too many digits, this certainly won't fit:
                    result = default(int);
                    return false;
                }
                else if (meaningfulDigits == max_digits_len)
                {
                    // 10-digit numbers can be several times larger than int.MaxValue, so overflow may result in any possible value.
                    // However, we only need to check the most significant digit to see if there's a mismatch.
                    // Note that int.MinValue always overflows, making it the only case where overflow is allowed:
                    if (!isNegative || value != int.MinValue)
                    {
                        // Any overflow will cause a leading digit mismatch:
                        if (value / 1000000000 != (ptr[length - max_digits_len] - UTF16CodeSet.code_0))
                        {
                            result = default(int);
                            return false;
                        }
                    }
                }
            }

            // -int.MinValue overflows back into int.MinValue, so that's ok:
            result = isNegative ? -value : value;
            return true;
        }

        
        // modified version for Int64
        internal static unsafe bool TryParse<T>(T source, out long result)
            where T : IJaggedArraySliceBase<Char16>
        {
            const int max_digits_len = 19;

            var length = source.Length;
            var ptr = (Char16*)source.GetUnsafePtr();

            if (length == 0)
            {
                result = default(long);
                return false;
            }

            var isNegative = ptr[0] == UTF16CodeSet.code_minus;
            var offset = isNegative ? 1 : 0;

            // It's faster to not operate directly on 'out' parameters:
            long value = 0;
            for (int i = offset; i < length; i++)
            {
                var c = ptr[i];
                if (c < UTF16CodeSet.code_0 || c > UTF16CodeSet.code_9)
                {
                    result = default(long);
                    return false;
                }
                else
                {
                    value = (value * 10) + (c - UTF16CodeSet.code_0);
                }
            }

            // Inputs with 19 digits or more might not fit in an integer, so they'll require additional checks:
            if (length - offset >= max_digits_len)
            {
                // Overflow/length checks should ignore leading zeroes:
                var meaningfulDigits = length - offset;
                for (int i = offset; i < length && ptr[i] == UTF16CodeSet.code_0; i++)
                    meaningfulDigits -= 1;

                if (meaningfulDigits > max_digits_len)
                {
                    // Too many digits, this certainly won't fit:
                    result = default(long);
                    return false;
                }
                else if (meaningfulDigits == max_digits_len)
                {
                    // 19-digit numbers can be several times larger than int.MaxValue, so overflow may result in any possible value.
                    // However, we only need to check the most significant digit to see if there's a mismatch.
                    // Note that int.MinValue always overflows, making it the only case where overflow is allowed:
                    if (!isNegative || value != long.MinValue)
                    {
                        // Any overflow will cause a leading digit mismatch:
                        if (value / 1000000000000000000 != (ptr[length - max_digits_len] - UTF16CodeSet.code_0))
                        {
                            result = default(long);
                            return false;
                        }
                    }
                }
            }

            // -int.MinValue overflows back into int.MinValue, so that's ok:
            result = isNegative ? -value : value;
            return true;
        }
    }
}
