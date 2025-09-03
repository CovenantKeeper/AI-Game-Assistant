namespace TheCovenantKeepers.AI_Game_Assistant
{
using UnityEngine;

namespace ChatGPTAssistant.Data
{
    [CreateAssetMenu(fileName = "NewScriptTemplate", menuName = "AI RPG Assistant/Script Template")]
    public class ScriptTemplate : ScriptableObject
    {
        [Header("Template Metadata")]
        public string templateName = "Default Template";
        [TextArea(2, 5)] public string description;

        [Header("Script Body")]
        [Tooltip("The raw C# script template with optional {{keywords}} for replacement.")]
        [TextArea(10, 30)]
        public string scriptContent;

        [Header("Editor Metadata")]
        public string defaultOutputPath = "Assets/Scripts/Generated";
        public string fileNamePrefix = "MyGenerated";
        public string fileNameSuffix = "";

        [Header("Options")]
        public bool allowOverwrite = false;
    }
}

}
