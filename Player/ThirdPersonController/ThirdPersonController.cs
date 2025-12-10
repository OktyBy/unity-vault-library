using UnityEngine;

namespace UnityVault.Player
{
    /// <summary>
    /// Third person controller with orbital camera.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float acceleration = 10f;

        [Header("Jumping")]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float gravity = -20f;

        [Header("Camera")]
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private float cameraDistance = 5f;
        [SerializeField] private float cameraHeight = 2f;
        [SerializeField] private float cameraSensitivity = 3f;
        [SerializeField] private float minVerticalAngle = -30f;
        [SerializeField] private float maxVerticalAngle = 60f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.2f;
        [SerializeField] private LayerMask groundMask;

        // Components
        private CharacterController controller;
        private Camera mainCamera;

        // State
        private Vector3 velocity;
        private float horizontalAngle;
        private float verticalAngle;
        private bool isGrounded;
        private bool isSprinting;

        // Properties
        public bool IsGrounded => isGrounded;
        public bool IsSprinting => isSprinting;
        public bool IsMoving => new Vector3(velocity.x, 0, velocity.z).sqrMagnitude > 0.01f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            mainCamera = Camera.main;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            HandleCameraInput();
            CheckGround();
            HandleMovement();
            HandleJumping();
            ApplyGravity();
            ApplyMovement();
        }

        private void LateUpdate()
        {
            UpdateCamera();
        }

        private void HandleCameraInput()
        {
            float mouseX = Input.GetAxis("Mouse X") * cameraSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * cameraSensitivity;

            horizontalAngle += mouseX;
            verticalAngle -= mouseY;
            verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);
        }

        private void UpdateCamera()
        {
            if (mainCamera == null) return;

            Vector3 targetPos = cameraTarget != null ? cameraTarget.position : transform.position + Vector3.up * cameraHeight;

            Quaternion rotation = Quaternion.Euler(verticalAngle, horizontalAngle, 0);
            Vector3 offset = rotation * new Vector3(0, 0, -cameraDistance);

            mainCamera.transform.position = targetPos + offset;
            mainCamera.transform.LookAt(targetPos);
        }

        private void CheckGround()
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask);

            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Vector3 input = new Vector3(horizontal, 0, vertical).normalized;

            if (input.magnitude > 0.1f)
            {
                // Get camera forward without Y component
                Vector3 cameraForward = mainCamera.transform.forward;
                cameraForward.y = 0;
                cameraForward.Normalize();

                Vector3 cameraRight = mainCamera.transform.right;
                cameraRight.y = 0;
                cameraRight.Normalize();

                // Calculate move direction relative to camera
                Vector3 moveDir = cameraForward * vertical + cameraRight * horizontal;
                moveDir.Normalize();

                // Rotate character to face movement direction
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                // Apply movement
                isSprinting = Input.GetKey(KeyCode.LeftShift);
                float speed = isSprinting ? sprintSpeed : walkSpeed;

                Vector3 targetVelocity = moveDir * speed;
                velocity.x = Mathf.MoveTowards(velocity.x, targetVelocity.x, acceleration * Time.deltaTime);
                velocity.z = Mathf.MoveTowards(velocity.z, targetVelocity.z, acceleration * Time.deltaTime);
            }
            else
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0, acceleration * Time.deltaTime);
                velocity.z = Mathf.MoveTowards(velocity.z, 0, acceleration * Time.deltaTime);
            }
        }

        private void HandleJumping()
        {
            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                velocity.y = jumpForce;
            }
        }

        private void ApplyGravity()
        {
            velocity.y += gravity * Time.deltaTime;
        }

        private void ApplyMovement()
        {
            controller.Move(velocity * Time.deltaTime);
        }

        public void SetCameraDistance(float distance)
        {
            cameraDistance = Mathf.Max(1f, distance);
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
