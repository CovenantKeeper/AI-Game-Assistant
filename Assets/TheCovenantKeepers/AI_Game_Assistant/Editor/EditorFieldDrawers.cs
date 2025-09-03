using UnityEngine;
using UnityEditor;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor.UI
{
    /// <summary>
    /// Centralized field drawing logic for all Masterlists in the AI RPG Assistant.
    /// Keeps UI code out of AssistantWindow.cs for maintainability.
    /// </summary>
    public static class EditorFieldDrawers
    {
        // -------------------------
        // Characters
        // -------------------------
        public static void DrawCharacterList()
        {
            EditorGUILayout.LabelField("Character Masterlist", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Character list UI goes here.", MessageType.Info);
            // TODO: Implement actual drawing logic from CharacterDatabase
        }

        // -------------------------
        // Items
        // -------------------------
        public static void DrawItemList()
        {
            EditorGUILayout.LabelField("Item Masterlist", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Item list UI goes here.", MessageType.Info);
            // TODO: Implement actual drawing logic from ItemDatabase
        }

        // -------------------------
        // Abilities
        // -------------------------
        public static void DrawAbilityList()
        {
            EditorGUILayout.LabelField("Ability Masterlist", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Ability list UI goes here.", MessageType.Info);
            // TODO: Implement actual drawing logic from AbilityDatabase
        }

        // -------------------------
        // Locations
        // -------------------------
        public static void DrawLocationList()
        {
            EditorGUILayout.LabelField("Location Masterlist", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Location list UI goes here.", MessageType.Info);
            // TODO: Implement actual drawing logic from LocationDatabase
        }

        // -------------------------
        // Quests
        // -------------------------
        public static void DrawQuestList()
        {
            EditorGUILayout.LabelField("Quest Masterlist", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Quest list UI goes here.", MessageType.Info);
            // TODO: Implement actual drawing logic from QuestDatabase
        }
    }
}
