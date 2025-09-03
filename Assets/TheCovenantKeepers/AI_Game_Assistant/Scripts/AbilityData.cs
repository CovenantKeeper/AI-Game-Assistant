using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    [CreateAssetMenu(menuName = "TheCovenantKeepers/AI/Data/Ability")]
    public class AbilityData : ScriptableObject
    {
        [Header("Identity")]
        public string AbilityID;
        public string AbilityName;
        [TextArea] public string Description;

        [Header("Type / Targeting")]
        public string AbilityType;
        public string TargetType;     // e.g., Single, Cone, Line, AoE
        public string Range;

        [Header("Costs / Timing")]
        public int ManaCost;
        public float CooldownSeconds;
        public float CastTimeSeconds;

        [Header("Effects")]
        public int DamageAmount;
        public string DamageType;     // Light, Fire, Shadow, etc.
        public int HealingAmount;
        public string BuffDebuffEffect;
        public float AreaOfEffectRadius;

        [Header("Assets & FX")]
        public string ProjectilePrefabPath;
        public string VFX_CastPath;
        public string VFX_HitPath;
        public string SFX_CastPath;
        public string SFX_HitPath;

        [Header("Animation / Progression")]
        public string AnimationTriggerCast;
        public string AnimationTriggerImpact;
        public int RequiredLevel;
        public string PrerequisiteAbilityID;

        [TextArea] public string Notes;

        public override string ToString() => string.IsNullOrEmpty(AbilityName) ? AbilityID : AbilityName;
    }
}
