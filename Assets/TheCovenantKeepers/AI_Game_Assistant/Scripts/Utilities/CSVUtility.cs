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
        /// Splits a single line of a CSV file, correctly handling commas inside quoted fields.
        /// </summary>
        /// <param name="line">The line of text to parse.</param>
        /// <returns>An enumerable of strings representing the columns.</returns>
        public static IEnumerable<string> SplitCsvLine(string line)
        {
            // This regex correctly handles quoted strings, including escaped quotes.
            const string pattern = ",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))";
            var parts = Regex.Split(line, pattern);
            foreach (var part in parts)
            {
                // Remove the quotes and trim whitespace
                yield return part.Trim(' ', '"');
            }
        }

        // --- CSV SAVING (WRITING) LOGIC ---

        /// <summary>
        /// Saves a list of string arrays to a CSV file at the specified path.
        /// </summary>
        /// <param name="data">The data to save, where the first entry is the header row.</param>
        /// <param name="path">The full file path to save to.</param>
        public static void SaveToCsv(List<string[]> data, string path)
        {
            var sb = new StringBuilder();

            foreach (var row in data)
            {
                var formattedRow = row.Select(EscapeCsvField).ToArray();
                sb.AppendLine(string.Join(",", formattedRow));
            }

            // Ensure the directory exists before writing
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Debug.Log($"Successfully saved CSV file to: {path}");
        }

        /// <summary>
        /// Escapes a single field for CSV format by adding quotes if it contains a comma or quotes.
        /// </summary>
        public static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return "";
            }

            // If the field contains a comma, a quote, or a newline, wrap it in quotes
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                // Escape existing quotes by doubling them up
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }

            return field;
        }
    }
}