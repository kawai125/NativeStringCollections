
namespace NativeStringCollections
{
    readonly struct Define
    {
        // for NativeTextStreamReader
        public const int DefaultBufferSize = 4096;
        public const int MinBufferSize = 1024;

        // for AsyncByteBuffer
        public const int MinByteBufferSize = 4096;
        //public const int MinByteBufferSize = 524288;  // 512kB

        // for AsyncTextFileLoader
        //public const int DefaultDecodeBlock = 128;
        public const int DefaultDecodeBlock = 512;
        public const int DefaultNumParser = 2;
        public const int NumParserLimit = 1048576;
    }

    readonly struct Base64Const
    {
        public const int LineBreakPos = 76;
    }
    public enum ReadJobState
    {
        // not in process
        UnLoaded,
        Completed,

        // in process
        ReadAsync,
        ParseText,
        PostProc,
        WaitForCallingComplete,
    }
}
