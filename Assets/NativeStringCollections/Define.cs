
namespace NativeStringCollections
{
    readonly struct Define
    {
        public const int DefaultBufferSize = 4096;
        public const int MinBufferSize = 1024;

        public const long MinByteBufferSize = 4096;  // for AsyncByteBuffer
        public const int ByteAlign = 16;

        public const int DefaultDecodeBlock = 1024;  // for AsyncTextFileLoader
        public const int DefaultNumParser = 2;
        public const int NumParserLimit = 1048576;
    }
}
