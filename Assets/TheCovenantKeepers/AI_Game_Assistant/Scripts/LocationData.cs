using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    [CreateAssetMenu(menuName = "TheCovenantKeepers/AI/Data/Location")]
    public class LocationData : ScriptableObject
    {
        [Header("Location")]
        public string Name;
        public string Region;
        public string Type;             // City, Sanctuary, Dungeon, etc.
        public string FactionControl;   // Covenant, Watchers, Neutral…

        public int DangerLevel;         // 1..10 or similar
        [TextArea] public string Lore;

        [Header("Asset Path")]
        public string PrefabPath;

        public override string ToString() => string.IsNullOrEmpty(Name) ? "(Unnamed Location)" : Name;
    }
}
