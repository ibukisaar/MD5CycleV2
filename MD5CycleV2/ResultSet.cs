using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace MD5CycleV2;

[DebuggerDisplay($"Count = {{{nameof(Count)}}}")]
unsafe sealed class ResultSet {
    const int MaxHashBit = 27;

    private HashTable hashTable;

    public int Count => hashTable.count;

    public ResultSet() {
        hashTable = new HashTable(6);
    }

    public ResultSet(int capacity) {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        int hashBit;

        if (capacity != 0) {
            hashBit = Math.Clamp(BitOperations.Log2((uint)capacity) + 1, 4, MaxHashBit);
        } else {
            hashBit = 4;
        }

        hashTable = new HashTable(hashBit);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ExpandCapacity() {
        if (hashTable.hashBit < MaxHashBit) {
            var newHashTable = new HashTable(hashTable.hashBit + 1);
            foreach (ref readonly Result r in hashTable) {
                newHashTable.Add(r, out _);
            }
            hashTable = newHashTable;
        } else {
            int newBufferLength = checked((int)((long)hashTable.buffer.Length * 3 / 2));
            Array.Resize(ref hashTable.buffer, newBufferLength);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Add(Result value, out Result existValue) {
        if (Count >= hashTable.buffer.Length) {
            ExpandCapacity();
        }

        return hashTable.Add(value, out existValue);
    }

    public bool TryGetValue(Result value, out Result actualValue)
        => hashTable.TryGetValue(value, out actualValue);

    public Enumerator GetEnumerator() => new(in hashTable);

    [DebuggerDisplay($"{{{nameof(Value)}}}, Next={{{nameof(Next)}}}")]
    internal struct Node {
        public Result Value;
        public int Next;
    }

    internal struct HashTable {
        public readonly int hashBit;
        public int count;
        public readonly int[] hashTable;
        public Node[] buffer;

        public readonly int HashCapacity => 1 << hashBit;

        public HashTable(int hashBit) {
            this.hashBit = hashBit;
            count = 0;

            hashTable = GC.AllocateUninitializedArray<int>(HashCapacity);
            hashTable.AsSpan().Fill(-1);

            buffer = GC.AllocateUninitializedArray<Node>(HashCapacity * 2);
        }

        public bool Add(Result value, out Result existValue) {
            Unsafe.SkipInit(out existValue);

            bool notExist = true;
            int hashIndex = Native.i128_HashCode(&value.hash, hashBit);
            ref int bufferIndex = ref hashTable[hashIndex];
            if (bufferIndex < 0) {
                bufferIndex = count;
                buffer[count] = new Node { Value = value, Next = -1 };
            } else {
                do {
                    ref Node right = ref buffer[bufferIndex];
                    int cmp = value.CompareTo(right.Value);

                    if (cmp < 0) {
                        break;
                    } else if (cmp > 0) {
                        bufferIndex = ref right.Next;
                    } else {
                        notExist = false;
                        existValue = right.Value;
                        cmp = value.iterateCnt.CompareTo(right.Value.iterateCnt);
                        if (cmp < 0) break;
                        bufferIndex = ref right.Next;
                    }
                } while (bufferIndex >= 0);

                buffer[count] = new Node { Value = value, Next = bufferIndex };
                bufferIndex = count;
            }

            count++;
            return notExist;
        }

        public readonly bool TryGetValue(Result value, out Result actualValue) {
            Unsafe.SkipInit(out actualValue);

            int hashIndex = Native.i128_HashCode(&value.hash, hashBit);
            int bufferIndex = hashTable[hashIndex];

            while (bufferIndex >= 0) {
                ref readonly Node node = ref buffer[bufferIndex];
                int cmp = value.CompareTo(node.Value);

                if (cmp < 0) break;

                if (cmp > 0) {
                    bufferIndex = node.Next;
                } else {
                    actualValue = node.Value;
                    return true;
                }
            }

            return false;
        }

        public readonly Enumerator GetEnumerator() => new(in this);
    }

    public ref struct Enumerator {
        private readonly int[] hashTable;
        private readonly Node[] buffer;
        private int hashIndex, bufferIndex;

        internal Enumerator(scoped ref readonly HashTable hashTable) {
            this.hashTable = hashTable.hashTable;
            buffer = hashTable.buffer;
            hashIndex = -1;
            bufferIndex = -1;
        }

        public readonly ref readonly Result Current => ref buffer[bufferIndex].Value;

        public bool MoveNext() {
            var hashTable = this.hashTable;

            if (hashIndex < 0) {
                for (int i = 0; i < hashTable.Length; i++) {
                    if (hashTable[i] >= 0) {
                        hashIndex = i;
                        bufferIndex = hashTable[i];
                        return true;
                    }
                }
                return false;
            }

            if (hashIndex < hashTable.Length) {
                ref readonly Node curr = ref buffer[bufferIndex];
                if (curr.Next >= 0) {
                    bufferIndex = curr.Next;
                    return true;
                }

                do {
                    ++hashIndex;
                    if (hashIndex >= hashTable.Length) {
                        return false;
                    }
                    bufferIndex = hashTable[hashIndex];
                } while (bufferIndex < 0);

                return true;
            }

            return false;
        }
    }
}

