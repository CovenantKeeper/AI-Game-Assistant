using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor.UI
{
    public class AssistantWindow : EditorWindow
    {
        private EnumField masterListTypeField;
        private VisualElement contentArea;

        [MenuItem("The Covenant Keepers/AI Game Assistant")]
        public static void ShowWindow()
        {
            GetWindow<AssistantWindow>("AI Game Assistant");
        }

        private void CreateGUI()
        {
            // Load UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/TheCovenantKeepers/AI_Game_Assistant/Editor/UI/AssistantWindow.uxml");

            if (visualTree != null)
            {
                VisualElement root = rootVisualElement;
                visualTree.CloneTree(root);

                // Load USS
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Assets/TheCovenantKeepers/AI_Game_Assistant/Editor/UI/AssistantWindow.uss");
                if (styleSheet != null)
                {
                    root.styleSheets.Add(styleSheet);
                }

                // Get UI references
                masterListTypeField = root.Q<EnumField>("masterlist-type-field");
                contentArea = root.Q<VisualElement>("content-area");

                // Init dropdown
                masterListTypeField.Init(MasterlistType.Character);
                masterListTypeField.value = MasterlistType.Character;
                masterListTypeField.RegisterValueChangedCallback(evt =>
                {
                    ShowSection((MasterlistType)evt.newValue);
                });

                // Default section
                ShowSection(MasterlistType.Character);
            }
            else
            {
                rootVisualElement.Add(new Label("Failed to load AssistantWindow.uxml"));
            }
        }

        private void ShowSection(MasterlistType type)
        {
            contentArea.Clear();

            switch (type)
            {
                case MasterlistType.Character:
                    contentArea.Add(new Label("🧙 Character Masterlist Editor"));
                    break;
                case MasterlistType.Item:
                    contentArea.Add(new Label("🗡️ Item Masterlist Editor"));
                    break;
                case MasterlistType.Ability:
                    contentArea.Add(new Label("✨ Ability Masterlist Editor"));
                    break;
                case MasterlistType.Quest:
                    contentArea.Add(new Label("📜 Quest Masterlist Editor"));
                    break;
                case MasterlistType.Location:
                    contentArea.Add(new Label("🌍 Location Masterlist Editor"));
                    break;
            }
        }
    }

    public enum MasterlistType
    {
        Character,
        Item,
        Ability,
        Quest,
        Location
    }
}
