using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    public static class LocationDatabase
    {
        public static readonly List<LocationData> Locations = new List<LocationData>();

        public static void LoadLocationMasterlist(string assetCsvPath)
        {
            Locations.Clear();

            if (string.IsNullOrEmpty(assetCsvPath) || !File.Exists(assetCsvPath))
            {
                Debug.LogWarning($"LocationDatabase: CSV not found at '{assetCsvPath}'.");
                return;
            }

            var lines = File.ReadAllLines(assetCsvPath);
            if (lines.Length == 0) { Debug.LogWarning("LocationDatabase: CSV is empty."); return; }
            if (lines.Length > 1 && lines[1].TrimStart().StartsWith("{"))
            {
                Debug.LogError("LocationDatabase: File seems to contain a JSON error response, not CSV. Regenerate after fixing API settings.");
                return;
            }

            var header = CSVUtility.SplitCsvLine(lines[0]).Select(s => s?.Trim()).ToArray();
            var norm = NormalizeHeader(header);

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i]?.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var values = CSVUtility.SplitCsvLine(line).Select(s => s?.Trim()).ToArray();
                var data = CreateFromRow<LocationData>(norm, values);
                if (data != null) Locations.Add(data);
            }

            Debug.Log($"LocationDatabase now contains {Locations.Count} entries.");
        }

        // generic helpers
        private static T CreateFromRow<T>(string[] normHeader, string[] values) where T : ScriptableObject
        {
            try
            {
                var type = typeof(T);
                var so = ScriptableObject.CreateInstance(type);

                for (int i = 0; i < normHeader.Length && i < values.Length; i++)
                {
                    var key = normHeader[i];
                    var raw = values[i]?.Trim();

                    var field = FindField(type, key);
                    if (field != null) { field.SetValue(so, ConvertTo(field.FieldType, raw)); continue; }

                    var prop = FindProperty(type, key);
                    if (prop != null) prop.SetValue(so, ConvertTo(prop.PropertyType, raw), null);
                }

                return (T)so;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"LocationDatabase row parse failed: {ex.Message}");
                return null;
            }
        }

        private static System.Reflection.FieldInfo FindField(Type t, string normalizedName)
        {
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (NormalizeToken(f.Name) == normalizedName) return f;
            return null;
        }
        private static System.Reflection.PropertyInfo FindProperty(Type t, string normalizedName)
        {
            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (p.CanWrite && NormalizeToken(p.Name) == normalizedName) return p;
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
        private static string NormalizeToken(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(" ", "").Replace("_", "").ToLowerInvariant();
    }
}
