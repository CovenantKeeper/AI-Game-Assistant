#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    /// <summary>
    /// Adds or repairs standard ability transitions on AnimatorController(s).
    /// - Ensures states Ability1/2/3/Ultimate exist (creates placeholders if missing)
    /// - Ensures parameters CastA1/CastA2/CastA3/CastUlt exist
    /// - Adds AnyState -> AbilityX (Trigger) transitions
    /// - Adds AbilityX -> BaseReturn transition (Idle/Walk/Run), immediate if clip is missing
    /// - Optional utilities to disable looping on ability clips to avoid stuck states
    /// </summary>
    public static class AnimatorTransitionsFixer
    {
        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Animator + "/Add/Repair Ability Transitions (Selected)")]
        public static void FixSelected()
        {
            var selection = Selection.objects;
            int processed = 0, fixedCtr = 0;
            foreach (var obj in selection)
            {
                AnimatorController ac = obj as AnimatorController;
                if (ac == null)
                {
                    var path = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(path)) ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                }
                if (ac == null) continue;
                processed++;
                fixedCtr += FixController(ac);
            }

            if (processed == 0)
                EditorUtility.DisplayDialog("Ability Transitions", "Select one or more AnimatorController assets.", "OK");
            else
                EditorUtility.DisplayDialog("Ability Transitions", $"Controllers: {processed}\nTransitions ensured/updated: {fixedCtr}", "OK");
        }

        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Clips + "/Disable Loop On Ability Clips (Selected Controller)")]
        public static void DisableLoopOnAbilityClipsSelected()
        {
            var ac = Selection.activeObject as AnimatorController;
            if (ac == null)
            {
                var path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(path)) ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            }
            if (ac == null)
            {
                EditorUtility.DisplayDialog("Disable Loop", "Select an AnimatorController asset.", "OK");
                return;
            }

            int changed = DisableLoopOnAbilityClips(ac);
            EditorUtility.DisplayDialog("Disable Loop", changed > 0 ? $"Updated {changed} clip(s)." : "No ability clips found or already non-looping.", "OK");
        }

        private static int DisableLoopOnAbilityClips(AnimatorController ac)
        {
            if (ac.layers == null || ac.layers.Length == 0) return 0;
            var sm = ac.layers[0].stateMachine;
            if (sm == null) return 0;
            string[] names = { "Ability1", "Ability2", "Ability3", "Ultimate", "LightAttack", "HeavyAttack", "Hit", "Dodge" };
            int changed = 0;
            foreach (var name in names)
            {
                var st = sm.states.FirstOrDefault(s => s.state != null && s.state.name == name).state;
                var clip = st != null ? st.motion as AnimationClip : null;
                if (clip == null) continue;
                if (SetClipLoop(clip, false)) changed++;
                // legacy wrap mode as a hint (some setups still read it)
                if (clip.wrapMode != WrapMode.Once) { clip.wrapMode = WrapMode.Once; EditorUtility.SetDirty(clip); }
            }
            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return changed;
        }

        // SerializedObject hack to toggle loop on .anim or imported clips
        private static bool SetClipLoop(AnimationClip clip, bool loop)
        {
            if (clip == null) return false;
            var so = new SerializedObject(clip);
            var settings = so.FindProperty("m_AnimationClipSettings");
            if (settings == null) return false;
            var loopTime = settings.FindPropertyRelative("m_LoopTime");
            var loopBlend = settings.FindPropertyRelative("m_LoopBlend");
            bool changed = false;
            if (loopTime != null && loopTime.boolValue != loop) { loopTime.boolValue = loop; changed = true; }
            if (loopBlend != null && loopBlend.boolValue != loop) { loopBlend.boolValue = loop; changed = true; }
            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(clip);
            }
            return changed;
        }

        private static int FixController(AnimatorController ac)
        {
            int changes = 0;
            if (ac.layers == null || ac.layers.Length == 0)
            {
                var layer = new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f, stateMachine = new AnimatorStateMachine() };
                ac.AddLayer(layer); changes++;
            }
            var sm = ac.layers[0].stateMachine ?? (ac.layers[0].stateMachine = new AnimatorStateMachine());

            AnimatorState BaseReturn()
            {
                var map = sm.states.ToDictionary(s => s.state.name, s => s.state);
                if (map.TryGetValue("Idle", out var idle) && idle != null) return idle;
                if (map.TryGetValue("Walk", out var walk) && walk != null) return walk;
                if (map.TryGetValue("Run", out var run) && run != null) return run;
                if (sm.states.Length > 0) return sm.states[0].state;
                // create a default idle placeholder if no states
                var clip = new AnimationClip { name = "Idle_Placeholder" };
                var st = sm.AddState("Idle"); st.motion = clip; sm.defaultState = st;
                AddSubAsset(ac, clip); changes++;
                return st;
            }

            // Ensure parameters
            changes += EnsureTrigger(ac, "CastA1");
            changes += EnsureTrigger(ac, "CastA2");
            changes += EnsureTrigger(ac, "CastA3");
            changes += EnsureTrigger(ac, "CastUlt");

            // Ensure states and transitions
            changes += EnsureAbility(ac, sm, "Ability1", "CastA1", BaseReturn());
            changes += EnsureAbility(ac, sm, "Ability2", "CastA2", BaseReturn());
            changes += EnsureAbility(ac, sm, "Ability3", "CastA3", BaseReturn());
            changes += EnsureAbility(ac, sm, "Ultimate", "CastUlt", BaseReturn());

            if (changes > 0)
            {
                EditorUtility.SetDirty(ac);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return changes;
        }

        private static int EnsureTrigger(AnimatorController ac, string name)
        {
            if (!ac.parameters.Any(p => p.name == name))
            {
                ac.AddParameter(name, AnimatorControllerParameterType.Trigger);
                return 1;
            }
            return 0;
        }

        private static int EnsureAbility(AnimatorController ac, AnimatorStateMachine sm, string stateName, string trigger, AnimatorState baseReturn)
        {
            int changes = 0;
            // Ensure state exists
            var state = sm.states.FirstOrDefault(s => s.state != null && s.state.name == stateName).state;
            if (state == null)
            {
                state = sm.AddState(stateName);
                changes++;
            }
            // Ensure it has a motion (placeholder if missing)
            if (state.motion == null)
            {
                var clip = new AnimationClip { name = stateName + "_Placeholder" };
                state.motion = clip; AddSubAsset(ac, clip); changes++;
            }

            // State safety: use write defaults to reset non-driven properties
            state.writeDefaultValues = true;

            // Ensure AnyState -> Ability transition with trigger
            bool hasAnyTo = sm.anyStateTransitions.Any(t => t != null && t.destinationState == state);
            if (!hasAnyTo)
            {
                var t = sm.AddAnyStateTransition(state);
                t.hasExitTime = false; t.duration = 0.05f; t.hasFixedDuration = true; t.AddCondition(AnimatorConditionMode.If, 0, trigger);
                changes++;
            }
            else
            {
                // ensure condition exists
                var t = sm.anyStateTransitions.First(x => x.destinationState == state);
                if (t.conditions == null || !t.conditions.Any(c => c.parameter == trigger))
                {
                    t.AddCondition(AnimatorConditionMode.If, 0, trigger);
                    changes++;
                }
                t.hasExitTime = false; t.hasFixedDuration = true;
            }

            // Ensure Ability -> BaseReturn transition
            bool hasOut = state.transitions != null && state.transitions.Any(tr => tr != null && tr.destinationState == baseReturn);
            if (!hasOut)
            {
                var back = state.AddTransition(baseReturn);
                if (state.motion != null)
                {
                    back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.1f; back.hasFixedDuration = true;
                }
                else
                {
                    back.hasExitTime = false; back.duration = 0.05f; back.hasFixedDuration = true;
                }
                changes++;
            }

            return changes;
        }

        private static void AddSubAsset(AnimatorController ac, Object sub)
        {
            var path = AssetDatabase.GetAssetPath(ac);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.AddObjectToAsset(sub, ac);
            }
        }
    }
}
#endif
