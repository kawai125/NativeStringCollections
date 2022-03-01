using System.Diagnostics;

using NativeStringCollections.Impl.csFastFloat.Constants;

namespace NativeStringCollections.Impl.csFastFloat.Structures
{

    internal unsafe struct DigitsBuffer
    {
        private fixed byte digits[(int)CalculationConstants.max_digits];

        public byte this[int index]
        {
            get => this[(uint)index];
            set => this[(uint)index] = value;
        }

        public byte this[uint index]
        {
            get
            {
                CheckIndex(index);
                return digits[index];
            }

            set
            {
                CheckIndex(index);
                digits[index] = value;
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndex(uint index)
        {
            if (index >= CalculationConstants.max_digits)
                throw new System.ArgumentOutOfRangeException($"index = {index} is out of range. range: [0, {CalculationConstants.max_digits-1}]");
        }
    }
}