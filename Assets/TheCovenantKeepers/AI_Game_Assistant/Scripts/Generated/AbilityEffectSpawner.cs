using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    /// <summary>
    /// Spawns ability effects (cast/hit) at a configured Transform (e.g., hand/weapon tip).
    /// Can be driven by AnimatorTestDriver or gameplay scripts.
    /// </summary>
    public class AbilityEffectSpawner : MonoBehaviour
    {
        [Header("Spawn Point")]
        public Transform spawn;

        [Header("Ability Effects")]
        public AbilityEffect A1;
        public AbilityEffect A2;
        public AbilityEffect A3;
        public AbilityEffect Ult;

        public void PlayA1Cast() => PlayCast(A1);
        public void PlayA2Cast() => PlayCast(A2);
        public void PlayA3Cast() => PlayCast(A3);
        public void PlayUltCast() => PlayCast(Ult);

        public void PlayA1Hit() => PlayHit(A1);
        public void PlayA2Hit() => PlayHit(A2);
        public void PlayA3Hit() => PlayHit(A3);
        public void PlayUltHit() => PlayHit(Ult);

        public void PlayCast(AbilityEffect fx)
        {
            if (fx == null) return; fx.SpawnCast(spawn);
        }
        public void PlayHit(AbilityEffect fx)
        {
            if (fx == null) return; fx.SpawnHit(spawn);
        }
    }
}
