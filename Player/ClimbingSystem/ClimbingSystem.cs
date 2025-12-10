using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Player
{
    /// <summary>
    /// Climbing system with ledge grabbing and wall climbing.
    /// </summary>
    public class ClimbingSystem : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private LayerMask climbableLayer;
        [SerializeField] private float wallCheckDistance = 0.6f;
        [SerializeField] private float ledgeCheckDistance = 1f;
        [SerializeField] private float ledgeCheckHeight = 2f;

        [Header("Climbing")]
        [SerializeField] private float climbSpeed = 3f;
        [SerializeField] private float wallClimbSpeed = 2f;
        [SerializeField] private float ledgeClimbDuration = 0.5f;
        [SerializeField] private bool requireStamina = true;
        [SerializeField] private float staminaDrainRate = 10f;

        [Header("Ledge Grab")]
        [SerializeField] private float ledgeGrabOffset = 0.3f;
        [SerializeField] private float autoGrabDistance = 0.5f;
        [SerializeField] private bool autoGrabLedges = true;

        [Header("Wall Jump")]
        [SerializeField] private bool enableWallJump = true;
        [SerializeField] private float wallJumpForce = 8f;
        [SerializeField] private Vector3 wallJumpDirection = new Vector3(0.5f, 1f, 0f);

        [Header("Input")]
        [SerializeField] private KeyCode grabKey = KeyCode.E;
        [SerializeField] private KeyCode climbUpKey = KeyCode.Space;

        [Header("Events")]
        [SerializeField] private UnityEvent onGrabLedge;
        [SerializeField] private UnityEvent onReleaseLedge;
        [SerializeField] private UnityEvent onClimbUp;
        [SerializeField] private UnityEvent onWallJump;
        [SerializeField] private UnityEvent onStartWallClimb;
        [SerializeField] private UnityEvent onEndWallClimb;

        // State
        private ClimbState currentState = ClimbState.None;
        private Vector3 ledgePosition;
        private Vector3 wallNormal;
        private float climbStartTime;

        // Components
        private CharacterController characterController;
        private Rigidbody rb;

        // External stamina reference
        private Func<float> getStamina;
        private Action<float> useStamina;

        // Properties
        public ClimbState State => currentState;
        public bool IsClimbing => currentState != ClimbState.None;
        public bool IsOnLedge => currentState == ClimbState.LedgeHang;
        public bool IsWallClimbing => currentState == ClimbState.WallClimb;

        // Events
        public event Action LedgeGrabbed;
        public event Action LedgeReleased;
        public event Action ClimbedUp;
        public event Action WallJumped;
        public event Action WallClimbStarted;
        public event Action WallClimbEnded;

        public enum ClimbState
        {
            None,
            LedgeHang,
            ClimbingUp,
            WallClimb
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            switch (currentState)
            {
                case ClimbState.None:
                    CheckForClimbables();
                    break;

                case ClimbState.LedgeHang:
                    UpdateLedgeHang();
                    break;

                case ClimbState.WallClimb:
                    UpdateWallClimb();
                    break;

                case ClimbState.ClimbingUp:
                    // Handled by coroutine
                    break;
            }
        }

        private void CheckForClimbables()
        {
            // Check for wall in front
            if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out RaycastHit wallHit, wallCheckDistance, climbableLayer))
            {
                wallNormal = wallHit.normal;

                // Check for ledge above
                Vector3 ledgeCheckStart = transform.position + Vector3.up * ledgeCheckHeight + transform.forward * wallCheckDistance;
                if (Physics.Raycast(ledgeCheckStart, Vector3.down, out RaycastHit ledgeHit, ledgeCheckHeight, climbableLayer))
                {
                    float ledgeHeight = ledgeHit.point.y;
                    float playerTop = transform.position.y + ledgeCheckHeight * 0.8f;

                    // Auto grab if close enough
                    if (autoGrabLedges && Mathf.Abs(ledgeHeight - playerTop) < autoGrabDistance)
                    {
                        if (!IsGrounded() || Input.GetKey(grabKey))
                        {
                            GrabLedge(ledgeHit.point);
                            return;
                        }
                    }
                }

                // Manual grab
                if (Input.GetKeyDown(grabKey))
                {
                    StartWallClimb(wallHit.point, wallHit.normal);
                }
            }
        }

        private void GrabLedge(Vector3 ledgePoint)
        {
            currentState = ClimbState.LedgeHang;
            ledgePosition = ledgePoint;

            // Position player at ledge
            Vector3 hangPosition = ledgePoint - transform.forward * ledgeGrabOffset;
            hangPosition.y = ledgePoint.y - ledgeCheckHeight * 0.9f;

            if (characterController != null)
            {
                characterController.enabled = false;
                transform.position = hangPosition;
                characterController.enabled = true;
            }
            else
            {
                transform.position = hangPosition;
            }

            // Face the wall
            transform.forward = -wallNormal;

            // Disable gravity
            if (rb != null)
            {
                rb.useGravity = false;
                rb.velocity = Vector3.zero;
            }

            LedgeGrabbed?.Invoke();
            onGrabLedge?.Invoke();

            Debug.Log("[Climbing] Grabbed ledge");
        }

        private void UpdateLedgeHang()
        {
            // Drain stamina
            if (requireStamina && useStamina != null)
            {
                useStamina(staminaDrainRate * Time.deltaTime);

                if (getStamina != null && getStamina() <= 0)
                {
                    ReleaseLedge();
                    return;
                }
            }

            // Climb up
            if (Input.GetKeyDown(climbUpKey))
            {
                StartCoroutine(ClimbUpLedge());
                return;
            }

            // Wall jump
            if (enableWallJump && Input.GetKeyDown(KeyCode.Space) && Input.GetKey(KeyCode.S))
            {
                PerformWallJump();
                return;
            }

            // Release
            if (Input.GetKeyDown(grabKey) || Input.GetKeyDown(KeyCode.S))
            {
                ReleaseLedge();
            }

            // Shimmy left/right
            float horizontal = Input.GetAxis("Horizontal");
            if (Mathf.Abs(horizontal) > 0.1f)
            {
                Vector3 shimmy = transform.right * horizontal * climbSpeed * 0.5f * Time.deltaTime;

                // Check if can shimmy
                Vector3 newPos = transform.position + shimmy;
                if (Physics.Raycast(newPos + Vector3.up * ledgeCheckHeight, Vector3.down, ledgeCheckHeight, climbableLayer))
                {
                    transform.position = newPos;
                }
            }
        }

        private System.Collections.IEnumerator ClimbUpLedge()
        {
            currentState = ClimbState.ClimbingUp;

            Vector3 startPos = transform.position;
            Vector3 endPos = ledgePosition + transform.forward * 0.5f;
            endPos.y = ledgePosition.y + 0.1f;

            float elapsed = 0;

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            while (elapsed < ledgeClimbDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / ledgeClimbDuration;

                // Arc motion
                Vector3 pos = Vector3.Lerp(startPos, endPos, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * 0.5f;

                transform.position = pos;
                yield return null;
            }

            transform.position = endPos;

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            if (rb != null)
            {
                rb.useGravity = true;
            }

            currentState = ClimbState.None;

            ClimbedUp?.Invoke();
            onClimbUp?.Invoke();

            Debug.Log("[Climbing] Climbed up ledge");
        }

        private void ReleaseLedge()
        {
            currentState = ClimbState.None;

            if (rb != null)
            {
                rb.useGravity = true;
            }

            LedgeReleased?.Invoke();
            onReleaseLedge?.Invoke();

            Debug.Log("[Climbing] Released ledge");
        }

        private void StartWallClimb(Vector3 wallPoint, Vector3 normal)
        {
            currentState = ClimbState.WallClimb;
            wallNormal = normal;
            climbStartTime = Time.time;

            // Face wall
            transform.forward = -normal;

            // Stick to wall
            if (rb != null)
            {
                rb.useGravity = false;
                rb.velocity = Vector3.zero;
            }

            WallClimbStarted?.Invoke();
            onStartWallClimb?.Invoke();

            Debug.Log("[Climbing] Started wall climb");
        }

        private void UpdateWallClimb()
        {
            // Drain stamina
            if (requireStamina && useStamina != null)
            {
                useStamina(staminaDrainRate * Time.deltaTime);

                if (getStamina != null && getStamina() <= 0)
                {
                    EndWallClimb();
                    return;
                }
            }

            // Check still on wall
            if (!Physics.Raycast(transform.position + Vector3.up, transform.forward, wallCheckDistance, climbableLayer))
            {
                EndWallClimb();
                return;
            }

            // Wall jump
            if (enableWallJump && Input.GetKeyDown(KeyCode.Space))
            {
                PerformWallJump();
                return;
            }

            // Release
            if (Input.GetKeyDown(grabKey))
            {
                EndWallClimb();
                return;
            }

            // Climb movement
            float vertical = Input.GetAxis("Vertical");
            float horizontal = Input.GetAxis("Horizontal");

            Vector3 movement = Vector3.up * vertical + transform.right * horizontal;
            movement *= wallClimbSpeed * Time.deltaTime;

            // Check for ledge at top
            if (vertical > 0)
            {
                Vector3 ledgeCheck = transform.position + Vector3.up * ledgeCheckHeight + transform.forward * wallCheckDistance;
                if (!Physics.Raycast(ledgeCheck, Vector3.down, ledgeCheckHeight * 0.5f, climbableLayer))
                {
                    // Found top of wall - grab ledge
                    Vector3 ledgePoint = transform.position + Vector3.up * (ledgeCheckHeight * 0.9f);
                    GrabLedge(ledgePoint);
                    return;
                }
            }

            transform.position += movement;
        }

        private void EndWallClimb()
        {
            currentState = ClimbState.None;

            if (rb != null)
            {
                rb.useGravity = true;
            }

            WallClimbEnded?.Invoke();
            onEndWallClimb?.Invoke();

            Debug.Log("[Climbing] Ended wall climb");
        }

        private void PerformWallJump()
        {
            currentState = ClimbState.None;

            Vector3 jumpDir = (wallNormal * wallJumpDirection.x + Vector3.up * wallJumpDirection.y).normalized;

            if (rb != null)
            {
                rb.useGravity = true;
                rb.velocity = jumpDir * wallJumpForce;
            }
            else if (characterController != null)
            {
                // Would need to communicate with character controller
            }

            WallJumped?.Invoke();
            onWallJump?.Invoke();

            Debug.Log("[Climbing] Wall jump!");
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
        public void SetStaminaSystem(Func<float> getter, Action<float> consumer)
        {
            getStamina = getter;
            useStamina = consumer;
        }

        public void ForceRelease()
        {
            if (currentState == ClimbState.LedgeHang)
            {
                ReleaseLedge();
            }
            else if (currentState == ClimbState.WallClimb)
            {
                EndWallClimb();
            }
        }

        public bool CanGrabLedge()
        {
            if (currentState != ClimbState.None) return false;

            Vector3 ledgeCheckStart = transform.position + Vector3.up * ledgeCheckHeight + transform.forward * wallCheckDistance;
            return Physics.Raycast(ledgeCheckStart, Vector3.down, ledgeCheckHeight, climbableLayer);
        }

        public void SetClimbSpeed(float speed) => climbSpeed = speed;
        public void SetWallJumpForce(float force) => wallJumpForce = force;

        private void OnDrawGizmosSelected()
        {
            // Wall check
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * wallCheckDistance);

            // Ledge check
            Gizmos.color = Color.green;
            Vector3 ledgeStart = transform.position + Vector3.up * ledgeCheckHeight + transform.forward * wallCheckDistance;
            Gizmos.DrawRay(ledgeStart, Vector3.down * ledgeCheckHeight);

            // Current ledge
            if (currentState == ClimbState.LedgeHang)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(ledgePosition, 0.2f);
            }
        }
    }
}
