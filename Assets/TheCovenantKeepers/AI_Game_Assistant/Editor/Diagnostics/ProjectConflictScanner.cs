// Assets/TheCovenantKeepers/AI_Game_Assistant/Editor/Diagnostics/ProjectConflictScanner.cs
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    public static class ProjectConflictScanner
    {
        // ---------- Paths ----------
        private const string StudioRoot = "Assets/TheCovenantKeepers";
        private const string AssistantRoot = "Assets/TheCovenantKeepers/AI_Game_Assistant";
        private const string DiagnosticsFolder = AssistantRoot + "/Diagnostics";
        private const string ReportPath = DiagnosticsFolder + "/ProjectConflictReport.txt";

        // Legacy folder we want to flag if it exists
        private const string LegacyAssistantRoot = "Assets/AI_RPG_Assistant";

        // ---------- Canonical CSV headers (current) ----------
        // Character (ID first)
        private const string HeaderCharacterIDFirst =
            "ID,Name,Type,Role,Affiliation,Class,Faction,Element,Gender,Level,MaxHealth,MaxMana,BaseAttack,BaseDefense,Speed,UltimateAbility,LoreBackground,ModelPath";

        // Legacy Character header we want to detect & report
        private const string HeaderCharacterLegacy =
            "Name,Type,Role,Affiliation,Class,Faction,Element,Gender,Health,Mana,Attack,Defense,Magic,Speed,UltimateAbility,LoreBackground,ModelPath";

        private const string HeaderItem =
            "ItemID,ItemName,ItemType,SubType,Description,ValueBuy,ValueSell,Weight,IsUsable,IsEquippable,EquipmentSlot,StatModifier1_Type,StatModifier1_Value,StatModifier2_Type,StatModifier2_Value,UseEffect,RequiredLevel,CraftingMaterials,Notes,PrefabPath";

        private const string HeaderAbility =
            "AbilityID,AbilityName,Description,AbilityType,TargetType,Range,ManaCost,CooldownSeconds,CastTimeSeconds,DamageAmount,DamageType,HealingAmount,BuffDebuffEffect,AreaOfEffectRadius,ProjectilePrefabPath,VFX_CastPath,VFX_HitPath,SFX_CastPath,SFX_HitPath,AnimationTriggerCast,AnimationTriggerImpact,RequiredLevel,PrerequisiteAbilityID,Notes";

        private const string HeaderQuest =
            "Title,Objective,Type,Reward,Region,LoreHint,PrefabPath";

        private const string HeaderLocation =
            "Name,Region,Type,FactionControl,DangerLevel,Lore,PrefabPath";

        // Old namespace we want to purge
        private const string LegacyNamespace = "namespace TheCovenantKeepers.AI_Game_Assistant";

        private static readonly string[] CsharpExt = { ".cs" };
        private static readonly string[] TextLikeExt = { ".csv", ".txt", ".json", ".uxml", ".uss", ".cs", ".md" };

        [MenuItem("The Covenant Keepers/AI Game Assistant/Diagnostics/Scan Project", priority = 1000)]
        public static void ScanProject()
        {
            EnsureFolder(DiagnosticsFolder);

            var allFiles = Directory.GetFiles(Application.dataPath, "*.*", SearchOption.AllDirectories);
            var assetRoot = Application.dataPath.Replace("\\", "/");
            var toAssetPath = new Func<string, string>(full =>
            {
                var norm = full.Replace("\\", "/");
                var idx = norm.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
                return idx >= 0 ? norm.Substring(idx + 1) : norm; // +1 -> drop leading slash
            });

            var csFiles = allFiles.Where(f => CsharpExt.Contains(Path.GetExtension(f))).ToArray();
            var textFiles = allFiles.Where(f => TextLikeExt.Contains(Path.GetExtension(f))).ToArray();

            var sb = new StringBuilder();
            sb.AppendLine("=== TheCovenantKeepers: Project Conflict Report ===");
            sb.AppendLine($"Scanned at: {DateTime.Now}");
            sb.AppendLine($"Assets root: {Application.dataPath}");
            sb.AppendLine($"C# files: {csFiles.Length}, Text-like files: {textFiles.Length}");
            sb.AppendLine();

            // ---- Studio root status ----
            sb.AppendLine("---- Studio Root Status ----");
            if (AssetDatabase.IsValidFolder(StudioRoot))
                sb.AppendLine($"OK: Studio root exists: {StudioRoot}");
            else
                sb.AppendLine($"ERROR: Studio root missing: {StudioRoot}");
            sb.AppendLine();

            // ---- Legacy folder residue ----
            sb.AppendLine("---- Legacy assistant folder residue ----");
            if (AssetDatabase.IsValidFolder(LegacyAssistantRoot))
                sb.AppendLine($"WARN: Legacy folder still exists: {LegacyAssistantRoot}");
            else
                sb.AppendLine("(no legacy folder found)");
            sb.AppendLine();

            // ---- Duplicate C# filenames (same basename in multiple locations) ----
            sb.AppendLine("---- Duplicate C# filenames (same basename in multiple locations) ----");
            var dupsByName = csFiles
                .GroupBy(f => Path.GetFileName(f))
                .Where(g => g.Count() > 1)
                .ToList();
            if (dupsByName.Count == 0) sb.AppendLine("(none)");
            else foreach (var g in dupsByName)
                {
                    sb.AppendLine($"• {g.Key}");
                    foreach (var p in g) sb.AppendLine($"    {toAssetPath(p)}");
                }
            sb.AppendLine();

            // ---- Duplicate type names across files ----
            sb.AppendLine("---- Duplicate type names across files (class/struct/enum/interface) ----");
            var typeNameRegex = new Regex(@"\b(class|struct|enum|interface)\s+([A-Za-z_][A-Za-z0-9_]*)",
                                          RegexOptions.Compiled);
            var typeToFiles = new Dictionary<string, HashSet<string>>();
            foreach (var f in csFiles)
            {
                string code = SafeRead(f);
                foreach (Match m in typeNameRegex.Matches(code))
                {
                    var typeName = m.Groups[2].Value;
                    if (!typeToFiles.TryGetValue(typeName, out var set))
                    {
                        set = new HashSet<string>();
                        typeToFiles[typeName] = set;
                    }
                    set.Add(toAssetPath(f));
                }
            }
            var dupTypes = typeToFiles.Where(kv => kv.Value.Count > 1).ToList();
            if (dupTypes.Count == 0) sb.AppendLine("(none)");
            else foreach (var kv in dupTypes)
                {
                    sb.AppendLine($"• {kv.Key}");
                    foreach (var p in kv.Value) sb.AppendLine($"    {p}");
                }
            sb.AppendLine();

            // ---- Old namespace usage ----
            sb.AppendLine("---- Old namespace usage (should be TheCovenantKeepers.AI_Game_Assistant) ----");
            var legacyNsHits = new List<string>();
            foreach (var f in csFiles)
            {
                string code = SafeRead(f);
                if (code.Contains(LegacyNamespace))
                    legacyNsHits.Add(toAssetPath(f));
            }
            if (legacyNsHits.Count == 0) sb.AppendLine("(none)");
            else foreach (var p in legacyNsHits) sb.AppendLine($"• {p}");
            sb.AppendLine();

            // ---- PromptProcessor definitions (by fully-qualified name heuristic) ----
            sb.AppendLine("---- PromptProcessor definitions ----");
            var ppHits = new List<string>();
            foreach (var f in csFiles)
            {
                string code = SafeRead(f);
                if (Regex.IsMatch(code, @"\bclass\s+PromptProcessor\b"))
                    ppHits.Add(toAssetPath(f));
            }
            if (ppHits.Count == 0) sb.AppendLine("(none)");
            else foreach (var p in ppHits) sb.AppendLine($"• {p}");
            sb.AppendLine();

            // ---- Header usage (where each schema string appears) ----
            var headerBuckets = new (string label, string header, List<string> hits)[]
            {
                ("Character (Legacy)",   HeaderCharacterLegacy,   new List<string>()),
                ("Character (ID-First)", HeaderCharacterIDFirst,  new List<string>()),
                ("Item",                 HeaderItem,              new List<string>()),
                ("Ability",              HeaderAbility,           new List<string>()),
                ("Quest",                HeaderQuest,             new List<string>()),
                ("Location",             HeaderLocation,          new List<string>()),
            };

            foreach (var f in textFiles)
            {
                string text = SafeRead(f);
                if (string.IsNullOrEmpty(text)) continue;

                foreach (var hb in headerBuckets)
                {
                    if (text.Contains(hb.header))
                    {
                        hb.hits.Add(toAssetPath(f));
                    }
                }
            }

            sb.AppendLine("---- Header usage (where each schema string appears) ----");
            foreach (var hb in headerBuckets)
            {
                sb.AppendLine($"• {hb.label}:");
                if (hb.hits.Count == 0) sb.AppendLine("    (none)");
                else foreach (var p in hb.hits) sb.AppendLine($"    {p}");
            }
            sb.AppendLine();

            // Save report
            File.WriteAllText(ReportPath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"✅ Project scan complete. Report saved to: {ReportPath}");
        }

        // ---------- Helpers ----------
        private static void EnsureFolder(string assetPath)
        {
            var parts = assetPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string SafeRead(string fullPath)
        {
            try
            {
                return File.ReadAllText(fullPath);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
#endif
