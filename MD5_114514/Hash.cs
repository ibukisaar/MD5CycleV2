using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace MD5CycleV2;

[StructLayout(LayoutKind.Explicit)]
unsafe struct Hash {
    [FieldOffset(0)] public uint i0;
    [FieldOffset(4)] public uint i1;
    [FieldOffset(8)] public uint i2;
    [FieldOffset(12)] public uint i3;

    [FieldOffset(0)] public ulong l0;
    [FieldOffset(8)] public ulong l1;

    [UnscopedRef]
    public Span<byte> Span => MemoryMarshal.AsBytes(new Span<Hash>(ref this));

    [UnscopedRef]
    public readonly ReadOnlySpan<byte> ReadOnlySpan => MemoryMarshal.AsBytes(new ReadOnlySpan<Hash>(in this));


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Init() {
        l0 = 0xefcdab8967452301UL;
        l1 = 0x1032547698badcfeUL;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Hash h1, Hash h2)
        => Unsafe.As<Hash, Vector128<uint>>(ref h1) == Unsafe.As<Hash, Vector128<uint>>(ref h2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Hash h1, Hash h2)
        => Unsafe.As<Hash, Vector128<uint>>(ref h1) != Unsafe.As<Hash, Vector128<uint>>(ref h2);

    public readonly override string ToString() {
        return Convert.ToHexString(MemoryMarshal.AsBytes(new ReadOnlySpan<Hash>(in this))).ToLowerInvariant();
    }

    public static implicit operator Hash(string s) {
        byte[] result = Convert.FromHexString(s);
        return MemoryMarshal.Read<Hash>(result);
    }
}
