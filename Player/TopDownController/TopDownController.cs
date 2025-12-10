using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Player
{
    /// <summary>
    /// Top-down controller for ARPG and twin-stick style games.
    /// </summary>
    public class TopDownController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float sprintSpeed = 10f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float deceleration = 10f;
        [SerializeField] private float rotationSpeed = 15f;

        [Header("Rotation Mode")]
        [SerializeField] private RotationMode rotationMode = RotationMode.FaceMouse;
        [SerializeField] private bool instantRotation = false;

        [Header("Dash")]
        [SerializeField] private bool enableDash = true;
        [SerializeField] private float dashDistance = 5f;
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float dashCooldown = 1f;
        [SerializeField] private KeyCode dashKey = KeyCode.Space;

        [Header("Input")]
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private bool useRawInput = false;

        [Header("Camera")]
        [SerializeField] private Camera gameCamera;

        [Header("Ground Check")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckDistance = 0.2f;

        [Header("Events")]
        [SerializeField] private UnityEvent onDashStart;
        [SerializeField] private UnityEvent onDashEnd;
        [SerializeField] private UnityEvent onSprintStart;
        [SerializeField] private UnityEvent onSprintEnd;

        // State
        private Vector3 moveDirection;
        private Vector3 currentVelocity;
        private bool isSprinting;
        private bool isDashing;
        private float lastDashTime = -999f;
        private Vector3 dashDirection;
        private float dashStartTime;

        // Components
        private CharacterController characterController;
        private Rigidbody rb;

        // Properties
        public Vector3 Velocity => currentVelocity;
        public float CurrentSpeed => currentVelocity.magnitude;
        public bool IsSprinting => isSprinting;
        public bool IsDashing => isDashing;
        public bool IsMoving => moveDirection.magnitude > 0.1f;
        public bool CanDash => enableDash && !isDashing && Time.time - lastDashTime >= dashCooldown;
        public Vector3 MoveDirection => moveDirection;

        // Events
        public event Action DashStarted;
        public event Action DashEnded;
        public event Action SprintStarted;
        public event Action SprintEnded;

        public enum RotationMode
        {
            FaceMovement,
            FaceMouse,
            FaceTarget,
            Manual
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            rb = GetComponent<Rigidbody>();

            if (gameCamera == null)
            {
                gameCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (isDashing)
            {
                UpdateDash();
                return;
            }

            HandleInput();
            HandleRotation();
            UpdateMovement();
        }

        private void HandleInput()
        {
            // Movement input
            float horizontal = useRawInput ? Input.GetAxisRaw("Horizontal") : Input.GetAxis("Horizontal");
            float vertical = useRawInput ? Input.GetAxisRaw("Vertical") : Input.GetAxis("Vertical");

            moveDirection = new Vector3(horizontal, 0, vertical).normalized;

            // Sprint
            bool wasSprinting = isSprinting;
            isSprinting = Input.GetKey(sprintKey) && IsMoving;

            if (isSprinting && !wasSprinting)
            {
                SprintStarted?.Invoke();
                onSprintStart?.Invoke();
            }
            else if (!isSprinting && wasSprinting)
            {
                SprintEnded?.Invoke();
                onSprintEnd?.Invoke();
            }

            // Dash
            if (Input.GetKeyDown(dashKey) && CanDash)
            {
                StartDash();
            }
        }

        private void HandleRotation()
        {
            Vector3 lookDirection = Vector3.zero;

            switch (rotationMode)
            {
                case RotationMode.FaceMovement:
                    if (IsMoving)
                    {
                        lookDirection = moveDirection;
                    }
                    break;

                case RotationMode.FaceMouse:
                    lookDirection = GetMouseDirection();
                    break;

                case RotationMode.FaceTarget:
                    // Would need target reference
                    break;

                case RotationMode.Manual:
                    // Handled externally
                    return;
            }

            if (lookDirection.magnitude > 0.1f)
            {
                lookDirection.y = 0;
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

                if (instantRotation)
                {
                    transform.rotation = targetRotation;
                }
                else
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
        }

        private Vector3 GetMouseDirection()
        {
            if (gameCamera == null) return transform.forward;

            Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, transform.position);

            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 point = ray.GetPoint(distance);
                Vector3 direction = point - transform.position;
                direction.y = 0;
                return direction.normalized;
            }

            return transform.forward;
        }

        private void UpdateMovement()
        {
            float targetSpeed = isSprinting ? sprintSpeed : moveSpeed;
            Vector3 targetVelocity = moveDirection * targetSpeed;

            // Accelerate/decelerate
            if (IsMoving)
            {
                currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * Time.deltaTime);
            }
            else
            {
                currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
            }

            // Apply movement
            Vector3 movement = currentVelocity * Time.deltaTime;

            if (characterController != null)
            {
                // Add gravity
                if (!characterController.isGrounded)
                {
                    movement.y = -9.81f * Time.deltaTime;
                }

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
        }

        private void StartDash()
        {
            isDashing = true;
            lastDashTime = Time.time;
            dashStartTime = Time.time;

            // Dash in movement direction, or facing direction if not moving
            dashDirection = IsMoving ? moveDirection : transform.forward;
            dashDirection.y = 0;
            dashDirection.Normalize();

            DashStarted?.Invoke();
            onDashStart?.Invoke();

            Debug.Log("[TopDown] Dash started");
        }

        private void UpdateDash()
        {
            float elapsed = Time.time - dashStartTime;
            float t = elapsed / dashDuration;

            if (t >= 1f)
            {
                EndDash();
                return;
            }

            // Dash movement
            float dashSpeed = dashDistance / dashDuration;
            Vector3 movement = dashDirection * dashSpeed * Time.deltaTime;

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
        }

        private void EndDash()
        {
            isDashing = false;

            DashEnded?.Invoke();
            onDashEnd?.Invoke();

            Debug.Log("[TopDown] Dash ended");
        }

        // Public methods
        public void SetMoveDirection(Vector3 direction)
        {
            moveDirection = direction.normalized;
            moveDirection.y = 0;
        }

        public void SetRotationMode(RotationMode mode)
        {
            rotationMode = mode;
        }

        public void LookAt(Vector3 position)
        {
            Vector3 direction = position - transform.position;
            direction.y = 0;

            if (direction.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        public void ForceDash(Vector3 direction)
        {
            if (!CanDash) return;

            dashDirection = direction.normalized;
            dashDirection.y = 0;

            isDashing = true;
            lastDashTime = Time.time;
            dashStartTime = Time.time;

            DashStarted?.Invoke();
            onDashStart?.Invoke();
        }

        public void SetMoveSpeed(float speed) => moveSpeed = speed;
        public void SetSprintSpeed(float speed) => sprintSpeed = speed;
        public void SetDashCooldown(float cooldown) => dashCooldown = cooldown;

        public void StopMovement()
        {
            currentVelocity = Vector3.zero;
            moveDirection = Vector3.zero;
        }

        /// <summary>
        /// Get aim direction for combat/shooting.
        /// </summary>
        public Vector3 GetAimDirection()
        {
            if (rotationMode == RotationMode.FaceMouse)
            {
                return GetMouseDirection();
            }
            return transform.forward;
        }

        /// <summary>
        /// Get world position the player is aiming at.
        /// </summary>
        public Vector3 GetAimPosition()
        {
            if (gameCamera == null) return transform.position + transform.forward * 10f;

            Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, transform.position);

            if (groundPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }

            return transform.position + transform.forward * 10f;
        }

        private void OnDrawGizmosSelected()
        {
            // Movement direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, moveDirection * 2f);

            // Aim direction (mouse)
            if (gameCamera != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, GetMouseDirection() * 3f);
            }

            // Dash cooldown indicator
            Gizmos.color = CanDash ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.3f);
        }
    }
}
