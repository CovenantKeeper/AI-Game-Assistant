#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TheCovenantKeepers.AI_Game_Assistant.Editor.Animation;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor.Menu
{
    /// <summary>
    /// Thin shims that keep legacy toolbar commands working, but route to:
    ///  - Assistant Window (opens)
    ///  - AnimationPipeline façade (single source of truth)
    /// </summary>
    public static class LegacyAnimationMenu
    {
        private const string Root = "The Covenant Keepers/AI Game Assistant/";

        [MenuItem(Root + "Open Assistant Window", priority = 0)]
        private static void OpenAssistantWindow()
        {
            AnimationPipeline.TryOpenAssistantWindow();
        }

        [MenuItem(Root + "Rebuild All", priority = 10)]
        private static void RebuildAll()
        {
            AnimationPipeline.TryOpenAssistantWindow();
            AnimationPipeline.RebuildAllForSelection();
        }

        [MenuItem(Root + "Generate Animator Controller", priority = 20)]
        private static void GenerateController()
        {
            AnimationPipeline.TryOpenAssistantWindow();
            AnimationPipeline.GenerateAnimatorControllerForSelection();
        }

        [MenuItem(Root + "Build Locomotion Blend Trees", priority = 21)]
        private static void BuildLocomotion()
        {
            AnimationPipeline.TryOpenAssistantWindow();
            AnimationPipeline.BuildLocomotionBlendTreesForSelection();
        }

        [MenuItem(Root + "Setup Upper-Body Layer", priority = 22)]
        private static void SetupUpperBody()
        {
            AnimationPipeline.TryOpenAssistantWindow();
            AnimationPipeline.SetupUpperBodyLayerForSelection();
        }

        [MenuItem(Root + "Create/Assign Avatar Masks", priority = 23)]
        private static void AvatarMasks()
        {
            AnimationPipeline.TryOpenAssistantWindow();
            AnimationPipeline.CreateAssignAvatarMasksForSelection();
        }

        [MenuItem(Root + "Apply Animator Presets", priority = 24)]
        private static void ApplyPresets()
        {
            AnimationPipeline.TryOpenAssistantWindow();
            AnimationPipeline.ApplyAnimatorPresetsForSelection();
        }
    }
}
#endif
