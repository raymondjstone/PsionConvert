using System;
using System.IO;
using PsionConvert;

string[] testFiles =
{
    @"C:\Users\passp\OneDrive\PSION\Raymond Stone\Backup\Internal\Database\Books",
    @"C:\Users\passp\OneDrive\PSION\Raymond Stone\Backup\Internal\Database\Cds",
    @"C:\Users\passp\OneDrive\PSION\Raymond Stone\Backup\Internal\Database\People",
    @"C:\Users\passp\OneDrive\PSION\Raymond Stone\Backup\Internal\Database\Vids",
};

foreach (string path in testFiles)
{
    if (!File.Exists(path)) continue;
    Console.WriteLine($"\n=== {Path.GetFileName(path)} ===");

    using (var s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { }
    byte[] data = File.ReadAllBytes(path);

    var result = PsionDbParser.Parse(data);
    Console.WriteLine($"Format: {result.FormatName}  Records: {result.Records.Count}");
    if (!string.IsNullOrEmpty(result.Error)) Console.WriteLine($"Error: {result.Error}");

    int show = path.Contains("Books") ? result.Records.Count : 8;
    for (int i = 0; i < Math.Min(result.Records.Count, show); i++)
        Console.WriteLine($"  [{i}] {string.Join(" | ", result.Records[i].Fields)}");
}
