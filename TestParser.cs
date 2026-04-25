// Quick test - delete this file after verification
#if DEBUG
using System;
using System.IO;

namespace PsionConvert
{
    static class TestParser
    {
        public static void RunTest(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            var result = PsionDbParser.Parse(data);

            Console.WriteLine($"Format: {result.FormatName}");
            Console.WriteLine($"Records: {result.Records.Count}");
            if (!string.IsNullOrEmpty(result.Error))
                Console.WriteLine($"Error: {result.Error}");

            for (int i = 0; i < Math.Min(result.Records.Count, 10); i++)
            {
                var r = result.Records[i];
                Console.WriteLine($"  [{string.Join(" | ", r.Fields)}]");
            }
        }
    }
}
#endif
