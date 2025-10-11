#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    public static class AnimatorPresetTemplates
    {
        private const string BowFolder = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations/2Hand-Bow";
        private const string PackRoot = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations";

        // Resolve a subfolder inside the ExplosiveLLC pack that contains all keywords
        private static string ResolvePackFolder(params string[] keywords)
        {
            if (!AssetDatabase.IsValidFolder(PackRoot)) return null;
            var guids = AssetDatabase.FindAssets("t:folder", new[] { PackRoot });
            int bestScore = int.MinValue; string bestPath = null;
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g).Replace('\\','/');
                var low = p.ToLowerInvariant();
                int score = 0; bool ok = true;
                foreach (var k in keywords ?? Array.Empty<string>())
                {
                    var kk = (k ?? string.Empty).ToLowerInvariant();
                    if (string.IsNullOrEmpty(kk)) continue;
                    if (!low.Contains(kk)) { ok = false; break; }
                    score += 10;
                }
                if (!ok) continue;
                if (score > bestScore) { bestScore = score; bestPath = p; }
            }
            return bestPath;
        }

        // Utility: find a type by simple name across loaded assemblies (fallback only)
        private static Type FindTypeByName(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetTypes().FirstOrDefault(x => x.Name == simpleName);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }

        // Prefer a type in a specific assembly
        private static Type FindTypeInAssembly(Assembly asm, string simpleName)
        {
            if (asm == null) return null;
            try { return asm.GetTypes().FirstOrDefault(x => x.Name == simpleName); }
            catch { return null; }
        }

        // Utility: create a List<T> via reflection
        private static object CreateList(Type elementType) => Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
        private static void ListAdd(object list, object item)
        {
            var mi = list.GetType().GetMethod("Add");
            mi?.Invoke(list, new[] { item });
        }

        private static object New(string typeName)
        {
            var t = FindTypeByName(typeName);
            return t != null ? Activator.CreateInstance(t) : null;
        }

        private static Type T(string typeName) => FindTypeByName(typeName);

        // Compat helper: discover clips via reflection to avoid IDE analyzer cross-project errors
        private static Dictionary<string, AnimationClip> DiscoverClipsCompat(string characterName, string modelPath)
        {
            try
            {
                var t = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                    })
                    .FirstOrDefault(x => x.FullName == "TheCovenantKeepers.AI_Game_Assistant.Editor.CharacterPrefabBuilder");
                var mi = t?.GetMethod("DiscoverClips", BindingFlags.Public | BindingFlags.Static);
                if (mi != null)
                {
                    var result = mi.Invoke(null, new object[] { characterName ?? string.Empty, modelPath ?? string.Empty }) as Dictionary<string, AnimationClip>;
                    if (result != null) return result;
                }
            }
            catch { }

            // Fallback: very light-weight scan
            var dict = new Dictionary<string, AnimationClip>(StringComparer.InvariantCultureIgnoreCase);
            IEnumerable<string> GuidSearch(string filter)
            {
                return AssetDatabase.FindAssets(filter);
            }
            static string Norm(string s) => (s ?? string.Empty).ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
            bool NameMatches(AnimationClip c, IEnumerable<string> tokens)
            {
                var n = Norm(c.name);
                foreach (var tkn in tokens)
                {
                    var tn = Norm(tkn);
                    if (!string.IsNullOrEmpty(tn) && n.Contains(tn)) return true;
                }
                return false;
            }
            void TryAddByTokens(string key, params string[] tokens)
            {
                if (dict.ContainsKey(key)) return;
                foreach (var guid in GuidSearch("t:AnimationClip"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip != null && NameMatches(clip, tokens)) { dict[key] = clip; break; }
                }
            }
            var charHint = string.IsNullOrEmpty(characterName) ? Array.Empty<string>() : new[] { characterName };
            TryAddByTokens("idle", new[] { "idle", "relax" }.Concat(charHint).ToArray());
            TryAddByTokens("walk", new[] { "walk", "move" }.Concat(charHint).ToArray());
            TryAddByTokens("run", new[] { "run", "sprint", "jog" }.Concat(charHint).ToArray());
            TryAddByTokens("jump", new[] { "jump", "leap" }.Concat(charHint).ToArray());
            TryAddByTokens("fall", new[] { "fall", "air" }.Concat(charHint).ToArray());
            TryAddByTokens("land", new[] { "land", "landing" }.Concat(charHint).ToArray());
            TryAddByTokens("light", new[] { "lightattack", "attack1", "punch" }.Concat(charHint).ToArray());
            TryAddByTokens("heavy", new[] { "heavyattack", "attack2", "smash" }.Concat(charHint).ToArray());
            TryAddByTokens("dodge", new[] { "dodge", "roll", "evade" }.Concat(charHint).ToArray());
            TryAddByTokens("block", new[] { "block", "guard", "shield" }.Concat(charHint).ToArray());
            TryAddByTokens("hit", new[] { "hit", "hurt", "impact" }.Concat(charHint).ToArray());
            TryAddByTokens("die", new[] { "die", "death" }.Concat(charHint).ToArray());
            return dict;
        }

        // Helpers for auto-saving assets
        private static string GetAssistantPackageRoot()
        {
            var t = Type.GetType("TheCovenantKeepers.AI_Game_Assistant.AssistantPaths");
            var f = t?.GetField("PackageRoot", BindingFlags.Public | BindingFlags.Static);
            return f?.GetValue(null) as string ?? "Assets/TheCovenantKeepers/AI_Game_Assistant";
        }
        private static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            assetPath = assetPath.Replace('\\', '/');
            if (!assetPath.StartsWith("Assets/")) return;
            var parts = assetPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // Build a new RPG preset ScriptableObject and return it (not saved yet)
        private static ScriptableObject BuildRpgPresetObject(string classKey, string presetNameOverride)
        {
            var presetType = T("AnimatorPreset");
            if (presetType == null) return null;
            var asm = presetType.Assembly;
            var paramDefType = FindTypeInAssembly(asm, "ParameterDefinition") ?? T("ParameterDefinition");
            var stateDefType = FindTypeInAssembly(asm, "StateDefinition") ?? T("StateDefinition");
            var transDefType = FindTypeInAssembly(asm, "TransitionDefinition") ?? T("TransitionDefinition");
            var condDefType = FindTypeInAssembly(asm, "ConditionDefinition") ?? T("ConditionDefinition");
            if (paramDefType == null || stateDefType == null || transDefType == null || condDefType == null) return null;

            var preset = ScriptableObject.CreateInstance(presetType) as ScriptableObject;
            string pname = string.IsNullOrWhiteSpace(presetNameOverride) ? ($"RPG_{classKey}") : presetNameOverride;
            presetType.GetField("presetName", BindingFlags.Public | BindingFlags.Instance)?.SetValue(preset, pname);
            presetType.GetField("characterClassKey", BindingFlags.Public | BindingFlags.Instance)?.SetValue(preset, string.IsNullOrWhiteSpace(classKey) ? "Default" : classKey);

            // parameters
            var pList = CreateList(paramDefType);
            void AddParam(string name, AnimatorControllerParameterType type)
            {
                var p = Activator.CreateInstance(paramDefType);
                paramDefType.GetField("name")?.SetValue(p, name);
                paramDefType.GetField("type")?.SetValue(p, type);
                ListAdd(pList, p);
            }
            AddParam("Speed", AnimatorControllerParameterType.Float);
            AddParam("VelocityY", AnimatorControllerParameterType.Float);
            AddParam("IsGrounded", AnimatorControllerParameterType.Bool);
            AddParam("Jump", AnimatorControllerParameterType.Trigger);
            AddParam("AttackLight", AnimatorControllerParameterType.Trigger);
            AddParam("AttackHeavy", AnimatorControllerParameterType.Trigger);
            AddParam("Dodge", AnimatorControllerParameterType.Trigger);
            AddParam("Block", AnimatorControllerParameterType.Bool);
            AddParam("Hit", AnimatorControllerParameterType.Trigger);
            AddParam("Die", AnimatorControllerParameterType.Trigger);
            AddParam("CastA1", AnimatorControllerParameterType.Trigger);
            AddParam("CastA2", AnimatorControllerParameterType.Trigger);
            AddParam("CastA3", AnimatorControllerParameterType.Trigger);
            AddParam("CastUlt", AnimatorControllerParameterType.Trigger);
            presetType.GetField("parameters")?.SetValue(preset, pList);

            // states
            var sList = CreateList(stateDefType);
            void AddState(string name, bool isDefault = false)
            {
                var s = Activator.CreateInstance(stateDefType);
                stateDefType.GetField("name")?.SetValue(s, name);
                stateDefType.GetField("isDefaultState")?.SetValue(s, isDefault);
                ListAdd(sList, s);
            }
            AddState("Idle", true);
            AddState("Walk"); AddState("Run");
            AddState("Jump"); AddState("Fall"); AddState("Land");
            AddState("LightAttack"); AddState("HeavyAttack");
            AddState("Dodge"); AddState("Block");
            AddState("HitReact"); AddState("Death");
            AddState("Ability1"); AddState("Ability2"); AddState("Ability3"); AddState("Ultimate");
            presetType.GetField("states")?.SetValue(preset, sList);

            // transitions
            var tList = CreateList(transDefType);
            object NewCond(string param, string modeName, float thr)
            {
                var c = Activator.CreateInstance(condDefType);
                var modeField = condDefType.GetField("mode");
                var enumType = modeField.FieldType;
                var mode = Enum.Parse(enumType, modeName, ignoreCase: true);
                condDefType.GetField("parameterName")?.SetValue(c, param);
                modeField?.SetValue(c, mode);
                condDefType.GetField("threshold")?.SetValue(c, thr);
                return c;
            }
            void AddTransition(string src, string dst, bool hasExit, float dur, params object[] conditions)
            {
                var tr = Activator.CreateInstance(transDefType);
                transDefType.GetField("sourceState")?.SetValue(tr, src);
                transDefType.GetField("destinationState")?.SetValue(tr, dst);
                transDefType.GetField("hasExitTime")?.SetValue(tr, hasExit);
                transDefType.GetField("duration")?.SetValue(tr, dur);
                var clist = CreateList(condDefType);
                foreach (var c in conditions) ListAdd(clist, c);
                transDefType.GetField("conditions")?.SetValue(tr, clist);
                ListAdd(tList, tr);
            }
            AddTransition("Idle", "Walk", false, 0.1f, NewCond("Speed", "Greater", 0.05f));
            AddTransition("Walk", "Idle", false, 0.1f, NewCond("Speed", "Less", 0.05f));
            AddTransition("Walk", "Run", false, 0.1f, NewCond("Speed", "Greater", 0.6f));
            AddTransition("Run", "Walk", false, 0.1f, NewCond("Speed", "Less", 0.6f));
            AddTransition("", "Jump", false, 0.05f, NewCond("Jump", "If", 0));
            AddTransition("", "Dodge", false, 0.05f, NewCond("Dodge", "If", 0));
            AddTransition("", "LightAttack", false, 0.05f, NewCond("AttackLight", "If", 0));
            AddTransition("", "HeavyAttack", false, 0.05f, NewCond("AttackHeavy", "If", 0));
            AddTransition("", "HitReact", false, 0.05f, NewCond("Hit", "If", 0));
            AddTransition("", "Death", false, 0.05f, NewCond("Die", "If", 0));
            AddTransition("", "Ability1", false, 0.05f, NewCond("CastA1", "If", 0));
            AddTransition("", "Ability2", false, 0.05f, NewCond("CastA2", "If", 0));
            AddTransition("", "Ability3", false, 0.05f, NewCond("CastA3", "If", 0));
            AddTransition("", "Ultimate", false, 0.05f, NewCond("CastUlt", "If", 0));
            presetType.GetField("transitions")?.SetValue(preset, tList);

            return preset;
        }

        // Find best existing preset for a class (prefer our folder and filled states)
        private static string FindBestPresetPathForClass(string classKey)
        {
            if (string.IsNullOrWhiteSpace(classKey)) return null;
            var guids = AssetDatabase.FindAssets("t:AnimatorPreset");
            if (guids == null || guids.Length == 0) return null;

            string root = GetAssistantPackageRoot();
            string preferredFolder = (root + "/AnimatorPresets").Replace('\\','/');

            string bestPath = null; int bestScore = int.MinValue;
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;
                var key = so.GetType().GetField("characterClassKey", BindingFlags.Public | BindingFlags.Instance)?.GetValue(so) as string;
                if (string.IsNullOrEmpty(key) || !string.Equals(key, classKey, StringComparison.InvariantCultureIgnoreCase))
                    continue;
                int score = 0;
                var p = (path ?? string.Empty).Replace('\\','/');
                if (p.StartsWith(preferredFolder, StringComparison.InvariantCultureIgnoreCase)) score += 1000;
                var fileName = System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                if (fileName.Contains(classKey.ToLowerInvariant())) score += 50;
                try
                {
                    var statesField = so.GetType().GetField("states", BindingFlags.Public | BindingFlags.Instance);
                    var states = statesField?.GetValue(so) as IEnumerable;
                    int filled = 0, total = 0;
                    if (states != null)
                    {
                        foreach (var s in states)
                        {
                            if (s == null) continue;
                            total++;
                            var clip = s.GetType().GetField("animationClip", BindingFlags.Public | BindingFlags.Instance)?.GetValue(s) as Motion;
                            if (clip != null) filled++;
                        }
                    }
                    score += filled * 5;
                    score += total;
                }
                catch { }

                if (score > bestScore) { bestScore = score; bestPath = path; }
            }
            return bestPath;
        }

        // Public: Create and save an RPG preset for a specific class key without prompting. Returns asset path.
        public static string CreateRpgPresetForClassAuto(string classKey)
        {
            var existing = FindBestPresetPathForClass(classKey ?? "Default");
            if (!string.IsNullOrEmpty(existing))
            {
                Debug.Log($"Using existing AnimatorPreset for class '{classKey}': {existing}");
                return existing;
            }

            if (!string.IsNullOrWhiteSpace(classKey) && classKey.Trim().Equals("Archer", StringComparison.InvariantCultureIgnoreCase))
            {
                var archerPath = CreateArcherBowPresetAsset();
                if (!string.IsNullOrEmpty(archerPath)) return archerPath;
            }

            // Warrior 1H / 2H (prefer 1H if ambiguous)
            if (!string.IsNullOrWhiteSpace(classKey) && classKey.IndexOf("warrior", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                string folder = ResolvePackFolder("1hand", "sword") ?? ResolvePackFolder("one", "hand", "sword");
                string key = "Warrior1H"; string name = "Warrior_1H";
                if (classKey.IndexOf("2", StringComparison.InvariantCultureIgnoreCase) >= 0 || classKey.IndexOf("two", StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    folder = ResolvePackFolder("2hand", "sword") ?? ResolvePackFolder("two", "hand", "sword") ?? folder;
                    key = "Warrior2H"; name = "Warrior_2H";
                }
                if (!string.IsNullOrEmpty(folder))
                {
                    var map = new Dictionary<string,string[]>
                    {
                        ["Idle"] = new[]{"idle"},
                        ["Walk"] = new[]{"walk"},
                        ["Run"] = new[]{"run"},
                        ["LightAttack"] = new[]{"attack1","slash","light"},
                        ["HeavyAttack"] = new[]{"attack2","heavy","smash"},
                        ["Dodge"] = new[]{"dodge","roll","evade"},
                        ["Block"] = new[]{"block","guard","shield"},
                        ["HitReact"] = new[]{"hit","hurt"},
                        ["Death"] = new[]{"die","death"},
                        ["Jump"] = new[]{"jump"},
                        ["Fall"] = new[]{"fall"},
                        ["Land"] = new[]{"land"},
                        ["Ability1"] = new[]{"attack3","combo"},
                        ["Ability2"] = new[]{"attack4","power"},
                        ["Ability3"] = new[]{"attack5","special"},
                        ["Ultimate"] = new[]{"ultimate","finisher","power"}
                    };
                    var p = CreatePresetWithFolder("Warrior", key, name, folder, map);
                    if (!string.IsNullOrEmpty(p)) return p;
                }
            }

            // Mage (Staff)
            if (!string.IsNullOrWhiteSpace(classKey) && classKey.IndexOf("mage", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                string folder = ResolvePackFolder("staff") ?? ResolvePackFolder("magic");
                if (!string.IsNullOrEmpty(folder))
                {
                    var map = new Dictionary<string,string[]>
                    {
                        ["Idle"] = new[]{"idle"},
                        ["Walk"] = new[]{"walk"},
                        ["Run"] = new[]{"run"},
                        ["LightAttack"] = new[]{"cast","spell","shoot","light"},
                        ["HeavyAttack"] = new[]{"cast","spell","heavy","charge"},
                        ["Dodge"] = new[]{"dodge","roll"},
                        ["Block"] = new[]{"block","guard"},
                        ["HitReact"] = new[]{"hit","hurt"},
                        ["Death"] = new[]{"die","death"},
                        ["Jump"] = new[]{"jump"},
                        ["Fall"] = new[]{"fall"},
                        ["Land"] = new[]{"land"},
                        ["Ability1"] = new[]{"cast","spell","a1"},
                        ["Ability2"] = new[]{"cast","spell","a2"},
                        ["Ability3"] = new[]{"cast","spell","a3"},
                        ["Ultimate"] = new[]{"ultimate","channel","power"}
                    };
                    var p = CreatePresetWithFolder("Mage", "Mage_Staff", "Mage_Staff", folder, map);
                    if (!string.IsNullOrEmpty(p)) return p;
                }
            }

            // Rogue (Dual)
            if (!string.IsNullOrWhiteSpace(classKey) && (classKey.IndexOf("rogue", StringComparison.InvariantCultureIgnoreCase) >= 0 || classKey.IndexOf("assassin", StringComparison.InvariantCultureIgnoreCase) >= 0))
            {
                string folder = ResolvePackFolder("dual") ?? ResolvePackFolder("dual", "dagger") ?? ResolvePackFolder("2", "weapon");
                if (!string.IsNullOrEmpty(folder))
                {
                    var map = new Dictionary<string,string[]>
                    {
                        ["Idle"] = new[]{"idle"},
                        ["Walk"] = new[]{"walk"},
                        ["Run"] = new[]{"run"},
                        ["LightAttack"] = new[]{"attack1","stab","slash","light"},
                        ["HeavyAttack"] = new[]{"attack2","spin","heavy"},
                        ["Dodge"] = new[]{"dodge","roll","evade"},
                        ["Block"] = new[]{"block","guard"},
                        ["HitReact"] = new[]{"hit","hurt"},
                        ["Death"] = new[]{"die","death"},
                        ["Jump"] = new[]{"jump"},
                        ["Fall"] = new[]{"fall"},
                        ["Land"] = new[]{"land"},
                        ["Ability1"] = new[]{"attack3","combo"},
                        ["Ability2"] = new[]{"attack4","flurry"},
                        ["Ability3"] = new[]{"attack5","execute"},
                        ["Ultimate"] = new[]{"ultimate","finisher"}
                    };
                    var p = CreatePresetWithFolder("Rogue", "Rogue_Dual", "Rogue_Dual", folder, map);
                    if (!string.IsNullOrEmpty(p)) return p;
                }
            }

            // Fallback to generic default preset
            var fallback = BuildRpgPresetObject(classKey ?? "Default", null);
            if (fallback == null) return null;
            string r = GetAssistantPackageRoot(); string f = (r + "/AnimatorPresets").Replace('\\','/'); EnsureFolder(f);
            string def = f + $"/RPG_{SanitizeFileName(classKey ?? "Default")}_Preset.asset";
            AssetDatabase.CreateAsset(fallback, def); AssetDatabase.SaveAssets();
            return def;
        }

        // Archer-specific preset that assigns bow clips from ExplosiveLLC folder; idempotent
        private static string CreateArcherBowPresetAsset()
        {
            if (!AssetDatabase.IsValidFolder(BowFolder)) return null;

            string root = GetAssistantPackageRoot();
            string folder = (root + "/AnimatorPresets").Replace('\\', '/');
            EnsureFolder(folder);
            string path = folder + "/RPG_Archer_Preset.asset"; // stable name to avoid duplicates

            var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (existing != null)
            {
                // Do not recreate; just reuse existing preset
                return path;
            }

            var presetObj = BuildRpgPresetObject("Archer", "RPG_Archer");
            if (presetObj == null) return null;

            // Assign bow clips to states
            var presetType = presetObj.GetType();
            var stateDefType = presetType.Assembly.GetTypes().FirstOrDefault(x => x.Name == "StateDefinition");
            var statesField = presetType.GetField("states", BindingFlags.Public | BindingFlags.Instance);
            var statesObj = statesField?.GetValue(presetObj) as IEnumerable;
            if (stateDefType != null && statesObj != null)
            {
                AnimationClip FindClip(params string[] tokens)
                {
                    AnimationClip best = null; int bestScore = int.MinValue;
                    foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { BowFolder }))
                    {
                        var p = AssetDatabase.GUIDToAssetPath(guid);
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                        if (clip == null) continue;
                        var n = (clip.name ?? string.Empty).ToLowerInvariant();
                        int score = 0; foreach (var t in tokens) { var tt = (t ?? string.Empty).ToLowerInvariant(); if (!string.IsNullOrEmpty(tt) && n.Contains(tt)) score += 10; }
                        if (score > bestScore) { best = clip; bestScore = score; }
                    }
                    return best;
                }

                var map = new Dictionary<string, AnimationClip>(StringComparer.InvariantCultureIgnoreCase)
                {
                    ["Idle"] = FindClip("idle"),
                    ["Walk"] = FindClip("walk"),
                    ["Run"]  = FindClip("run-forward", "run"),
                    ["LightAttack"] = FindClip("attack4", "shoot"),
                    ["HeavyAttack"] = FindClip("attack5", "attack6", "charge", "aim"),
                    ["Ability1"] = FindClip("attack4"),
                    ["Ability2"] = FindClip("attack5"),
                    ["Ability3"] = FindClip("attack6"),
                    ["Ultimate"] = FindClip("boost1", "ultimate", "power"),
                    ["Dodge"] = FindClip("dodge", "roll", "evade"),
                    ["HitReact"] = FindClip("hit", "hurt", "impact"),
                    ["Death"] = FindClip("die", "death"),
                    ["Jump"] = FindClip("jump"),
                    ["Fall"] = FindClip("fall"),
                    ["Land"] = FindClip("land", "landing"),
                };

                foreach (var s in statesObj)
                {
                    if (s == null) continue;
                    var name = stateDefType.GetField("name", BindingFlags.Public | BindingFlags.Instance)?.GetValue(s) as string;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (map.TryGetValue(name, out var clip) && clip != null)
                    {
                        stateDefType.GetField("animationClip", BindingFlags.Public | BindingFlags.Instance)?.SetValue(s, clip);
                    }
                }
            }

            AssetDatabase.CreateAsset(presetObj, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(presetObj);
            Selection.activeObject = presetObj;
            Debug.Log($"Created Archer AnimatorPreset at {path}.");
            return path;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Default";
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }

        // Removed MenuItem attributes: callable from Assistant Window only
        public static void CreateDefaultRpgPreset()
        {
            // Get the main preset type first
            var presetType = T("AnimatorPreset");
            if (presetType == null)
            {
                EditorUtility.DisplayDialog("AnimatorPreset Types Missing", "Could not locate AnimatorPreset type. Ensure Assets/TheCovenantKeepers/AI_Game_Assistant/Scripts/AnimatorPreset.cs exists and compiles.", "OK");
                return;
            }

            // Resolve related types from the same assembly as AnimatorPreset to avoid picking Mono.Cecil.ParameterDefinition etc.
            var asm = presetType.Assembly;
            var paramDefType = FindTypeInAssembly(asm, "ParameterDefinition") ?? T("ParameterDefinition");
            var stateDefType = FindTypeInAssembly(asm, "StateDefinition") ?? T("StateDefinition");
            var transDefType = FindTypeInAssembly(asm, "TransitionDefinition") ?? T("TransitionDefinition");
            var condDefType = FindTypeInAssembly(asm, "ConditionDefinition") ?? T("ConditionDefinition");

            if (paramDefType == null || stateDefType == null || transDefType == null || condDefType == null)
            {
                EditorUtility.DisplayDialog("AnimatorPreset Types Missing", "Could not locate one or more definition types (Parameter/State/Transition/Condition). Ensure AnimatorPreset.cs defines them and compiles.", "OK");
                return;
            }

            // Create ScriptableObject instance via reflection
            var preset = ScriptableObject.CreateInstance(presetType) as ScriptableObject;
            presetType.GetField("presetName", BindingFlags.Public | BindingFlags.Instance)?.SetValue(preset, "RPG_Default");
            presetType.GetField("characterClassKey", BindingFlags.Public | BindingFlags.Instance)?.SetValue(preset, "Default");

            // Build parameters
            var pList = CreateList(paramDefType);
            void AddParam(string name, AnimatorControllerParameterType type)
            {
                var p = Activator.CreateInstance(paramDefType);
                paramDefType.GetField("name")?.SetValue(p, name);
                paramDefType.GetField("type")?.SetValue(p, type);
                ListAdd(pList, p);
            }

            AddParam("Speed", AnimatorControllerParameterType.Float);
            AddParam("VelocityY", AnimatorControllerParameterType.Float);
            AddParam("IsGrounded", AnimatorControllerParameterType.Bool);
            AddParam("Jump", AnimatorControllerParameterType.Trigger);
            AddParam("AttackLight", AnimatorControllerParameterType.Trigger);
            AddParam("AttackHeavy", AnimatorControllerParameterType.Trigger);
            AddParam("Dodge", AnimatorControllerParameterType.Trigger);
            AddParam("Block", AnimatorControllerParameterType.Bool);
            AddParam("Hit", AnimatorControllerParameterType.Trigger);
            AddParam("Die", AnimatorControllerParameterType.Trigger);
            AddParam("CastA1", AnimatorControllerParameterType.Trigger);
            AddParam("CastA2", AnimatorControllerParameterType.Trigger);
            AddParam("CastA3", AnimatorControllerParameterType.Trigger);
            AddParam("CastUlt", AnimatorControllerParameterType.Trigger);

            presetType.GetField("parameters")?.SetValue(preset, pList);

            // Build states
            var sList = CreateList(stateDefType);
            void AddState(string name, bool isDefault = false)
            {
                var s = Activator.CreateInstance(stateDefType);
                stateDefType.GetField("name")?.SetValue(s, name);
                stateDefType.GetField("isDefaultState")?.SetValue(s, isDefault);
                // animationClip left null, user assigns per class/character
                ListAdd(sList, s);
            }

            AddState("Idle", true);
            AddState("Walk");
            AddState("Run");
            AddState("Jump");
            AddState("Fall");
            AddState("Land");
            AddState("LightAttack");
            AddState("HeavyAttack");
            AddState("Dodge");
            AddState("Block");
            AddState("HitReact");
            AddState("Death");
            AddState("Ability1");
            AddState("Ability2");
            AddState("Ability3");
            AddState("Ultimate");

            presetType.GetField("states")?.SetValue(preset, sList);

            // Build transitions
            var tList = CreateList(transDefType);
            object NewCond(string param, string modeName, float thr)
            {
                var c = Activator.CreateInstance(condDefType);
                var modeField = condDefType.GetField("mode");
                var enumType = modeField.FieldType; // AnimatorConditionMode
                var mode = Enum.Parse(enumType, modeName, ignoreCase: true);
                condDefType.GetField("parameterName")?.SetValue(c, param);
                modeField?.SetValue(c, mode);
                condDefType.GetField("threshold")?.SetValue(c, thr);
                return c;
            }
            void AddTransition(string src, string dst, bool hasExit, float dur, params object[] conditions)
            {
                var tr = Activator.CreateInstance(transDefType);
                transDefType.GetField("sourceState")?.SetValue(tr, src);
                transDefType.GetField("destinationState")?.SetValue(tr, dst);
                transDefType.GetField("hasExitTime")?.SetValue(tr, hasExit);
                transDefType.GetField("duration")?.SetValue(tr, dur);
                var clist = CreateList(condDefType);
                foreach (var c in conditions) ListAdd(clist, c);
                transDefType.GetField("conditions")?.SetValue(tr, clist);
                ListAdd(tList, tr);
            }

            // Idle <-> Walk/Run
            AddTransition("Idle", "Walk", false, 0.1f, NewCond("Speed", "Greater", 0.05f));
            AddTransition("Walk", "Idle", false, 0.1f, NewCond("Speed", "Less", 0.05f));
            AddTransition("Walk", "Run", false, 0.1f, NewCond("Speed", "Greater", 0.6f));
            AddTransition("Run", "Walk", false, 0.1f, NewCond("Speed", "Less", 0.6f));

            // AnyState style: represent by empty source; the builder interprets these as AnyState
            AddTransition("", "Jump", false, 0.05f, NewCond("Jump", "If", 0));
            AddTransition("", "Dodge", false, 0.05f, NewCond("Dodge", "If", 0));
            AddTransition("", "LightAttack", false, 0.05f, NewCond("AttackLight", "If", 0));
            AddTransition("", "HeavyAttack", false, 0.05f, NewCond("AttackHeavy", "If", 0));
            AddTransition("", "HitReact", false, 0.05f, NewCond("Hit", "If", 0));
            AddTransition("", "Death", false, 0.05f, NewCond("Die", "If", 0));
            AddTransition("", "Ability1", false, 0.05f, NewCond("CastA1", "If", 0));
            AddTransition("", "Ability2", false, 0.05f, NewCond("CastA2", "If", 0));
            AddTransition("", "Ability3", false, 0.05f, NewCond("CastA3", "If", 0));
            AddTransition("", "Ultimate", false, 0.05f, NewCond("CastUlt", "If", 0));

            presetType.GetField("transitions")?.SetValue(preset, tList);

            var path = EditorUtility.SaveFilePanelInProject("Save RPG AnimatorPreset", "RPG_DefaultPreset", "asset", "Choose location for the AnimatorPreset asset");
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(preset);
                Selection.activeObject = preset;
                Debug.Log($"? Created AnimatorPreset at {path}. Assign clips per state and set characterClassKey to match Character.Class.");
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        // NEW: Auto-fill AnimationClips into a preset using discovered clips
        public static void AutoFillClipsInSelectedPreset()
        {
            var presetType = T("AnimatorPreset");
            var stateDefType = presetType != null ? presetType.Assembly.GetTypes().FirstOrDefault(x => x.Name == "StateDefinition") : null;
            if (presetType == null || stateDefType == null)
            {
                EditorUtility.DisplayDialog("AnimatorPreset Missing", "AnimatorPreset types not found.", "OK");
                return;
            }

            // Find target preset from selection, or from selected CharacterData's class
            ScriptableObject targetPreset = null;
            ScriptableObject selectedCharacter = null;

            foreach (var obj in Selection.objects)
            {
                if (obj is ScriptableObject so)
                {
                    if (so.GetType() == presetType) targetPreset = so;
                    else if (so.GetType().Name == "CharacterData") selectedCharacter = so;
                }
            }

            if (targetPreset == null && selectedCharacter != null)
            {
                // Find a preset matching the character's Class
                var cls = selectedCharacter.GetType().GetField("Class", BindingFlags.Public | BindingFlags.Instance)?.GetValue(selectedCharacter) as string ?? "Default";
                var guids = AssetDatabase.FindAssets("t:AnimatorPreset");
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (so == null || so.GetType() != presetType) continue;
                    var key = presetType.GetField("characterClassKey", BindingFlags.Public | BindingFlags.Instance)?.GetValue(so) as string;
                    if (!string.IsNullOrEmpty(key) && string.Equals(key, cls, StringComparison.InvariantCultureIgnoreCase))
                    {
                        targetPreset = so; break;
                    }
                }
            }

            if (targetPreset == null)
            {
                EditorUtility.DisplayDialog("Select Preset", "Select an AnimatorPreset (or select a CharacterData with a matching preset created).", "OK");
                return;
            }

            // Determine discovery hints
            string characterName = null;
            string modelPath = null;
            if (selectedCharacter != null)
            {
                characterName = selectedCharacter.GetType().GetField("Name", BindingFlags.Public | BindingFlags.Instance)?.GetValue(selectedCharacter) as string;
                modelPath = selectedCharacter.GetType().GetField("ModelPath", BindingFlags.Public | BindingFlags.Instance)?.GetValue(selectedCharacter) as string;
            }

            // Use compat discovery to avoid compile-time dependency across IDE projects
            var found = DiscoverClipsCompat(characterName ?? string.Empty, modelPath ?? string.Empty);
            if (found == null || found.Count == 0)
            {
                if (!EditorUtility.DisplayDialog("No Clips Found", "Could not auto-discover clips near the model or project. Try assigning a ModelPath on the CharacterData or place clips near the model. Proceed to continue with global scan?", "OK", "Cancel"))
                    return;
            }

            // Map state names to discovery keys
            string MapKey(string stateName)
            {
                if (string.IsNullOrEmpty(stateName)) return null;
                var s = stateName.Replace(" ", "").ToLowerInvariant();
                if (s.Contains("idle")) return "idle";
                if (s.Contains("walk") || s.Contains("move")) return "walk";
                if (s.Contains("run") || s.Contains("sprint") || s.Contains("jog")) return "run";
                if (s.Contains("jump")) return "jump";
                if (s.Contains("fall") || s.Contains("air")) return "fall";
                if (s.Contains("land")) return "land";
                if (s.Contains("light")) return "light";
                if (s.Contains("heavy")) return "heavy";
                if (s.Contains("dodge") || s.Contains("roll") || s.Contains("evade")) return "dodge";
                if (s.Contains("block") || s.Contains("guard")) return "block";
                if (s.Contains("hit") || s.Contains("react") || s.Contains("hurt")) return "hit";
                if (s.Contains("death") || s == "die") return "die";
                if (s.Contains("ability1") || s == "a1") return "a1";
                if (s.Contains("ability2") || s == "a2") return "a2";
                if (s.Contains("ability3") || s == "a3") return "a3";
                if (s.Contains("ultimate") || s.Contains("ult")) return "ult";
                return null;
            }

            int filled = 0, total = 0;
            var statesField = presetType.GetField("states", BindingFlags.Public | BindingFlags.Instance);
            var statesObj = statesField?.GetValue(targetPreset) as IEnumerable;
            if (statesObj != null)
            {
                foreach (var s in statesObj)
                {
                    if (s == null) continue;
                    total++;
                    var sname = stateDefType.GetField("name", BindingFlags.Public | BindingFlags.Instance)?.GetValue(s) as string;
                    var clipField = stateDefType.GetField("animationClip", BindingFlags.Public | BindingFlags.Instance);
                    var currentClip = clipField?.GetValue(s) as AnimationClip;
                    if (currentClip != null) continue; // leave existing
                    var key = MapKey(sname);
                    if (string.IsNullOrEmpty(key)) continue;
                    if (found.TryGetValue(key, out var clip) && clip != null)
                    {
                        clipField.SetValue(s, clip);
                        filled++;
                    }
                }
            }

            EditorUtility.SetDirty(targetPreset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(targetPreset);
            Debug.Log($"? Auto-fill complete: set {filled}/{total} state clips on preset '{targetPreset.name}'.");
        }

        // Generic creator: build preset for classKey from a specific animations folder and a state->token map
        private static string CreatePresetWithFolder(string classKey, string fileSafeKey, string displayName, string folderPath, Dictionary<string, string[]> tokenMap)
        {
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath)) return null;

            string root = GetAssistantPackageRoot();
            string presetsFolder = (root + "/AnimatorPresets").Replace('\\','/');
            EnsureFolder(presetsFolder);
            string path = presetsFolder + $"/RPG_{fileSafeKey}_Preset.asset";

            // Idempotent: reuse existing asset if present
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null)
                return path;

            var presetObj = BuildRpgPresetObject(classKey, $"RPG_{displayName}");
            if (presetObj == null) return null;

            // Assign clips per token map using only the specified folder
            var presetType = presetObj.GetType();
            var stateDefType = presetType.Assembly.GetTypes().FirstOrDefault(x => x.Name == "StateDefinition");
            var statesField = presetType.GetField("states", BindingFlags.Public | BindingFlags.Instance);
            var statesObj = statesField?.GetValue(presetObj) as IEnumerable;
            if (stateDefType != null && statesObj != null && tokenMap != null)
            {
                AnimationClip FindClip(params string[] tokens)
                {
                    AnimationClip best = null; int bestScore = int.MinValue;
                    foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folderPath }))
                    {
                        var p = AssetDatabase.GUIDToAssetPath(guid);
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                        if (clip == null) continue;
                        var n = (clip.name ?? string.Empty).ToLowerInvariant();
                        int score = 0;
                        foreach (var t in tokens ?? Array.Empty<string>())
                        {
                            var tt = (t ?? string.Empty).ToLowerInvariant();
                            if (!string.IsNullOrEmpty(tt) && n.Contains(tt)) score += 10;
                        }
                        if (score > bestScore) { best = clip; bestScore = score; }
                    }
                    return best;
                }

                foreach (var s in statesObj)
                {
                    if (s == null) continue;
                    var name = stateDefType.GetField("name", BindingFlags.Public | BindingFlags.Instance)?.GetValue(s) as string;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (tokenMap.TryGetValue(name, out var tokens))
                    {
                        var clip = FindClip(tokens);
                        if (clip != null)
                            stateDefType.GetField("animationClip", BindingFlags.Public | BindingFlags.Instance)?.SetValue(s, clip);
                    }
                }
            }

            AssetDatabase.CreateAsset(presetObj, path);
            AssetDatabase.SaveAssets();
            return path;
        }
    }
}
#endif