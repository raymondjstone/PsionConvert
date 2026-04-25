# PsionConvert

A Windows desktop app (WinUI 3) for reading Psion Organiser Series 3/3a/3c database files and converting them to plain text or CSV.

## What it does

Opens Psion Organiser `.ODB`-style database files (no file extension on device backups), parses the binary format, and displays the records in a table. Records can be copied to the clipboard or saved as `.txt` (aligned columns) or `.csv`.

## Supported formats

Psion database files use one of two storage layouts. Both share the same file magic (`50 00 00 10`).

| Layout | Used by | Fields |
|---|---|---|
| Variable-length (length-prefixed fields, separators `0x05`/`0x15`/`0x55`) | Books, Cds, Vids, People, and most user databases | 2–4 fields per record, variable width |
| Fixed-width (124-byte records, B-tree key index) | Not exposed to user; Books files use this internally as an index | 3 fixed fields |

Books files store records twice: a B-tree key index (with 12-byte truncated titles) followed by the full variable-length records. The parser uses the full records.

## Building

Requires Visual Studio 2022 or the .NET 8 SDK with Windows App SDK 1.8.

```
dotnet build
```

Targets `net8.0-windows10.0.19041.0`, platforms x86/x64/ARM64.

## Project layout

```
PsionConvert/
  PsionDbParser.cs       Core parser — format detection and record extraction
  MainWindow.xaml/.cs    UI — file picker, list view, copy/save
  RecordViewModel.cs     Binding model for the list view
  TestConsole/           Console test harness (not part of the main app)
```

## Parser notes

- Format is detected by scanning the whole file for `separator + length-byte + printable-text` triples. Three or more matches → variable-length format.
- Variable-length records: `[sep][len][field][len][field]…` repeated. Fields containing non-printable bytes or lengths > 80 are treated as binary overhead and skipped.
- Fixed-width fallback: 124-byte records (`8` header + `52` field1 + `52` field2 + `12` field3), with optional 16-byte gap blocks at page boundaries.
