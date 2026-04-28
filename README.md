# PsionConvert

A Windows desktop app (WinUI 3) for reading Psion Organiser Series 3/3a/3c database files and Psion Series 5 (EPOC32) Agenda files, and converting them to plain text or CSV.

## What it does

Opens Psion backup files (no file extension on device backups), parses the binary format, and displays the records in a table. Records can be copied to the clipboard or saved as `.txt` (aligned columns) or `.csv`.

The correct parser is selected automatically based on the file contents.

## Supported formats

All supported formats share the same file magic (`50 00 00 10`).

### Psion Series 3/3a/3c Organiser databases

Two storage layouts:

| Layout | Used by | Fields |
|---|---|---|
| Variable-length (length-prefixed fields, separators `0x05`/`0x15`/`0x55`) | Books, Cds, Vids, People, and most user databases | 2–4 fields per record, variable width |
| Fixed-width (124-byte records, B-tree key index) | Not exposed to user; Books files use this internally as an index | 3 fixed fields |

Books files store records twice: a B-tree key index (with 12-byte truncated titles) followed by the full variable-length records. The parser uses the full records.

### Psion Series 5 (EPOC32) Agenda files

Detected by the presence of the string `AGENDA` in the first 256 bytes of the file.

| Record type | Description | Output fields |
|---|---|---|
| Anniversary (`0x41`) | Annually repeating event | Date (day + month), "Anniversary", description |
| Birthday (`0x42`) | Birthday with stored birth year | Date (day + month + year), "Birthday", name |
| One-time event (`0x08`) | Single dated event | Date (day + month + year), "Event", description |

Records are deduplicated (the EPOC32 B-tree structure stores many entries twice) and sorted chronologically.

## Building

Requires Visual Studio 2022 or the .NET 8 SDK with Windows App SDK 1.8.

```
dotnet build
```

Targets `net8.0-windows10.0.19041.0`, platforms x86/x64/ARM64.

## Project layout

```
PsionConvert/
  PsionDbParser.cs       Series 3 database parser — format detection and record extraction
  PsionAgendaParser.cs   Series 5 Agenda parser — anniversaries, birthdays, one-time events
  MainWindow.xaml/.cs    UI — file picker, list view, copy/save
  RecordViewModel.cs     Binding model for the list view
  TestConsole/           Console test harness (not part of the main app)
```

## Parser notes

**Series 3 databases:**
- Format is detected by scanning the whole file for `separator + length-byte + printable-text` triples. Three or more matches → variable-length format.
- Variable-length records: `[sep][len][field][len][field]…` repeated. Fields containing non-printable bytes or lengths > 80 are treated as binary overhead and skipped.
- Fixed-width fallback: 124-byte records (`8` header + `52` field1 + `52` field2 + `12` field3), with optional 16-byte gap blocks at page boundaries.

**Series 5 Agenda:**
- Records are located by scanning for `FF 18 92 04` / `FF 18 93 04` markers.
- Day numbers are days since 1 January 1980 (epoch = day 0).
- String length is encoded as `(textLength + 1) × 2`.
- One-time event start day is stored as uint16 followed by a uint16 time-of-day field (minutes past midnight).
