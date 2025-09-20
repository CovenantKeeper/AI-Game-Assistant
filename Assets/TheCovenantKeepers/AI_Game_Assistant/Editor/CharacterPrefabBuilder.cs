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
        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Build + "/Build Selected Character Prefab")]
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

        // helper to ensure a base layer/state machine exists
        private static AnimatorStateMachine EnsureBaseLayerAndGetSM(AnimatorController controller)
        {
            if (controller.layers == null || controller.layers.Length == 0)
            {
                var layer = new AnimatorControllerLayer
                {
                    name = "Base Layer",
                    defaultWeight = 1f,
                    stateMachine = new AnimatorStateMachine()
                };
                controller.AddLayer(layer);
            }
            if (controller.layers[0].stateMachine == null)
            {
                var sm = new AnimatorStateMachine();
                var layer = controller.layers[0];
                layer.stateMachine = sm;
                controller.layers = controller.layers.Select((l, i) => i == 0 ? layer : l).ToArray();
            }
            return controller.layers[0].stateMachine;
        }

        private static void EnsureStatesHavePlaceholders(AnimatorController controller)
        {
            if (controller == null) return;
            var sm = EnsureBaseLayerAndGetSM(controller);
            foreach (var child in sm.states)
            {
                var st = child.state;
                if (st == null) continue;
                if (st.motion == null)
                {
                    var clip = new AnimationClip { name = $"{st.name}_Placeholder" };
                    st.motion = clip;
                }
            }
        }

        // Ensure the controller has at least one default state (placeholder idle) so it's always valid
        private static void EnsureControllerIsValid(AnimatorController controller)
        {
            if (controller == null) return;
            var sm = EnsureBaseLayerAndGetSM(controller);

            // Add Speed parameter if it's a locomotion style controller and parameter missing
            if (!controller.parameters.Any(p => p.name == "Speed"))
            {
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            }

            // Ensure every state has a motion to make exit times work reliably
            EnsureStatesHavePlaceholders(controller);

            if (sm.states == null || sm.states.Length == 0)
            {
                // Create a tiny placeholder idle clip as a sub-asset so the controller is valid
                var clip = new AnimationClip { name = "Idle_Placeholder" };
                var path = AssetDatabase.GetAssetPath(controller);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.AddObjectToAsset(clip, controller);
                    AssetDatabase.ImportAsset(path);
                }
                var state = sm.AddState("Idle");
                state.motion = clip;
                sm.defaultState = state;
            }
            else if (sm.defaultState == null)
            {
                sm.defaultState = sm.states[0].state;
            }

            EditorUtility.SetDirty(controller);
        }

        private static void RepairAnimatorController(AnimatorController controller)
        {
            if (controller == null) return;
            EnsureBaseLayerAndGetSM(controller);
            EnsureControllerIsValid(controller);

            // Ensure placeholder clips are embedded as sub-assets
            var path = AssetDatabase.GetAssetPath(controller);
            if (!string.IsNullOrEmpty(path))
            {
                foreach (var st in controller.layers[0].stateMachine.states)
                {
                    if (st.state == null) continue;
                    var motion = st.state.motion as AnimationClip;
                    if (motion == null) continue;
                    var mPath = AssetDatabase.GetAssetPath(motion);
                    if (string.IsNullOrEmpty(mPath))
                    {
                        AssetDatabase.AddObjectToAsset(motion, controller);
                    }
                }
            }

            AssetDatabase.SaveAssets();
        }

        // Quick-fix: repair currently selected AnimatorController assets
        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Animator + "/Repair Selected AnimatorController(s)")]
        public static void RepairSelectedAnimatorControllers()
        {
            int fixedCount = 0;
            foreach (var obj in Selection.objects)
            {
                AnimatorController ac = obj as AnimatorController;
                if (ac == null)
                {
                    var path = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(path))
                        ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                }
                if (ac == null) continue;
                RepairAnimatorController(ac);
                fixedCount++;
            }
            if (fixedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            EditorUtility.DisplayDialog("Repair AnimatorControllers", fixedCount > 0 ? $"Fixed {fixedCount} controller(s)." : "No AnimatorController selected.", "OK");
        }

        // Bulk-fix: scan project for AnimatorControllers and repair them
        [MenuItem("The Covenant Keepers/AI Game Assistant/Animator/Repair All AnimatorControllers In Project", priority = 201)]
        public static void RepairAllAnimatorControllersInProject()
        {
            var guids = AssetDatabase.FindAssets("t:AnimatorController");
            int fixedCount = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (ac == null) continue;
                RepairAnimatorController(ac);
                fixedCount++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Repair AnimatorControllers", $"Fixed {fixedCount} controller(s).", "OK");
        }

        private static RuntimeAnimatorController SaveAnimatorController(AnimatorController controller, string controllerPath)
        {
            var dir = Path.GetDirectoryName(controllerPath).Replace('\\','/');
            EnsureFolder(dir);

            // Replace existing asset if present to avoid keeping a previously broken asset
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null)
            {
                try { RepairAnimatorController(existing); } catch { }
                AssetDatabase.DeleteAsset(controllerPath);
            }

            // Guarantee states have placeholder motions before save
            EnsureStatesHavePlaceholders(controller);

            AssetDatabase.CreateAsset(controller, controllerPath);

            // After creating the asset, embed any in-memory placeholder clips as sub-assets
            foreach (var st in controller.layers[0].stateMachine.states)
            {
                var clip = st.state.motion as AnimationClip;
                if (clip == null) continue;
                var mPath = AssetDatabase.GetAssetPath(clip);
                if (string.IsNullOrEmpty(mPath))
                {
                    AssetDatabase.AddObjectToAsset(clip, controller);
                }
            }

            AssetDatabase.SaveAssets();
            var saved = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            RepairAnimatorController(saved);
            return saved as RuntimeAnimatorController;
        }

        // NEW: Build only the AnimatorController for a CharacterData and return its asset path
        public static string BuildAnimatorControllerForCharacter(ScriptableObject characterData)
        {
            if (characterData == null) return null;

            string GetString(string field) => characterData.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance)?.GetValue(characterData) as string;

            var name = GetString("Name");
            var modelPath = GetString("ModelPath");
            var cls = GetString("Class");

            try
            {
                RuntimeAnimatorController controller = null;
                var preset = FindAnimatorPresetForClass(cls ?? "Default");
                if (preset != null)
                {
                    controller = BuildAnimatorControllerFor(name ?? "Character", preset);
                }
                else
                {
                    controller = BuildAnimatorControllerAuto(name ?? "Character", modelPath, characterData);
                }

                if (controller == null)
                {
                    Debug.LogWarning($"AnimatorController could not be built for '{name}'. Ensure clips are assigned or discoverable.");
                    return null;
                }

                var path = AssetDatabase.GetAssetPath(controller);
                if (string.IsNullOrEmpty(path))
                {
                    var baseRoot = GetAssistantPackageRoot() ?? "Assets/TheCovenantKeepers/AI_Game_Assistant";
                    var controllersFolder = Path.Combine(baseRoot, "Prefabs", "Controllers").Replace('\\', '/');
                    path = Path.Combine(controllersFolder, controller.name + ".controller").Replace('\\', '/');
                }
                AssetDatabase.ImportAsset(path);
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to build AnimatorController for '{name}': {ex.Message}\n{ex}");
                return null;
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

            RuntimeAnimatorController explicitController = null;
            var animatorCtrlField = characterData.GetType().GetField("AnimatorController", BindingFlags.Public | BindingFlags.Instance);
            if (animatorCtrlField != null)
                explicitController = animatorCtrlField.GetValue(characterData) as RuntimeAnimatorController;

            var root = new GameObject(string.IsNullOrWhiteSpace(name) ? "Character" : name);
            try
            {
                var meta = root.AddComponent<CharacterMetadata>();
                meta.sourceData = characterData;
                meta.prefabKind = kind;
                var assetPath = AssetDatabase.GetAssetPath(characterData);
                if (!string.IsNullOrEmpty(assetPath)) meta.sourceGuid = AssetDatabase.AssetPathToGUID(assetPath);

                // Tag Player prefabs so cameras can auto-find them
                if (string.Equals(kind, "Player", StringComparison.InvariantCultureIgnoreCase))
                {
                    try { root.tag = "Player"; } catch { /* tag may not exist */ }
                }

                GameObject modelInstance = null;
                Animator modelAnimator = null;
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
                        modelAnimator = modelInstance.GetComponentInChildren<Animator>();
                    }
                    else
                    {
                        Debug.LogWarning($"Model not found at '{modelPath}'. Prefab will have no visual");
                    }
                }

                // Ensure required components for gameplay
                var controller = EnsureComponent<CharacterController>(root);
                var rb = EnsureComponent<Rigidbody>(root);
                rb.useGravity = false; // CharacterController handles gravity via scripts; keep rigidbody inert
                rb.isKinematic = true; // avoid physics forces interfering with CharacterController

                // Prefer the Animator attached to the model (has Avatar). Fallback to root Animator if missing.
                var animator = modelAnimator != null ? modelAnimator : EnsureComponent<Animator>(root);

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
                {
                    // As a safety, repair controller if needed
                    var ac = builtController as AnimatorController ?? AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(builtController));
                    if (ac != null) RepairAnimatorController(ac);
                    animator.runtimeAnimatorController = builtController;
                }

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

                // Compute collider center so that the controller's bottom aligns with the model's bounds bottom
                var worldBottom = new Vector3(b.center.x, b.min.y, b.center.z);
                var localBottom = root.transform.InverseTransformPoint(worldBottom);
                controller.center = new Vector3(0f, localBottom.y + controller.height * 0.5f, 0f);

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
            var rootStateMachine = EnsureBaseLayerAndGetSM(controller);

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

            // Create states
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
                    state.motion = clipMotion; // may be null; we'll fix later
                    if (isDefaultObj is bool b && b && defaultState == null)
                        defaultState = state;
                }
            }

            // Determine base return state preference
            AnimatorState BaseReturn()
            {
                var map = rootStateMachine.states.ToDictionary(x => x.state.name, x => x.state);
                if (map.TryGetValue("Idle", out var idle)) return idle;
                if (map.TryGetValue("Walk", out var walk)) return walk;
                if (map.TryGetValue("Run", out var run)) return run;
                return defaultState ?? (rootStateMachine.states.Length > 0 ? rootStateMachine.states[0].state : null);
            }

            if (defaultState == null && rootStateMachine.states.Length > 0)
                defaultState = rootStateMachine.states[0].state;
            if (defaultState != null)
                rootStateMachine.defaultState = defaultState;

            // Build transitions (support AnyState via empty source)
            var transitionsField = preset.GetType().GetField("transitions", BindingFlags.Public | BindingFlags.Instance);
            var transitions = transitionsField?.GetValue(preset) as IEnumerable;
            if (transitions != null)
            {
                var stateLookup = rootStateMachine.states.ToDictionary(x => x.state.name, x => x.state);
                foreach (var t in transitions)
                {
                    if (t == null) continue;
                    string src = t.GetType().GetField("sourceState", BindingFlags.Public | BindingFlags.Instance)?.GetValue(t) as string;
                    string dst = t.GetType().GetField("destinationState", BindingFlags.Public | BindingFlags.Instance)?.GetValue(t) as string;
                    if (string.IsNullOrWhiteSpace(dst)) continue;

                    stateLookup.TryGetValue(dst, out var to);
                    if (to == null) continue;

                    AnimatorStateTransition trans;
                    if (string.IsNullOrWhiteSpace(src))
                    {
                        // Treat empty source as AnyState transition
                        trans = rootStateMachine.AddAnyStateTransition(to);
                    }
                    else
                    {
                        stateLookup.TryGetValue(src, out var from);
                        if (from == null) continue;
                        trans = from.AddTransition(to);
                    }

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

            // Add sensible default return transitions for non-locomotion states if none were authored
            {
                var map = rootStateMachine.states.ToDictionary(x => x.state.name, x => x.state);
                var baseReturn = BaseReturn();
                if (baseReturn != null)
                {
                    foreach (var kv in map)
                    {
                        var nameKey = kv.Key;
                        var st = kv.Value;
                        if (nameKey == "Idle" || nameKey == "Walk" || nameKey == "Run" || nameKey == "Death") continue;
                        bool hasOutgoing = st.transitions != null && st.transitions.Length > 0;
                        if (hasOutgoing) continue;
                        var back = st.AddTransition(baseReturn);
                        // If state has no motion, do not rely on exit time
                        if (st.motion == null)
                        {
                            back.hasExitTime = false; back.duration = 0.05f;
                        }
                        else
                        {
                            back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.08f;
                        }
                    }
                }
            }

            // Make sure it's valid even if no states were defined in the preset
            EnsureControllerIsValid(controller);

            var baseRoot = GetAssistantPackageRoot() ?? "Assets/TheCovenantKeepers/AI_Game_Assistant";
            var controllersFolder = Path.Combine(baseRoot, "Prefabs", "Controllers").Replace('\\', '/');
            var controllerPath = Path.Combine(controllersFolder, controller.name + ".controller").Replace('\\', '/');
            return SaveAnimatorController(controller, controllerPath);
        }

        private static RuntimeAnimatorController BuildAnimatorControllerAuto(string name, string modelPath, ScriptableObject characterData = null)
        {
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

            var discovered = DiscoverClips(name, modelPath);
            void TryBring(string key)
            {
                if (!explicitClips.ContainsKey(key) && discovered.TryGetValue(key, out var clip)) explicitClips[key] = clip;
            }
            TryBring("idle"); TryBring("walk"); TryBring("run");
            TryBring("jump"); TryBring("fall"); TryBring("land");
            TryBring("dodge"); TryBring("light"); TryBring("heavy");
            TryBring("block"); TryBring("hit"); TryBring("die");

            var controller = new AnimatorController();
            controller.name = $"{name}_AutoController";
            // Locomotion & ability params
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("VelocityY", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackLight", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackHeavy", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dodge", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Block", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("CastA1", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("CastA2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("CastA3", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("CastUlt", AnimatorControllerParameterType.Trigger);

            var sm = EnsureBaseLayerAndGetSM(controller);

            // Locomotion states
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

            if (idle != null && walk != null)
            {
                var t1 = idle.AddTransition(walk); t1.hasExitTime = false; t1.duration = 0.1f; t1.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");
                var t2 = walk.AddTransition(idle); t2.hasExitTime = false; t2.duration = 0.1f; t2.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
            }
            if (walk != null && run != null)
            {
                var t3 = walk.AddTransition(run); t3.hasExitTime = false; t3.duration = 0.1f; t3.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed");
                var t4 = run.AddTransition(walk); t4.hasExitTime = false; t4.duration = 0.1f; t4.AddCondition(AnimatorConditionMode.Less, 0.6f, "Speed");
            }

            // Helper: return-to base state is any existing locomotion state, prefer idle
            AnimatorState BaseReturn()
            {
                if (idle != null) return idle;
                if (walk != null) return walk;
                if (run != null) return run;
                // make a placeholder state if none
                var st = sm.AddState("Idle");
                sm.defaultState = st;
                return st;
            }

            void AddTriggeredState(string key, string stateName, string triggerName, float backExitTime = 0.9f)
            {
                if (!explicitClips.TryGetValue(key, out var clip)) return;
                var st = sm.AddState(stateName);
                st.motion = clip;
                var any = sm.AddAnyStateTransition(st);
                any.hasExitTime = false; any.duration = 0.05f; any.AddCondition(AnimatorConditionMode.If, 0, triggerName);
                var back = st.AddTransition(BaseReturn());
                back.hasExitTime = true; back.exitTime = Mathf.Clamp01(backExitTime); back.duration = 0.1f;
            }

            // Jump/Fall/Land: use Jump trigger; optionally chain to Fall and Land if clips exist
            if (explicitClips.ContainsKey("jump"))
            {
                var jump = sm.AddState("Jump"); jump.motion = explicitClips["jump"];
                var any = sm.AddAnyStateTransition(jump); any.hasExitTime = false; any.duration = 0.05f; any.AddCondition(AnimatorConditionMode.If, 0, "Jump");
                AnimatorState nextAfterJump = null;
                if (explicitClips.TryGetValue("fall", out var fallClip))
                {
                    var fall = sm.AddState("Fall"); fall.motion = fallClip;
                    var j2f = jump.AddTransition(fall); j2f.hasExitTime = true; j2f.exitTime = 0.9f; j2f.duration = 0.05f;
                    nextAfterJump = fall;
                }
                if (explicitClips.TryGetValue("land", out var landClip))
                {
                    var land = sm.AddState("Land"); land.motion = landClip;
                    var from = nextAfterJump ?? jump;
                    var toLand = from.AddTransition(land); toLand.hasExitTime = true; toLand.exitTime = 0.9f; toLand.duration = 0.05f;
                    var l2base = land.AddTransition(BaseReturn()); l2base.hasExitTime = true; l2base.exitTime = 0.9f; l2base.duration = 0.05f;
                }
                else
                {
                    var back = (nextAfterJump ?? jump).AddTransition(BaseReturn());
                    back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.05f;
                }
            }

            // Combat and reactions
            AddTriggeredState("dodge", "Dodge", "Dodge", 0.95f);
            AddTriggeredState("light", "LightAttack", "AttackLight", 0.95f);
            AddTriggeredState("heavy", "HeavyAttack", "AttackHeavy", 0.95f);
            AddTriggeredState("hit", "HitReact", "Hit", 0.85f);
            if (explicitClips.TryGetValue("block", out var blockClip))
            {
                var block = sm.AddState("Block"); block.motion = blockClip;
                var anyToBlock = sm.AddAnyStateTransition(block); anyToBlock.hasExitTime = false; anyToBlock.duration = 0.05f; anyToBlock.AddCondition(AnimatorConditionMode.If, 0, "Block");
                var blockToBase = block.AddTransition(BaseReturn()); blockToBase.hasExitTime = false; blockToBase.duration = 0.05f; blockToBase.AddCondition(AnimatorConditionMode.IfNot, 0, "Block");
            }
            if (explicitClips.TryGetValue("die", out var dieClip))
            {
                var death = sm.AddState("Death"); death.motion = dieClip; death.writeDefaultValues = true;
                var anyToDeath = sm.AddAnyStateTransition(death); anyToDeath.hasExitTime = false; anyToDeath.duration = 0.05f; anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Die");
                // intentionally no return
            }

            // Ability states if clips exist
            void AddAbilityState(string key, string stateName, string triggerName)
            {
                if (!explicitClips.TryGetValue(key, out var clip)) return;
                var abilityState = sm.AddState(stateName);
                abilityState.motion = clip;
                var anyToAbility = sm.AddAnyStateTransition(abilityState);
                anyToAbility.hasExitTime = false;
                anyToAbility.duration = 0.05f;
                anyToAbility.AddCondition(AnimatorConditionMode.If, 0, triggerName);
                var back = abilityState.AddTransition(BaseReturn());
                back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.1f;
            }

            AddAbilityState("a1", "Ability1", "CastA1");
            AddAbilityState("a2", "Ability2", "CastA2");
            AddAbilityState("a3", "Ability3", "CastA3");
            AddAbilityState("ult", "Ultimate", "CastUlt");

            EnsureControllerIsValid(controller);

            var baseRoot = GetAssistantPackageRoot() ?? "Assets/TheCovenantKeepers/AI_Game_Assistant";
            var controllersFolder = Path.Combine(baseRoot, "Prefabs", "Controllers").Replace('\\', '/');
            EnsureFolder(controllersFolder);
            var controllerPath = Path.Combine(controllersFolder, controller.name + ".controller").Replace('\\', '/');
            return SaveAnimatorController(controller, controllerPath);
        }

        // Make discover method public so other tools (e.g., AnimatorPresetTemplates) can reuse it
        public static Dictionary<string, AnimationClip> DiscoverClips(string characterName, string modelPath)
        {
            var dict = new Dictionary<string, AnimationClip>(StringComparer.InvariantCultureIgnoreCase);

            List<string> searchFolders = new List<string>();
            if (!string.IsNullOrEmpty(modelPath))
            {
                var mFolder = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(mFolder) && mFolder.StartsWith("Assets")) searchFolders.Add(mFolder);
            }
            try
            {
                var settings = ChatGPTSettings.Get();
                var field = typeof(ChatGPTSettings).GetField("AdditionalAnimationSearchFolders", BindingFlags.Public | BindingFlags.Instance);
                var val = field != null ? field.GetValue(settings) as string[] : null;
                if (val != null)
                {
                    foreach (var f in val)
                    {
                        var p = (f ?? string.Empty).Replace('\\', '/');
                        if (!string.IsNullOrEmpty(p) && p.StartsWith("Assets") && AssetDatabase.IsValidFolder(p))
                            searchFolders.Add(p);
                    }
                }
            }
            catch { }

            if (AssetDatabase.IsValidFolder("Assets/ExplosiveLLC")) searchFolders.Add("Assets/ExplosiveLLC");
            searchFolders = searchFolders.Distinct().ToList();

            IEnumerable<string> GuidSearch(string filter, IEnumerable<string> folders)
            {
                if (folders != null && folders.Any())
                    return AssetDatabase.FindAssets(filter, folders.ToArray());
                return AssetDatabase.FindAssets(filter);
            }

            static string Norm(string s)
            {
                return (s ?? string.Empty)
                    .ToLowerInvariant()
                    .Replace(" ", "")
                    .Replace("_", "")
                    .Replace("-", "")
                    .Replace(".", "")
                    .Replace("(", "")
                    .Replace(")", "")
                    .Replace("/", "")
                    .Replace("\\", "");
            }

            bool NameMatches(AnimationClip c, IEnumerable<string> tokens)
            {
                var n = Norm(c.name);
                foreach (var t in tokens)
                {
                    var tn = Norm(t);
                    if (string.IsNullOrEmpty(tn)) continue;
                    if (n.Contains(tn)) return true;
                }
                return false;
            }

            void TryAddByTokens(string key, IEnumerable<string> tokens)
            {
                if (dict.ContainsKey(key)) return;

                AnimationClip best = null;
                void ScanGuids(IEnumerable<string> guids)
                {
                    foreach (var guid in guids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if (clip == null) continue;
                        if (NameMatches(clip, tokens))
                        {
                            if (best == null) { best = clip; continue; }
                            var p = AssetDatabase.GetAssetPath(clip);
                            var bp = AssetDatabase.GetAssetPath(best);
                            bool prefer = (p.Contains("/ExplosiveLLC/") && !bp.Contains("/ExplosiveLLC/")) ||
                                          (!string.IsNullOrEmpty(modelPath) && p.StartsWith(Path.GetDirectoryName(modelPath) ?? string.Empty));
                            if (prefer) best = clip;
                        }
                    }
                }

                ScanGuids(GuidSearch("t:AnimationClip", searchFolders));
                if (best == null)
                    ScanGuids(AssetDatabase.FindAssets("t:AnimationClip"));
                if (best != null) dict[key] = best;
            }

            var charHints = new[] { characterName };

            // Locomotion
            TryAddByTokens("idle", new[] { "idle", "idlebattle", "idlerelaxed", "idle_01", "relaxidle" }.Concat(charHints));
            TryAddByTokens("walk", new[] { "walk", "move", "walkforward", "locomotion", "strafe" }.Concat(charHints));
            TryAddByTokens("run", new[] { "run", "sprint", "jog", "movefast" }.Concat(charHints));

            // Jumping/air/landing
            TryAddByTokens("jump", new[] { "jump", "hop", "leap" }.Concat(charHints));
            TryAddByTokens("fall", new[] { "fall", "air", "inair" }.Concat(charHints));
            TryAddByTokens("land", new[] { "land", "landing" }.Concat(charHints));

            // Combat
            TryAddByTokens("light", new[] { "lightattack", "attack1", "attack_light", "slashlight", "punch", "attack" }.Concat(charHints));
            TryAddByTokens("heavy", new[] { "heavyattack", "attack2", "attack_heavy", "smash", "powerattack" }.Concat(charHints));
            TryAddByTokens("dodge", new[] { "dodge", "roll", "evade", "sidestep" }.Concat(charHints));
            TryAddByTokens("block", new[] { "block", "guard", "shield" }.Concat(charHints));
            TryAddByTokens("hit", new[] { "hit", "hurt", "impact", "damage", "gethit" }.Concat(charHints));
            TryAddByTokens("die", new[] { "die", "death", "dead" }.Concat(charHints));

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
