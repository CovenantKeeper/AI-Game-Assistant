using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    [CreateAssetMenu(fileName = "CHR_", menuName = "AI Assistant/RPG Character")]
    public class RpgCharacterSO : ScriptableObject
    {
        [Header("Identity")]
        public string characterName;
        public string @class;
        public string faction;
        // === Extra identity/meta used by AssistantWindow ===
        public string type;          // e.g., "Archangel", "Beast", etc.
        public string role;          // e.g., "Tank", "Support"
        public string affiliation;   // e.g., "Heavenly Host"
        public string element;       // e.g., "Light", "Air"
        public string gender;        // e.g., "Male", "Female", "Unknown"
        public string subClass;      // optional subclass
        public string resourceType;  // e.g., "Mana", "Grace"
        public string weaponType;    // e.g., "Sword", "Bow"

        [Header("Model & Controller")]
        public string modelPath;                         // keep for backwards compatibility
        public GameObject modelPrefab;                   // preferred going forward
        public RuntimeAnimatorController controller;     // runtime-safe controller reference
        public ScriptableObject animatorPreset;          // your AnimatorPreset ScriptableObject

        [Header("Core Stats")]
        public int health;
        public int mana;
        public int attack;
        public int defense;
        public int magic;
        public int speed;
        // ---- Attributes ----
        public int strength;             // NEW
        public int agility;              // NEW
        public int intelligence;         // NEW

        // ---- Combat ----
        public int armor;                // NEW
        public int magicResist;          // NEW
        public float attackSpeed;        // NEW
        public float moveSpeed;          // already have a "Speed" core stat; keep both if you like
        public float range;              // NEW
        public float critChance;         // NEW  (0–100 or 0–1 — you choose; stay consistent)
        public float critDamageMultiplier = 2f; // NEW (default 2x)
        public float armorPenetration;   // NEW
        public float magicPenetration;   // NEW
        public float lifeSteal;          // NEW
        public float spellVamp;          // NEW
        public float cooldownReduction;  // NEW
        public float tenacity;           // NEW

        // ---- Meta ----
        public int level;                // NEW

        [Header("Lore")]
        [TextArea] public string loreBackground;
        public string ultimateAbility;

        [Header("Clips (Optional, used for preset/controller generation)")]
        public AnimationClip Idle;
        public AnimationClip Walk;
        public AnimationClip Run;
        public AnimationClip Jump;
        public AnimationClip Fall;
        public AnimationClip Land;
        public AnimationClip LightAttack;
        public AnimationClip HeavyAttack;
        public AnimationClip Block;
        public AnimationClip Dodge;
        public AnimationClip HitReact;
        public AnimationClip Death;
        public AnimationClip Ability1;
        public AnimationClip Ability2;
        public AnimationClip Ability3;
        public AnimationClip Ultimate;

        // === Passive + abilities text (for UI/prompt reflection) ===
        public string passiveName;
        [TextArea] public string passiveDescription;

        public string ability1Name;
        [TextArea] public string ability1Description;

        public string ability2Name;
        [TextArea] public string ability2Description;

        public string ability3Name;
        [TextArea] public string ability3Description;

        [TextArea] public string ultimateDescription;

        // Put this in AssistantWindow (anywhere in the class)
    }
}
