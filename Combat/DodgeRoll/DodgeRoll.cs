using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Combat
{
    /// <summary>
    /// Dodge and roll system with invincibility frames.
    /// </summary>
    public class DodgeRoll : MonoBehaviour
    {
        [Header("Dodge Settings")]
        [SerializeField] private float dodgeDistance = 5f;
        [SerializeField] private float dodgeDuration = 0.4f;
        [SerializeField] private float dodgeCooldown = 0.8f;
        [SerializeField] private AnimationCurve dodgeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("I-Frames")]
        [SerializeField] private bool hasIFrames = true;
        [SerializeField] private float iFrameStart = 0.05f;
        [SerializeField] private float iFrameDuration = 0.3f;

        [Header("Stamina")]
        [SerializeField] private bool useStamina = true;
        [SerializeField] private float staminaCost = 20f;

        [Header("Input")]
        [SerializeField] private KeyCode dodgeKey = KeyCode.Space;

        [Header("Layer Settings")]
        [SerializeField] private LayerMask obstacleLayer;
        [SerializeField] private float obstacleCheckRadius = 0.5f;

        [Header("Events")]
        [SerializeField] private UnityEvent onDodgeStart;
        [SerializeField] private UnityEvent onDodgeEnd;
        [SerializeField] private UnityEvent onIFrameStart;
        [SerializeField] private UnityEvent onIFrameEnd;
        [SerializeField] private UnityEvent onDodgeFailed;

        // State
        private bool isDodging;
        private bool isInvincible;
        private float dodgeStartTime;
        private float lastDodgeTime = -999f;
        private Vector3 dodgeDirection;
        private Vector3 dodgeStartPosition;

        // References
        private CharacterController characterController;
        private Rigidbody rb;
        private Collider mainCollider;

        // External stamina reference
        private Func<float> getStamina;
        private Action<float> useStaminaAction;

        // Properties
        public bool IsDodging => isDodging;
        public bool IsInvincible => isInvincible;
        public bool CanDodge => !isDodging && Time.time - lastDodgeTime >= dodgeCooldown && HasEnoughStamina();
        public float CooldownRemaining => Mathf.Max(0, dodgeCooldown - (Time.time - lastDodgeTime));
        public float CooldownPercent => Mathf.Clamp01((Time.time - lastDodgeTime) / dodgeCooldown);

        // Events
        public event Action DodgeStarted;
        public event Action DodgeEnded;
        public event Action IFrameStarted;
        public event Action IFrameEnded;
        public event Action DodgeFailed;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            rb = GetComponent<Rigidbody>();
            mainCollider = GetComponent<Collider>();
        }

        private void Update()
        {
            HandleInput();
            UpdateDodge();
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(dodgeKey))
            {
                TryDodge(GetInputDirection());
            }
        }

        private Vector3 GetInputDirection()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector3 direction = new Vector3(horizontal, 0, vertical).normalized;

            // If no input, dodge backward
            if (direction.magnitude < 0.1f)
            {
                direction = -transform.forward;
            }
            else
            {
                // Convert to world space based on camera
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 camForward = cam.transform.forward;
                    camForward.y = 0;
                    camForward.Normalize();

                    Vector3 camRight = cam.transform.right;
                    camRight.y = 0;
                    camRight.Normalize();

                    direction = camForward * vertical + camRight * horizontal;
                    direction.Normalize();
                }
            }

            return direction;
        }

        public bool TryDodge(Vector3 direction)
        {
            if (!CanDodge)
            {
                DodgeFailed?.Invoke();
                onDodgeFailed?.Invoke();
                return false;
            }

            if (direction.magnitude < 0.1f)
            {
                direction = -transform.forward;
            }

            // Check for obstacles
            if (!CanDodgeInDirection(direction))
            {
                DodgeFailed?.Invoke();
                onDodgeFailed?.Invoke();
                return false;
            }

            StartDodge(direction.normalized);
            return true;
        }

        private bool CanDodgeInDirection(Vector3 direction)
        {
            return !Physics.SphereCast(
                transform.position + Vector3.up * obstacleCheckRadius,
                obstacleCheckRadius,
                direction,
                out _,
                dodgeDistance,
                obstacleLayer
            );
        }

        private void StartDodge(Vector3 direction)
        {
            isDodging = true;
            dodgeStartTime = Time.time;
            lastDodgeTime = Time.time;
            dodgeDirection = direction;
            dodgeStartPosition = transform.position;

            // Consume stamina
            if (useStamina)
            {
                useStaminaAction?.Invoke(staminaCost);
            }

            // Start I-frames
            if (hasIFrames)
            {
                Invoke(nameof(StartIFrames), iFrameStart);
            }

            DodgeStarted?.Invoke();
            onDodgeStart?.Invoke();

            Debug.Log($"[Dodge] Started dodge towards {direction}");
        }

        private void UpdateDodge()
        {
            if (!isDodging) return;

            float elapsed = Time.time - dodgeStartTime;
            float normalizedTime = elapsed / dodgeDuration;

            if (normalizedTime >= 1f)
            {
                EndDodge();
                return;
            }

            // Calculate movement
            float curveValue = dodgeCurve.Evaluate(normalizedTime);
            Vector3 targetPosition = dodgeStartPosition + dodgeDirection * dodgeDistance * curveValue;

            // Apply movement
            if (characterController != null)
            {
                Vector3 movement = targetPosition - transform.position;
                characterController.Move(movement);
            }
            else if (rb != null)
            {
                rb.MovePosition(targetPosition);
            }
            else
            {
                transform.position = targetPosition;
            }
        }

        private void StartIFrames()
        {
            if (!isDodging) return;

            isInvincible = true;

            // Disable collision temporarily
            if (mainCollider != null)
            {
                // Could change layer or disable collider
            }

            IFrameStarted?.Invoke();
            onIFrameStart?.Invoke();

            Invoke(nameof(EndIFrames), iFrameDuration);

            Debug.Log("[Dodge] I-frames started");
        }

        private void EndIFrames()
        {
            isInvincible = false;

            // Re-enable collision
            if (mainCollider != null)
            {
                // Restore layer or enable collider
            }

            IFrameEnded?.Invoke();
            onIFrameEnd?.Invoke();

            Debug.Log("[Dodge] I-frames ended");
        }

        private void EndDodge()
        {
            isDodging = false;

            // Make sure I-frames are ended
            if (isInvincible)
            {
                EndIFrames();
            }

            DodgeEnded?.Invoke();
            onDodgeEnd?.Invoke();

            Debug.Log("[Dodge] Dodge completed");
        }

        public void CancelDodge()
        {
            if (!isDodging) return;

            CancelInvoke(nameof(StartIFrames));
            CancelInvoke(nameof(EndIFrames));

            if (isInvincible)
            {
                EndIFrames();
            }

            isDodging = false;
            DodgeEnded?.Invoke();
            onDodgeEnd?.Invoke();
        }

        private bool HasEnoughStamina()
        {
            if (!useStamina) return true;
            if (getStamina == null) return true;
            return getStamina() >= staminaCost;
        }

        /// <summary>
        /// Set external stamina system reference.
        /// </summary>
        public void SetStaminaSystem(Func<float> getter, Action<float> consumer)
        {
            getStamina = getter;
            useStaminaAction = consumer;
        }

        /// <summary>
        /// Check if a position would be hit during dodge (for external damage systems).
        /// </summary>
        public bool WouldDodgeHit(Vector3 attackPosition, float attackRadius)
        {
            if (!isInvincible) return true;
            return false; // Invincible during I-frames
        }

        // Configuration methods
        public void SetDodgeDistance(float distance) => dodgeDistance = distance;
        public void SetDodgeDuration(float duration) => dodgeDuration = duration;
        public void SetDodgeCooldown(float cooldown) => dodgeCooldown = cooldown;
        public void SetIFrameDuration(float duration) => iFrameDuration = duration;
        public void SetStaminaCost(float cost) => staminaCost = cost;
    }
}
