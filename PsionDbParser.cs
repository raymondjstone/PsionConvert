using System;
using System.Collections.Generic;
using System.Text;

namespace PsionConvert
{
    public class DbRecord
    {
        public List<string> Fields { get; } = new();

        public DbRecord(params string[] fields)
        {
            Fields.AddRange(fields);
        }
    }

    public class ParseResult
    {
        public List<DbRecord> Records { get; } = new();
        public string FormatName { get; set; } = "";
        public string Error { get; set; } = "";
    }

    public static class PsionDbParser
    {
        // Fixed-width format constants (Books-style)
        private const int FixedRecordSize = 124;
        private const int FixedHeaderSize = 8;
        private const int FixedField1Size = 52;
        private const int FixedField2Size = 52;
        private const int FixedField3Size = 12;
        private const int FixedGapSize = 16;

        public static ParseResult Parse(byte[] data)
        {
            if (data.Length < 64)
                return new ParseResult { Error = "File too small to be a Psion DB file." };

            // Both formats share the same file magic
            if (data[0] != 0x50 || data[1] != 0x00 || data[2] != 0x00 || data[3] != 0x10)
                return new ParseResult { Error = "Not a recognised Psion Organiser database file." };

            if (IsVariableLengthFormat(data))
                return ParseVariableLength(data);

            return ParseFixedWidth(data);
        }

        // Variable-length format: scan whole file for separator+length+printable-text triples
        private static bool IsVariableLengthFormat(byte[] data)
        {
            int separators = 0;
            for (int i = 0x60; i < data.Length - 8; i++)
            {
                if (!IsRecordSeparator(data[i])) continue;
                int len = data[i + 1];
                if (len == 0 || len > 50 || i + 1 + len >= data.Length) continue;
                bool allPrintable = true;
                for (int j = i + 2; j < i + 2 + len && j < data.Length; j++)
                {
                    if (data[j] < 0x20 || data[j] > 0x7E) { allPrintable = false; break; }
                }
                if (allPrintable) separators++;
            }
            return separators >= 3;
        }

        // 0x55, 0x15, 0x05 all act as record separators across the Psion variable-length DB formats
        private static bool IsRecordSeparator(byte b) => b == 0x55 || b == 0x15 || b == 0x05;

        private static ParseResult ParseVariableLength(byte[] data)
        {
            var result = new ParseResult { FormatName = "Variable-length" };

            int pos = FindFirstRecordSeparator(data);
            if (pos < 0)
            {
                result.Error = "Could not find record data in file.";
                return result;
            }

            while (pos < data.Length)
            {
                if (!IsRecordSeparator(data[pos])) { pos++; continue; }
                pos++; // skip separator

                var fields = new List<string>();
                while (pos < data.Length && !IsRecordSeparator(data[pos]))
                {
                    int fieldLen = data[pos];
                    pos++;

                    if (fieldLen == 0) break;
                    if (pos + fieldLen > data.Length) break;

                    // Field lengths > 80 are likely binary structural data, not text
                    if (fieldLen > 80) { pos -= 1; break; }

                    bool printable = true;
                    for (int i = pos; i < pos + fieldLen; i++)
                    {
                        if (data[i] < 0x20 || data[i] > 0x7E) { printable = false; break; }
                    }

                    if (!printable) { pos -= 1; break; } // stop reading this record, seek next separator

                    fields.Add(Encoding.ASCII.GetString(data, pos, fieldLen));
                    pos += fieldLen;
                }

                if (fields.Count > 0 && fields[0].Length > 0)
                    result.Records.Add(new DbRecord(fields.ToArray()));

                // Advance past any binary garbage to next separator
                while (pos < data.Length && !IsRecordSeparator(data[pos]))
                    pos++;
            }

            return result;
        }

        private static int FindFirstRecordSeparator(byte[] data)
        {
            for (int i = 0x80; i < data.Length - 2; i++)
            {
                if (!IsRecordSeparator(data[i])) continue;
                int len = data[i + 1];
                if (len == 0 || len > 50 || i + 1 + len >= data.Length) continue;
                bool ok = true;
                for (int j = i + 2; j < i + 2 + len; j++)
                {
                    if (data[j] < 0x20 || data[j] > 0x7E) { ok = false; break; }
                }
                if (ok) return i;
            }
            return -1;
        }

        private static ParseResult ParseFixedWidth(byte[] data)
        {
            var result = new ParseResult { FormatName = "Fixed-width (Books)" };

            int startOffset = FindFixedWidthStart(data);
            if (startOffset < 0)
            {
                result.Error = "Could not locate record data.";
                return result;
            }

            int pos = startOffset;
            while (pos + FixedRecordSize <= data.Length)
            {
                uint recordId = BitConverter.ToUInt32(data, pos + 4);

                // Zero ID = gap block (16 bytes) interspersed in stream
                if (recordId == 0)
                {
                    // Verify this is a gap by checking if a valid record starts 16 bytes ahead
                    if (pos + FixedGapSize + FixedRecordSize <= data.Length &&
                        BitConverter.ToUInt32(data, pos + FixedGapSize + 4) != 0 &&
                        data[pos + FixedGapSize + FixedHeaderSize] >= 0x20)
                    {
                        pos += FixedGapSize;
                        continue;
                    }
                    pos += FixedRecordSize;
                    continue;
                }

                string f1 = ReadNullTerminated(data, pos + FixedHeaderSize, FixedField1Size);
                string f2 = ReadNullTerminated(data, pos + FixedHeaderSize + FixedField1Size, FixedField2Size);
                string f3 = ReadNullTerminated(data, pos + FixedHeaderSize + FixedField1Size + FixedField2Size, FixedField3Size);

                if (f1.Length > 0)
                    result.Records.Add(new DbRecord(f1, f2, f3));

                pos += FixedRecordSize;
            }

            return result;
        }

        private static int FindFixedWidthStart(byte[] data)
        {
            // Scan from 0x100 onwards for the first valid record.
            // Author names always start with an uppercase letter (A-Z).
            for (int i = 0x100; i + FixedRecordSize <= data.Length; i++)
            {
                uint id = BitConverter.ToUInt32(data, i + 4);
                if (id == 0) continue;

                byte firstChar = data[i + FixedHeaderSize];
                if (firstChar < 'A' || firstChar > 'Z') continue; // uppercase only

                // Verify a second record follows at i + 124
                if (i + FixedRecordSize + FixedRecordSize <= data.Length)
                {
                    uint nextId = BitConverter.ToUInt32(data, i + FixedRecordSize + 4);
                    byte nextChar = data[i + FixedRecordSize + FixedHeaderSize];
                    if (nextId != 0 && IsUppercase(nextChar))
                        return i;

                    // Maybe there's a 16-byte gap before the second record
                    if (i + FixedRecordSize + FixedGapSize + FixedRecordSize <= data.Length)
                    {
                        nextId = BitConverter.ToUInt32(data, i + FixedRecordSize + FixedGapSize + 4);
                        nextChar = data[i + FixedRecordSize + FixedGapSize + FixedHeaderSize];
                        if (nextId != 0 && IsUppercase(nextChar))
                            return i;
                    }
                }
            }
            return -1;
        }

        private static bool IsUppercase(byte b) => b >= 'A' && b <= 'Z';

        private static string ReadNullTerminated(byte[] data, int offset, int maxLength)
        {
            if (offset >= data.Length) return "";
            int end = offset;
            int limit = Math.Min(offset + maxLength, data.Length);
            while (end < limit && data[end] != 0) end++;
            return Encoding.ASCII.GetString(data, offset, end - offset);
        }
    }
}
