#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    public static class CharacterPrefabBuilder
    {
        [MenuItem("The Covenant Keepers/AI Game Assistant/Build/Build Selected Character Prefab")]
        public static void BuildSelectedCharacterPrefab()
        {
            var data = Selection.activeObject as ScriptableObject;
            if (data == null || data.GetType().Name != "CharacterData")
            {
                Debug.LogWarning("Select a CharacterData asset first.");
                return;
            }

            var prefabPath = BuildCharacterPrefab(data, kind: "Player");
            if (!string.IsNullOrEmpty(prefabPath))
            {
                EditorUtility.RevealInFinder(prefabPath);
                Debug.Log($"? Built prefab: {prefabPath}");
            }
        }

        // Builds a prefab for the given character ScriptableObject. Returns the asset path.
        public static string BuildCharacterPrefab(ScriptableObject characterData, string kind)
        {
            if (characterData == null)
            {
                Debug.LogError("Character data is null");
                return null;
            }

            // Reflection helpers to read fields from CharacterData without hard ref
            string GetString(string field) => characterData.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance)?.GetValue(characterData) as string;

            var name = GetString("Name");
            var modelPath = GetString("ModelPath");
            var cls = GetString("Class");

            // Try get an explicitly assigned AnimatorController
            RuntimeAnimatorController explicitController = null;
            var animatorCtrlField = characterData.GetType().GetField("AnimatorController", BindingFlags.Public | BindingFlags.Instance);
            if (animatorCtrlField != null)
                explicitController = animatorCtrlField.GetValue(characterData) as RuntimeAnimatorController;

            // Root
            var root = new GameObject(string.IsNullOrWhiteSpace(name) ? "Character" : name);
            try
            {
                // Attach metadata (no hard dependency: use simple holder)
                var meta = root.AddComponent<CharacterMetadata>();
                meta.sourceData = characterData;
                meta.prefabKind = kind;
                var assetPath = AssetDatabase.GetAssetPath(characterData);
                if (!string.IsNullOrEmpty(assetPath)) meta.sourceGuid = AssetDatabase.AssetPathToGUID(assetPath);

                GameObject modelInstance = null;
                // Try to instantiate model from ModelPath if exists
                if (!string.IsNullOrWhiteSpace(modelPath))
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    if (model != null)
                    {
                        modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                        modelInstance.name = model.name;
                        modelInstance.transform.localPosition = Vector3.zero;
                        modelInstance.transform.localRotation = Quaternion.identity;
                        modelInstance.transform.localScale = Vector3.one;
                    }
                    else
                    {
                        Debug.LogWarning($"Model not found at '{modelPath}'. Prefab will have no visual");
                    }
                }

                // Ensure required components for gameplay
                var controller = EnsureComponent<CharacterController>(root);
                var rb = EnsureComponent<Rigidbody>(root);
                rb.useGravity = true;
                rb.isKinematic = true; // CharacterController handles movement; keep rigidbody kinematic to avoid conflicts
                var animator = EnsureComponent<Animator>(root);

                // Auto fit CharacterController using renderer bounds, if we have a model
                TryFitCharacterControllerFromModel(controller, root, modelInstance);

                // Build/assign animator controller priority: explicit > preset > auto
                RuntimeAnimatorController builtController = explicitController;
                if (builtController == null)
                {
                    var preset = FindAnimatorPresetForClass(cls ?? "Default");
                    if (preset != null)
                    {
                        builtController = BuildAnimatorControllerFor(name ?? root.name, preset);
                    }
                    else
                    {
                        builtController = BuildAnimatorControllerAuto(name ?? root.name, modelPath, characterData);
                        if (builtController != null)
                            Debug.Log($"Built basic animator controller for '{name}' using assigned/discovered clips.");
                    }
                }
                if (builtController != null)
                    animator.runtimeAnimatorController = builtController;

                // Save prefab
                var baseRoot = GetAssistantPackageRoot() ?? "Assets/TheCovenantKeepers/AI_Game_Assistant";
                var folder = Path.Combine(baseRoot, "Prefabs", kind).Replace('\\', '/');
                EnsureFolder(folder);
                var file = Path.Combine(folder, SanitizeFileName(root.name) + ".prefab").Replace('\\', '/');
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, file);
                AssetDatabase.ImportAsset(file);
                return file;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to build prefab for '{name}': {ex.Message}\n{ex}");
                return null;
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void TryFitCharacterControllerFromModel(CharacterController controller, GameObject root, GameObject modelInstance)
        {
            if (controller == null) return;
            try
            {
                var renderers = (modelInstance ?? root).GetComponentsInChildren<Renderer>();
                if (renderers == null || renderers.Length == 0) return;
                var b = new Bounds(renderers[0].bounds.center, Vector3.zero);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    b.Encapsulate(r.bounds);
                }
                // Convert world center to local
                var localCenter = root.transform.InverseTransformPoint(b.center);
                controller.height = Mathf.Max(0.5f, b.size.y);
                controller.radius = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.5f, 0.1f, controller.height * 0.5f);
                controller.center = new Vector3(0, localCenter.y - root.transform.position.y, 0);
                controller.slopeLimit = 45f;
                controller.stepOffset = Mathf.Clamp(controller.height * 0.25f, 0.1f, 0.5f);
                controller.skinWidth = 0.08f;
            }
            catch { /* ignore fit issues */ }
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        // Find AnimatorPreset ScriptableObject by class key using reflection
        private static ScriptableObject FindAnimatorPresetForClass(string classKey)
        {
            var guids = AssetDatabase.FindAssets("t:AnimatorPreset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;
                var f = so.GetType().GetField("characterClassKey", BindingFlags.Public | BindingFlags.Instance);
                var val = f?.GetValue(so) as string;
                if (!string.IsNullOrEmpty(val) && string.Equals(val, classKey, StringComparison.InvariantCultureIgnoreCase))
                    return so;
            }
            return null;
        }

        private static RuntimeAnimatorController BuildAnimatorControllerFor(string name, ScriptableObject preset)
        {
            var controller = new AnimatorController();
            controller.name = $"{name}_Controller";
            var rootStateMachine = controller.layers[0].stateMachine;

            // Parameters
            var parametersField = preset.GetType().GetField("parameters", BindingFlags.Public | BindingFlags.Instance);
            var parameters = parametersField?.GetValue(preset) as IEnumerable;
            if (parameters != null)
            {
                foreach (var p in parameters)
                {
                    if (p == null) continue;
                    var pname = p.GetType().GetField("name", BindingFlags.Public | BindingFlags.Instance)?.GetValue(p) as string;
                    var ptypeObj = p.GetType().GetField("type", BindingFlags.Public | BindingFlags.Instance)?.GetValue(p);
                    if (string.IsNullOrWhiteSpace(pname) || ptypeObj == null) continue;
                    var ptype = (AnimatorControllerParameterType)ptypeObj;
                    controller.AddParameter(pname, ptype);
                }
            }

            // States
            AnimatorState defaultState = null;
            var statesField = preset.GetType().GetField("states", BindingFlags.Public | BindingFlags.Instance);
            var states = statesField?.GetValue(preset) as IEnumerable;
            if (states != null)
            {
                foreach (var s in states)
                {
                    if (s == null) continue;
                    var sname = s.GetType().GetField("name", BindingFlags.Public | BindingFlags.Instance)?.GetValue(s) as string;
                    var clipMotion = s.GetType().GetField("animationClip", BindingFlags.Public | BindingFlags.Instance)?.GetValue(s) as Motion;
                    var isDefaultObj = s.GetType().GetField("isDefaultState", BindingFlags.Public | BindingFlags.Instance)?.GetValue(s);
                    if (string.IsNullOrWhiteSpace(sname)) continue;

                    var state = rootStateMachine.AddState(sname);
                    state.motion = clipMotion;
                    if (isDefaultObj is bool b && b && defaultState == null)
                        defaultState = state;
                }
            }

            if (defaultState == null && rootStateMachine.states.Length > 0)
                defaultState = rootStateMachine.states[0].state;
            if (defaultState != null)
                rootStateMachine.defaultState = defaultState;

            // Transitions
            var transitionsField = preset.GetType().GetField("transitions", BindingFlags.Public | BindingFlags.Instance);
            var transitions = transitionsField?.GetValue(preset) as IEnumerable;
            if (transitions != null)
            {
                foreach (var t in transitions)
                {
                    if (t == null) continue;
                    string src = t.GetType().GetField("sourceState", BindingFlags.Public | BindingFlags.Instance)?.GetValue(t) as string;
                    string dst = t.GetType().GetField("destinationState", BindingFlags.Public | BindingFlags.Instance)?.GetValue(t) as string;
                    if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(dst)) continue;

                    var from = rootStateMachine.states.FirstOrDefault(x => x.state.name == src).state;
                    var to = rootStateMachine.states.FirstOrDefault(x => x.state.name == dst).state;
                    if (from == null || to == null) continue;

                    var trans = from.AddTransition(to);
                    var hasExitObj = t.GetType().GetField("hasExitTime", BindingFlags.Public | BindingFlags.Instance)?.GetValue(t);
                    var durObj = t.GetType().GetField("duration", BindingFlags.Public | BindingFlags.Instance)?.GetValue(t);
                    trans.hasExitTime = hasExitObj is bool hb && hb;
                    trans.duration = durObj is float fd ? Mathf.Max(0, fd) : 0f;

                    var condsField = t.GetType().GetField("conditions", BindingFlags.Public | BindingFlags.Instance);
                    var conds = condsField?.GetValue(t) as IEnumerable;
                    if (conds != null)
                    {
                        foreach (var cond in conds)
                        {
                            if (cond == null) continue;
                            string pName = cond.GetType().GetField("parameterName", BindingFlags.Public | BindingFlags.Instance)?.GetValue(cond) as string;
                            var modeObj = cond.GetType().GetField("mode", BindingFlags.Public | BindingFlags.Instance)?.GetValue(cond);
                            var thrObj = cond.GetType().GetField("threshold", BindingFlags.Public | BindingFlags.Instance)?.GetValue(cond);
                            if (string.IsNullOrWhiteSpace(pName) || modeObj == null) continue;
                            var mode = (AnimatorConditionMode)modeObj;
                            var thr = thrObj is float f ? f : 0f;
                            trans.AddCondition(mode, thr, pName);
                        }
                    }
                }
            }

            // Store controller under Prefabs/Controllers
            var baseRoot = GetAssistantPackageRoot() ?? "Assets/TheCovenantKeepers/AI_Game_Assistant";
            var controllersFolder = Path.Combine(baseRoot, "Prefabs", "Controllers").Replace('\\', '/');
            EnsureFolder(controllersFolder);
            var controllerPath = Path.Combine(controllersFolder, controller.name + ".controller").Replace('\\', '/');
            AssetDatabase.CreateAsset(controller, controllerPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        }

        // Basic animator builder if no preset exists. Attempts to discover common clips (Idle/Walk/Run) near the model and use assigned ability clips if present.
        private static RuntimeAnimatorController BuildAnimatorControllerAuto(string name, string modelPath, ScriptableObject characterData = null)
        {
            // First read explicitly assigned clips on CharacterData if present
            var explicitClips = new Dictionary<string, AnimationClip>(StringComparer.InvariantCultureIgnoreCase);
            if (characterData != null)
            {
                AnimationClip GetClip(string field) => characterData.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance)?.GetValue(characterData) as AnimationClip;
                var idleClipAssigned = GetClip("IdleClip");
                var walkClipAssigned = GetClip("WalkClip");
                var runClipAssigned = GetClip("RunClip");
                var a1ClipAssigned = GetClip("Ability1Clip");
                var a2ClipAssigned = GetClip("Ability2Clip");
                var a3ClipAssigned = GetClip("Ability3Clip");
                var ultClipAssigned = GetClip("UltimateClip");
                if (idleClipAssigned != null) explicitClips["idle"] = idleClipAssigned;
                if (walkClipAssigned != null) explicitClips["walk"] = walkClipAssigned;
                if (runClipAssigned != null) explicitClips["run"] = runClipAssigned;
                if (a1ClipAssigned != null) explicitClips["a1"] = a1ClipAssigned;
                if (a2ClipAssigned != null) explicitClips["a2"] = a2ClipAssigned;
                if (a3ClipAssigned != null) explicitClips["a3"] = a3ClipAssigned;
                if (ultClipAssigned != null) explicitClips["ult"] = ultClipAssigned;
            }

            // Discover locomotion clips if not explicitly assigned
            var discovered = DiscoverClips(name, modelPath);
            if (!explicitClips.ContainsKey("idle") && discovered.TryGetValue("idle", out var dIdle)) explicitClips["idle"] = dIdle;
            if (!explicitClips.ContainsKey("walk") && discovered.TryGetValue("walk", out var dWalk)) explicitClips["walk"] = dWalk;
            if (!explicitClips.ContainsKey("run") && discovered.TryGetValue("run", out var dRun)) explicitClips["run"] = dRun;

            if (explicitClips.Count == 0)
                return null;

            var controller = new AnimatorController();
            controller.name = $"{name}_AutoController";
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("CastA1", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("CastA2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("CastA3", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("CastUlt", AnimatorControllerParameterType.Trigger);
            var sm = controller.layers[0].stateMachine;

            AnimatorState idle = null, walk = null, run = null;
            if (explicitClips.TryGetValue("idle", out var idleClip))
            {
                idle = sm.AddState("Idle");
                idle.motion = idleClip;
                sm.defaultState = idle;
            }
            if (explicitClips.TryGetValue("walk", out var walkClip))
            {
                walk = sm.AddState("Walk");
                walk.motion = walkClip;
            }
            if (explicitClips.TryGetValue("run", out var runClip))
            {
                run = sm.AddState("Run");
                run.motion = runClip;
            }
            // Add simple transitions based on Speed
            if (idle != null && walk != null)
            {
                var t1 = idle.AddTransition(walk); t1.hasExitTime = false; t1.duration = 0.1f; t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                var t2 = walk.AddTransition(idle); t2.hasExitTime = false; t2.duration = 0.1f; t2.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }
            if (walk != null && run != null)
            {
                var t3 = walk.AddTransition(run); t3.hasExitTime = false; t3.duration = 0.1f; t3.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");
                var t4 = run.AddTransition(walk); t4.hasExitTime = false; t4.duration = 0.1f; t4.AddCondition(AnimatorConditionMode.Less, 0.6f, "Speed");
            }

            // Ability states if clips exist
            void AddAbilityState(string key, string stateName, string triggerName)
            {
                if (!explicitClips.TryGetValue(key, out var clip)) return;
                var abilityState = sm.AddState(stateName);
                abilityState.motion = clip;
                // AnyState -> ability using trigger
                var anyToAbility = sm.AddAnyStateTransition(abilityState);
                anyToAbility.hasExitTime = false;
                anyToAbility.duration = 0.05f;
                anyToAbility.AddCondition(AnimatorConditionMode.If, 0, triggerName);
                // Back to idle after play
                if (idle != null)
                {
                    var back = abilityState.AddTransition(idle);
                    back.hasExitTime = true;
                    back.exitTime = 0.95f;
                    back.duration = 0.1f;
                }
            }

            AddAbilityState("a1", "Ability1", "CastA1");
            AddAbilityState("a2", "Ability2", "CastA2");
            AddAbilityState("a3", "Ability3", "CastA3");
            AddAbilityState("ult", "Ultimate", "CastUlt");

            // Save controller asset
            var baseRoot = GetAssistantPackageRoot() ?? "Assets/TheCovenantKeepers/AI_Game_Assistant";
            var controllersFolder = Path.Combine(baseRoot, "Prefabs", "Controllers").Replace('\\', '/');
            EnsureFolder(controllersFolder);
            var controllerPath = Path.Combine(controllersFolder, controller.name + ".controller").Replace('\\', '/');
            AssetDatabase.CreateAsset(controller, controllerPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        }

        private static Dictionary<string, AnimationClip> DiscoverClips(string characterName, string modelPath)
        {
            var dict = new Dictionary<string, AnimationClip>(StringComparer.InvariantCultureIgnoreCase);

            IEnumerable<string> GuidSearch(string filter, string folderPath = null)
            {
                if (!string.IsNullOrEmpty(folderPath))
                {
                    return AssetDatabase.FindAssets(filter, new[] { folderPath });
                }
                return AssetDatabase.FindAssets(filter);
            }

            string folder = null;
            if (!string.IsNullOrEmpty(modelPath))
            {
                folder = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder) && !folder.StartsWith("Assets")) folder = null;
            }

            void TryAddClip(string key, Func<AnimationClip, bool> predicate)
            {
                if (dict.ContainsKey(key)) return;
                IEnumerable<string> guids = GuidSearch("t:AnimationClip", folder);
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip == null) continue;
                    if (predicate(clip))
                    {
                        dict[key] = clip;
                        break;
                    }
                }
            }

            // Try by common names and character name hints
            TryAddClip("idle", c => NameLike(c.name, characterName) && c.name.ToLower().Contains("idle"));
            TryAddClip("walk", c => NameLike(c.name, characterName) && c.name.ToLower().Contains("walk"));
            TryAddClip("run", c => NameLike(c.name, characterName) && c.name.ToLower().Contains("run"));

            // Fallback: without character hint
            if (!dict.ContainsKey("idle")) TryAddClip("idle", c => c.name.ToLower().Contains("idle"));
            if (!dict.ContainsKey("walk")) TryAddClip("walk", c => c.name.ToLower().Contains("walk"));
            if (!dict.ContainsKey("run")) TryAddClip("run", c => c.name.ToLower().Contains("run"));

            return dict;
        }

        private static bool NameLike(string clipName, string charName)
        {
            if (string.IsNullOrWhiteSpace(clipName) || string.IsNullOrWhiteSpace(charName)) return true;
            var a = clipName.Replace(" ", "").ToLowerInvariant();
            var b = charName.Replace(" ", "").ToLowerInvariant();
            return a.Contains(b) || b.Contains(a);
        }

        private static string GetAssistantPackageRoot()
        {
            // Try to read AssistantPaths.PackageRoot via reflection if available
            var t = Type.GetType("TheCovenantKeepers.AI_Game_Assistant.AssistantPaths");
            var f = t?.GetField("PackageRoot", BindingFlags.Public | BindingFlags.Static);
            return f?.GetValue(null) as string;
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
