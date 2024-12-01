using System;
using System.Runtime.InteropServices;

namespace MD5CycleV2;

unsafe internal static class Cuda {
    const string Dll = @"D:\VS2022\MD5CycleV2\x64\Release\CudaDll.dll";


    [DllImport(Dll, EntryPoint = "init")]
    public extern static void Init(int blockCount, int threadCount, int maxResultCount, ref readonly Hash inHashes);

    [DllImport(Dll, EntryPoint = "release")]
    public extern static void Release();

    [DllImport(Dll, EntryPoint = "md5")]
    public extern static int MD5(long* start, ref Result result, int useMask = 0);

    [DllImport(Dll, EntryPoint = "md5_vec")]
    public extern static int MD5Vec(long* start, ref Result result);

    [DllImport(Dll, EntryPoint = "read_hashes")]
    public extern static void ReadHashes(ref Hash outHashes);

    [DllImport(Dll, EntryPoint = "get_error")]
    public extern static nint GetError(int error);

    [DllImport(Dll, EntryPoint = "_114514_md5")]
    public extern static int MD5_114514(long* start, ref Result result);

    [DllImport(Dll, EntryPoint = "init114514")]
    public extern static void Init114514(int blockCount, int threadCount, int maxResultCount);

    [DllImport(Dll, EntryPoint = "write_hashes")]
    public extern static void WriteHashes(ref readonly Hash outHashes);
}
