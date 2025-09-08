#if UNITY_EDITOR
namespace TheCovenantKeepers.AI_Game_Assistant
{
    using UnityEngine;
#if HAS_INPUT_SYSTEM
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.Utilities;
#endif
    using UnityEditor;

    namespace ChatGPTUnityPlugin.InputTools
    {
        public static class InputSupportUtility
        {
            public enum InputSystemType
            {
                OldInputManager,
                NewInputSystem,
                Both,
                Unknown
            }

            public static InputSystemType DetectInputSystem()
            {
                var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
                bool usingNew = symbols.Contains("ENABLE_INPUT_SYSTEM");
                bool usingOld = symbols.Contains("ENABLE_LEGACY_INPUT_MANAGER");

                if (usingNew && usingOld) return InputSystemType.Both;
                if (usingNew) return InputSystemType.NewInputSystem;
                if (usingOld) return InputSystemType.OldInputManager;

                return InputSystemType.Unknown;
            }

#if HAS_INPUT_SYSTEM
            public static void AddActionToAsset(InputActionAsset asset, string actionMapName, string actionName, string binding, InputActionType type = InputActionType.Button)
            {
                if (asset == null)
                {
                    Debug.LogWarning("No InputActionAsset provided. Skipping input generation.");
                    return;
                }

                var map = asset.FindActionMap(actionMapName);
                if (map == null)
                {
                    map = new InputActionMap(actionMapName);
                    asset.AddActionMap(map);
                }

                var action = map.FindAction(actionName);
                if (action == null)
                {
                    action = map.AddAction(actionName, type);
                }

                bool alreadyHasBinding = false;
                foreach (var b in action.bindings)
                {
                    if (b.path == binding)
                    {
                        alreadyHasBinding = true;
                        break;
                    }
                }

                if (!alreadyHasBinding)
                {
                    action.AddBinding(binding);
                    Debug.Log($"✅ Added Input Action: {actionName} with binding '{binding}' to map '{actionMapName}'");
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                }
            }
#endif

            public static void WarnIfUsingOldSystem(string expectedInputName)
            {
                Debug.LogWarning($"⚠ You are using the old Input Manager. Please ensure '{expectedInputName}' is defined in Project Settings > Input.");
            }
        }
    }
}
#endif
