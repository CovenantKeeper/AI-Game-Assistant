#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    /// <summary>
    /// Asset pipeline hook to repair AnimatorControllers on import/rename/move.
    /// Also exposes a menu to repair selected/all.
    /// </summary>
    public class AnimatorControllerAutoRepairPostprocessor : AssetPostprocessor
    {
        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Animator + "/Repair Selected AnimatorController(s)")]
        public static void RepairSelectedAnimatorControllers()
        {
            int fixedCount = 0;
            foreach (var obj in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (ac == null) continue;
                Repair(ac);
                fixedCount++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Repair AnimatorControllers", fixedCount > 0 ? $"Fixed {fixedCount} controller(s)." : "No AnimatorController selected.", "OK");
        }

        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Animator + "/Repair All AnimatorControllers In Project", priority = 201)]
        public static void RepairAllAnimatorControllersInProject()
        {
            var guids = AssetDatabase.FindAssets("t:AnimatorController");
            int fixedCount = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (ac == null) continue;
                Repair(ac);
                fixedCount++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Repair AnimatorControllers", $"Fixed {fixedCount} controller(s).", "OK");
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            void Scan(string path)
            {
                if (string.IsNullOrEmpty(path)) return;
                if (!path.EndsWith(".controller")) return;
                var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (ac == null) return;
                if (ac.layers == null || ac.layers.Length == 0 || ac.layers[0].stateMachine == null)
                {
                    Repair(ac);
                }
            }

            foreach (var p in importedAssets) Scan(p);
            foreach (var p in movedAssets) Scan(p);
        }

        private static void Repair(AnimatorController ac)
        {
            if (ac == null) return;
            // minimal inline repair
            if (ac.layers == null || ac.layers.Length == 0)
            {
                var layer = new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f, stateMachine = new AnimatorStateMachine() };
                ac.AddLayer(layer);
            }
            var layer0 = ac.layers[0];
            if (layer0.stateMachine == null)
            {
                layer0.stateMachine = new AnimatorStateMachine();
                var layers = ac.layers; layers[0] = layer0; ac.layers = layers;
            }
            var sm = ac.layers[0].stateMachine;
            if (sm.states.Length == 0)
            {
                var clip = new AnimationClip { name = "Idle_Placeholder" };
                var st = sm.AddState("Idle"); st.motion = clip; sm.defaultState = st;
                var path = AssetDatabase.GetAssetPath(ac);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.AddObjectToAsset(clip, ac);
                    AssetDatabase.ImportAsset(path);
                }
            }
            if (sm.defaultState == null && sm.states.Length > 0) sm.defaultState = sm.states[0].state;
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
