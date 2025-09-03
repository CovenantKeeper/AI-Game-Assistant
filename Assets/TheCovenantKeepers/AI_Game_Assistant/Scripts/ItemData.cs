using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    [CreateAssetMenu(menuName = "TheCovenantKeepers/AI/Data/Item")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        public string ItemID;
        public string ItemName;
        public string ItemType;
        public string SubType;

        [TextArea] public string Description;

        [Header("Economy & Weight")]
        public int ValueBuy;
        public int ValueSell;
        public float Weight;

        [Header("Equip / Use")]
        public bool IsUsable;
        public bool IsEquippable;
        public string EquipmentSlot;

        [Header("Stat Modifiers")]
        public string StatModifier1_Type;
        public string StatModifier1_Value;
        public string StatModifier2_Type;
        public string StatModifier2_Value;

        [Header("Gameplay")]
        public string UseEffect;
        public int RequiredLevel;
        [TextArea] public string CraftingMaterials;
        [TextArea] public string Notes;

        [Header("Asset Path")]
        public string PrefabPath;

        // Optional extension (not in header, but safe)
        public int MaxStack = 1;

        public override string ToString() => string.IsNullOrEmpty(ItemName) ? ItemID : $"{ItemName} ({ItemType})";
    }
}
