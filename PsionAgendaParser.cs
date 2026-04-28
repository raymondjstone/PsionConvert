using System;
using System.Collections.Generic;
using System.Text;

namespace PsionConvert
{
    public static class PsionAgendaParser
    {
        // Day 0 = January 1, 1980
        private static readonly DateTime Epoch = new DateTime(1980, 1, 1);

        public static bool IsAgendaFile(byte[] data)
        {
            if (data.Length < 16) return false;
            if (data[0] != 0x50 || data[1] != 0x00 || data[2] != 0x00 || data[3] != 0x10)
                return false;

            // Look for "AGENDA" string within the first 256 bytes
            int limit = Math.Min(256, data.Length - 5);
            for (int i = 0; i < limit; i++)
            {
                if (data[i] == 0x41 && data[i + 1] == 0x47 && data[i + 2] == 0x45 &&
                    data[i + 3] == 0x4E && data[i + 4] == 0x44 && data[i + 5] == 0x41)
                    return true;
            }
            return false;
        }

        public static ParseResult Parse(byte[] data)
        {
            var result = new ParseResult { FormatName = "Psion Agenda" };
            var seen = new HashSet<string>();
            var entries = new List<(DateTime sortDate, string dateStr, string typeStr, string text)>();

            for (int i = 0; i < data.Length - 20; i++)
            {
                // Scan for FF 18 92 04 or FF 18 93 04 record markers
                if (data[i] != 0xFF || data[i + 1] != 0x18 ||
                    (data[i + 2] != 0x92 && data[i + 2] != 0x93) ||
                    data[i + 3] != 0x04)
                    continue;

                int pos = i + 4;
                if (pos >= data.Length) break;

                byte rt = data[pos++];

                // Some entries have a 2-byte record ID before the type byte (e.g. 48 00 08...)
                if (rt != 0x05 && rt != 0x08)
                {
                    if (pos + 1 < data.Length && data[pos] == 0x00 && data[pos + 1] == 0x08)
                    {
                        pos++;           // skip second byte of ID
                        rt = data[pos++]; // real type byte = 0x08
                    }
                    else continue;
                }

                if (rt == 0x05)
                    TryParseAnniversary(data, pos, seen, entries);
                else if (rt == 0x08)
                    TryParseOneTimeEvent(data, pos, seen, entries);
            }

            // Sort chronologically
            entries.Sort((a, b) => a.sortDate.CompareTo(b.sortDate));

            foreach (var (_, dateStr, typeStr, text) in entries)
                result.Records.Add(new DbRecord(dateStr, typeStr, text));

            return result;
        }

        // Anniversary / birthday record (type 05)
        // Structure after the type byte:
        //   [day: u16] [A2 AC 01 00] [00 01 00] [typeByte] [00 08 00 00] [lenByte] [text]
        //   06 [day: u16] [day: u16] [00 00] [year: u16] [flags...]
        private static void TryParseAnniversary(
            byte[] data, int pos,
            HashSet<string> seen,
            List<(DateTime, string, string, string)> entries)
        {
            if (pos + 2 > data.Length) return;
            ushort dayNum = BitConverter.ToUInt16(data, pos); pos += 2;

            // Validate UID prefix A2 AC 01 00
            if (pos + 4 > data.Length || data[pos] != 0xA2 || data[pos + 1] != 0xAC) return;
            pos += 4;

            // Skip flags 00 01 00
            if (pos + 3 > data.Length) return;
            pos += 3;

            // Sub-type: 41=Anniversary, 42=Birthday
            if (pos >= data.Length) return;
            byte typeByte = data[pos++];
            if (typeByte != 0x41 && typeByte != 0x42) return;

            // Validate 00 08 00 00
            if (pos + 4 > data.Length || data[pos] != 0x00 || data[pos + 1] != 0x08) return;
            pos += 4;

            // Length byte: encodes (textLength + 1) * 2
            if (pos >= data.Length) return;
            byte lenByte = data[pos++];
            int textLen = (lenByte >> 1) - 1;
            if (textLen <= 0 || textLen > 80 || pos + textLen > data.Length) return;

            string text = ReadPrintable(data, pos, textLen);
            pos += textLen;

            if (string.IsNullOrWhiteSpace(text)) return;

            // 06 separator
            if (pos >= data.Length || data[pos] != 0x06) return;
            pos++;

            // day x2 + 00 00 + year
            if (pos + 6 > data.Length) return;
            pos += 4; // skip repeated day numbers
            pos += 2; // skip 00 00
            ushort storedYear = pos + 2 <= data.Length ? BitConverter.ToUInt16(data, pos) : (ushort)0;

            DateTime baseDate = Epoch.AddDays(dayNum);
            DateTime sortDate;
            string dateStr;
            string typeStr;

            if (typeByte == 0x42) // Birthday — use actual birth year
            {
                if (storedYear >= 1900)
                {
                    try
                    {
                        sortDate = new DateTime(storedYear, baseDate.Month, baseDate.Day);
                        dateStr = sortDate.ToString("d MMM yyyy");
                    }
                    catch
                    {
                        sortDate = baseDate;
                        dateStr = baseDate.ToString("d MMM");
                    }
                }
                else
                {
                    sortDate = baseDate;
                    dateStr = baseDate.ToString("d MMM");
                }
                typeStr = "Birthday";
            }
            else if (dayNum <= 365) // Annual event falling in the base year 1980 — repeating
            {
                sortDate = baseDate;
                dateStr = baseDate.ToString("d MMM");
                typeStr = "Anniversary";
            }
            else // Specific one-off date stored as anniversary (e.g. first day at job)
            {
                sortDate = baseDate;
                dateStr = baseDate.ToString("d MMM yyyy");
                typeStr = "Anniversary";
            }

            string key = dateStr + "|" + text;
            if (seen.Add(key))
                entries.Add((sortDate, dateStr, typeStr, text));
        }

        // One-time event record (type 08)
        // Structure after the type byte:
        //   [00 00] [lenByte] [text] 06 [startDay: u32] [endDay: u32] [flags...]
        private static void TryParseOneTimeEvent(
            byte[] data, int pos,
            HashSet<string> seen,
            List<(DateTime, string, string, string)> entries)
        {
            if (pos + 2 > data.Length || data[pos] != 0x00 || data[pos + 1] != 0x00) return;
            pos += 2;

            if (pos >= data.Length) return;
            byte lenByte = data[pos++];
            int textLen = (lenByte >> 1) - 1;
            if (textLen <= 0 || textLen > 80 || pos + textLen > data.Length) return;

            string text = ReadPrintable(data, pos, textLen);
            pos += textLen;

            if (string.IsNullOrWhiteSpace(text)) return;

            // 06 separator
            if (pos >= data.Length || data[pos] != 0x06) return;
            pos++;

            if (pos + 2 > data.Length) return;
            ushort startDay = BitConverter.ToUInt16(data, pos);

            // Sanity check: valid range covers roughly 1980–2070
            if (startDay == 0 || startDay > 33000) return;

            DateTime date = Epoch.AddDays(startDay);
            string dateStr = date.ToString("d MMM yyyy");
            string key = dateStr + "|" + text;

            if (seen.Add(key))
                entries.Add((date, dateStr, "Event", text));
        }

        private static string ReadPrintable(byte[] data, int offset, int length)
        {
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                byte b = data[offset + i];
                if (b >= 0x20 && b <= 0x7E)
                    sb.Append((char)b);
                else if (b == 0) // null = end of string
                    break;
            }
            return sb.ToString().Trim();
        }
    }
}
