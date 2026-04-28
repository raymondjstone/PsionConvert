using System;
using System.IO;
using PsionConvert;

string[] testFiles =
{
    @"C:\Users\passp\OneDrive\PSION\Raymond Stone\Backup\External\Documents\Agenda",
    @"C:\Users\passp\OneDrive\PSION\Raymond Stone\Backup\Internal\Database\Books",
    @"C:\Users\passp\OneDrive\PSION\Raymond Stone\Backup\Internal\Database\Cds",
    @"C:\Users\passp\OneDrive\PSION\Raymond Stone\Backup\Internal\Database\People",
    @"C:\Users\passp\OneDrive\PSION\Raymond Stone\Backup\Internal\Database\Vids",
};

foreach (string path in testFiles)
{
    if (!File.Exists(path)) continue;
    Console.WriteLine($"\n=== {Path.GetFileName(path)} ===");

    byte[] data = File.ReadAllBytes(path);

    ParseResult result;
    if (PsionAgendaParser.IsAgendaFile(data))
        result = PsionAgendaParser.Parse(data);
    else
        result = PsionDbParser.Parse(data);

    Console.WriteLine($"Format: {result.FormatName}  Records: {result.Records.Count}");
    if (!string.IsNullOrEmpty(result.Error)) Console.WriteLine($"Error: {result.Error}");

    int show = path.Contains("Books") ? result.Records.Count : result.Records.Count;
    for (int i = 0; i < Math.Min(result.Records.Count, show); i++)
        Console.WriteLine($"  [{i + 1}] {string.Join(" | ", result.Records[i].Fields)}");
}
