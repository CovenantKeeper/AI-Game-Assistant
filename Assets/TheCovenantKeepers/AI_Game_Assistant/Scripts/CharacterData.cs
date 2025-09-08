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
        public string SubClass; // e.g., Tank, Support, Assassin
        public string Faction;
        public string Element;
        public string Gender;
        public string ResourceType; // e.g., Mana, Energy, Rage

        [Header("Primary Stats (CSV uses Health/Mana/Attack/Defense/Magic/Speed)")]
        [SerializeField] private int health;
        [SerializeField] private int mana;
        [SerializeField] private int attack;
        [SerializeField] private int defense;
        [SerializeField] private int magic;
        [SerializeField] private float speed;

        [Header("Attributes")]
        public int Strength;
        public int Agility;
        public int Intelligence;

        [Header("Combat")]
        public int Armor;           // physical resistance
        public int MagicResist;     // magical resistance
        public float AttackSpeed;   // attacks per second
        public float MoveSpeed;     // units per second
        public float Range;         // meters/units
        public float CritChance;    // 0..1
        public float CritDamageMultiplier = 2f; // e.g., 2.0 = 200%
        public float ArmorPenetration; // flat/ratio simplified
        public float MagicPenetration; // flat/ratio simplified
        public float LifeSteal;     // 0..1
        public float SpellVamp;     // 0..1
        public float CooldownReduction; // 0..1
        public float Tenacity;      // 0..1 crowd control reduction

        // Optional legacy/UI fields
        public int Level;

        [Header("Abilities & Lore")]
        public string PassiveName;
        [TextArea] public string PassiveDescription;
        public string Ability1Name; [TextArea] public string Ability1Description;
        public string Ability2Name; [TextArea] public string Ability2Description;
        public string Ability3Name; [TextArea] public string Ability3Description;

        // Per-ability gameplay fields
        [Header("A1 Stats")] public int Ability1Cost; public float Ability1Cooldown; public float Ability1Range; public string Ability1Target;
        [Header("A2 Stats")] public int Ability2Cost; public float Ability2Cooldown; public float Ability2Range; public string Ability2Target;
        [Header("A3 Stats")] public int Ability3Cost; public float Ability3Cooldown; public float Ability3Range; public string Ability3Target;

        public string UltimateAbility;
        [TextArea] public string UltimateDescription;
        [TextArea] public string LoreBackground;

        [Header("Assets")]
        public string ModelPath;
        public RuntimeAnimatorController AnimatorController; // optional per-character controller

        [Header("Animation Clips (optional)")]
        public AnimationClip IdleClip;
        public AnimationClip WalkClip;
        public AnimationClip RunClip;
        [Space]
        public AnimationClip Ability1Clip;
        public AnimationClip Ability2Clip;
        public AnimationClip Ability3Clip;
        public AnimationClip UltimateClip;

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

    // Distinct asset types that reuse CharacterData schema
    [CreateAssetMenu(menuName = "TheCovenantKeepers/AI/Data/Beast")]
    public class BeastData : CharacterData { }

    [CreateAssetMenu(menuName = "TheCovenantKeepers/AI/Data/Spirit")]
    public class SpiritData : CharacterData { }
}
