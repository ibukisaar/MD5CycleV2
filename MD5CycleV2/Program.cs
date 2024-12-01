using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using MD5CycleV2;

const int blockCount = 68;
const int threadCount = 512;
const long iterateStep = 1 << 24;
const int maxResultCount = 65536;
const int VectorSize = 1;

CancellationTokenSource cts = new();

Console.CancelKeyPress += (_, e) => {
    if (!cts.IsCancellationRequested) {
        Console.WriteLine("exit...");
        cts.Cancel();
    }
    e.Cancel = true;
};

Console.WriteLine(Environment.CurrentDirectory);
Get(31025, 172623136223, 27570, 133086316249);
//Start(cts.Token);
//Test();
//SpeedTest();


//byte[] data = new byte[16];
//Random.Shared.NextBytes(data);
//Vector128<uint> v = Vector128.Create(data).AsUInt32();
//Console.WriteLine($"{v[0]:x8}, {v[1]:x8}, {v[2]:x8}, {v[3]:x8}");

//Vector128<uint> indexes = Vector128.Create(0x0c0d0e0fu, 0x08090a0bu, 0x04050607u, 0x00010203u);
//var r = Ssse3.Shuffle(v.AsByte(), indexes.AsByte()).AsUInt32();
//Console.WriteLine($"{r[0]:x8}, {r[1]:x8}, {r[2]:x8}, {r[3]:x8}");

//SpeedTest();


unsafe static void Start(CancellationToken cancellationToken) {
    TimeSpan saveTimeout = TimeSpan.FromMinutes(5);
    TimeSpan backupTimeout = TimeSpan.FromMinutes(60);

    var (result, iterateCnt, hashes) = FileTool.Load(blockCount, threadCount);

    Int128[] mins = new Int128[4];
    Int128[] maxs = new Int128[2];

    Int128 avg = Tool.CalcResults(result, mins, maxs);
    Tool.PrintResults(avg, mins, maxs);

    Cuda.Init(blockCount, threadCount, maxResultCount, ref MemoryMarshal.GetArrayDataReference(hashes));

    Result[] resultArray = GC.AllocateUninitializedArray<Result>(maxResultCount);

    Stopwatch saveTimer = Stopwatch.StartNew();
    Stopwatch backupTimer = Stopwatch.StartNew();
    Task calcTask = Task.CompletedTask;

    while (!cancellationToken.IsCancellationRequested) {
        TimeSpan startTime = saveTimer.Elapsed;
        int resultCount = Cuda.MD5(&iterateCnt, ref MemoryMarshal.GetArrayDataReference(resultArray), useMask: 1);
        TimeSpan endTime = saveTimer.Elapsed;

        if (resultCount < 0) {
            string errorMsg = Marshal.PtrToStringAnsi(Cuda.GetError(resultCount)) ?? "未知";
            Tool.ColorPrint($"CUDA错误: {errorMsg}", ConsoleColor.Red);
            Thread.Sleep(500);
            continue;
        }

        if (resultCount > maxResultCount) {
            Tool.ColorPrint($"警告：返回值溢出({resultCount})", ConsoleColor.Yellow);
            resultCount = maxResultCount;
        }

        try {
            calcTask.Wait(cancellationToken);
        } catch (TaskCanceledException) {

        }

        for (int i = 0; i < resultCount; i++) {
            if (result.Add(resultArray[i], out Result other) is false) {
                Tool.ColorPrint($"发生碰撞：{resultArray[i]} <===> {other}", ConsoleColor.Green);
            }
        }

        double speed = (iterateStep * blockCount * threadCount) / (endTime - startTime).TotalSeconds / 1000_000;

        Console.WriteLine($"iterate: {iterateCnt,13} (2^{Math.Log2(iterateCnt):0.0000}), speed: {speed,8:0.00} MH/s, hashset: {result.Count,10} (+{resultCount}), {DateTime.Now}");

        try {
            calcTask = Task.Run([SkipLocalsInit] () => {
                Span<Int128> tempMins = stackalloc Int128[4];
                Span<Int128> tempMaxs = stackalloc Int128[2];
                Int128 avg = Tool.CalcResults(result, tempMins, tempMaxs);
                if (!tempMins.SequenceEqual(mins) || !tempMaxs.SequenceEqual(maxs)) {
                    tempMins.CopyTo(mins);
                    tempMaxs.CopyTo(maxs);
                    Tool.PrintResults(avg, mins, maxs);
                }

            }, cancellationToken);
        } catch (TaskCanceledException) {

        }

        if (backupTimer.Elapsed >= backupTimeout) {
            FileTool.BackupFile();
            Console.WriteLine("==================================================");

            backupTimer.Restart();
        }

        if (saveTimer.Elapsed >= saveTimeout) {
            Cuda.ReadHashes(ref MemoryMarshal.GetArrayDataReference(hashes));

            FileTool.Save(result, iterateCnt, hashes);
            Console.WriteLine("--------------------------------------------------");

            saveTimer.Restart();
        }
    }

    // 程序退出

    if (backupTimer.Elapsed >= saveTimeout) {
        FileTool.BackupFile(copy: true);
    }
}

unsafe static void Test() {
    Result[] resultArray1 = new Result[maxResultCount];
    Result[] resultArray2 = new Result[maxResultCount];
    Hash[] hashes1 = new Hash[blockCount * threadCount];
    Random.Shared.NextBytes(MemoryMarshal.AsBytes(hashes1.AsSpan()));
    Hash[] hashes2 = hashes1[..];

    {
        Console.WriteLine("start 1");
        long iterateCnt = 0;
        Cuda.Init(blockCount, threadCount, maxResultCount, ref MemoryMarshal.GetArrayDataReference(hashes1));
        int resultCount = Cuda.MD5(&iterateCnt, ref MemoryMarshal.GetArrayDataReference(resultArray1));
        Cuda.ReadHashes(ref MemoryMarshal.GetArrayDataReference(hashes1));
        Cuda.Release();
    }

    {
        Console.WriteLine("start 2");
        long iterateCnt = 0;
        Cuda.Init(blockCount, threadCount, maxResultCount, ref MemoryMarshal.GetArrayDataReference(hashes2));
        int resultCount = Cuda.MD5Vec(&iterateCnt, ref MemoryMarshal.GetArrayDataReference(resultArray2));
        Cuda.ReadHashes(ref MemoryMarshal.GetArrayDataReference(hashes2));
        Cuda.Release();
    }

    if (hashes1.AsSpan().SequenceEqual(hashes2)) {
        Console.WriteLine("hash OK");
    } else {
        Console.WriteLine("hash ERROR");
    }

    Array.Sort(resultArray1);
    Array.Sort(resultArray2);

    if (resultArray1.AsSpan().SequenceEqual(resultArray2)) {
        Console.WriteLine("result OK");
    } else {
        Console.WriteLine("result ERROR");
    }
}

unsafe static Hash CreateHash() {
    Hash a = default;
    Random.Shared.NextBytes(MemoryMarshal.AsBytes(new Span<Hash>(ref a)));
    a.i0 = 0;
    return a;
}

unsafe static void SpeedTest() {
    ResultSet results = new ResultSet();

    for (int i = 0; i < 512; i++) {
        Hash a = CreateHash();
        Console.WriteLine(a);
        Console.WriteLine(results.Add(new Result(a, i), out _));

        if (i == 511) {
            a.i0 = 1024;
            Result b = new Result(a, 11111111);
            results.Add(b, out var actualValue);
            Console.WriteLine(actualValue.HashValue);
        }
    }

    Result[] rs = [.. results];

    Console.WriteLine(rs.Length);
}

unsafe static void SpeedTest2() {
    Result[] resultArray = new Result[maxResultCount];
    Hash[] hashes1 = new Hash[blockCount * threadCount * VectorSize];
    Random.Shared.NextBytes(MemoryMarshal.AsBytes(hashes1.AsSpan()));

    long iterateCnt = 0;
    Cuda.Init(blockCount, threadCount, maxResultCount, ref MemoryMarshal.GetArrayDataReference(hashes1));

    Stopwatch stopwatch = new Stopwatch();

    while (true) {
        stopwatch.Restart();
        int resultCount = Cuda.MD5(&iterateCnt, ref MemoryMarshal.GetArrayDataReference(resultArray));
        stopwatch.Stop();

        if (resultCount < 0) {
            string errorMsg = Marshal.PtrToStringAnsi(Cuda.GetError(resultCount)) ?? "未知";
            Tool.ColorPrint($"CUDA错误: {errorMsg}", ConsoleColor.Red);
            Thread.Sleep(500);
            continue;
        }

        double speed = (iterateStep * hashes1.Length) / stopwatch.Elapsed.TotalSeconds / 1000_000;

        Console.WriteLine($"iterate: {iterateCnt,13} (2^{Math.Log2(iterateCnt):0.0000}), speed: {speed,8:0.00} MH/s, result: +{resultCount}, {stopwatch.Elapsed}");
    }
}


unsafe static void Get(int index1, long iterate1, int index2, long iterate2) {
    if (iterate1 > iterate2) {
        (index1, index2) = (index2, index1);
        (iterate1, iterate2) = (iterate2, iterate1);
    }

    Hash hash1, hash2;
    using (var fileStream = File.OpenRead(FileTool.InitHashesFile)) {
        fileStream.Position = index1 * sizeof(Hash);
        fileStream.ReadExactly(new Span<byte>(&hash1, sizeof(Hash)));
        fileStream.Position = index2 * sizeof(Hash);
        fileStream.ReadExactly(new Span<byte>(&hash2, sizeof(Hash)));
    }

    Stopwatch stopwatch = Stopwatch.StartNew();

    long diffIterate = iterate2 - iterate1;

    Console.WriteLine($"diffIterate: {diffIterate}, {hash1}, {hash2}");

    while (diffIterate-- > 0) {
        mask104_md5_1(&hash2);

        if ((diffIterate & 0x7fffffff) == 0) {
            Console.WriteLine($"diff: {diffIterate}, {hash2}, {stopwatch.Elapsed}");
        }
    }

    Console.WriteLine($"iterate1: {iterate1}, {hash1}, {hash2}, {stopwatch.Elapsed}");

    Hash2 prev_pair = default;
    Hash2 hash_pair = new(hash1, hash2);
    while (iterate1 > 0) {
        if ((iterate1 & 0x7fffffff) == 0) {
            (hash1, hash2) = hash_pair;
            Console.WriteLine($"{iterate1}, {hash1}, {hash2}, {stopwatch.Elapsed}");
        }

        iterate1--;
        prev_pair = hash_pair;
        if (mask104_md5_equals_2(&hash_pair) == 0) {
            continue;
        }

        (hash1, hash2) = prev_pair;
        Console.WriteLine($"{iterate1}, {hash1}, {hash2}, {stopwatch.Elapsed}");
        (hash1, hash2) = hash_pair;
        Console.WriteLine($"{hash1}, {hash2}");
        break;
    }


    [SuppressGCTransition]
    [DllImport(@"D:\VS2022\MD5环\x64\Release\MD5.dll")]
    static extern void mask104_md5_1(Hash* hash);

    [SuppressGCTransition]
    [DllImport(@"D:\VS2022\MD5环\x64\Release\MD5.dll")]
    static extern int mask104_md5_equals_2(Hash2* hash_pair);
}

[StructLayout(LayoutKind.Sequential)]
struct Hash2 {
    public uint h_a_0, h_b_0;
    public uint h_a_1, h_b_1;
    public uint h_a_2, h_b_2;
    public uint h_a_3, h_b_3;

    public Hash2(Hash hash_a, Hash hash_b) {
        h_a_0 = hash_a.i0;
        h_a_1 = hash_a.i1;
        h_a_2 = hash_a.i2;
        h_a_3 = hash_a.i3;
        h_b_0 = hash_b.i0;
        h_b_1 = hash_b.i1;
        h_b_2 = hash_b.i2;
        h_b_3 = hash_b.i3;
    }

    public readonly void Deconstruct(out Hash hash_a, out Hash hash_b) {
        Unsafe.SkipInit(out hash_a);
        Unsafe.SkipInit(out hash_b);
        hash_a.i0 = h_a_0;
        hash_a.i1 = h_a_1;
        hash_a.i2 = h_a_2;
        hash_a.i3 = h_a_3;
        hash_b.i0 = h_b_0;
        hash_b.i1 = h_b_1;
        hash_b.i2 = h_b_2;
        hash_b.i3 = h_b_3;
    }
}