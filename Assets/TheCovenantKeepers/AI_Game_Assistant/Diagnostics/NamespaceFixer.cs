#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    public static class NamespaceFixer
    {
        private const string OldNs = "TheCovenantKeepers.AIRPGAssistant";
        private const string NewNs = "TheCovenantKeepers.AI_Game_Assistant";

        [MenuItem("The Covenant Keepers/AI Game Assistant/Diagnostics/Fix Namespaces (Runtime & Editor)")]
        public static void Fix()
        {
            var root = Application.dataPath.Replace("\\", "/");
            var targetRoot = root + "/TheCovenantKeepers/AI_Game_Assistant";

            int filesTouched = 0;
            var sb = new StringBuilder();

            if (Directory.Exists(targetRoot))
            {
                var csFiles = Directory.GetFiles(targetRoot, "*.cs", SearchOption.AllDirectories);
                foreach (var f in csFiles)
                {
                    var text = File.ReadAllText(f);
                    var updated = text;

                    // runtime namespaces/usings
                    updated = updated.Replace($"namespace {OldNs}", $"namespace {NewNs}");
                    updated = updated.Replace($"using {OldNs};", $"using {NewNs};");

                    // .Editor namespaces/usings
                    updated = updated.Replace($"namespace {OldNs}.Editor", $"namespace {NewNs}.Editor");
                    updated = updated.Replace($"using {OldNs}.Editor;", $"using {NewNs}.Editor;");

                    if (!ReferenceEquals(text, updated) && text != updated)
                    {
                        File.WriteAllText(f, updated, Encoding.UTF8);
                        filesTouched++;
                        sb.AppendLine(ToAssetsPath(f));
                    }
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[NamespaceFixer] Done. Files updated: {filesTouched}\n{sb}");
        }

        private static string ToAssetsPath(string full)
        {
            full = full.Replace("\\", "/");
            var assets = Application.dataPath.Replace("\\", "/");
            return full.StartsWith(assets, StringComparison.OrdinalIgnoreCase)
                ? "Assets" + full.Substring(assets.Length)
                : full;
        }
    }
}
#endif
