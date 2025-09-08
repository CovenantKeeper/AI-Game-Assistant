using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    // Runtime component that tags a prefab with its source data.
    public class CharacterMetadata : MonoBehaviour
    {
        // Optional: source ScriptableObject (e.g., CharacterData)
        public ScriptableObject sourceData;
        // Optional: GUID string for the asset for resilience across moves
        public string sourceGuid;

        public string prefabKind; // e.g. Player, Enemy, NPC
    }
}
