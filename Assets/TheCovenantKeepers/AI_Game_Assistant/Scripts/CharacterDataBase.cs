using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    public static class CharacterDatabase
    {
        public static readonly List<CharacterData> Characters = new List<CharacterData>();

        public static void LoadCharacterMasterlist(string assetCsvPath)
        {
            Characters.Clear();

            if (string.IsNullOrEmpty(assetCsvPath) || !File.Exists(assetCsvPath))
            {
                Debug.LogWarning($"CharacterDatabase: CSV not found at '{assetCsvPath}'.");
                return;
            }

            var lines = File.ReadAllLines(assetCsvPath);
            if (lines.Length == 0) { Debug.LogWarning("CharacterDatabase: CSV is empty."); return; }

            // Header
            var header = SplitCsv(lines[0]).ToArray();
            var normHeader = NormalizeHeader(header);

            // Rows
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i]?.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var values = SplitCsv(line).ToArray();
                var data = CreateFromRow<CharacterData>(normHeader, values);
                if (data != null) Characters.Add(data);
            }

            Debug.Log($"CharacterDatabase now contains {Characters.Count} entries.");
        }

        // ---------- Generic helpers ----------

        private static CharacterData CreateFromRow<T>(string[] normHeader, string[] values) where T : ScriptableObject
        {
            try
            {
                var type = typeof(T);
                var so = ScriptableObject.CreateInstance(type);

                for (int i = 0; i < normHeader.Length && i < values.Length; i++)
                {
                    var key = normHeader[i];          // normalized header token
                    var raw = values[i];

                    // Try fields first
                    var field = FindField(type, key);
                    if (field != null)
                    {
                        object v = ConvertTo(field.FieldType, raw);
                        field.SetValue(so, v);
                        continue;
                    }

                    // Then properties with setter
                    var prop = FindProperty(type, key);
                    if (prop != null)
                    {
                        object v = ConvertTo(prop.PropertyType, raw);
                        prop.SetValue(so, v, null);
                    }
                }

                return (CharacterData)so;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"CharacterDatabase row parse failed: {ex.Message}");
                return null;
            }
        }

        private static FieldInfo FindField(Type t, string normalizedName)
        {
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (NormalizeToken(f.Name) == normalizedName) return f;
            }
            return null;
        }

        private static PropertyInfo FindProperty(Type t, string normalizedName)
        {
            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!p.CanWrite) continue;
                if (NormalizeToken(p.Name) == normalizedName) return p;
            }
            return null;
        }

        private static object ConvertTo(Type targetType, string raw)
        {
            try
            {
                if (targetType == typeof(string)) return raw ?? "";
                if (targetType == typeof(int)) return int.TryParse(raw, out var i) ? i : 0;
                if (targetType == typeof(float)) return float.TryParse(raw, out var f) ? f : 0f;
                if (targetType == typeof(double)) return double.TryParse(raw, out var d) ? d : 0d;
                if (targetType == typeof(bool))
                {
                    if (bool.TryParse(raw, out var b)) return b;
                    if (int.TryParse(raw, out var bi)) return bi != 0;
                    return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
                }
                return raw;
            }
            catch { return targetType.IsValueType ? Activator.CreateInstance(targetType) : null; }
        }

        private static string[] NormalizeHeader(string[] header)
        {
            var arr = new string[header.Length];
            for (int i = 0; i < header.Length; i++) arr[i] = NormalizeToken(header[i]);
            return arr;
        }

        private static string NormalizeToken(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : s.Replace(" ", "").Replace("_", "").ToLowerInvariant();
        }

        /// <summary>CSV split supporting quotes and commas inside quotes.</summary>
        private static List<string> SplitCsv(string line)
        {
            var result = new List<string>();
            if (line == null) { result.Add(""); return result; }

            bool inQuotes = false;
            var cur = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        // Escaped quote
                        cur.Append('\"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(cur.ToString());
                    cur.Length = 0;
                }
                else
                {
                    cur.Append(c);
                }
            }

            result.Add(cur.ToString());
            return result;
        }
    }
}
