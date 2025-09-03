using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace TheCovenantKeepers.AI_Game_Assistant.Editor.UI
{
    public enum MasterlistType
    {
        Character,
        Item,
        Ability,
        Quest,
        Location
    }

    public class AssistantWindow : EditorWindow
    {
        [MenuItem("The Covenant Keepers/AI Game Assistant")]
        public static void ShowWindow()
        {
            GetWindow<AssistantWindow>("AI Game Assistant");
        }

        private void CreateGUI()
        {
            // Load the UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/TheCovenantKeepers/AI_Game_Assistant/Editor/UI/AssistantWindow.uxml");
            if (visualTree != null)
            {
                visualTree.CloneTree(rootVisualElement);
                InitializeFields();
            }
            else
            {
                rootVisualElement.Add(new Label("Could not find AssistantWindow.uxml"));
            }
        }

        private void InitializeFields()
        {
            var masterlistTypeField = rootVisualElement.Q<EnumField>("masterlist-type-field");
            if (masterlistTypeField != null)
            {
                masterlistTypeField.Init(MasterlistType.Character);
            }
        }
    }
}
