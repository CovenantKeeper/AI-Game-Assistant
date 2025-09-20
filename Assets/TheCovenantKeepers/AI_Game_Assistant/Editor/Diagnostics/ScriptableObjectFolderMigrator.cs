#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Diagnostics
{
    /// <summary>
    /// One-click migration for legacy ScriptableObjects from AI_RPG_Assistant to the new Studio root.
    /// Use this if your SOs still live under Assets/AI_RPG_Assistant/Data/ScriptableObjects.
    /// </summary>
    public static class ScriptableObjectFolderMigrator
    {
        private const string LegacySoRoot = "Assets/AI_RPG_Assistant/Data/ScriptableObjects";
        private const string NewSoRoot = "Assets/TheCovenantKeepers/AI_Game_Assistant/Data/ScriptableObjects";

        [MenuItem("The Covenant Keepers/AI Game Assistant/Diagnostics/Migrate ScriptableObjects To Studio Root", priority = 202)]
        public static void MigrateScriptableObjects()
        {
            try
            {
                AssetDatabase.StartAssetEditing();

                if (!AssetDatabase.IsValidFolder(LegacySoRoot))
                {
                    EditorUtility.DisplayDialog("Migrate ScriptableObjects", "Legacy folder not found:\n" + LegacySoRoot, "OK");
                    return;
                }

                // Ensure destination tree exists
                EnsureFolderTree(NewSoRoot);

                // Move all contents (folders first)
                MoveFolderContents(LegacySoRoot, NewSoRoot);

                // Try delete empty legacy folder
                TryDeleteEmptyFolder(LegacySoRoot);

                EditorUtility.DisplayDialog("Migrate ScriptableObjects", "? Migration complete. Assets moved to:\n" + NewSoRoot, "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ScriptableObject migration failed: {ex.Message}\n{ex}");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }

        private static void MoveFolderContents(string srcFolder, string dstFolder)
        {
            if (!AssetDatabase.IsValidFolder(srcFolder)) return;

            // Create destination subfolders mirroring source
            var allPaths = AssetDatabase.FindAssets(string.Empty, new[] { srcFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(p => p.Count(c => c == '/')) // shallow to deep
                .ToList();

            foreach (var path in allPaths)
            {
                if (path == srcFolder) continue;
                if (path.EndsWith(".meta")) continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    var rel = MakeRelative(path, srcFolder);
                    EnsureFolderTree(Combine(dstFolder, rel));
                }
            }

            // Now move files
            foreach (var path in allPaths.Where(p => !AssetDatabase.IsValidFolder(p)))
            {
                if (path.EndsWith(".meta")) continue;
                var rel = MakeRelative(path, srcFolder);
                var dstPath = Combine(dstFolder, rel);
                EnsureFolderTree(Path.GetDirectoryName(dstPath).Replace('\\','/'));

                var err = AssetDatabase.MoveAsset(path, dstPath);
                if (!string.IsNullOrEmpty(err))
                {
                    // Collision-safe rename
                    var alt = AppendSuffix(dstPath, " (migrated)");
                    err = AssetDatabase.MoveAsset(path, alt);
                    if (!string.IsNullOrEmpty(err))
                        Debug.LogError($"Move failed: {path} -> {dstPath}. {err}");
                    else
                        Debug.LogWarning($"Renamed on move due to collision: {alt}");
                }
            }
        }

        private static void EnsureFolderTree(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            assetPath = assetPath.Replace('\\', '/');
            if (!assetPath.StartsWith("Assets")) return;
            var parts = assetPath.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void TryDeleteEmptyFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
            if (guids == null || guids.Length == 0)
                AssetDatabase.DeleteAsset(folder);
        }

        private static string Combine(string a, string b) => (a.TrimEnd('/', '\\') + "/" + b.TrimStart('/', '\\')).Replace('\\','/');
        private static string MakeRelative(string full, string root) => full.Substring(root.Length).TrimStart('/', '\\');
        private static string AppendSuffix(string path, string suffix)
        {
            var dir = Path.GetDirectoryName(path).Replace('\\','/');
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            return $"{dir}/{name}{suffix}{ext}";
        }
    }
}
#endif
