// Assets/TheCovenantKeepers/AI_Game_Assistant/Scripts/CsvFlex.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    /// <summary>
    /// Finds the LAST stable CSV block in a text blob (header + rows),
    /// infers the column count from the header (no hard-coding),
    /// and returns only rows that match that count.
    /// </summary>
    internal static class CsvFlex
    {
        public sealed class CsvBlock
        {
            public string Header;
            public string[] Columns;
            public int ColumnCount;
            public List<string> DataLines = new();
            public string ToCsv() => Header + "\n" + string.Join("\n", DataLines);
        }

        public static CsvBlock ExtractLastConsistentCsvBlock(string raw, int minDataRows = 1)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // Normalize + split (trim empties)
            var text = raw.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = new List<string>();
            foreach (var ln in text.Split('\n'))
            {
                var t = ln.Trim();
                if (!string.IsNullOrEmpty(t)) lines.Add(t);
            }
            if (lines.Count == 0) return null;

            // Scan EVERY potential header; choose the one that yields the MOST matching rows
            int bestHeaderIndex = -1;
            int bestCols = 0;
            List<string> bestRows = null;

            for (int h = 0; h < lines.Count - 1; h++)
            {
                var header = lines[h];
                int cols = CountColumns(header);
                var rows = new List<string>();

                for (int r = h + 1; r < lines.Count; r++)
                {
                    var line = lines[r];
                    if (line == header) continue;                 // skip repeated header echoes
                    if (CountColumns(line) == cols) rows.Add(line);
                }

                if (rows.Count >= Math.Max(1, minDataRows))
                {
                    if (bestRows == null || rows.Count > bestRows.Count)
                    {
                        bestHeaderIndex = h;
                        bestCols = cols;
                        bestRows = rows;
                    }
                }
            }

            if (bestHeaderIndex < 0 || bestRows == null || bestRows.Count == 0) return null;

            var chosenHeader = lines[bestHeaderIndex];
            var block = new CsvBlock
            {
                Header = chosenHeader,
                Columns = SplitCsvRow(chosenHeader),
                ColumnCount = bestCols,
                DataLines = bestRows
            };
            return block;
        }

        // --- Helpers: CSV counting & splitting with quote awareness ---

        public static int CountColumns(string line)
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

        public static string[] SplitCsvRow(string line)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    // toggle quotes, but allow escaped double quotes ("")
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"'); i++; // consume escaped quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    list.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            list.Add(sb.ToString());
            return list.ToArray();
        }

        public static string NormalizeToHeader(string csv, string targetHeader)
        {
            if (string.IsNullOrWhiteSpace(csv)) return csv;

            var text = csv.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            var lines = text.Split('\n');
            if (lines.Length == 0) return csv;

            var srcHeader = lines[0].Trim();
            var srcCols = SplitCsvRow(srcHeader);
            var tgtCols = SplitCsvRow(targetHeader);

            // Build source column index (case-insensitive)
            var srcIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < srcCols.Length; i++)
            {
                var key = srcCols[i].Trim();
                if (!srcIndex.ContainsKey(key)) srcIndex[key] = i;
            }

            var sb = new StringBuilder();
            sb.AppendLine(targetHeader);

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                var row = SplitCsvRow(line);

                for (int t = 0; t < tgtCols.Length; t++)
                {
                    if (t > 0) sb.Append(',');
                    var tgt = tgtCols[t].Trim();
                    string cell = "";
                    if (srcIndex.TryGetValue(tgt, out var si) && si < row.Length) cell = row[si];
                    sb.Append(QuoteIfNeeded(cell));
                }
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        private static string QuoteIfNeeded(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            bool needsQuotes = v.IndexOfAny(new[] { ',', '"', '\n', '\r', ';' }) >= 0;
            if (v.Contains("\"")) v = v.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{v}\"" : v;
        }

        public static CsvBlock ExtractMergedCsvByDominantSchema(string raw, int minDataRows = 1)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // Normalize + strip empties
            var text = raw.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = new List<string>();
            foreach (var ln in text.Split('\n'))
            {
                var t = ln.Trim();
                if (!string.IsNullOrEmpty(t)) lines.Add(t);
            }
            if (lines.Count == 0) return null;

            // 1) Group by column count and separate header-like vs data-like
            var groups = new Dictionary<int, (List<string> headers, List<string> data)>();
            foreach (var line in lines)
            {
                int cols = CountColumns(line);
                if (!groups.TryGetValue(cols, out var g))
                    g = (new List<string>(), new List<string>());
                if (IsLikelyHeader(line)) g.headers.Add(line); else g.data.Add(line);
                groups[cols] = g;
            }
            if (groups.Count == 0) return null;

            // 2) Pick the schema with the MOST data-like rows
            int dominantCols = -1;
            List<string> dominantData = null;
            List<string> dominantHeaders = null;
            foreach (var kv in groups)
            {
                var dataList = kv.Value.data;
                if (dataList.Count >= Math.Max(1, minDataRows))
                {
                    if (dominantData == null || dataList.Count > dominantData.Count)
                    {
                        dominantCols = kv.Key;
                        dominantData = dataList;
                        dominantHeaders = kv.Value.headers;
                    }
                }
            }
            if (dominantCols < 1 || dominantData == null || dominantData.Count == 0) return null;

            // 3) Choose a header candidate for this schema (prefer a real header if present)
            string header = null;
            if (dominantHeaders != null && dominantHeaders.Count > 0)
                header = dominantHeaders[dominantHeaders.Count - 1]; // bias toward the last printed header
            else
                header = $"Col1{string.Concat(Enumerable.Range(2, dominantCols - 1).Select(i => $",Col{i}"))}"; // synthesized fallback

            // 4) Merge ALL data-like lines that match this schema (skip header-like lines)
            var merged = new List<string>();
            foreach (var line in lines)
            {
                if (CountColumns(line) != dominantCols) continue;
                if (IsLikelyHeader(line)) continue; // drop any header echoes
                merged.Add(line);
            }
            if (merged.Count < Math.Max(1, minDataRows)) return null;

            return new CsvBlock
            {
                Header = header,
                Columns = SplitCsvRow(header),
                ColumnCount = dominantCols,
                DataLines = merged
            };
        }

        // --- heuristics for header detection (schema-agnostic) ---
        private static bool IsLikelyHeader(string line)
        {
            var cells = SplitCsvRow(line);
            int wordy = 0, bad = 0;
            foreach (var c in cells)
            {
                var s = c.Trim();
                if (s.IndexOf('/') >= 0 || s.IndexOf('\\') >= 0 || s.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    return false; // looks like a path/data row
                bool hasDigit = false;
                for (int i = 0; i < s.Length; i++) if (char.IsDigit(s[i])) { hasDigit = true; break; }
                if (!hasDigit) wordy++; else bad++;
            }
            // header typically has mostly words and few digits
            return wordy >= Math.Max(2, cells.Length / 2);
        }
    }
}
