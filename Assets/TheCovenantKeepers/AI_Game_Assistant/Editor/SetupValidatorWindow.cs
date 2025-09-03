#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor
{
    public class SetupValidatorWindow : EditorWindow
    {
        // Main entry in The Covenant Keepers menu (normalized)
        [MenuItem("The Covenant Keepers/AI Game Assistant/Setup Validator")]
        public static void ShowWindow()
        {
            GetWindow<SetupValidatorWindow>("Setup Validator");
        }

        // Optional quick action (also under The Covenant Keepers)
        [MenuItem("The Covenant Keepers/AI Game Assistant/Run Setup Validation")]
        public static void RunValidationMenu()
        {
            RunValidation();
        }

        private static void RunValidation()
        {
            // Keep this lightweight/compile-safe
            Debug.Log("[Setup Validator] Basic check ran (customize as needed).\n");
            // Add any simple checks here (e.g. confirm key assets exist).
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
