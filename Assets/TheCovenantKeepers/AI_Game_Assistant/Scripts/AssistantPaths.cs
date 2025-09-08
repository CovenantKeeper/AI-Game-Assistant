using System.IO;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    public static class AssistantPaths
    {
        // Studio roots
        public const string StudioRoot = "Assets/TheCovenantKeepers";
        public const string PackageRoot = StudioRoot + "/AI_Game_Assistant";

        // Editor/UI roots
        public const string EditorRoot = PackageRoot + "/Editor";
        public const string EditorUIRoot = EditorRoot + "/UI";

        // Data roots
        public const string DataRoot = PackageRoot + "/Data";
        public const string Data = DataRoot; // legacy alias used by older code

        // Scripts
        public const string ScriptsRoot = PackageRoot + "/Scripts";
        public const string GeneratedScripts = ScriptsRoot + "/Generated";

        // Editor/UI files
        public const string AssistantUxml = "Assets/TheCovenantKeepers/AI_Game_Assistant/Editor/UI/AssistantWindow.uxml";
        public const string AssistantUss = "Assets/TheCovenantKeepers/AI_Game_Assistant/Editor/UI/AssistantWindow.uss";

        // Type-specific folders (normalize to forward slashes on Windows)
        public static string CharactersPath => Path.Combine(DataRoot, "Characters").Replace('\\','/');
        public static string ItemsPath => Path.Combine(DataRoot, "Items").Replace('\\','/');
        public static string AbilitiesPath => Path.Combine(DataRoot, "Abilities").Replace('\\','/');
        public static string QuestsPath => Path.Combine(DataRoot, "Quests").Replace('\\','/');
        public static string LocationsPath => Path.Combine(DataRoot, "Locations").Replace('\\','/');
        public static string BeastsPath => Path.Combine(DataRoot, "Beasts").Replace('\\','/');
        public static string SpiritsPath => Path.Combine(DataRoot, "Spirits").Replace('\\','/');

        // Generated CSV targets (now inside subfolders)
        public static string GeneratedCharacterCsv => Path.Combine(CharactersPath, "Generated_CharacterMasterlist.csv").Replace('\\','/');
        public static string GeneratedItemCsv      => Path.Combine(ItemsPath,      "Generated_ItemMasterlist.csv").Replace('\\','/');
        public static string GeneratedAbilityCsv   => Path.Combine(AbilitiesPath,  "Generated_AbilityMasterlist.csv").Replace('\\','/');
        public static string GeneratedQuestCsv     => Path.Combine(QuestsPath,     "Generated_QuestMasterlist.csv").Replace('\\','/');
        public static string GeneratedLocationCsv  => Path.Combine(LocationsPath,  "Generated_LocationMasterlist.csv").Replace('\\','/');
        public static string GeneratedBeastCsv     => Path.Combine(BeastsPath,     "Generated_BeastMasterlist.csv").Replace('\\','/');
        public static string GeneratedSpiritCsv    => Path.Combine(SpiritsPath,    "Generated_SpiritMasterlist.csv").Replace('\\','/');

        // Namespace used throughout the assistant
        public const string DefaultNamespace = "TheCovenantKeepers.AI_Game_Assistant";

#if UNITY_EDITOR
        // -------- Folder utilities --------
        public static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            // Normalize to forward slashes to avoid Unity creating folders with underscores
            assetPath = assetPath.Replace('\\','/');
            if (!assetPath.StartsWith("Assets/")) return;

            var parts = assetPath.Split('/');
            if (parts.Length < 2) return;

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!UnityEditor.AssetDatabase.IsValidFolder(next))
                {
                    UnityEditor.AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        public static void EnsureDirectoryForFile(string assetFilePath)
        {
            if (string.IsNullOrEmpty(assetFilePath)) return;
            int idx = assetFilePath.LastIndexOf('/');
            if (idx <= 0) return;
            string dir = assetFilePath.Substring(0, idx);
            EnsureFolder(dir);
        }

        public static void EnsureAllFolders()
        {
            EnsureFolder(StudioRoot);
            EnsureFolder(PackageRoot);
            EnsureFolder(EditorRoot);
            EnsureFolder(EditorUIRoot);
            EnsureFolder(DataRoot);
            EnsureFolder(ScriptsRoot);
            EnsureFolder(GeneratedScripts);
            // Ensure the type subfolders exist
            EnsureFolder(CharactersPath);
            EnsureFolder(ItemsPath);
            EnsureFolder(AbilitiesPath);
            EnsureFolder(QuestsPath);
            EnsureFolder(LocationsPath);
            EnsureFolder(BeastsPath);
            EnsureFolder(SpiritsPath);
        }

        // -------- Legacy migration --------
        /// <summary>
        /// Moves any CSVs from the old package location (Assets/AI_RPG_Assistant/Data)
        /// into the new DataRoot paths, preserving meta GUIDs when possible.
        /// Safe to call every launch; skips files that are already moved.
        /// </summary>
        public static void MigrateLegacyCsvs()
        {
            const string legacyPackageRoot = "Assets/AI_RPG_Assistant";
            const string legacyDataRoot = legacyPackageRoot + "/Data";

            // If legacy root doesn't exist, nothing to do.
            if (!UnityEditor.AssetDatabase.IsValidFolder(legacyPackageRoot) &&
                !UnityEditor.AssetDatabase.IsValidFolder(legacyDataRoot))
            {
                return;
            }

            EnsureAllFolders();

            var map = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Generated_CharacterMasterlist.csv", GeneratedCharacterCsv },
                { "Generated_ItemMasterlist.csv",      GeneratedItemCsv      },
                { "Generated_AbilityMasterlist.csv",   GeneratedAbilityCsv   },
                { "Generated_QuestMasterlist.csv",     GeneratedQuestCsv     },
                { "Generated_LocationMasterlist.csv",  GeneratedLocationCsv  },
            };

            foreach (var kv in map)
            {
                string fileName = kv.Key;
                string oldPath = legacyDataRoot + "/" + fileName;
                string newPath = kv.Value;

                // If the old asset doesn't exist, skip.
                if (!System.IO.File.Exists(ToSystemPath(oldPath)))
                    continue;

                EnsureDirectoryForFile(newPath);

                // Try AssetDatabase.MoveAsset first to preserve meta GUID.
                string err = UnityEditor.AssetDatabase.MoveAsset(oldPath, newPath);
                if (!string.IsNullOrEmpty(err))
                {
                    // Fallback: copy & delete if move fails.
                    try
                    {
                        System.IO.File.Copy(ToSystemPath(oldPath), ToSystemPath(newPath), overwrite: true);
                        UnityEditor.AssetDatabase.ImportAsset(newPath);
                        UnityEditor.FileUtil.DeleteFileOrDirectory(oldPath);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[AssistantPaths] Failed to migrate {oldPath} -> {newPath}: {ex.Message}");
                    }
                }
            }

            UnityEditor.AssetDatabase.Refresh();
        }

        private static string ToSystemPath(string assetPath)
        {
            // Convert "Assets/..." to full system path
            var projectAssets = Application.dataPath.Replace("\\", "/");
            if (!assetPath.StartsWith("Assets/")) return assetPath.Replace("\\", "/");
            return projectAssets.Substring(0, projectAssets.Length - "Assets".Length) + assetPath;
        }
#endif
    }
}
