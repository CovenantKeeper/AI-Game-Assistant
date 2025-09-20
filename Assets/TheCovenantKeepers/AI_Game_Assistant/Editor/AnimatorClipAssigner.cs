#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    public static class AnimatorClipAssigner
    {
        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Animator + "/Assign Clips From Selected Folder -> Selected Controller")]
        public static void AssignFromSelectedFolderToSelectedController()
        {
            // Folder
            string folder = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                EditorUtility.DisplayDialog("Assign Clips", "Select a folder in Project view that contains AnimationClips.", "OK");
                return;
            }
            // Controller
            var controllers = Selection.objects
                .Select(o => o as AnimatorController ?? AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(o)))
                .Where(a => a != null).ToList();
            if (controllers.Count == 0)
            {
                EditorUtility.DisplayDialog("Assign Clips", "Also select an AnimatorController asset.", "OK");
                return;
            }

            int totalAssigned = 0;
            foreach (var ac in controllers)
            {
                totalAssigned += AssignFolderToController(folder, ac);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Assign Clips", $"Assigned {totalAssigned} state clip(s) from\n{folder}", "OK");
        }

        [MenuItem(TheCovenantKeepers.AI_Game_Assistant.Editor.TckMenu.Animator + "/Assign Clips From Folder... -> Selected Controller")]
        public static void AssignFromFolderPrompt()
        {
            string start = Application.dataPath;
            string abs = EditorUtility.OpenFolderPanel("Pick folder with AnimationClips", start, "");
            if (string.IsNullOrEmpty(abs)) return;
            string rel = AbsToAssetPath(abs);
            if (string.IsNullOrEmpty(rel) || !AssetDatabase.IsValidFolder(rel))
            {
                EditorUtility.DisplayDialog("Assign Clips", "Pick a folder under the project Assets/ directory.", "OK");
                return;
            }

            var ac = Selection.activeObject as AnimatorController;
            if (ac == null)
            {
                var path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(path)) ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            }
            if (ac == null)
            {
                EditorUtility.DisplayDialog("Assign Clips", "Select an AnimatorController asset.", "OK");
                return;
            }

            int assigned = AssignFolderToController(rel, ac);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Assign Clips", $"Assigned {assigned} state clip(s) from\n{rel}", "OK");
        }

        private static int AssignFolderToController(string folder, AnimatorController ac)
        {
            var sm = EnsureStateMachine(ac);
            var clips = LoadClipsInFolder(folder);
            if (clips.Count == 0) return 0;

            // Map keywords per state
            var map = new Dictionary<string, string[]>(System.StringComparer.InvariantCultureIgnoreCase)
            {
                {"Idle", new[]{"idle","aimidle","stand"} },
                {"Walk", new[]{"walk","move","locomotion","strafe"} },
                {"Run", new[]{"run","sprint","jog"} },
                {"Jump", new[]{"jump","leap","hop"} },
                {"Fall", new[]{"fall","air","inair"} },
                {"Land", new[]{"land","landing"} },
                {"Dodge", new[]{"dodge","roll","evade","sidestep"} },
                {"Block", new[]{"block","guard","aim"} },
                {"Hit", new[]{"hit","hurt","impact","damage","gethit"} },
                {"Death", new[]{"die","death"} },
                // Bow pack typical names
                {"LightAttack", new[]{"shoot","fire","release","attack","shot"} },
                {"HeavyAttack", new[]{"power","charged","power shot","charge"} },
                {"Ability1", new[]{"ability","special","skill","power shot","charged"} },
                {"Ability2", new[]{"ability2","special2","skill2"} },
                {"Ability3", new[]{"ability3","special3","skill3"} },
                {"Ultimate", new[]{"ultimate","ult"} },
            };

            int assigned = 0;
            foreach (var kv in map)
            {
                var state = FindOrCreateState(sm, kv.Key);
                var clip = FindBest(clips, kv.Value);
                if (clip != null)
                {
                    state.motion = clip;
                    // Set loop flags heuristically
                    bool loop = kv.Key is "Idle" or "Walk" or "Run" or "Fall" or "Block";
                    SetLoop(clip, loop);
                    EditorUtility.SetDirty(state);
                    assigned++;
                }
            }
            return assigned;
        }

        private static AnimatorStateMachine EnsureStateMachine(AnimatorController ac)
        {
            if (ac.layers == null || ac.layers.Length == 0)
            {
                var layer = new AnimatorControllerLayer { name = "Base Layer", defaultWeight = 1f, stateMachine = new AnimatorStateMachine() };
                ac.AddLayer(layer);
            }
            if (ac.layers[0].stateMachine == null)
                ac.layers[0].stateMachine = new AnimatorStateMachine();
            return ac.layers[0].stateMachine;
        }

        private static List<AnimationClip> LoadClipsInFolder(string folder)
        {
            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[]{folder});
            var list = new List<AnimationClip>();
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                if (c != null) list.Add(c);
            }
            return list;
        }

        private static AnimationClip FindBest(List<AnimationClip> clips, IEnumerable<string> tokens)
        {
            AnimationClip best = null;
            int bestScore = 0;
            foreach (var c in clips)
            {
                var name = c.name.ToLowerInvariant();
                int score = 0;
                foreach (var t in tokens)
                {
                    var tok = (t ?? "").ToLowerInvariant();
                    if (string.IsNullOrEmpty(tok)) continue;
                    if (name.Contains(tok)) score += tok.Length;
                }
                if (score > bestScore)
                {
                    bestScore = score; best = c;
                }
            }
            return best;
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string name)
        {
            var existing = sm.states.FirstOrDefault(s => s.state != null && s.state.name == name).state;
            if (existing != null) return existing;
            return sm.AddState(name);
        }

        private static void SetLoop(AnimationClip clip, bool loop)
        {
            if (clip == null) return;
            var so = new SerializedObject(clip);
            var settings = so.FindProperty("m_AnimationClipSettings");
            if (settings != null)
            {
                var loopTime = settings.FindPropertyRelative("m_LoopTime");
                var loopBlend = settings.FindPropertyRelative("m_LoopBlend");
                if (loopTime != null) loopTime.boolValue = loop;
                if (loopBlend != null) loopBlend.boolValue = loop;
                so.ApplyModifiedProperties();
            }
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            EditorUtility.SetDirty(clip);
        }

        private static string AbsToAssetPath(string abs)
        {
            abs = abs.Replace('\\','/');
            var projectAssets = Application.dataPath.Replace('\\','/');
            if (!abs.StartsWith(projectAssets)) return null;
            return "Assets" + abs.Substring(projectAssets.Length);
        }
    }
}
#endif
