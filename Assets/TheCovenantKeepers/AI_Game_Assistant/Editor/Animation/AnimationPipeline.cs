#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor.Animation
{
    /// <summary>
    /// One façade for ALL animation-generation actions.
    /// The Assistant Window and any legacy menu items should ONLY call into this class.
    /// </summary>
    public static class AnimationPipeline
    {
        private const string LogPrefix = "[AI Game Assistant][Animation] ";

        /// <summary>Convenience logger with a consistent prefix.</summary>
        private static void Log(string msg) => Debug.Log(LogPrefix + msg);
        private static void Warn(string msg) => Debug.LogWarning(LogPrefix + msg);
        private static void Error(string msg) => Debug.LogError(LogPrefix + msg);

        /// <summary>Returns current selection. We’ll improve filtering later.</summary>
        private static UnityEngine.Object[] CurrentSelectionOrWarn()
        {
            var sel = Selection.objects ?? Array.Empty<UnityEngine.Object>();
            if (sel.Length == 0)
            {
                Warn("Nothing selected. Select a character prefab, model, or asset and try again.");
            }
            return sel;
        }

        // =========== High-level façade methods (call these from UI) ===========

        public static void GenerateAnimatorControllerForSelection()
        {
            var targets = CurrentSelectionOrWarn();
            if (targets.Length == 0) return;

            foreach (var t in targets)
            {
                Log($"Generate Animator Controller → {t.name}");
                // TODO: Create/locate AnimatorController asset, add base layers/states.
                // For now, just log so we compile safely.
            }
            Log("Done: Generate Animator Controller (stub).");
        }

        public static void BuildLocomotionBlendTreesForSelection()
        {
            var targets = CurrentSelectionOrWarn();
            if (targets.Length == 0) return;

            foreach (var t in targets)
            {
                Log($"Build Locomotion Blend Trees (Walk/Run) → {t.name}");
                // TODO: Create 1D/2D BlendTrees and wire to controller.
            }
            Log("Done: Build Locomotion Blend Trees (stub).");
        }

        public static void SetupUpperBodyLayerForSelection()
        {
            var targets = CurrentSelectionOrWarn();
            if (targets.Length == 0) return;

            foreach (var t in targets)
            {
                Log($"Setup Upper-Body Layer → {t.name}");
                // TODO: Create additive/override layer for upper-body attacks/casting.
            }
            Log("Done: Setup Upper-Body Layer (stub).");
        }

        public static void CreateAssignAvatarMasksForSelection()
        {
            var targets = CurrentSelectionOrWarn();
            if (targets.Length == 0) return;

            foreach (var t in targets)
            {
                Log($"Create/Assign Avatar Masks → {t.name}");
                // TODO: Create AvatarMask assets and assign to layers as needed.
            }
            Log("Done: Create/Assign Avatar Masks (stub).");
        }

        public static void ApplyAnimatorPresetsForSelection()
        {
            var targets = CurrentSelectionOrWarn();
            if (targets.Length == 0) return;

            foreach (var t in targets)
            {
                Log($"Apply Animator Presets → {t.name}");
                // TODO: Load preset ScriptableObjects and apply to controller.
            }
            Log("Done: Apply Animator Presets (stub).");
        }

        /// <summary>Deterministic end-to-end build flow in one click.</summary>
        public static void RebuildAllForSelection()
        {
            var targets = CurrentSelectionOrWarn();
            if (targets.Length == 0) return;

            Log("Rebuild All → Start");
            try
            {
                GenerateAnimatorControllerForSelection();
                BuildLocomotionBlendTreesForSelection();
                SetupUpperBodyLayerForSelection();
                CreateAssignAvatarMasksForSelection();
                ApplyAnimatorPresetsForSelection();
                Log("Rebuild All → Success");
            }
            catch (Exception ex)
            {
                Error("Rebuild All → Failed: " + ex.Message);
                throw;
            }
        }

        // =========== Optional helper: try to open your Assistant Window ===========

        /// <summary>
        /// Tries to open the Assistant Window via reflection. Safe if not found.
        /// </summary>
        public static EditorWindow TryOpenAssistantWindow()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t => t.FullName ==
                    "TheCovenantKeepers.AI_Game_Assistant.Editor.UI.AssistantWindow");

            if (type != null)
            {
                var win = EditorWindow.GetWindow(type);
                win.titleContent = new GUIContent("AI Game Assistant");
                win.Show();
                Log("Opened Assistant Window.");
                // If later you add a public method like OpenWithTab(\"Animations\"), you can invoke it here via reflection.
                return win;
            }

            Warn("AssistantWindow type not found. (This is OK for now.)");
            return null;
        }
    }
}
#endif
