using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Player
{
    /// <summary>
    /// 3D character controller with movement, jumping, and crouching.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterController3D : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float crouchSpeed = 2.5f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float deceleration = 10f;

        [Header("Jumping")]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private int maxJumps = 1;
        [SerializeField] private float coyoteTime = 0.15f;
        [SerializeField] private float jumpBufferTime = 0.1f;

        [Header("Crouching")]
        [SerializeField] private float standingHeight = 2f;
        [SerializeField] private float crouchHeight = 1f;
        [SerializeField] private float crouchTransitionSpeed = 10f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.2f;
        [SerializeField] private LayerMask groundMask;

        [Header("Events")]
        [SerializeField] private UnityEvent onJumped;
        [SerializeField] private UnityEvent onLanded;
        [SerializeField] private UnityEvent<bool> onCrouchChanged;

        // State
        private CharacterController controller;
        private Vector3 velocity;
        private Vector2 input;
        private bool isGrounded;
        private bool wasGrounded;
        private bool isSprinting;
        private bool isCrouching;
        private int jumpsRemaining;
        private float lastGroundedTime;
        private float lastJumpPressTime;
        private float currentHeight;

        // Properties
        public bool IsGrounded => isGrounded;
        public bool IsSprinting => isSprinting;
        public bool IsCrouching => isCrouching;
        public bool IsMoving => input.sqrMagnitude > 0.01f;
        public Vector3 Velocity => velocity;
        public float CurrentSpeed => new Vector3(velocity.x, 0, velocity.z).magnitude;

        // Events
        public event Action Jumped;
        public event Action Landed;
        public event Action<bool> CrouchChanged;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            currentHeight = standingHeight;
        }

        private void Update()
        {
            GatherInput();
            CheckGround();
            HandleMovement();
            HandleJumping();
            HandleCrouching();
            ApplyGravity();
            ApplyMovement();
        }

        private void GatherInput()
        {
            input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching;

            if (Input.GetButtonDown("Jump"))
            {
                lastJumpPressTime = Time.time;
            }

            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                ToggleCrouch();
            }
        }

        private void CheckGround()
        {
            wasGrounded = isGrounded;
            isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask);

            if (isGrounded)
            {
                lastGroundedTime = Time.time;
                jumpsRemaining = maxJumps;

                if (!wasGrounded)
                {
                    Landed?.Invoke();
                    onLanded?.Invoke();
                }
            }
        }

        private void HandleMovement()
        {
            float targetSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
            Vector3 moveDir = transform.right * input.x + transform.forward * input.y;
            moveDir.Normalize();

            Vector3 targetVelocity = moveDir * targetSpeed;
            float rate = input.sqrMagnitude > 0 ? acceleration : deceleration;

            velocity.x = Mathf.MoveTowards(velocity.x, targetVelocity.x, rate * Time.deltaTime);
            velocity.z = Mathf.MoveTowards(velocity.z, targetVelocity.z, rate * Time.deltaTime);
        }

        private void HandleJumping()
        {
            bool canCoyoteJump = Time.time - lastGroundedTime <= coyoteTime;
            bool hasBufferedJump = Time.time - lastJumpPressTime <= jumpBufferTime;

            if (hasBufferedJump && (canCoyoteJump || jumpsRemaining > 0))
            {
                Jump();
                lastJumpPressTime = 0;
            }
        }

        private void Jump()
        {
            velocity.y = jumpForce;
            jumpsRemaining--;

            Jumped?.Invoke();
            onJumped?.Invoke();
        }

        private void HandleCrouching()
        {
            float targetHeight = isCrouching ? crouchHeight : standingHeight;
            currentHeight = Mathf.MoveTowards(currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            controller.height = currentHeight;
            controller.center = new Vector3(0, currentHeight / 2f, 0);
        }

        private void ToggleCrouch()
        {
            if (isCrouching)
            {
                // Check if can stand up
                if (!Physics.Raycast(transform.position, Vector3.up, standingHeight - crouchHeight + 0.1f))
                {
                    isCrouching = false;
                    CrouchChanged?.Invoke(false);
                    onCrouchChanged?.Invoke(false);
                }
            }
            else
            {
                isCrouching = true;
                CrouchChanged?.Invoke(true);
                onCrouchChanged?.Invoke(true);
            }
        }

        private void ApplyGravity()
        {
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }
            else
            {
                velocity.y += gravity * Time.deltaTime;
            }
        }

        private void ApplyMovement()
        {
            controller.Move(velocity * Time.deltaTime);
        }

        public void SetPosition(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
            }
        }
    }
}
