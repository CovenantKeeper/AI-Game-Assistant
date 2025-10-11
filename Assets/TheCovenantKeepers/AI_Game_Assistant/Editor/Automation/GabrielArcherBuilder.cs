#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor.Automation
{
    public static class GabrielArcherBuilder
    {
        private const string TargetCharacterName = "Gabriel";
        private const string TargetClass = "Archer";
        private const string FallbackAssetPath = "Assets/TheCovenantKeepers/AI_Game_Assistant/Data/ScriptableObjects/Characters/Character_Gabriel (migrated).asset";

        // Use literal menu path to avoid cross-asm const refs
        [MenuItem("The Covenant Keepers/AI Game Assistant/Character/Build Archer: Gabriel (One-Click)", priority = 10)]
        public static void BuildGabrielArcher()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Build Gabriel Archer", "Locating CharacterData asset...", 0.1f);
                var so = FindGabrielCharacter();
                if (so == null)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Build Gabriel Archer", "CharacterData for 'Gabriel' not found. Ensure the asset exists.", "OK");
                    return;
                }

                // Ensure basic identity fields via reflection
                SetFieldValue(so, "Name", string.IsNullOrWhiteSpace(GetFieldValue<string>(so, "Name")) ? TargetCharacterName : GetFieldValue<string>(so, "Name"));
                var currentClass = GetFieldValue<string>(so, "Class");
                if (!string.Equals(currentClass?.Trim(), TargetClass, StringComparison.InvariantCultureIgnoreCase))
                {
                    SetFieldValue(so, "Class", TargetClass);
                    EditorUtility.SetDirty(so);
                }

                // Ensure an AnimatorPreset for Archer exists (best-effort)
                EditorUtility.DisplayProgressBar("Build Gabriel Archer", "Ensuring Archer AnimatorPreset...", 0.25f);
                TryCallStatic("TheCovenantKeepers.AI_Game_Assistant.Editor.AnimatorPresetTemplates", "CreateRpgPresetForClassAuto", new object[] { TargetClass });

                // Build/assign AnimatorController using CharacterPrefabBuilder.BuildAnimatorControllerForCharacter(so)
                EditorUtility.DisplayProgressBar("Build Gabriel Archer", "Building AnimatorController...", 0.45f);
                var ctrlPath = TryCallStatic<string>("TheCovenantKeepers.AI_Game_Assistant.Editor.CharacterPrefabBuilder", "BuildAnimatorControllerForCharacter", new object[] { so });
                if (!string.IsNullOrEmpty(ctrlPath))
                {
                    var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);
                    if (ctrl != null)
                    {
                        SetFieldValue(so, "AnimatorController", ctrl);
                        EditorUtility.SetDirty(so);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Build the Player prefab using CharacterPrefabBuilder.BuildCharacterPrefab(so, "Player")
                EditorUtility.DisplayProgressBar("Build Gabriel Archer", "Building Player prefab...", 0.65f);
                var prefabPath = TryCallStatic<string>("TheCovenantKeepers.AI_Game_Assistant.Editor.CharacterPrefabBuilder", "BuildCharacterPrefab", new object[] { so, "Player" });
                if (string.IsNullOrEmpty(prefabPath))
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Build Gabriel Archer", "Prefab build failed. See Console for details.", "OK");
                    return;
                }

                // Ensure AbilityEffectSpawner + spawn transform
                EditorUtility.DisplayProgressBar("Build Gabriel Archer", "Adding AbilityEffectSpawner...", 0.8f);
                EnsureSpawnerOnPrefab(prefabPath);

                EditorUtility.ClearProgressBar();
                EditorUtility.RevealInFinder(prefabPath);
                Debug.Log($"? Gabriel Archer built successfully. Prefab at: {prefabPath}");
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"Build Gabriel Archer failed: {ex.Message}\n{ex}");
                EditorUtility.DisplayDialog("Build Gabriel Archer", "An error occurred. See Console.", "OK");
            }
        }

        private static ScriptableObject FindGabrielCharacter()
        {
            // 1) Try the explicit migrated path first
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(FallbackAssetPath);
            if (IsCharacterData(so)) return so;

            // 2) Search by type name and Name == Gabriel
            var guids = AssetDatabase.FindAssets("t:CharacterData");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (!IsCharacterData(asset)) continue;
                var nameVal = GetFieldValue<string>(asset, "Name");
                if (!string.IsNullOrEmpty(nameVal) && string.Equals(nameVal.Trim(), TargetCharacterName, StringComparison.InvariantCultureIgnoreCase))
                    return asset;

                var fileName = System.IO.Path.GetFileNameWithoutExtension(path) ?? string.Empty;
                if (fileName.IndexOf("Character_Gabriel", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    return asset;
            }
            return null;
        }

        private static bool IsCharacterData(UnityEngine.Object obj)
        {
            if (obj == null) return false;
            var t = obj.GetType();
            // Accept derived types too
            return t.FullName == "TheCovenantKeepers.AI_Game_Assistant.CharacterData" || t.IsSubclassOf(FindType("TheCovenantKeepers.AI_Game_Assistant.CharacterData"));
        }

        private static void EnsureSpawnerOnPrefab(string prefabPath)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null) return;

                var spawnerType = FindType("TheCovenantKeepers.AI_Game_Assistant.AbilityEffectSpawner");
                Component spawner = null;
                if (spawnerType != null)
                {
                    spawner = root.GetComponent(spawnerType);
                    if (spawner == null) spawner = root.AddComponent(spawnerType);
                }

                // Guess a spawn transform
                var spawn = GuessSpawnTransform(root.transform);
                if (spawner != null)
                {
                    var field = spawnerType.GetField("spawn", BindingFlags.Public | BindingFlags.Instance);
                    if (field != null) field.SetValue(spawner, spawn);
                }

                // Optional: enable driver key triggers if exists
                var driverType = FindType("TheCovenantKeepers.AI_Game_Assistant.AnimatorTestDriver");
                if (driverType != null)
                {
                    var driver = root.GetComponent(driverType);
                    if (driver != null)
                    {
                        var prop = driverType.GetField("triggerSpawnerOnKeys", BindingFlags.Public | BindingFlags.Instance);
                        if (prop != null && prop.FieldType == typeof(bool)) prop.SetValue(driver, true);
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static Transform GuessSpawnTransform(Transform root)
        {
            var all = root.GetComponentsInChildren<Transform>(true);

            // 1) Explicit marker
            var explicitSpawn = all.FirstOrDefault(x =>
                string.Equals(x.name, "VFX_Spawn", StringComparison.InvariantCultureIgnoreCase));
            if (explicitSpawn != null) return explicitSpawn;

            // 2) Common right-hand / weapon hints
            Transform best = null; int bestScore = int.MinValue;
            foreach (var t in all)
            {
                var n = t.name.ToLowerInvariant();
                int score = int.MinValue;
                if (n.Contains("righthand") || n.Contains("hand_r") || n.Contains("hand.r")) score = 100;
                else if (n.Contains("weapon") || n.Contains("muzzle") || n.Contains("wrist")) score = 80;
                else if (n.Contains("upperchest") || n.Contains("chest") || n.Contains("spine2") || n.Contains("spine1")) score = 60;
                if (score > bestScore) { bestScore = score; best = t; }
            }
            if (best != null) return best;

            // 3) Create a neutral under chest/root
            var chest = all.FirstOrDefault(t =>
            {
                var n = t.name.ToLowerInvariant();
                return n.Contains("upperchest") || n.Contains("chest") || n.Contains("spine2") || n.Contains("spine1");
            });
            var parent = chest != null ? chest : root;
            var go = new GameObject("VFX_Spawn");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.15f, 0.20f);
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        // --- Reflection helpers ---
        private static T TryCallStatic<T>(string typeName, string methodName, object[] args)
        {
            var t = FindType(typeName);
            if (t == null) return default;
            var m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (m == null) return default;
            try
            {
                var result = m.Invoke(null, args);
                if (result is T ok) return ok;
                return default;
            }
            catch { return default; }
        }

        private static void TryCallStatic(string typeName, string methodName, object[] args)
        {
            TryCallStatic<object>(typeName, methodName, args);
        }

        private static Type FindType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = asm.GetType(fullName); } catch { t = null; }
                if (t != null) return t;
            }
            return null;
        }

        private static T GetFieldValue<T>(object obj, string field)
        {
            if (obj == null) return default;
            var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return default;
            try { var v = f.GetValue(obj); if (v is T t) return t; } catch { }
            return default;
        }

        private static void SetFieldValue(object obj, string field, object value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return;
            try { f.SetValue(obj, value); } catch { }
        }
    }
}
#endif
