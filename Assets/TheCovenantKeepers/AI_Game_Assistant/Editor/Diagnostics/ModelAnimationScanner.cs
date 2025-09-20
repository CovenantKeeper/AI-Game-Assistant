#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    public static class ModelAnimationScanner
    {
        private const string AssistantRoot = "Assets/TheCovenantKeepers/AI_Game_Assistant";
        private const string DiagnosticsFolder = AssistantRoot + "/Diagnostics";
        private const string ReportPath = DiagnosticsFolder + "/ModelAnimationScanReport.txt";

        [MenuItem("The Covenant Keepers/AI Game Assistant/Diagnostics/Scan Models & Animations", priority = 1001)]
        public static void Scan()
        {
            EnsureFolder(DiagnosticsFolder);

            var sb = new StringBuilder();
            sb.AppendLine("=== TheCovenantKeepers: Model & Animation Scan ===");
            sb.AppendLine($"Scanned at: {DateTime.Now}");
            sb.AppendLine();

            // Gather models and animations under common folders if present
            var modelGuids = SafeFindAssets("t:GameObject", new[] { "Assets/Models" });
            var animGuids  = SafeFindAssets("t:AnimationClip", new[] { "Assets/Animations", "Assets/Models" });

            sb.AppendLine("-- Asset Summary --");
            sb.AppendLine($"Models found (prefabs under Assets/Models): {modelGuids.Length}");
            sb.AppendLine($"Animation clips found (under Assets/Animations or Assets/Models): {animGuids.Length}");
            sb.AppendLine();

            // List a few examples
            sb.AppendLine("Examples (up to 10 each):");
            foreach (var g in modelGuids.Take(10)) sb.AppendLine("  Model: " + AssetDatabase.GUIDToAssetPath(g));
            foreach (var g in animGuids.Take(10)) sb.AppendLine("  Anim:  " + AssetDatabase.GUIDToAssetPath(g));
            sb.AppendLine();

            // Scan CharacterData assets for model + clips availability
            var charGuids = SafeFindAssets("t:CharacterData", null);
            sb.AppendLine("-- CharacterData Checks --");
            sb.AppendLine($"CharacterData assets: {charGuids.Length}");
            if (charGuids.Length == 0)
            {
                sb.AppendLine("(none found)");
            }
            else
            {
                foreach (var guid in charGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (so == null) continue;

                    string name = GetString(so, "Name") ?? "<unnamed>";
                    string cls = GetString(so, "Class") ?? "";
                    string modelPath = GetString(so, "ModelPath") ?? "";

                    bool modelOk = !string.IsNullOrEmpty(modelPath) && AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) != null;

                    // Discover clips either from fields or by name near model
                    var clipReport = new List<string>();
                    var requiredKeys = new[] { "idle", "walk", "run" };
                    var optionalKeys = new[] { "a1", "a2", "a3", "ult" };

                    var discovered = DiscoverClipsNearModel(name, modelPath);

                    foreach (var k in requiredKeys)
                    {
                        var assigned = GetClip(so, FieldForKey(k));
                        var ok = assigned != null || discovered.ContainsKey(k);
                        clipReport.Add($"{k}:{(ok ? "OK" : "MISSING")}");
                    }
                    foreach (var k in optionalKeys)
                    {
                        var assigned = GetClip(so, FieldForKey(k));
                        var ok = assigned != null || discovered.ContainsKey(k);
                        clipReport.Add($"{k}:{(ok ? "ok" : "-")}");
                    }

                    sb.AppendLine($"• {name} ({cls})");
                    sb.AppendLine($"    Asset: {path}");
                    sb.AppendLine($"    Model: {(modelOk ? "OK" : string.IsNullOrEmpty(modelPath) ? "MISSING path" : $"Not found at '{modelPath}'")}");
                    if (!string.IsNullOrEmpty(modelPath)) sb.AppendLine($"    ModelPath: {modelPath}");
                    sb.AppendLine($"    Clips: {string.Join(", ", clipReport)}");
                }
            }

            File.WriteAllText(ReportPath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"? Model/Animation scan complete. Report saved to: {ReportPath}");
            EditorUtility.RevealInFinder(ReportPath);
        }

        private static string FieldForKey(string key)
        {
            switch (key)
            {
                case "idle": return "IdleClip";
                case "walk": return "WalkClip";
                case "run":  return "RunClip";
                case "a1":   return "Ability1Clip";
                case "a2":   return "Ability2Clip";
                case "a3":   return "Ability3Clip";
                case "ult":  return "UltimateClip";
                default: return null;
            }
        }

        private static string GetString(ScriptableObject so, string field)
        {
            var f = so.GetType().GetField(field, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            return f?.GetValue(so) as string;
        }
        private static AnimationClip GetClip(ScriptableObject so, string field)
        {
            if (string.IsNullOrEmpty(field)) return null;
            var f = so.GetType().GetField(field, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            return f?.GetValue(so) as AnimationClip;
        }

        private static Dictionary<string, AnimationClip> DiscoverClipsNearModel(string characterName, string modelPath)
        {
            var dict = new Dictionary<string, AnimationClip>(StringComparer.InvariantCultureIgnoreCase);
            string folder = null;
            if (!string.IsNullOrEmpty(modelPath))
            {
                folder = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder) && !folder.StartsWith("Assets")) folder = null;
            }

            IEnumerable<string> GuidSearch(string filter)
            {
                if (!string.IsNullOrEmpty(folder))
                    return AssetDatabase.FindAssets(filter, new[] { folder });
                return AssetDatabase.FindAssets(filter);
            }

            bool NameLike(string clipName, string charName)
            {
                if (string.IsNullOrWhiteSpace(clipName) || string.IsNullOrWhiteSpace(charName)) return true;
                var a = clipName.Replace(" ", "").ToLowerInvariant();
                var b = charName.Replace(" ", "").ToLowerInvariant();
                return a.Contains(b) || b.Contains(a);
            }

            void TryAdd(string key, Func<AnimationClip, bool> predicate)
            {
                if (dict.ContainsKey(key)) return;
                foreach (var g in GuidSearch("t:AnimationClip"))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                    if (clip == null) continue;
                    if (predicate(clip)) { dict[key] = clip; break; }
                }
            }

            TryAdd("idle", c => NameLike(c.name, characterName) && c.name.ToLower().Contains("idle"));
            TryAdd("walk", c => NameLike(c.name, characterName) && c.name.ToLower().Contains("walk"));
            TryAdd("run",  c => NameLike(c.name, characterName) && c.name.ToLower().Contains("run"));

            // fallback without name hint
            if (!dict.ContainsKey("idle")) TryAdd("idle", c => c.name.ToLower().Contains("idle"));
            if (!dict.ContainsKey("walk")) TryAdd("walk", c => c.name.ToLower().Contains("walk"));
            if (!dict.ContainsKey("run"))  TryAdd("run",  c => c.name.ToLower().Contains("run"));

            // abilities (optional)
            TryAdd("a1", c => c.name.ToLower().Contains("a1"));
            TryAdd("a2", c => c.name.ToLower().Contains("a2"));
            TryAdd("a3", c => c.name.ToLower().Contains("a3"));
            TryAdd("ult", c => c.name.ToLower().Contains("ult") || c.name.ToLower().Contains("ultimate"));

            return dict;
        }

        private static string[] SafeFindAssets(string filter, string[] folders)
        {
            try { return folders == null ? AssetDatabase.FindAssets(filter) : AssetDatabase.FindAssets(filter, folders); }
            catch { return Array.Empty<string>(); }
        }

        private static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            assetPath = assetPath.Replace('\\', '/');
            if (!assetPath.StartsWith("Assets/")) return;
            var parts = assetPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string current = parts[0]; // Assets
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
