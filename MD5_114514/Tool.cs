using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MD5CycleV2;

static class Tool {
    public static void ColorPrint(string? msg, ConsoleColor color) {
        ConsoleColor oldColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(msg);
        Console.ForegroundColor = oldColor;
    }


    unsafe public static void PrintResults(Int128 avg, ReadOnlySpan<Int128> mins, ReadOnlySpan<Int128> maxs, ConsoleColor color = ConsoleColor.Yellow) {
        const double Offset = 24;

        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine($" 平均: {avg:x32} (2^{Math.Log2((double)avg) - Offset:0.0000})");
        for (int i = 0; i < mins.Length; i++) {
            Console.WriteLine($"最小{i}: {mins[i]:x32} (2^{Math.Log2((double)mins[i]) - Offset:0.0000})");
        }
        for (int i = maxs.Length - 1; i >= 0; i--) {
            Console.WriteLine($"最大{i}: {maxs[i]:x32} (2^{Math.Log2((double)maxs[i]) - Offset:0.0000})");
        }
        Console.ForegroundColor = oldColor;
    }
}

