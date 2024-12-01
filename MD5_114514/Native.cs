using System;
using System.Runtime.InteropServices;

namespace MD5CycleV2;

[System.Security.SuppressUnmanagedCodeSecurity]
unsafe static class Native {
    const string Dll = @"D:\VS2022\MD5CycleV2\x64\Release\Int128Dll.dll";

    [SuppressGCTransition]
    [DllImport(Dll)]
    public static extern int i128_CompareTo(Hash* a, Hash* b);

    [SuppressGCTransition]
    [DllImport(Dll)]
    public static extern int i128_CompareTo2(Hash* a, Hash* b);

    [SuppressGCTransition]
    [DllImport(Dll)]
    public static extern int i128_HashCode(Hash* a, int bits);

    [SuppressGCTransition]
    [DllImport(Dll)]
    public static extern void i128_hash_sub(out Int128 r, in Hash left, in Hash right);

    [SuppressGCTransition]
    [DllImport(Dll)]
    public static extern void i128_add(Int128* r, in Int128 other);

    [SuppressGCTransition]
    [DllImport(Dll)]
    public static extern void i128_mins(Int128* mins, int n, Int128* other);

    [SuppressGCTransition]
    [DllImport(Dll)]
    public static extern void i128_maxs(Int128* maxs, int n, Int128* other);
}
