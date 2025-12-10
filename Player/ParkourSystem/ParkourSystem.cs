using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Player
{
    /// <summary>
    /// Parkour system with vaulting, sliding, and wall running.
    /// </summary>
    public class ParkourSystem : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private LayerMask obstacleLayer;
        [SerializeField] private float detectionRange = 1.5f;
        [SerializeField] private float vaultHeightMin = 0.5f;
        [SerializeField] private float vaultHeightMax = 1.5f;

        [Header("Vault")]
        [SerializeField] private float vaultSpeed = 5f;
        [SerializeField] private float vaultDuration = 0.5f;
        [SerializeField] private float vaultCooldown = 0.3f;
        [SerializeField] private AnimationCurve vaultCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Slide")]
        [SerializeField] private float slideSpeed = 8f;
        [SerializeField] private float slideDuration = 0.8f;
        [SerializeField] private float slideCooldown = 0.5f;
        [SerializeField] private float slideHeight = 0.5f;
        [SerializeField] private KeyCode slideKey = KeyCode.LeftControl;

        [Header("Wall Run")]
        [SerializeField] private bool enableWallRun = true;
        [SerializeField] private float wallRunSpeed = 7f;
        [SerializeField] private float wallRunDuration = 2f;
        [SerializeField] private float wallRunGravity = -2f;
        [SerializeField] private float wallRunAngle = 15f;
        [SerializeField] private float wallJumpForce = 10f;

        [Header("Requirements")]
        [SerializeField] private float minimumSpeed = 3f;
        [SerializeField] private bool requireSprint = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onVaultStart;
        [SerializeField] private UnityEvent onVaultEnd;
        [SerializeField] private UnityEvent onSlideStart;
        [SerializeField] private UnityEvent onSlideEnd;
        [SerializeField] private UnityEvent onWallRunStart;
        [SerializeField] private UnityEvent onWallRunEnd;
        [SerializeField] private UnityEvent onWallJump;

        // State
        private ParkourState currentState = ParkourState.None;
        private float stateStartTime;
        private float lastVaultTime;
        private float lastSlideTime;
        private Vector3 parkourStartPos;
        private Vector3 parkourEndPos;
        private Vector3 wallNormal;
        private float originalHeight;
        private int wallRunSide; // 1 = right, -1 = left

        // Components
        private CharacterController characterController;
        private CapsuleCollider capsuleCollider;
        private Rigidbody rb;

        // Properties
        public ParkourState State => currentState;
        public bool IsPerformingParkour => currentState != ParkourState.None;
        public bool IsVaulting => currentState == ParkourState.Vaulting;
        public bool IsSliding => currentState == ParkourState.Sliding;
        public bool IsWallRunning => currentState == ParkourState.WallRunning;

        // Events
        public event Action VaultStarted;
        public event Action VaultEnded;
        public event Action SlideStarted;
        public event Action SlideEnded;
        public event Action WallRunStarted;
        public event Action WallRunEnded;
        public event Action WallJumped;

        public enum ParkourState
        {
            None,
            Vaulting,
            Sliding,
            WallRunning
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            capsuleCollider = GetComponent<CapsuleCollider>();
            rb = GetComponent<Rigidbody>();

            if (characterController != null)
            {
                originalHeight = characterController.height;
            }
            else if (capsuleCollider != null)
            {
                originalHeight = capsuleCollider.height;
            }
        }

        private void Update()
        {
            switch (currentState)
            {
                case ParkourState.None:
                    CheckForParkourActions();
                    break;

                case ParkourState.Vaulting:
                    UpdateVault();
                    break;

                case ParkourState.Sliding:
                    UpdateSlide();
                    break;

                case ParkourState.WallRunning:
                    UpdateWallRun();
                    break;
            }
        }

        private void CheckForParkourActions()
        {
            float currentSpeed = GetCurrentSpeed();
            bool meetsSpeedRequirement = !requireSprint || currentSpeed >= minimumSpeed;

            // Check for slide
            if (Input.GetKeyDown(slideKey) && IsGrounded() && meetsSpeedRequirement)
            {
                if (Time.time - lastSlideTime >= slideCooldown)
                {
                    StartSlide();
                    return;
                }
            }

            // Check for vault
            if (meetsSpeedRequirement && Time.time - lastVaultTime >= vaultCooldown)
            {
                VaultInfo vaultInfo = CheckForVault();
                if (vaultInfo.canVault)
                {
                    StartVault(vaultInfo);
                    return;
                }
            }

            // Check for wall run
            if (enableWallRun && !IsGrounded() && meetsSpeedRequirement)
            {
                WallRunInfo wallInfo = CheckForWallRun();
                if (wallInfo.canWallRun)
                {
                    StartWallRun(wallInfo);
                }
            }
        }

        private VaultInfo CheckForVault()
        {
            VaultInfo info = new VaultInfo();

            // Check for obstacle in front
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (!Physics.Raycast(origin, transform.forward, out RaycastHit hit, detectionRange, obstacleLayer))
            {
                return info;
            }

            // Check obstacle height
            Vector3 topCheck = hit.point + Vector3.up * vaultHeightMax;
            if (Physics.Raycast(topCheck, Vector3.down, out RaycastHit topHit, vaultHeightMax, obstacleLayer))
            {
                float obstacleHeight = topHit.point.y - transform.position.y;

                if (obstacleHeight >= vaultHeightMin && obstacleHeight <= vaultHeightMax)
                {
                    // Check for landing space
                    Vector3 landingCheck = topHit.point + transform.forward * 1f + Vector3.up;
                    if (!Physics.Raycast(landingCheck, Vector3.down, out RaycastHit landHit, 3f, obstacleLayer))
                    {
                        landHit.point = landingCheck - Vector3.up * 3f;
                    }

                    info.canVault = true;
                    info.startPos = transform.position;
                    info.vaultPoint = topHit.point + Vector3.up * 0.2f;
                    info.endPos = landHit.point + Vector3.up * 0.1f;
                    info.obstacleHeight = obstacleHeight;
                }
            }

            return info;
        }

        private void StartVault(VaultInfo info)
        {
            currentState = ParkourState.Vaulting;
            stateStartTime = Time.time;
            lastVaultTime = Time.time;
            parkourStartPos = info.startPos;
            parkourEndPos = info.endPos;

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            VaultStarted?.Invoke();
            onVaultStart?.Invoke();

            Debug.Log("[Parkour] Vault started");
        }

        private void UpdateVault()
        {
            float elapsed = Time.time - stateStartTime;
            float t = elapsed / vaultDuration;

            if (t >= 1f)
            {
                EndVault();
                return;
            }

            // Curved motion over obstacle
            float curveT = vaultCurve.Evaluate(t);
            Vector3 pos = Vector3.Lerp(parkourStartPos, parkourEndPos, curveT);

            // Add arc height
            float arcHeight = Mathf.Sin(t * Mathf.PI) * 0.5f;
            pos.y += arcHeight;

            transform.position = pos;
        }

        private void EndVault()
        {
            transform.position = parkourEndPos;

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            currentState = ParkourState.None;

            VaultEnded?.Invoke();
            onVaultEnd?.Invoke();

            Debug.Log("[Parkour] Vault ended");
        }

        private void StartSlide()
        {
            currentState = ParkourState.Sliding;
            stateStartTime = Time.time;
            lastSlideTime = Time.time;

            // Shrink collider
            if (characterController != null)
            {
                characterController.height = slideHeight;
                characterController.center = Vector3.up * (slideHeight / 2f);
            }
            else if (capsuleCollider != null)
            {
                capsuleCollider.height = slideHeight;
                capsuleCollider.center = Vector3.up * (slideHeight / 2f);
            }

            SlideStarted?.Invoke();
            onSlideStart?.Invoke();

            Debug.Log("[Parkour] Slide started");
        }

        private void UpdateSlide()
        {
            float elapsed = Time.time - stateStartTime;

            // Check for end conditions
            if (elapsed >= slideDuration || !Input.GetKey(slideKey))
            {
                // Check if can stand up
                if (CanStandUp())
                {
                    EndSlide();
                    return;
                }
            }

            // Slide movement
            float speedFactor = 1f - (elapsed / slideDuration) * 0.5f;
            Vector3 movement = transform.forward * slideSpeed * speedFactor * Time.deltaTime;

            if (characterController != null)
            {
                characterController.Move(movement + Vector3.down * 0.1f);
            }
            else
            {
                transform.position += movement;
            }
        }

        private void EndSlide()
        {
            // Restore collider
            if (characterController != null)
            {
                characterController.height = originalHeight;
                characterController.center = Vector3.up * (originalHeight / 2f);
            }
            else if (capsuleCollider != null)
            {
                capsuleCollider.height = originalHeight;
                capsuleCollider.center = Vector3.up * (originalHeight / 2f);
            }

            currentState = ParkourState.None;

            SlideEnded?.Invoke();
            onSlideEnd?.Invoke();

            Debug.Log("[Parkour] Slide ended");
        }

        private bool CanStandUp()
        {
            return !Physics.Raycast(transform.position + Vector3.up * slideHeight, Vector3.up, originalHeight - slideHeight, obstacleLayer);
        }

        private WallRunInfo CheckForWallRun()
        {
            WallRunInfo info = new WallRunInfo();

            // Check right wall
            if (Physics.Raycast(transform.position + Vector3.up, transform.right, out RaycastHit rightHit, detectionRange, obstacleLayer))
            {
                info.canWallRun = true;
                info.wallNormal = rightHit.normal;
                info.side = 1;
                return info;
            }

            // Check left wall
            if (Physics.Raycast(transform.position + Vector3.up, -transform.right, out RaycastHit leftHit, detectionRange, obstacleLayer))
            {
                info.canWallRun = true;
                info.wallNormal = leftHit.normal;
                info.side = -1;
                return info;
            }

            return info;
        }

        private void StartWallRun(WallRunInfo info)
        {
            currentState = ParkourState.WallRunning;
            stateStartTime = Time.time;
            wallNormal = info.wallNormal;
            wallRunSide = info.side;

            if (rb != null)
            {
                rb.useGravity = false;
            }

            // Tilt camera/body
            Vector3 euler = transform.eulerAngles;
            euler.z = -wallRunAngle * wallRunSide;
            transform.eulerAngles = euler;

            WallRunStarted?.Invoke();
            onWallRunStart?.Invoke();

            Debug.Log($"[Parkour] Wall run started ({(wallRunSide > 0 ? "right" : "left")})");
        }

        private void UpdateWallRun()
        {
            float elapsed = Time.time - stateStartTime;

            // Check for end conditions
            bool stillOnWall = Physics.Raycast(transform.position + Vector3.up, transform.right * wallRunSide, detectionRange, obstacleLayer);

            if (elapsed >= wallRunDuration || !stillOnWall || IsGrounded())
            {
                EndWallRun();
                return;
            }

            // Wall jump
            if (Input.GetKeyDown(KeyCode.Space))
            {
                PerformWallJump();
                return;
            }

            // Wall run movement
            Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up) * -wallRunSide;
            Vector3 movement = wallForward * wallRunSpeed + Vector3.up * wallRunGravity;

            if (characterController != null)
            {
                characterController.Move(movement * Time.deltaTime);
            }
            else if (rb != null)
            {
                rb.velocity = movement;
            }
            else
            {
                transform.position += movement * Time.deltaTime;
            }
        }

        private void EndWallRun()
        {
            if (rb != null)
            {
                rb.useGravity = true;
            }

            // Reset tilt
            Vector3 euler = transform.eulerAngles;
            euler.z = 0;
            transform.eulerAngles = euler;

            currentState = ParkourState.None;

            WallRunEnded?.Invoke();
            onWallRunEnd?.Invoke();

            Debug.Log("[Parkour] Wall run ended");
        }

        private void PerformWallJump()
        {
            Vector3 jumpDir = (wallNormal + Vector3.up).normalized;

            if (rb != null)
            {
                rb.useGravity = true;
                rb.velocity = jumpDir * wallJumpForce;
            }

            // Reset tilt
            Vector3 euler = transform.eulerAngles;
            euler.z = 0;
            transform.eulerAngles = euler;

            currentState = ParkourState.None;

            WallJumped?.Invoke();
            onWallJump?.Invoke();

            Debug.Log("[Parkour] Wall jump!");
        }

        private float GetCurrentSpeed()
        {
            if (characterController != null)
            {
                return characterController.velocity.magnitude;
            }
            else if (rb != null)
            {
                return rb.velocity.magnitude;
            }
            return 0f;
        }

        private bool IsGrounded()
        {
            if (characterController != null)
            {
                return characterController.isGrounded;
            }
            return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.2f);
        }

        // Public methods
        public void ForceEndParkour()
        {
            switch (currentState)
            {
                case ParkourState.Vaulting:
                    EndVault();
                    break;
                case ParkourState.Sliding:
                    EndSlide();
                    break;
                case ParkourState.WallRunning:
                    EndWallRun();
                    break;
            }
        }

        public bool CanVault()
        {
            return currentState == ParkourState.None && CheckForVault().canVault;
        }

        public bool CanSlide()
        {
            return currentState == ParkourState.None && IsGrounded() && Time.time - lastSlideTime >= slideCooldown;
        }

        private void OnDrawGizmosSelected()
        {
            // Detection range
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * detectionRange);

            // Wall run detection
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position + Vector3.up, transform.right * detectionRange);
            Gizmos.DrawRay(transform.position + Vector3.up, -transform.right * detectionRange);

            // Vault heights
            Gizmos.color = Color.green;
            Vector3 pos = transform.position + transform.forward * 0.5f;
            Gizmos.DrawLine(pos + Vector3.up * vaultHeightMin, pos + Vector3.up * vaultHeightMax);
        }

        private struct VaultInfo
        {
            public bool canVault;
            public Vector3 startPos;
            public Vector3 vaultPoint;
            public Vector3 endPos;
            public float obstacleHeight;
        }

        private struct WallRunInfo
        {
            public bool canWallRun;
            public Vector3 wallNormal;
            public int side;
        }
    }
}
