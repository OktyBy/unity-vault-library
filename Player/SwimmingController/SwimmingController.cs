using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Player
{
    /// <summary>
    /// Swimming controller with water physics and breath management.
    /// </summary>
    public class SwimmingController : MonoBehaviour
    {
        [Header("Swimming Movement")]
        [SerializeField] private float swimSpeed = 4f;
        [SerializeField] private float surfaceSwimSpeed = 5f;
        [SerializeField] private float verticalSwimSpeed = 3f;
        [SerializeField] private float acceleration = 5f;
        [SerializeField] private float drag = 2f;

        [Header("Diving")]
        [SerializeField] private KeyCode diveKey = KeyCode.LeftControl;
        [SerializeField] private KeyCode surfaceKey = KeyCode.Space;
        [SerializeField] private float diveSpeed = 4f;
        [SerializeField] private float buoyancy = 1f;

        [Header("Breath")]
        [SerializeField] private float maxBreath = 30f;
        [SerializeField] private float breathDrainRate = 1f;
        [SerializeField] private float breathRecoveryRate = 5f;
        [SerializeField] private float drowningDamage = 10f;
        [SerializeField] private float drowningInterval = 1f;

        [Header("Water Detection")]
        [SerializeField] private LayerMask waterLayer;
        [SerializeField] private float waterCheckRadius = 0.5f;
        [SerializeField] private float surfaceOffset = 0.5f;

        [Header("Effects")]
        [SerializeField] private float underwaterGravity = -1f;
        [SerializeField] private float surfaceBobAmount = 0.1f;
        [SerializeField] private float surfaceBobSpeed = 2f;

        [Header("Events")]
        [SerializeField] private UnityEvent onEnterWater;
        [SerializeField] private UnityEvent onExitWater;
        [SerializeField] private UnityEvent onStartDiving;
        [SerializeField] private UnityEvent onSurface;
        [SerializeField] private UnityEvent onBreathDepleted;
        [SerializeField] private UnityEvent<float> onBreathChanged;

        // State
        private bool isInWater;
        private bool isUnderwater;
        private bool isAtSurface;
        private float currentBreath;
        private float waterSurfaceY;
        private float lastDrowningTime;
        private Vector3 swimVelocity;

        // Components
        private CharacterController characterController;
        private Rigidbody rb;
        private Core.HealthSystem healthSystem;

        // Properties
        public bool IsInWater => isInWater;
        public bool IsUnderwater => isUnderwater;
        public bool IsAtSurface => isAtSurface;
        public float CurrentBreath => currentBreath;
        public float BreathPercent => currentBreath / maxBreath;
        public float WaterSurfaceY => waterSurfaceY;

        // Events
        public event Action EnteredWater;
        public event Action ExitedWater;
        public event Action StartedDiving;
        public event Action Surfaced;
        public event Action BreathDepleted;
        public event Action<float> BreathChanged;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            rb = GetComponent<Rigidbody>();
            healthSystem = GetComponent<Core.HealthSystem>();

            currentBreath = maxBreath;
        }

        private void Update()
        {
            CheckWaterState();

            if (isInWater)
            {
                HandleSwimmingInput();
                UpdateSwimming();
                UpdateBreath();
            }
        }

        private void CheckWaterState()
        {
            // Check if in water
            Collider[] waterColliders = Physics.OverlapSphere(transform.position, waterCheckRadius, waterLayer);
            bool wasInWater = isInWater;
            isInWater = waterColliders.Length > 0;

            if (isInWater && !wasInWater)
            {
                OnEnterWater(waterColliders[0]);
            }
            else if (!isInWater && wasInWater)
            {
                OnExitWater();
            }

            if (isInWater)
            {
                // Check if underwater or at surface
                bool wasUnderwater = isUnderwater;
                isUnderwater = transform.position.y + surfaceOffset < waterSurfaceY;
                isAtSurface = !isUnderwater && transform.position.y < waterSurfaceY + surfaceOffset;

                if (isUnderwater && !wasUnderwater)
                {
                    StartedDiving?.Invoke();
                    onStartDiving?.Invoke();
                }
                else if (!isUnderwater && wasUnderwater)
                {
                    Surfaced?.Invoke();
                    onSurface?.Invoke();
                }
            }
        }

        private void OnEnterWater(Collider waterCollider)
        {
            // Get water surface height
            waterSurfaceY = waterCollider.bounds.max.y;

            // Disable gravity
            if (rb != null)
            {
                rb.useGravity = false;
            }

            EnteredWater?.Invoke();
            onEnterWater?.Invoke();

            Debug.Log("[Swimming] Entered water");
        }

        private void OnExitWater()
        {
            isUnderwater = false;
            isAtSurface = false;
            swimVelocity = Vector3.zero;

            // Re-enable gravity
            if (rb != null)
            {
                rb.useGravity = true;
            }

            ExitedWater?.Invoke();
            onExitWater?.Invoke();

            Debug.Log("[Swimming] Exited water");
        }

        private void HandleSwimmingInput()
        {
            // Get input
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            // Calculate swim direction
            Vector3 inputDir = Vector3.zero;

            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 camForward = cam.transform.forward;
                Vector3 camRight = cam.transform.right;

                // For underwater, include vertical component
                if (isUnderwater)
                {
                    inputDir = camForward * vertical + camRight * horizontal;
                }
                else
                {
                    // Surface swimming - horizontal only
                    camForward.y = 0;
                    camForward.Normalize();
                    camRight.y = 0;
                    camRight.Normalize();

                    inputDir = camForward * vertical + camRight * horizontal;
                }
            }
            else
            {
                inputDir = transform.forward * vertical + transform.right * horizontal;
            }

            // Vertical movement
            float verticalInput = 0f;

            if (Input.GetKey(diveKey))
            {
                verticalInput = -1f;
            }
            else if (Input.GetKey(surfaceKey))
            {
                verticalInput = 1f;
            }

            // Apply buoyancy when not pressing anything
            if (!Input.GetKey(diveKey) && !Input.GetKey(surfaceKey) && isUnderwater)
            {
                verticalInput = buoyancy * 0.5f;
            }

            // Calculate target velocity
            float speed = isAtSurface ? surfaceSwimSpeed : swimSpeed;
            Vector3 targetVelocity = inputDir.normalized * speed;
            targetVelocity.y = verticalInput * verticalSwimSpeed;

            // Accelerate towards target
            swimVelocity = Vector3.MoveTowards(swimVelocity, targetVelocity, acceleration * Time.deltaTime);

            // Apply drag
            swimVelocity *= (1f - drag * Time.deltaTime);
        }

        private void UpdateSwimming()
        {
            Vector3 movement = swimVelocity * Time.deltaTime;

            // Surface bobbing
            if (isAtSurface && swimVelocity.magnitude < 0.5f)
            {
                float bob = Mathf.Sin(Time.time * surfaceBobSpeed) * surfaceBobAmount;
                movement.y += bob * Time.deltaTime;
            }

            // Clamp to water bounds
            float newY = transform.position.y + movement.y;
            if (newY > waterSurfaceY - surfaceOffset * 0.5f)
            {
                newY = waterSurfaceY - surfaceOffset * 0.5f;
                movement.y = newY - transform.position.y;
                swimVelocity.y = 0;
            }

            // Apply movement
            if (characterController != null)
            {
                characterController.Move(movement);
            }
            else if (rb != null)
            {
                rb.MovePosition(transform.position + movement);
            }
            else
            {
                transform.position += movement;
            }

            // Rotate towards movement direction
            if (swimVelocity.magnitude > 0.1f)
            {
                Vector3 lookDir = swimVelocity;
                if (!isUnderwater)
                {
                    lookDir.y = 0;
                }

                if (lookDir.magnitude > 0.1f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 5f * Time.deltaTime);
                }
            }
        }

        private void UpdateBreath()
        {
            if (isUnderwater)
            {
                // Drain breath
                currentBreath -= breathDrainRate * Time.deltaTime;
                currentBreath = Mathf.Max(0, currentBreath);

                BreathChanged?.Invoke(currentBreath);
                onBreathChanged?.Invoke(currentBreath);

                // Drowning
                if (currentBreath <= 0)
                {
                    if (Time.time - lastDrowningTime >= drowningInterval)
                    {
                        lastDrowningTime = Time.time;
                        ApplyDrowningDamage();
                    }

                    BreathDepleted?.Invoke();
                    onBreathDepleted?.Invoke();
                }
            }
            else
            {
                // Recover breath at surface
                if (currentBreath < maxBreath)
                {
                    currentBreath += breathRecoveryRate * Time.deltaTime;
                    currentBreath = Mathf.Min(maxBreath, currentBreath);

                    BreathChanged?.Invoke(currentBreath);
                    onBreathChanged?.Invoke(currentBreath);
                }
            }
        }

        private void ApplyDrowningDamage()
        {
            if (healthSystem != null)
            {
                healthSystem.TakeDamage(drowningDamage);
                Debug.Log($"[Swimming] Drowning! Took {drowningDamage} damage");
            }
        }

        // Public methods
        public void SetWaterSurface(float y)
        {
            waterSurfaceY = y;
        }

        public void RefillBreath()
        {
            currentBreath = maxBreath;
            BreathChanged?.Invoke(currentBreath);
            onBreathChanged?.Invoke(currentBreath);
        }

        public void AddBreath(float amount)
        {
            currentBreath = Mathf.Min(maxBreath, currentBreath + amount);
            BreathChanged?.Invoke(currentBreath);
            onBreathChanged?.Invoke(currentBreath);
        }

        public void ForceDive()
        {
            if (isInWater && !isUnderwater)
            {
                swimVelocity.y = -diveSpeed;
            }
        }

        public void ForceSurface()
        {
            if (isUnderwater)
            {
                swimVelocity.y = diveSpeed;
            }
        }

        public void SetSwimSpeed(float speed) => swimSpeed = speed;
        public void SetMaxBreath(float max) => maxBreath = max;

        private void OnDrawGizmosSelected()
        {
            // Water check radius
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, waterCheckRadius);

            // Surface offset
            if (isInWater)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(
                    new Vector3(transform.position.x - 1, waterSurfaceY, transform.position.z),
                    new Vector3(transform.position.x + 1, waterSurfaceY, transform.position.z)
                );
            }
        }
    }
}
