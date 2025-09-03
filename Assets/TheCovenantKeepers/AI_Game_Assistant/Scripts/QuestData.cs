using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    [CreateAssetMenu(menuName = "TheCovenantKeepers/AI/Data/Quest")]
    public class QuestData : ScriptableObject
    {
        [Header("Quest")]
        public string Title;
        [TextArea] public string Objective;
        public string Type;         // Main, Side, Hunt, Escort, etc.
        public string Reward;
        public string Region;
        [TextArea] public string LoreHint;

        [Header("Asset Path")]
        public string PrefabPath;

        public override string ToString() => string.IsNullOrEmpty(Title) ? "(Untitled Quest)" : Title;
    }
}
