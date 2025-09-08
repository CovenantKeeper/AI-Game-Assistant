#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Diagnostics
{
    public static class StudioFolderMigrator
    {
        private const string SrcRoot = "Assets/AI_RPG_Assistant";
        private const string DstRoot = "Assets/TheCovenantKeepers/AI_Game_Assistant";
        private const string StudioRoot = "Assets/TheCovenantKeepers";
        private static readonly string[] SubFoldersToMerge = {
            "Editor", "Scripts", "Data", "Settings", "UI", "Editor/UI"
        };

        // Unified under a single top-level menu name
        [MenuItem("The Covenant Keepers/AI Game Assistant/Diagnostics/Migrate Project To Studio Root")]
        public static void Migrate()
        {
            try
            {
                AssetDatabase.StartAssetEditing();

                EnsureFolder(StudioRoot);
                EnsureFolder(DstRoot);

                if (!AssetDatabase.IsValidFolder(SrcRoot))
                {
                    Debug.Log($"No '{SrcRoot}' folder found. Nothing to migrate.");
                    return;
                }

                // Move known subfolders if they exist; otherwise move all contents
                bool movedSomething = false;
                foreach (var sub in SubFoldersToMerge)
                {
                    var src = CombinePath(SrcRoot, sub);
                    if (!AssetDatabase.IsValidFolder(src)) continue;

                    var dst = CombinePath(DstRoot, sub);
                    EnsureFolderPath(dst);

                    MoveFolderContents(src, dst);
                    movedSomething = true;
                }

                // Move any remaining files/folders under SrcRoot
                MoveFolderContents(SrcRoot, DstRoot);
                movedSomething = true;

                AssetDatabase.Refresh();
                Debug.Log(movedSomething
                    ? $"✅ Migration complete. Everything is now under '{DstRoot}'."
                    : "✅ Nothing to migrate; destination already canonical.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Migration failed: {ex.Message}\n{ex.StackTrace}");
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
            EnsureFolderPath(dstFolder);

            var guids = AssetDatabase.FindAssets("", new[] { srcFolder });
            // move folders first (deepest first) to establish tree
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderByDescending(p => p.Length).ToList();

            foreach (var path in paths)
            {
                if (path == srcFolder || path == dstFolder) continue;

                // skip meta
                if (path.EndsWith(".meta")) continue;

                // If it's a folder, ensure parallel dst folder exists then continue;
                if (AssetDatabase.IsValidFolder(path))
                {
                    var rel = MakeRelative(path, srcFolder);
                    EnsureFolderPath(CombinePath(dstFolder, rel));
                    continue;
                }

                // For files, compute destination path and move
                var relFile = MakeRelative(path, srcFolder);
                var dstPath = CombinePath(dstFolder, relFile);
                EnsureFolderPath(Path.GetDirectoryName(dstPath).Replace("\\", "/"));

                var error = AssetDatabase.MoveAsset(path, dstPath);
                if (!string.IsNullOrEmpty(error))
                {
                    // Resolve collisions by appending "(migrated)"
                    var alt = AppendSuffix(dstPath, " (migrated)");
                    error = AssetDatabase.MoveAsset(path, alt);
                    if (!string.IsNullOrEmpty(error))
                        Debug.LogError($"Move failed: {path} -> {dstPath}. {error}");
                    else
                        Debug.LogWarning($"Renamed on move due to collision: {alt}");
                }
            }

            // If the source folder is now empty, you can remove it (optional)
            TryDeleteEmptyFolder(srcFolder);
        }

        private static string CombinePath(string a, string b)
            => (a.TrimEnd('/', '\\') + "/" + b.TrimStart('/', '\\')).Replace("\\", "/");

        private static string MakeRelative(string full, string root)
            => full.Substring(root.Length).TrimStart('/', '\\');

        private static string AppendSuffix(string path, string suffix)
        {
            var dir = Path.GetDirectoryName(path).Replace("\\", "/");
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            return $"{dir}/{name}{suffix}{ext}";
        }

        private static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/').ToList();
            if (parts.Count < 2 || parts[0] != "Assets") return;

            string current = "Assets";
            for (int i = 1; i < parts.Count; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static void EnsureFolderPath(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            EnsureFolder(folderPath);
        }

        private static void TryDeleteEmptyFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            var guids = AssetDatabase.FindAssets("", new[] { folder });
            if (guids == null || guids.Length == 0)
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
#endif
