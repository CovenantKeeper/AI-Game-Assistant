using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    public static class CSVUtility
    {
        // --- CSV PARSING (READING) LOGIC ---

        /// <summary>
        /// Splits a single CSV line into fields using a robust state machine.
        /// Supports quoted fields, escaped quotes ("") and custom separator.
        /// Does NOT support embedded newlines; use ReadCsvFromString for that.
        /// </summary>
        public static IEnumerable<string> SplitCsvLine(string line, char separator = ',')
        {
            if (line == null)
                yield break;

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // Escaped quote
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++; // skip next quote
                        }
                        else
                        {
                            inQuotes = false; // closing quote
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true; // opening quote
                    }
                    else if (c == separator)
                    {
                        yield return sb.ToString();
                        sb.Length = 0;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            yield return sb.ToString();
        }

        /// <summary>
        /// Read CSV content from a string. Handles quoted fields, escaped quotes, and newlines inside quotes.
        /// </summary>
        public static List<string[]> ReadCsvFromString(string csv, char separator = ',')
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(csv)) return rows;

            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == separator)
                    {
                        row.Add(field.ToString());
                        field.Length = 0;
                    }
                    else if (c == '\r')
                    {
                        // Normalize CRLF/CR as end of line
                        if (i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                        row.Add(field.ToString());
                        field.Length = 0;
                        rows.Add(row.ToArray());
                        row.Clear();
                    }
                    else if (c == '\n')
                    {
                        row.Add(field.ToString());
                        field.Length = 0;
                        rows.Add(row.ToArray());
                        row.Clear();
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
            }

            // finalize last field/row
            row.Add(field.ToString());
            rows.Add(row.ToArray());
            return rows;
        }

        /// <summary>
        /// Read CSV content from a file path (UTF-8). Handles embedded newlines in quoted fields.
        /// </summary>
        public static List<string[]> ReadCsvFromFile(string path, char separator = ',')
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new List<string[]>();

            var text = File.ReadAllText(path, Encoding.UTF8);
            return ReadCsvFromString(text, separator);
        }

        // --- CSV SAVING (WRITING) LOGIC ---

        /// <summary>
        /// Saves a list of string arrays to a CSV file at the specified path (UTF-8 without BOM).
        /// Backwards-compatible wrapper.
        /// </summary>
        public static void SaveToCsv(List<string[]> data, string path)
        {
            SaveToCsv((IEnumerable<string[]>)data, path, includeBom: false, append: false);
        }

        /// <summary>
        /// Saves rows to a CSV file with options for BOM and append.
        /// </summary>
        public static void SaveToCsv(IEnumerable<string[]> rows, string path, bool includeBom = false, bool append = false)
        {
            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                if (row == null)
                {
                    sb.AppendLine();
                    continue;
                }
                var formattedRow = row.Select(EscapeCsvField);
                sb.AppendLine(string.Join(",", formattedRow));
            }

            // Ensure the directory exists before writing
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: includeBom);
            if (append && File.Exists(path))
                File.AppendAllText(path, sb.ToString(), encoding);
            else
                File.WriteAllText(path, sb.ToString(), encoding);

            Debug.Log($"Successfully saved CSV file to: {path}");
        }

        /// <summary>
        /// Saves header + rows to CSV (convenience overload).
        /// </summary>
        public static void SaveToCsv(string[] header, IEnumerable<string[]> rows, string path, bool includeBom = false)
        {
            var all = new List<string[]>(capacity: 1 + (rows as ICollection<string[]>)?.Count ?? 0) { header };
            if (rows != null) all.AddRange(rows);
            SaveToCsv((IEnumerable<string[]>)all, path, includeBom);
        }

        /// <summary>
        /// Escapes a single field for CSV format by adding quotes if needed and doubling inner quotes.
        /// Also quotes fields with leading/trailing spaces or tabs.
        /// </summary>
        public static string EscapeCsvField(string field)
        {
            if (field == null)
                return string.Empty;

            bool needsQuotes =
                field.Contains(",") ||
                field.Contains("\"") ||
                field.Contains("\r") ||
                field.Contains("\n") ||
                field.Contains("\t") ||
                (field.Length > 0 && (field[0] == ' ' || field[field.Length - 1] == ' '));

            if (!needsQuotes) return field;

            // Escape existing quotes by doubling them up
            var escaped = field.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
    }
}