
namespace NativeStringCollections.Impl.csFastFloat.Structures
{
    internal struct value128
    {
        public ulong low;
        public ulong high;

        public value128(ulong h, ulong l) : this()
        {
            high = h;
            low = l;
        }
    }
}