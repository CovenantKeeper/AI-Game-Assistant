// Assets/TheCovenantKeepers/AI_Game_Assistant/Scripts/CsvSanitizer.cs
using System;
using System.Text;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    internal static class CsvSanitizer
    {
        public static int GetColumnCount(string line)
        {
            bool inQuotes = false; int cols = 1;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes) cols++;
            }
            return cols;
        }

        public static string StripToExpectedHeader(string raw, string expectedHeader)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            var text = raw.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            var lines = text.Split('\n');

            int headerIdx = -1;
            for (int i = 0; i < lines.Length; i++)
                if (string.Equals(lines[i].Trim(), expectedHeader, StringComparison.Ordinal))
                    headerIdx = i;

            if (headerIdx < 0) return raw;

            int expectedCols = GetColumnCount(expectedHeader);
            var sb = new StringBuilder();
            sb.AppendLine(expectedHeader);

            for (int i = headerIdx + 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (string.Equals(line, expectedHeader, StringComparison.Ordinal)) continue;
                if (GetColumnCount(line) == expectedCols) sb.AppendLine(line);
            }
            return sb.ToString().TrimEnd();
        }
    }
}
