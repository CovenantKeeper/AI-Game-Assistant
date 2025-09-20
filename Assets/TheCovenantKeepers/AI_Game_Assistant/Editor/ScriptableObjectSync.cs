#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    /// <summary>
    /// Reflection-based sync of Character ScriptableObjects so this editor script does not require
    /// direct compile-time references to runtime assembly types (handles first domain compile after new asmdefs).
    /// </summary>
    public static class ScriptableObjectSync
    {
        public struct DiffResult { public List<string> added; public List<string> updated; public List<string> unchanged; public List<string> removed; }

        private const string CharacterTypeName = "TheCovenantKeepers.AI_Game_Assistant.CharacterData";
        private const string CharacterDbTypeName = "TheCovenantKeepers.AI_Game_Assistant.CharacterDatabase";
        private const string ChatGPTSettingsTypeName = "TheCovenantKeepers.AI_Game_Assistant.ChatGPTSettings";
        private const string AssistantPathsTypeName = "TheCovenantKeepers.AI_Game_Assistant.AssistantPaths";

        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Data + "/Sync Character ScriptableObjects", priority = 50)]
        public static void MenuSyncCharacters()
        {
            if (!TryGetStaticStringMember(AssistantPathsTypeName, "GeneratedCharacterCsv", out string csv))
            {
                EditorUtility.DisplayDialog("Sync Aborted", "AssistantPaths not available yet (recompile needed).", "OK");
                return;
            }
            if (!File.Exists(csv)) { EditorUtility.DisplayDialog("Character CSV Missing", $"No generated CSV found at:\n{csv}", "OK"); return; }

            var chars = LoadCharacters(csv);
            if (chars == null)
            {
                EditorUtility.DisplayDialog("Sync Aborted", "CharacterDatabase.LoadCharacters not available yet.", "OK");
                return;
            }
            var diff = SyncCharacterScriptableObjects(chars);
            ShowDiffDialog("Characters", diff);
        }

        private static IList LoadCharacters(string path)
        {
            var dbType = FindType(CharacterDbTypeName);
            if (dbType == null) return null;
            var m = dbType.GetMethod("LoadCharacters", BindingFlags.Public | BindingFlags.Static);
            if (m == null) return null;
            return m.Invoke(null, new object[] { path }) as IList; // List<CharacterData>
        }

        public static DiffResult SyncCharacterScriptableObjects(IList characters)
        {
            var diff = new DiffResult { added = new List<string>(), updated = new List<string>(), unchanged = new List<string>(), removed = new List<string>() };

            var charType = FindType(CharacterTypeName);
            if (charType == null)
            {
                Debug.LogError("CharacterData type not found.");
                return diff;
            }

            string root = GetSettingsScriptableObjectPath();
            if (string.IsNullOrEmpty(root))
            {
                // fallback default
                if (TryGetStaticStringMember(AssistantPathsTypeName, "PackageRoot", out var pkgRoot))
                    root = pkgRoot + "/Data/ScriptableObjects/";
                else root = "Assets/TheCovenantKeepers/AI_Game_Assistant/Data/ScriptableObjects/";
            }
            if (!root.EndsWith("/")) root += "/";
            var charRoot = root + "Characters/";
            EnsureFolder(charRoot);

            // Load existing assets
            var guids = AssetDatabase.FindAssets("t:CharacterData", new[] { charRoot.TrimEnd('/') });
            var existingByName = new Dictionary<string, UnityEngine.Object>();
            var nameProp = charType.GetField("Name", BindingFlags.Public | BindingFlags.Instance);
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var asset = AssetDatabase.LoadAssetAtPath(path, charType);
                if (asset == null) continue;
                var key = (nameProp?.GetValue(asset) as string ?? string.Empty).Trim();
                if (!existingByName.ContainsKey(key)) existingByName[key] = asset;
            }

            var encountered = new HashSet<UnityEngine.Object>();

            foreach (var obj in characters)
            {
                var nameVal = (nameProp?.GetValue(obj) as string) ?? string.Empty;
                var cleanName = Sanitize(nameVal);
                if (string.IsNullOrEmpty(cleanName)) cleanName = "Unnamed";
                string finalName = cleanName; int safety = 0;
                while (existingByName.ContainsKey(finalName) && !NamesMatch(existingByName[finalName], obj, nameProp))
                { safety++; finalName = cleanName + "_" + safety; if (safety > 1000) break; }

                if (existingByName.TryGetValue(finalName, out var existing))
                {
                    bool changed = HasChanged(existing, obj, charType);
                    CopyFields(existing, obj, charType);
                    encountered.Add(existing);
                    if (changed) diff.updated.Add(finalName); else diff.unchanged.Add(finalName);
                }
                else
                {
                    var asset = ScriptableObject.CreateInstance(charType);
                    CopyFields(asset, obj, charType);
                    var assetPath = charRoot + "Character_" + finalName + ".asset";
                    AssetDatabase.CreateAsset(asset, assetPath);
                    diff.added.Add(finalName);
                    existingByName[finalName] = asset;
                    encountered.Add(asset);
                }
            }

            foreach (var kv in existingByName)
                if (!encountered.Contains(kv.Value)) diff.removed.Add(kv.Key);

            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            return diff;
        }

        // --- Reflection helpers ---
        private static readonly string[] CompareFields = { "Health","Mana","Attack","Defense","Magic","Speed","Class","Faction","UltimateAbility","PassiveName","Ability1Name","Ability2Name","Ability3Name","LoreBackground" };

        private static bool HasChanged(object a, object b, Type t)
        {
            foreach (var fName in CompareFields)
            {
                var f = t.GetField(fName, BindingFlags.Public | BindingFlags.Instance);
                if (f == null) continue;
                var av = f.GetValue(a); var bv = f.GetValue(b);
                if (!Equals(av, bv)) return true;
            }
            return false;
        }

        private static void CopyFields(object target, object source, Type t)
        {
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var f in fields)
            {
                try { f.SetValue(target, f.GetValue(source)); }
                catch { }
            }
            EditorUtility.SetDirty((UnityEngine.Object)target);
        }

        private static bool NamesMatch(object assetObj, object srcObj, FieldInfo nameField)
        {
            return ((nameField?.GetValue(assetObj) as string) ?? string.Empty).Trim() == ((nameField?.GetValue(srcObj) as string) ?? string.Empty).Trim();
        }

        private static string Sanitize(string name)
        { if (string.IsNullOrWhiteSpace(name)) return string.Empty; var invalid = new string(Path.GetInvalidFileNameChars()); var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray()); return cleaned.Replace(' ', '_'); }

        private static void ShowDiffDialog(string label, DiffResult diff)
        { var msg = $"{label} SO Sync Complete\nAdded: {diff.added.Count}\nUpdated: {diff.updated.Count}\nUnchanged: {diff.unchanged.Count}\nPotentially Removed (not in CSV): {diff.removed.Count}"; Debug.Log(msg); if (diff.removed.Count > 0) msg += "\n\n(Removed list not deleted automatically)"; EditorUtility.DisplayDialog($"{label} Sync", msg, "OK"); }

        private static bool TryGetStaticStringMember(string typeName, string memberName, out string value)
        {
            value = null;
            var t = FindType(typeName);
            if (t == null) return false;

            // First try field
            var f = t.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null)
            {
                var v = f.GetValue(null);
                if (v is string s1) { value = s1; return true; }
            }

            // Then try property (e.g., AssistantPaths.GeneratedCharacterCsv)
            var p = t.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (p != null && p.CanRead)
            {
                try
                {
                    var v = p.GetValue(null, null);
                    if (v is string s2) { value = s2; return true; }
                }
                catch { }
            }

            return false;
        }

        private static string GetSettingsScriptableObjectPath()
        {
            var t = FindType(ChatGPTSettingsTypeName); if (t == null) return null;
            var getMethod = t.GetMethod("Get", BindingFlags.Public | BindingFlags.Static); if (getMethod == null) return null;
            var inst = getMethod.Invoke(null, null); if (inst == null) return null;
            var field = t.GetField("ScriptableObjectPath", BindingFlags.Public | BindingFlags.Instance);
            return field?.GetValue(inst) as string;
        }

        private static Type FindType(string fullName)
        {
            // Try direct first
            var t = Type.GetType(fullName);
            if (t != null) return t;
            // Search all loaded assemblies to avoid needing assembly-qualified names
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = asm.GetType(fullName); }
                catch { t = null; }
                if (t != null) return t;
            }
            return null;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            folder = folder.Replace("\\", "/");
            if (!folder.StartsWith("Assets")) return;
            var parts = folder.Split('/');
            var current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif