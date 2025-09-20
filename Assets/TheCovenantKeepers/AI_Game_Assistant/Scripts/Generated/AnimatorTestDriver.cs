using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TheCovenantKeepers.AI_Game_Assistant
{
    [RequireComponent(typeof(Transform))]
    [DisallowMultipleComponent]
    public class AnimatorTestDriver : MonoBehaviour
    {
        [Header("Movement Test")]
        public float moveSpeed = 3f;
        public float acceleration = 6f;
        public string speedParam = "Speed"; // matches auto controller

        [Header("Grounding/Jump")]
        public string isGroundedParam = "IsGrounded";
        public string velocityYParam = "VelocityY";
        public string jumpTrigger = "Jump";
        public float jumpHeight = 2.2f;
        public float gravity = -19.6f; // ~2x -9.81f for snappier feel

        [Header("Anti-Sink Stabilizer")]
        [Tooltip("Total stabilization window after abilities (locks vertical).")]
        public float groundStabilizeDuration = 0.35f;
        [Range(0.0f, 0.1f), Tooltip("Max downward step allowed while stabilizing when no baseline was captured.")]
        public float stabilizeMaxDownStep = 0.01f;
        [Tooltip("Extra clearance above the ground while locked.")]
        public float groundClearance = 0.02f;

        [Header("Combat Triggers/Flags")] public string dodgeTrigger = "Dodge";
        public string lightTrigger = "AttackLight";
        public string heavyTrigger = "AttackHeavy";
        public string blockBool = "Block";
        public string hitTrigger = "Hit";
        public string dieTrigger = "Die";

        [Header("Block Input")]
        [Tooltip("Allow holding RMB to Block. Disable if RMB is used for camera orbit to avoid getting stuck in Block state while orbiting.")]
        public bool allowRightMouseForBlock = false;

        [Header("Ability Triggers")] public string a1Trigger = "CastA1";
        public string a2Trigger = "CastA2";
        public string a3Trigger = "CastA3";
        public string ultTrigger = "CastUlt";

        [Header("Optional VFX (spawn on ability cast)")]
        public Transform vfxSpawn;
        public GameObject a1Vfx;
        public GameObject a2Vfx;
        public GameObject a3Vfx;
        public GameObject ultVfx;

        [Header("AbilityEffectSpawner (optional)")]
        [Tooltip("If true, will call AbilityEffectSpawner on the same GameObject when ability keys are pressed. Falls back to raw VFX fields above if spawner not present.")]
        public bool triggerSpawnerOnKeys = true;

        [Tooltip("Optional explicit Animator reference. If left empty, the driver will search in self and children.")]
        public Animator animatorOverride;

        private Animator _anim;
        private CharacterController _cc;
        private float _currentSpeed;
        private float _vy;
        private bool _dead;
        private AbilityEffectSpawner _spawner;
        private float _groundStabilizeTimer;

        private bool _hasBaseline;
        private float _baselineY;

        private void Awake()
        {
            _anim = animatorOverride != null ? animatorOverride : GetComponent<Animator>();
            if (_anim == null)
                _anim = GetComponentInChildren<Animator>(); // prefab builder often puts Animator on model child

            _cc = GetComponent<CharacterController>();
            _spawner = GetComponentInChildren<AbilityEffectSpawner>();

            // We drive motion via script; disable root motion to avoid animation displacing character vertically
            if (_anim != null) _anim.applyRootMotion = false;

            // Improve tiny-collision response to avoid gradual sinking on small deltas
            if (_cc != null) _cc.minMoveDistance = 0f;
        }

        private void Update()
        {
            if (_anim == null) return;

            // Die toggle to lock controls (K key)
            if (PressedDie()) { _anim.SetTrigger(dieTrigger); _dead = true; }
            if (_dead) { ApplyAnimatorStatics(); return; }

            // Block hold
            bool blocking = IsBlockHeld(allowRightMouseForBlock);
            _anim.SetBool(blockBool, blocking);

            // Read movement input and move
            Vector2 move = ReadMoveInput();
            float target = Mathf.Clamp01(move.magnitude);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, target, acceleration * Time.deltaTime);
            _anim.SetFloat(speedParam, _currentSpeed);

            // CharacterController simple motion with gravity/jump
            bool grounded = false;
            if (_cc != null)
            {
                grounded = _cc.isGrounded;

                // Stabilizer countdown
                if (_groundStabilizeTimer > 0f)
                    _groundStabilizeTimer -= Time.deltaTime;
                if (_groundStabilizeTimer <= 0f)
                    _hasBaseline = false; // release baseline when timer ends

                // Jump
                if (PressedJump() && grounded && _groundStabilizeTimer <= 0f)
                {
                    _vy = Mathf.Sqrt(Mathf.Max(0.01f, jumpHeight) * -2f * gravity);
                    _anim.SetTrigger(jumpTrigger);
                    grounded = false; // will be airborne this frame
                }

                // Horizontal movement
                Vector3 dir = new Vector3(move.x, 0, move.y);
                if (dir.sqrMagnitude > 1f) dir.Normalize();
                Vector3 delta = dir * (moveSpeed * Time.deltaTime);

                // Vertical movement: three modes
                if (_hasBaseline)
                {
                    // Enforce baseline: keep controller bottom >= baseline + clearance
                    float bottomY = ControllerBottomWorldY(_cc);
                    float targetBottom = _baselineY + Mathf.Max(0.0f, groundClearance);
                    float correction = targetBottom - bottomY;
                    if (correction > 0f)
                    {
                        delta.y += correction; // move up to maintain clearance
                        _vy = -2f; // keep slight downward bias only
                    }
                    else
                    {
                        // allow tiny downward movement but clamp
                        float downStep = -Mathf.Abs(_vy) * Time.deltaTime;
                        downStep = Mathf.Clamp(downStep, -stabilizeMaxDownStep, 0f);
                        delta.y += downStep;
                        _vy = -2f;
                    }
                }
                else if (grounded || _groundStabilizeTimer > 0f)
                {
                    // No baseline captured, but still stabilize: clamp down-step
                    float downStep = -Mathf.Abs(_vy) * Time.deltaTime;
                    downStep = Mathf.Clamp(downStep, -stabilizeMaxDownStep, 0f);
                    delta.y = downStep;
                    _vy = -2f;
                }
                else
                {
                    _vy += gravity * Time.deltaTime;
                    delta.y = _vy * Time.deltaTime;
                }

                // Move
                _cc.Move(delta);

                // Post-move ground snap for tiny overlaps
                if (_cc.isGrounded)
                {
                    GroundSnap(_cc, groundClearance);
                }

                if (dir != Vector3.zero)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 12f * Time.deltaTime);

                // Re-evaluate grounded after move for animator
                grounded = _cc.isGrounded;
            }
            else
            {
                // Fallback: move transform without grounding info
                Vector3 dir = new Vector3(move.x, 0, move.y).normalized;
                transform.position += dir * (moveSpeed * Time.deltaTime);
                grounded = true; // assume grounded
            }

            // Animator physics params
            _anim.SetBool(isGroundedParam, grounded);
            _anim.SetFloat(velocityYParam, _vy);

            // Combat inputs
            if (PressedDodge()) _anim.SetTrigger(dodgeTrigger);
            if (PressedLight()) _anim.SetTrigger(lightTrigger);
            if (PressedHeavy()) _anim.SetTrigger(heavyTrigger);
            if (PressedHit()) _anim.SetTrigger(hitTrigger);

            // Abilities (start stabilization and capture baseline)
            if (PressedA1()) { _anim.SetTrigger(a1Trigger); TriggerAbility(1); StartGroundLock(); }
            if (PressedA2()) { _anim.SetTrigger(a2Trigger); TriggerAbility(2); StartGroundLock(); }
            if (PressedA3()) { _anim.SetTrigger(a3Trigger); TriggerAbility(3); StartGroundLock(); }
            if (PressedUlt()) { _anim.SetTrigger(ultTrigger); TriggerAbility(4); StartGroundLock(); }

            ApplyAnimatorStatics();
        }

        private void StartGroundLock()
        {
            _groundStabilizeTimer = Mathf.Max(_groundStabilizeTimer, groundStabilizeDuration);
            _hasBaseline = false;
            if (_cc == null) return;

            // Raycast down from just above the controller bottom to capture baseline ground height
            float bottomY = ControllerBottomWorldY(_cc);
            Vector3 origin = new Vector3(transform.position.x, bottomY + 0.2f, transform.position.z);
            float radius = Mathf.Max(0.01f, _cc.radius - _cc.skinWidth);
            if (Physics.SphereCast(origin, radius, Vector3.down, out var hit, 1.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                _baselineY = hit.point.y;
                _hasBaseline = true;
            }
        }

        private static float ControllerBottomWorldY(CharacterController cc)
        {
            return cc.transform.position.y + cc.center.y - cc.height * 0.5f + cc.radius;
        }

        private static void GroundSnap(CharacterController cc, float clearance)
        {
            // Compute foot position
            float bottomY = cc.center.y - cc.height * 0.5f + cc.radius;
            Vector3 feet = cc.transform.position + new Vector3(0f, bottomY, 0f);
            Vector3 origin = feet + Vector3.up * 0.05f; // small lift to avoid starting inside ground

            // SphereCast a short distance to find ground
            float castDist = 0.25f;
            float radius = Mathf.Max(0.01f, cc.radius - cc.skinWidth);
            if (Physics.SphereCast(origin, radius, Vector3.down, out var hit, castDist, ~0, QueryTriggerInteraction.Ignore))
            {
                float desiredClearance = Mathf.Max(0.0f, clearance);
                float penetration = desiredClearance - hit.distance;
                if (penetration > 0f)
                {
                    cc.Move(Vector3.up * (penetration + 0.001f));
                }
            }
        }

        private void TriggerAbility(int index)
        {
            // Prefer spawner if present
            if (triggerSpawnerOnKeys && _spawner != null)
            {
                switch (index)
                {
                    case 1: _spawner.PlayCast(_spawner.A1); break;
                    case 2: _spawner.PlayCast(_spawner.A2); break;
                    case 3: _spawner.PlayCast(_spawner.A3); break;
                    case 4: _spawner.PlayCast(_spawner.Ult); break;
                }
                return;
            }

            // Fallback to raw VFX prefabs
            switch (index)
            {
                case 1: SpawnVfx(a1Vfx); break;
                case 2: SpawnVfx(a2Vfx); break;
                case 3: SpawnVfx(a3Vfx); break;
                case 4: SpawnVfx(ultVfx); break;
            }
        }

        private void ApplyAnimatorStatics()
        {
            // Reserved for future params (e.g., aim, etc.)
        }

        private void SpawnVfx(GameObject prefab)
        {
            if (prefab == null) return;
            var t = vfxSpawn != null ? vfxSpawn : transform;
            var go = Instantiate(prefab, t.position, t.rotation);
            Destroy(go, 6f);
        }

        private static Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 v = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v.y -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) v.x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) v.x -= 1f;
            }
            if (Gamepad.current != null)
                v += Gamepad.current.leftStick.ReadValue();
            return Vector2.ClampMagnitude(v, 1f);
#else
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            return new Vector2(h, v);
#endif
        }

        private static bool PressedJump()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                   (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        private static bool PressedDodge()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame) ||
                   (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.LeftShift);
#endif
        }

        private static bool PressedLight()
        {
#if ENABLE_INPUT_SYSTEM
            return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                   (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame);
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        private static bool PressedHeavy()
        {
#if ENABLE_INPUT_SYSTEM
            return (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
                   (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame);
#else
            return Input.GetMouseButtonDown(1);
#endif
        }

        private static bool IsBlockHeld(bool allowRmb)
        {
#if ENABLE_INPUT_SYSTEM
            bool rmb = allowRmb && Mouse.current != null && Mouse.current.rightButton.isPressed;
            bool ctrl = Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;
            bool lt = Gamepad.current != null && Gamepad.current.leftTrigger.isPressed;
            return rmb || ctrl || lt;
#else
            bool rmb = allowRmb && Input.GetMouseButton(1);
            return rmb || Input.GetKey(KeyCode.LeftControl);
#endif
        }

        private static bool PressedHit()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.H);
#endif
        }

        private static bool PressedDie()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.K);
#endif
        }

        private static bool PressedA1()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Alpha1);
#endif
        }
        private static bool PressedA2()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Alpha2);
#endif
        }
        private static bool PressedA3()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Alpha3);
#endif
        }
        private static bool PressedUlt()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.digit4Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Alpha4);
#endif
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            var r = new Rect(12, 12, 640, 132);
            GUILayout.BeginArea(r, GUI.skin.box);
            GUILayout.Label("Animator Test/Ability Driver\nMove: WASD/Arrows (or LS) | Jump: Space/A | Dodge: Shift/X | Light: LMB/RT | Heavy: RMB/RB | Block: Hold LCtrl/LT" + (allowRightMouseForBlock ? " or RMB" : "") + " | Hit: H | Die: K | Abilities: 1/2/3/4\nTip: RMB is reserved for camera orbit by default; enable 'Allow Right Mouse For Block' on the driver if you want RMB to Block.");
            GUILayout.EndArea();
        }
#endif
    }
}
