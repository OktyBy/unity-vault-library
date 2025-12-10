using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Player
{
    /// <summary>
    /// 2D platformer character controller with coyote time and jump buffering.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterController2D : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float acceleration = 50f;
        [SerializeField] private float deceleration = 50f;
        [SerializeField] private float airControl = 0.8f;

        [Header("Jumping")]
        [SerializeField] private float jumpForce = 12f;
        [SerializeField] private float jumpCutMultiplier = 0.5f;
        [SerializeField] private int maxJumps = 1;
        [SerializeField] private float coyoteTime = 0.1f;
        [SerializeField] private float jumpBufferTime = 0.1f;

        [Header("Gravity")]
        [SerializeField] private float fallGravityMultiplier = 2.5f;
        [SerializeField] private float maxFallSpeed = 20f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
        [SerializeField] private LayerMask groundMask;

        [Header("Wall Check")]
        [SerializeField] private bool enableWallSlide = false;
        [SerializeField] private Transform wallCheck;
        [SerializeField] private float wallSlideSpeed = 2f;
        [SerializeField] private Vector2 wallJumpForce = new Vector2(10f, 12f);

        [Header("Events")]
        [SerializeField] private UnityEvent onJumped;
        [SerializeField] private UnityEvent onLanded;
        [SerializeField] private UnityEvent<bool> onFacingChanged;

        // Components
        private Rigidbody2D rb;

        // State
        private float horizontalInput;
        private bool isGrounded;
        private bool wasGrounded;
        private bool isWallSliding;
        private bool facingRight = true;
        private int jumpsRemaining;
        private float lastGroundedTime;
        private float lastJumpPressTime;

        // Properties
        public bool IsGrounded => isGrounded;
        public bool IsWallSliding => isWallSliding;
        public bool FacingRight => facingRight;
        public bool IsMoving => Mathf.Abs(horizontalInput) > 0.01f;
        public Vector2 Velocity => rb.linearVelocity;

        // Events
        public event Action Jumped;
        public event Action Landed;
        public event Action<bool> FacingChanged;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.freezeRotation = true;
        }

        private void Update()
        {
            GatherInput();
            CheckGround();

            if (enableWallSlide)
            {
                CheckWall();
            }
        }

        private void FixedUpdate()
        {
            HandleMovement();
            HandleJumping();
            HandleGravity();

            if (enableWallSlide && isWallSliding)
            {
                HandleWallSlide();
            }
        }

        private void GatherInput()
        {
            horizontalInput = Input.GetAxisRaw("Horizontal");

            if (Input.GetButtonDown("Jump"))
            {
                lastJumpPressTime = Time.time;
            }

            if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
            }

            // Flip sprite
            if (horizontalInput > 0 && !facingRight)
            {
                Flip();
            }
            else if (horizontalInput < 0 && facingRight)
            {
                Flip();
            }
        }

        private void CheckGround()
        {
            wasGrounded = isGrounded;
            isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundMask);

            if (isGrounded)
            {
                lastGroundedTime = Time.time;
                jumpsRemaining = maxJumps;

                if (!wasGrounded && rb.linearVelocity.y <= 0)
                {
                    Landed?.Invoke();
                    onLanded?.Invoke();
                }
            }
        }

        private void CheckWall()
        {
            if (wallCheck == null) return;

            float direction = facingRight ? 1f : -1f;
            bool touchingWall = Physics2D.Raycast(wallCheck.position, Vector2.right * direction, 0.2f, groundMask);

            isWallSliding = touchingWall && !isGrounded && rb.linearVelocity.y < 0;
        }

        private void HandleMovement()
        {
            float targetSpeed = horizontalInput * moveSpeed;
            float accelRate = Mathf.Abs(horizontalInput) > 0 ? acceleration : deceleration;

            if (!isGrounded)
            {
                accelRate *= airControl;
            }

            float speedDiff = targetSpeed - rb.linearVelocity.x;
            float movement = speedDiff * accelRate * Time.fixedDeltaTime;

            rb.linearVelocity = new Vector2(rb.linearVelocity.x + movement, rb.linearVelocity.y);
        }

        private void HandleJumping()
        {
            bool canCoyoteJump = Time.time - lastGroundedTime <= coyoteTime;
            bool hasBufferedJump = Time.time - lastJumpPressTime <= jumpBufferTime;

            if (hasBufferedJump)
            {
                if (isWallSliding)
                {
                    WallJump();
                    lastJumpPressTime = 0;
                }
                else if (canCoyoteJump || jumpsRemaining > 0)
                {
                    Jump();
                    lastJumpPressTime = 0;
                }
            }
        }

        private void Jump()
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpsRemaining--;
            lastGroundedTime = 0;

            Jumped?.Invoke();
            onJumped?.Invoke();
        }

        private void WallJump()
        {
            float direction = facingRight ? -1f : 1f;
            rb.linearVelocity = new Vector2(wallJumpForce.x * direction, wallJumpForce.y);
            Flip();

            Jumped?.Invoke();
            onJumped?.Invoke();
        }

        private void HandleWallSlide()
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -wallSlideSpeed);
        }

        private void HandleGravity()
        {
            if (rb.linearVelocity.y < 0)
            {
                rb.gravityScale = fallGravityMultiplier;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -maxFallSpeed));
            }
            else
            {
                rb.gravityScale = 1f;
            }
        }

        private void Flip()
        {
            facingRight = !facingRight;
            transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
            FacingChanged?.Invoke(facingRight);
            onFacingChanged?.Invoke(facingRight);
        }

        public void SetPosition(Vector2 position)
        {
            rb.position = position;
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
            }
        }
    }
}
