
namespace NativeStringCollections
{
    readonly struct Define
    {
        // for NativeTextStreamReader
        public const int DefaultBufferSize = 4096;
        public const int MinBufferSize = 1024;

        // for AsyncByteBuffer
        public const long MinByteBufferSize = 4096;
        public const int ByteAlign = 16;

        // for AsyncTextFileLoader
        public const int DefaultDecodeBlock = 1024;
        public const int DefaultNumParser = 2;
        public const int NumParserLimit = 1048576;
    }

    readonly struct Base64Const
    {
        public const int LineBreakPos = 76;
    }
}
