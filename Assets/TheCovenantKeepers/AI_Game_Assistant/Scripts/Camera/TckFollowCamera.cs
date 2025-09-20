using UnityEngine;
#if HAS_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TheCovenantKeepers.AI_Game_Assistant
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class TckFollowCamera : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;
        [Tooltip("Offset from target pivot (usually head/chest height)."), SerializeField]
        public Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);

        [Header("Distance/Zoom")]
        [Range(0.5f, 30f)] public float distance = 6f;
        [Range(0.3f, 30f)] public float minDistance = 2f;
        [Range(0.5f, 60f)] public float maxDistance = 12f;
        [Range(0.05f, 2f)] public float zoomSensitivity = 0.5f;

        [Header("Orbit Controls")]
        public bool requireRightMouseToOrbit = true;
        [Range(30f, 540f)] public float yawSpeed = 180f;
        [Range(30f, 540f)] public float pitchSpeed = 120f;
        [Range(-89f, 0f)] public float minPitch = -40f;
        [Range(0f, 89f)] public float maxPitch = 70f;
        public bool invertY = false;

        [Header("Smoothing")]
        [Range(0f, 30f)] public float positionDamping = 10f;
        [Range(0f, 30f)] public float rotationDamping = 12f;

        [Header("Collision")]
        public LayerMask collisionMask = ~0; // Everything by default
        [Range(0.01f, 0.5f)] public float collisionBuffer = 0.2f;
        public bool ignoreTargetColliders = true;

        // State
        private float yaw;
        private float pitch = 10f;
        private Vector3 camVelocity;

        private float _nextTargetSearchTime;

        private void Reset()
        {
            var cam = GetComponent<Camera>();
            if (cam != null && cam.tag != "MainCamera") cam.tag = "MainCamera";
            var listener = GetComponent<AudioListener>();
            if (listener == null) gameObject.AddComponent<AudioListener>();
        }

        private void Awake()
        {
            if (target == null)
            {
                FindTargetAutomatically();
                SnapToTargetImmediate();
            }
            // Initialize yaw/pitch from current rotation
            var e = transform.eulerAngles;
            yaw = e.y;
            pitch = NormalizePitch(e.x);
        }

        private void Update()
        {
            if (target == null)
            {
                if (Time.time >= _nextTargetSearchTime)
                {
                    FindTargetAutomatically();
                    _nextTargetSearchTime = Time.time + 2f;
                }
            }

            HandleInput();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            FollowTarget(Time.deltaTime);
        }

        private void HandleInput()
        {
            // Zoom
            float scroll = ReadScroll();
            if (Mathf.Abs(scroll) > Mathf.Epsilon)
            {
                distance -= scroll * (maxDistance - minDistance) * zoomSensitivity;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }

            bool orbiting = !requireRightMouseToOrbit || IsOrbitHeld();

            if (orbiting)
            {
                ReadMouseDelta(out float mx, out float my);
                yaw += mx * yawSpeed * Time.unscaledDeltaTime;
                pitch += (invertY ? my : -my) * pitchSpeed * Time.unscaledDeltaTime;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }
        }

        private void FollowTarget(float dt)
        {
            var focus = target.position + targetOffset;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var desiredPos = focus - rot * Vector3.forward * distance;

            // Collision avoid (ignore target's own colliders if requested)
            if (ignoreTargetColliders)
            {
                desiredPos = ResolveCameraPositionWithIgnore(focus, desiredPos);
            }
            else
            {
                if (Physics.Linecast(focus, desiredPos, out var hit, collisionMask, QueryTriggerInteraction.Ignore))
                {
                    desiredPos = hit.point + hit.normal * collisionBuffer;
                }
            }

            // Smooth position
            if (positionDamping > 0f)
                transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref camVelocity, 1f / Mathf.Max(0.0001f, positionDamping));
            else
                transform.position = desiredPos;

            // Smooth rotation to look at focus
            var lookRot = Quaternion.LookRotation((focus - transform.position).normalized, Vector3.up);
            if (rotationDamping > 0f)
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 1f - Mathf.Exp(-rotationDamping * dt));
            else
                transform.rotation = lookRot;
        }

        private Vector3 ResolveCameraPositionWithIgnore(Vector3 focus, Vector3 desiredPos)
        {
            var dir = desiredPos - focus;
            var dist = dir.magnitude;
            if (dist <= 1e-4f) return desiredPos;
            dir /= dist;

            var hits = Physics.RaycastAll(focus, dir, dist, collisionMask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return desiredPos;

            // Identify colliders belonging to the target root
            Transform root = target;
            while (root.parent != null) root = root.parent;

            float maxSafeDist = dist;
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                var hitRoot = h.collider.transform;
                while (hitRoot.parent != null) hitRoot = hitRoot.parent;
                if (hitRoot == root) continue; // ignore self
                maxSafeDist = Mathf.Min(maxSafeDist, h.distance - collisionBuffer);
            }

            maxSafeDist = Mathf.Max(0.0f, maxSafeDist);
            return focus + dir * maxSafeDist;
        }

        private void FindTargetAutomatically()
        {
            // 1) Try a tagged Player
            var tagObj = GameObject.FindGameObjectWithTag("Player");
            if (tagObj != null) { target = tagObj.transform; return; }

            // 2) Try objects that have a component named "CharacterMetadata" with prefabKind == "Player"
            var allRoots = GameObject.FindObjectsOfType<GameObject>();
            foreach (var go in allRoots)
            {
                if (go == null) continue;
                var comp = go.GetComponent("CharacterMetadata");
                if (comp != null)
                {
                    var type = comp.GetType();
                    var field = type.GetField("prefabKind");
                    if (field != null)
                    {
                        var val = field.GetValue(comp) as string;
                        if (!string.IsNullOrEmpty(val) && val.ToLowerInvariant() == "player")
                        {
                            target = go.transform;
                            return;
                        }
                    }
                }
            }

            // 3) Fallback: any CharacterController
            var cc = FindObjectOfType<CharacterController>();
            if (cc != null) { target = cc.transform; return; }
        }

        private void SnapToTargetImmediate()
        {
            if (target == null) return;
            var focus = target.position + targetOffset;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var desiredPos = focus - rot * Vector3.forward * distance;
            transform.position = desiredPos;
            transform.rotation = Quaternion.LookRotation((focus - transform.position).normalized, Vector3.up);
        }

        private static float NormalizePitch(float xRot)
        {
            // Map Euler X to [-180..180]
            float xp = xRot;
            if (xp > 180f) xp -= 360f;
            return Mathf.Clamp(xp, -89f, 89f);
        }

        // Input abstraction supporting both Old and New Input Systems
        private float ReadScroll()
        {
#if HAS_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                // Mouse scroll is typically large; scale down
                var v = Mouse.current.scroll.ReadValue().y;
                return v * 0.1f;
            }
            return 0f;
#else
            return Input.GetAxis("Mouse ScrollWheel");
#endif
        }

        private bool IsOrbitHeld()
        {
#if HAS_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
            return Input.GetMouseButton(1);
#endif
        }

        private void ReadMouseDelta(out float mx, out float my)
        {
#if HAS_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                var d = Mouse.current.delta.ReadValue();
                const float scale = 0.02f; // convert pixel delta to axis-like value
                mx = d.x * scale;
                my = d.y * scale;
                return;
            }
            mx = my = 0f;
#else
            mx = Input.GetAxis("Mouse X");
            my = Input.GetAxis("Mouse Y");
#endif
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (target == null) return;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(target.position + targetOffset, 0.1f);
            Gizmos.DrawLine(transform.position, target.position + targetOffset);
        }
#endif
    }
}
