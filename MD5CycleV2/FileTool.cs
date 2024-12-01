using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace MD5CycleV2;

static class FileTool {
    readonly static string BasePath = Environment.CurrentDirectory;
    public readonly static string InitHashesFile = Path.Combine(BasePath, "init.hashes");
    readonly static string DictFile = Path.Combine(BasePath, "result");
    readonly static string HashesFile = Path.Combine(BasePath, "hashes");
    readonly static string BakDictFile = Path.Combine(BasePath, "bak_result");
    readonly static string BakHashesFile = Path.Combine(BasePath, "bak_hashes");
    readonly static string MD5File = Path.Combine(BasePath, "md5");

    [SkipLocalsInit]
    static Hash GetFileMD5(string filename) {
        using var stream = File.OpenRead(filename);
        Unsafe.SkipInit(out Hash hash);

        MD5.HashData(stream, hash.Span);

        return hash;
    }

    [SkipLocalsInit]
    static Hash? GetAllMD5() {
        try {
            ReadOnlySpan<Hash> hash2 = [GetFileMD5(DictFile), GetFileMD5(HashesFile)];
            Unsafe.SkipInit(out Hash hash);
            MD5.HashData(MemoryMarshal.AsBytes(hash2), hash.Span);
            return hash;
        } catch {
            return null;
        }
    }

    static bool CheckMD5() {
        try {
            ReadOnlySpan<byte> saveMd5 = Convert.FromHexString(File.ReadAllText(MD5File));
            if (saveMd5.Length != 16) return false;
            if (GetAllMD5() is not Hash calcMd5) return false;

            return calcMd5 == MemoryMarshal.Read<Hash>(saveMd5);
        } catch {
            return false;
        }
    }

    unsafe static void SaveDict(Stream stream, ResultSet result, long iterateCnt) {
        stream.Write(new ReadOnlySpan<byte>(&iterateCnt, sizeof(long)));

        foreach (ref readonly Result r in result) {
            stream.Write(MemoryMarshal.AsBytes(new ReadOnlySpan<Result>(in r)));
        }
    }

    static void SaveHashes(Stream stream, Hash[] hashes) {
        stream.Write(MemoryMarshal.AsBytes((ReadOnlySpan<Hash>)hashes));
    }

    public static void BackupFile(bool copy = false) {
        if (!CheckMD5()) {
            Tool.ColorPrint($"备份错误：md5校验失败", ConsoleColor.Red);
            return;
        }

        try {
            BackupFile(DictFile, BakDictFile, copy);
            BackupFile(HashesFile, BakHashesFile, copy);
        } catch (Exception e) {
            Delete(BakDictFile);
            Delete(BakHashesFile);

            Tool.ColorPrint($"备份失败：{e}", ConsoleColor.Red);
        }


        static void BackupFile(string file, string bakFile, bool copy) {
            if (copy) {
                File.Copy(file, bakFile, overwrite: true);
            } else {
                File.Move(file, bakFile, overwrite: true);
            }
        }

        static void Delete(string file) {
            try {
                File.Delete(file);
            } catch {

            }
        }
    }

    public static void Save(ResultSet result, long iterateCnt, Hash[] hashes) {
        try {
            using (var dictStream = File.OpenWrite(DictFile))
            using (var hashesStream = File.OpenWrite(HashesFile)) {
                SaveDict(dictStream, result, iterateCnt);
                SaveHashes(hashesStream, hashes);
            }

            if (GetAllMD5() is Hash md5) {
                string strMd5 = Convert.ToHexString(md5.ReadOnlySpan);
                File.WriteAllText(MD5File, strMd5);
            } else {
                Tool.ColorPrint("保存失败：未能成功计算文件MD5", ConsoleColor.Red);
            }
        } catch (Exception e) {
            Tool.ColorPrint($"保存失败：{e}", ConsoleColor.Red);
        }
    }

    unsafe static Hash[]? LoadHashes(string hashesFile) {
        try {
            using var hashesStream = File.OpenRead(hashesFile);

            int hashCount = Math.DivRem(checked((int)hashesStream.Length), sizeof(Hash), out int rem);
            if (rem != 0) throw new FormatException($"文件'{hashesFile}'字节数不对齐");

            Hash[] hashes = GC.AllocateUninitializedArray<Hash>(hashCount);
            hashesStream.ReadExactly(MemoryMarshal.AsBytes(hashes.AsSpan()));

            return hashes;
        } catch (IOException) {
            return null;
        }
    }

    unsafe static (ResultSet result, long iterateCnt, Hash[] hashes)? Load(string dictFile, string hashesFile) {
        if (LoadHashes(hashesFile) is not Hash[] hashes) return null;

        try {
            using var dictStream = File.OpenRead(dictFile);

            long iterateCnt = 0;
            dictStream.ReadExactly(new Span<byte>(&iterateCnt, sizeof(long)));

            int resultCount = checked((int)Math.DivRem(dictStream.Length - sizeof(long), sizeof(Result), out long rem2));
            if (rem2 != 0) throw new FormatException($"文件'{dictFile}'字节数不对齐");

            Tool.ColorPrint($"总数: {resultCount}, 加载中...", ConsoleColor.Yellow);

            var results = new ResultSet(resultCount);
            Result resultValue = default;

            for (int i = 0; i < resultCount; i++) {
                dictStream.ReadExactly(new Span<byte>(&resultValue, sizeof(Result)));

                if (!results.Add(resultValue, out Result existValue)) {
                    Tool.ColorPrint($"碰撞: {resultValue} <===> {existValue}", ConsoleColor.Green);
                }
            }

            return (results, iterateCnt, hashes);
        } catch (IOException) {
            return null;
        }
    }

    public static (ResultSet result, long iterateCnt, Hash[] hashes) Load(int blockCount, int threadCount) {
        if (CheckMD5()) {
            if (Load(DictFile, HashesFile) is { } result) {
                Tool.ColorPrint($"加载配置成功", ConsoleColor.Green);
                return result;
            }
        }

        if (Load(BakDictFile, BakHashesFile) is { } bakResult) {
            Tool.ColorPrint($"检测到配置文件损坏，但已从备份配置文件中加载", ConsoleColor.DarkYellow);
            return bakResult;
        }

        if (LoadHashes(InitHashesFile) is Hash[] hashes) {
            Tool.ColorPrint($"未能找到配置文件，但已加载初始化配置", ConsoleColor.DarkYellow);
            return (new ResultSet(), 0, hashes);
        }

        Hash[] initHashes = new Hash[blockCount * threadCount];
        Random.Shared.NextBytes(MemoryMarshal.AsBytes(initHashes.AsSpan()));
        using (var fileStream = File.OpenWrite(InitHashesFile)) {
            fileStream.Write(MemoryMarshal.AsBytes(initHashes.AsSpan()));
        }

        Tool.ColorPrint($"未能找到配置文件和初始文件，但已创建随机初始文件", ConsoleColor.DarkYellow);

        return (new ResultSet(), 0, initHashes);
    }
}

