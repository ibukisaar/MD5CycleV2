using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MD5CycleV2;


[StructLayout(LayoutKind.Sequential)]
readonly struct Result : IEquatable<Result>, IComparable<Result> {
    public readonly Hash hash;
    public readonly long iterateCnt;

    public int Id => (int)hash.i0;

    public Hash HashValue => new Hash { i1 = hash.i1, l1 = hash.l1 };

    internal Result(Hash hash, long iterateCnt) {
        this.hash = hash;
        this.iterateCnt = iterateCnt;
    }

    public override int GetHashCode() {
        return hash.i3.GetHashCode();
    }

    public override bool Equals([NotNullWhen(true)] object? obj) {
        return obj is Result other && Equals(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Result other) {
        return (((hash.l0 ^ other.hash.l0) & 0xffffffff_00000000UL) | (hash.l1 ^ other.hash.l1)) == 0;
    }

    public override string ToString() {
        return $"{Id,5}, {HashValue}, {iterateCnt}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    unsafe public int CompareTo(Result other) {
        Hash a = hash;
        Hash b = other.hash;
        return Native.i128_CompareTo(&a, &b);
    }
}

