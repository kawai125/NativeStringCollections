
namespace NativeStringCollections
{
    public readonly struct Define
    {
        // for NativeTextStreamReader
        public const int DefaultBufferSize = 4096;
        public const int MinBufferSize = 1024;

        // for AsyncByteBuffer
        public const int MinByteBufferSize = 4096;

        // for AsyncTextFileLoader
        public const int MinDecodeBlock = 64;
        public const int DefaultDecodeBlock = 2048;
        public const int DefaultNumParser = 2;
        public const int NumParserLimit = 1048576;
    }

    public readonly struct Base64Const
    {
        public const int LineBreakPos = 76;
    }
    public enum ReadJobState
    {
        // not in process
        UnLoaded,
        Completed,

        // in process for loading
        ReadAsync,
        ParseText,
        PostProc,

        // in process for unloading
        UnLoadJob,

        // waiting for calling Complete()
        WaitForCallingComplete,
    }
}
