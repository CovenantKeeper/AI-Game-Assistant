using System.Collections;
using UnityEngine;
#if UNITY_2021_2_OR_NEWER
using UnityEngine.VFX;
#endif

namespace TheCovenantKeepers.AI_Game_Assistant
{
    /// <summary>
    /// Simple utility to auto-destroy spawned VFX/SFX containers.
    /// If lifetime <= 0, attempts to infer it from contained ParticleSystems.
    /// </summary>
    public class EffectAutoDestroy : MonoBehaviour
    {
        [Tooltip("Seconds before destroying. If <= 0, will try to infer from ParticleSystems.")]
        public float lifetime = 0f;

        [Tooltip("Extra delay added on top of computed/explicit lifetime.")]
        public float extraDelay = 0.25f;

        [Tooltip("If true and ParticleSystems are found, waits until all have finished emitting before destroy.")]
        public bool waitForParticles = true;

        [Tooltip("Also destroy this object's parent if it becomes empty afterwards.")]
        public bool destroyParentIfEmpty = false;

        private Coroutine _routine;

        private void OnEnable()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(DestroyRoutine());
        }

        private IEnumerator DestroyRoutine()
        {
            float ttl = lifetime;
            if (ttl <= 0f)
            {
                // Try infer from ParticleSystems
                var ps = GetComponentsInChildren<ParticleSystem>(true);
                if (ps != null && ps.Length > 0)
                {
                    float maxDur = 0f;
                    foreach (var p in ps)
                    {
                        if (p == null) continue;
                        var main = p.main;
                        float dl = main.duration;
                        float sl = 0f;
#if UNITY_2022_1_OR_NEWER
                        if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
                            sl = main.startLifetime.constant;
                        else if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
                            sl = main.startLifetime.constantMax;
#else
                        sl = main.startLifetime.constant; // fallback; not exact
#endif
                        float candidate = (main.loop ? 3f : dl + sl); // if looping, cap to 3s by default
                        if (candidate > maxDur) maxDur = candidate;
                    }
                    ttl = Mathf.Max(0.1f, maxDur);
                }
#if UNITY_2021_2_OR_NEWER
                else
                {
                    // VisualEffect has no standard lifetime; default 3s
                    var vfx = GetComponentInChildren<VisualEffect>(true);
                    if (vfx != null) ttl = 3f;
                }
#endif
                if (ttl <= 0f) ttl = 3f; // final fallback
            }

            ttl += Mathf.Max(0f, extraDelay);

            if (waitForParticles)
            {
                // Start emission and wait for completion (best effort)
                var ps = GetComponentsInChildren<ParticleSystem>(true);
                if (ps != null && ps.Length > 0)
                {
                    float t = 0f;
                    while (t < ttl)
                    {
                        bool anyAlive = false;
                        foreach (var p in ps)
                        {
                            if (p == null) continue;
                            if (p.IsAlive(true)) { anyAlive = true; break; }
                        }
                        if (!anyAlive) break;
                        t += Time.deltaTime;
                        yield return null;
                    }
                }
                else
                {
                    yield return new WaitForSeconds(ttl);
                }
            }
            else
            {
                yield return new WaitForSeconds(ttl);
            }

            var parent = transform.parent;
            Destroy(gameObject);
            if (destroyParentIfEmpty && parent != null && parent.childCount == 0)
                Destroy(parent.gameObject);
        }
    }
}
