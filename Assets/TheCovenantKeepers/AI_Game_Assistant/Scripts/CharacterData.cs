using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    [CreateAssetMenu(menuName = "TheCovenantKeepers/AI/Data/Character")]
    public class CharacterData : ScriptableObject
    {
        [Header("Identity")]
        public Object Icon;
        public string Name;
        public string Type;
        public string Role;
        public string Affiliation;
        public string Class;
        public string Faction;
        public string Element;
        public string Gender;

        [Header("Stats (CSV uses Health/Mana/Attack/Defense/Magic/Speed)")]
        [SerializeField] private int health;
        [SerializeField] private int mana;
        [SerializeField] private int attack;
        [SerializeField] private int defense;
        [SerializeField] private int magic;
        [SerializeField] private float speed;

        // Optional legacy/UI fields (not in current CSV, safe to keep)
        public int Level;

        [Header("Extras")]
        public string UltimateAbility;
        [TextArea] public string LoreBackground;
        public string ModelPath;

        // --- CSV direct names ---
        public int Health { get => health; set => health = value; }
        public int Mana { get => mana; set => mana = value; }
        public int Attack { get => attack; set => attack = value; }
        public int Defense { get => defense; set => defense = value; }
        public int Magic { get => magic; set => magic = value; }
        public float Speed { get => speed; set => speed = value; }

        // --- Synonyms for existing editor UI bindings ---
        public int MaxHealth { get => health; set => health = value; }
        public int MaxMana { get => mana; set => mana = value; }
        public int BaseAttack { get => attack; set => attack = value; }
        public int BaseDefense { get => defense; set => defense = value; }

        public override string ToString() => string.IsNullOrEmpty(Name) ? "(Unnamed Character)" : Name;
    }
}
