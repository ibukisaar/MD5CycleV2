using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MD5CycleV2;


[StructLayout(LayoutKind.Sequential)]
readonly struct Result : IEquatable<Result>, IComparable<Result> {
    public readonly Hash hash;
    public readonly int index;
    public readonly int iterateCnt;

    public override int GetHashCode() {
        return hash.i3.GetHashCode();
    }

    public override bool Equals([NotNullWhen(true)] object? obj) {
        return obj is Result other && Equals(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Result other) {
        return ((hash.l0 ^ other.hash.l0) | (hash.l1 ^ other.hash.l1)) == 0;
    }

    public override string ToString() {
        string s = $"{hash}";
        return "1145141919810" + s[0..3] + s[16..32];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    unsafe public int CompareTo(Result other) {
        Hash a = hash;
        Hash b = other.hash;
        return Native.i128_CompareTo(&a, &b);
    }
}

