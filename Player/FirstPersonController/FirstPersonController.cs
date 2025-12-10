using UnityEngine;

namespace UnityVault.Player
{
    /// <summary>
    /// First person controller with mouse look and movement.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float crouchSpeed = 2.5f;
        [SerializeField] private float acceleration = 10f;

        [Header("Jumping")]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float gravity = -20f;

        [Header("Mouse Look")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float maxLookAngle = 90f;
        [SerializeField] private bool invertY = false;
        [SerializeField] private bool lockCursor = true;

        [Header("Head Bob")]
        [SerializeField] private bool enableHeadBob = true;
        [SerializeField] private float bobFrequency = 10f;
        [SerializeField] private float bobAmplitude = 0.05f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.2f;
        [SerializeField] private LayerMask groundMask;

        // Components
        private CharacterController controller;

        // State
        private Vector3 velocity;
        private float cameraPitch;
        private float bobTimer;
        private float defaultCameraY;
        private bool isGrounded;
        private bool isSprinting;

        // Properties
        public bool IsGrounded => isGrounded;
        public bool IsSprinting => isSprinting;
        public bool IsMoving => velocity.sqrMagnitude > 0.01f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (cameraTransform == null)
            {
                cameraTransform = Camera.main?.transform;
            }

            if (cameraTransform != null)
            {
                defaultCameraY = cameraTransform.localPosition.y;
            }
        }

        private void Start()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            CheckGround();
            HandleMouseLook();
            HandleMovement();
            HandleJumping();
            ApplyGravity();
            ApplyMovement();

            if (enableHeadBob)
            {
                HandleHeadBob();
            }
        }

        private void CheckGround()
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask);

            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }
        }

        private void HandleMouseLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            if (invertY) mouseY = -mouseY;

            transform.Rotate(Vector3.up * mouseX);

            cameraPitch -= mouseY;
            cameraPitch = Mathf.Clamp(cameraPitch, -maxLookAngle, maxLookAngle);

            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            }
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            isSprinting = Input.GetKey(KeyCode.LeftShift);
            float speed = isSprinting ? sprintSpeed : walkSpeed;

            Vector3 moveDir = transform.right * horizontal + transform.forward * vertical;
            moveDir.Normalize();

            Vector3 targetVelocity = moveDir * speed;

            velocity.x = Mathf.MoveTowards(velocity.x, targetVelocity.x, acceleration * Time.deltaTime);
            velocity.z = Mathf.MoveTowards(velocity.z, targetVelocity.z, acceleration * Time.deltaTime);
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

        private void HandleHeadBob()
        {
            if (cameraTransform == null) return;

            if (isGrounded && IsMoving)
            {
                bobTimer += Time.deltaTime * bobFrequency * (isSprinting ? 1.5f : 1f);
                float bobOffset = Mathf.Sin(bobTimer) * bobAmplitude;
                cameraTransform.localPosition = new Vector3(
                    cameraTransform.localPosition.x,
                    defaultCameraY + bobOffset,
                    cameraTransform.localPosition.z
                );
            }
            else
            {
                bobTimer = 0f;
                cameraTransform.localPosition = new Vector3(
                    cameraTransform.localPosition.x,
                    Mathf.Lerp(cameraTransform.localPosition.y, defaultCameraY, Time.deltaTime * 5f),
                    cameraTransform.localPosition.z
                );
            }
        }

        public void SetSensitivity(float sensitivity)
        {
            mouseSensitivity = sensitivity;
        }

        public void SetInvertY(bool invert)
        {
            invertY = invert;
        }

        public void ToggleCursor()
        {
            lockCursor = !lockCursor;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;
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
