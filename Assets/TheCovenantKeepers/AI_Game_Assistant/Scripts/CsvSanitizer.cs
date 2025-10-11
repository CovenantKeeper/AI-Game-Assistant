// CsvSanitizer.cs (Editor-only)
using System;
using System.Text;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor.CSV
{
    internal static class CsvSanitizer
    {
        public static int GetColumnCount(string line)
        {
            bool inQuotes = false;
            int cols = 1;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes) cols++;
            }
            return cols;
        }

        /// Keeps only the block starting at the LAST occurrence of 'expectedHeader'.
        /// Infers column count from that header; drops any repeated headers and rows
        /// with mismatching column counts.
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

        /// Variant: allow multiple possible headers; picks the last match and infers columns.
        public static string StripToAnyAllowedHeader(string raw, params string[] allowedHeaders)
        {
            if (string.IsNullOrEmpty(raw) || allowedHeaders == null || allowedHeaders.Length == 0) return raw;

            var text = raw.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            var lines = text.Split('\n');

            int headerIdx = -1;
            string chosenHeader = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                foreach (var h in allowedHeaders)
                {
                    if (string.Equals(line, h, StringComparison.Ordinal))
                    {
                        headerIdx = i;
                        chosenHeader = h;
                    }
                }
            }
            if (headerIdx < 0) return raw;

            int expectedCols = GetColumnCount(chosenHeader);

            var sb = new StringBuilder();
            sb.AppendLine(chosenHeader);

            for (int i = headerIdx + 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (string.Equals(line, chosenHeader, StringComparison.Ordinal)) continue;
                if (GetColumnCount(line) == expectedCols) sb.AppendLine(line);
            }
            return sb.ToString().TrimEnd();
        }
    }
}
