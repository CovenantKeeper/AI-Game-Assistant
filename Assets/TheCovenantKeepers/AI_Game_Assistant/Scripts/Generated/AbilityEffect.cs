using UnityEngine;

namespace TheCovenantKeepers.AI_Game_Assistant
{
    public enum EffectSpawnSpace { World, Local }

    /// <summary>
    /// ScriptableObject describing simple ability VFX/SFX payloads.
    /// Use AbilityEffectSpawner to play them or call SpawnCast/SpawnHit directly.
    /// </summary>
    public class AbilityEffect : ScriptableObject
    {
        [Header("Cast Phase (optional)")]
        public GameObject castVfx;
        public AudioClip castSfx;

        [Header("Impact Phase (optional)")]
        public GameObject hitVfx;
        public AudioClip hitSfx;

        [Header("Placement")]
        public Vector3 positionOffset;
        public Vector3 rotationOffsetEuler;
        public bool parentToSpawn = false;
        public EffectSpawnSpace spawnSpace = EffectSpawnSpace.World;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        public void SpawnCast(Transform spawn)
        {
            SpawnInternal(castVfx, castSfx, spawn);
        }

        public void SpawnHit(Transform spawn)
        {
            SpawnInternal(hitVfx, hitSfx, spawn);
        }

        private void SpawnInternal(GameObject vfx, AudioClip sfx, Transform spawn)
        {
            if (vfx != null)
            {
                var rot = (spawn != null ? spawn.rotation : Quaternion.identity) * Quaternion.Euler(rotationOffsetEuler);
                var pos = (spawn != null ? spawn.position : Vector3.zero);
                if (spawnSpace == EffectSpawnSpace.Local && spawn != null) pos = spawn.TransformPoint(positionOffset); else pos += positionOffset;
                var go = Object.Instantiate(vfx, pos, rot);
                if (parentToSpawn && spawn != null) go.transform.SetParent(spawn, true);
            }
            if (sfx != null)
            {
                var pos = spawn != null ? spawn.position : Vector3.zero;
                AudioSource.PlayClipAtPoint(sfx, pos, sfxVolume);
            }
        }
    }
}
