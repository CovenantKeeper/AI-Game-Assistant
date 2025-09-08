#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    public class SetupValidatorWindow : EditorWindow
    {
        // Unified under the same menu hierarchy
        [MenuItem("The Covenant Keepers/AI Game Assistant/Diagnostics/Setup Validator")]
        public static void ShowWindow()
        {
            GetWindow<SetupValidatorWindow>("Setup Validator");
        }

        [MenuItem("The Covenant Keepers/AI Game Assistant/Diagnostics/Run Setup Validation")]
        public static void RunValidationMenu()
        {
            RunValidation();
        }

        private static void RunValidation()
        {
            Debug.Log("[Setup Validator] Basic check ran (customize as needed).\n");
        }

        private void OnGUI()
        {
            GUILayout.Label("AI Assistant – Setup Validator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Click the button below to run a basic validation.\n" +
                "Extend RunValidation() with whatever checks you need.",
                MessageType.Info);

            if (GUILayout.Button("Run Validation"))
            {
                RunValidation();
            }
        }
    }
}
#endif
