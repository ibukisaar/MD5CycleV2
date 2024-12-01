using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using MD5CycleV2;

const int blockCount = 68 * 2;
const int threadCount = 256;
const long iterateStep = 1 << 24;
const int maxResultCount = 64;

Hash sourceHash = default;
Random.Shared.NextBytes(sourceHash.Span[0..12]);

long total = 0;
Stopwatch timer = new();
Hash[] hashes = GC.AllocateUninitializedArray<Hash>(blockCount);
Result[] resultArray = GC.AllocateUninitializedArray<Result>(maxResultCount);
Span<byte> inputBuffer = stackalloc byte[32];
Span<byte> outputMD5 = stackalloc byte[16];

Cuda.Init114514(blockCount, threadCount, maxResultCount);

for (int i = 0; i < hashes.Length; i++) {
    hashes[i] = sourceHash;
    hashes[i].i2 += (uint)i;
}

unsafe {
    for (uint batch = sourceHash.i2 + blockCount; ; batch += blockCount) {
        long iterateCnt = 0;
        //Random.Shared.NextBytes(MemoryMarshal.AsBytes(hashes.AsSpan()));
        for (int i = 0; i < hashes.Length; i++) {
            hashes[i].i2 += blockCount;
        }

        Cuda.WriteHashes(ref MemoryMarshal.GetArrayDataReference(hashes));

        for (int k = 0; k < 1; k++) {
            timer.Restart();
            int resultCount = Cuda.MD5_114514(&iterateCnt, ref MemoryMarshal.GetArrayDataReference(resultArray));
            timer.Stop();

            if (resultCount < 0) {
                string errorMsg = Marshal.PtrToStringAnsi(Cuda.GetError(resultCount)) ?? "未知";
                Tool.ColorPrint($"CUDA错误: {errorMsg}", ConsoleColor.Red);
                return;
            }

            if (resultCount > maxResultCount) {
                Tool.ColorPrint($"警告：返回值溢出({resultCount})", ConsoleColor.Yellow);
                resultCount = maxResultCount;
            }

            total += iterateStep * blockCount * threadCount;
            double speed = (iterateStep * blockCount * threadCount) / timer.Elapsed.TotalSeconds / 1000_000;
            Console.WriteLine($"batch: {batch:x8}, total: {total} (2^{Math.Log2(total):0.0000}), speed: {speed,8:0.00} MH/s, result: +{resultCount}, {DateTime.Now}");

            if (resultCount != 0) {
                using var writer = File.AppendText("result.txt");

                for (int i = 0; i < resultCount; i++) {
                    string input = $"{resultArray[i]}";
                    Encoding.ASCII.GetBytes(input, inputBuffer);
                    MD5.HashData(inputBuffer, outputMD5);
                    string output = HashStr(outputMD5);

                    string resultMsg = $"[{resultArray[i].hash.i2:x8}] {input} => {output}";
                    Tool.ColorPrint(resultMsg, output.StartsWith("1145141919810") ? ConsoleColor.Green : ConsoleColor.Yellow);
                    writer.WriteLine(resultMsg);
                }
            }
        }

        //Tool.ColorPrint(new string('=', 90), ConsoleColor.Magenta);
    }
}

static string HashStr(ReadOnlySpan<byte> input) => Convert.ToHexString(input).ToLowerInvariant();