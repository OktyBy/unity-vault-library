using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityVault.Combat
{
    /// <summary>
    /// Lock-on targeting system for Souls-like combat.
    /// </summary>
    public class LockOnSystem : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float detectionRange = 20f;
        [SerializeField] private float lockOnAngle = 60f; // Degrees from forward
        [SerializeField] private LayerMask targetLayer;
        [SerializeField] private LayerMask obstacleLayer;

        [Header("Lock-On Behavior")]
        [SerializeField] private float maxLockDistance = 25f;
        [SerializeField] private bool breakOnObstacle = true;
        [SerializeField] private float obstacleBreakDelay = 0.5f;
        [SerializeField] private bool autoSwitchOnDeath = true;

        [Header("Camera")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float cameraSmoothSpeed = 10f;
        [SerializeField] private Vector3 lockOnOffset = new Vector3(0, 1.5f, 0);

        [Header("Input")]
        [SerializeField] private KeyCode lockOnKey = KeyCode.Tab;
        [SerializeField] private KeyCode switchTargetKey = KeyCode.Q;
        [SerializeField] private bool useMouse = true;
        [SerializeField] private float mouseSwitchThreshold = 0.5f;

        [Header("UI")]
        [SerializeField] private GameObject lockOnIndicatorPrefab;
        [SerializeField] private Vector3 indicatorOffset = new Vector3(0, 2f, 0);

        [Header("Events")]
        [SerializeField] private UnityEvent<Transform> onTargetLocked;
        [SerializeField] private UnityEvent onTargetLost;
        [SerializeField] private UnityEvent<Transform> onTargetSwitched;

        // State
        private Transform currentTarget;
        private List<Transform> potentialTargets = new List<Transform>();
        private GameObject currentIndicator;
        private float obstacleTimer;
        private bool isLocked;

        // Properties
        public bool IsLocked => isLocked && currentTarget != null;
        public Transform CurrentTarget => currentTarget;
        public Vector3 TargetPosition => currentTarget != null ? currentTarget.position + lockOnOffset : Vector3.zero;
        public float DistanceToTarget => currentTarget != null ? Vector3.Distance(transform.position, currentTarget.position) : 0f;

        // Events
        public event Action<Transform> TargetLocked;
        public event Action TargetLost;
        public event Action<Transform> TargetSwitched;

        private void Awake()
        {
            if (cameraTransform == null)
            {
                cameraTransform = Camera.main?.transform;
            }
        }

        private void Update()
        {
            HandleInput();
            UpdateLockOn();
            UpdateIndicator();
        }

        private void LateUpdate()
        {
            if (isLocked && cameraTransform != null)
            {
                UpdateCameraLookAt();
            }
        }

        private void HandleInput()
        {
            // Toggle lock-on
            if (Input.GetKeyDown(lockOnKey))
            {
                if (isLocked)
                {
                    ReleaseLock();
                }
                else
                {
                    TryLockOn();
                }
            }

            // Switch target
            if (isLocked)
            {
                if (Input.GetKeyDown(switchTargetKey))
                {
                    SwitchTarget(1);
                }

                // Mouse-based switching
                if (useMouse)
                {
                    float mouseX = Input.GetAxis("Mouse X");
                    if (Mathf.Abs(mouseX) > mouseSwitchThreshold)
                    {
                        SwitchTarget(mouseX > 0 ? 1 : -1);
                    }
                }
            }
        }

        private void UpdateLockOn()
        {
            if (!isLocked || currentTarget == null) return;

            // Check if target is still valid
            if (!IsTargetValid(currentTarget))
            {
                if (autoSwitchOnDeath)
                {
                    SwitchToNextValidTarget();
                }
                else
                {
                    ReleaseLock();
                }
                return;
            }

            // Check distance
            float distance = Vector3.Distance(transform.position, currentTarget.position);
            if (distance > maxLockDistance)
            {
                ReleaseLock();
                return;
            }

            // Check for obstacles
            if (breakOnObstacle)
            {
                if (IsObstructed(currentTarget))
                {
                    obstacleTimer += Time.deltaTime;
                    if (obstacleTimer >= obstacleBreakDelay)
                    {
                        ReleaseLock();
                    }
                }
                else
                {
                    obstacleTimer = 0f;
                }
            }

            // Face target
            FaceTarget();
        }

        public bool TryLockOn()
        {
            RefreshPotentialTargets();

            if (potentialTargets.Count == 0)
            {
                Debug.Log("[LockOn] No targets in range");
                return false;
            }

            // Find best target (closest to center of screen)
            Transform bestTarget = FindBestTarget();

            if (bestTarget != null)
            {
                LockOnTo(bestTarget);
                return true;
            }

            return false;
        }

        private void RefreshPotentialTargets()
        {
            potentialTargets.Clear();

            Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRange, targetLayer);

            foreach (var col in colliders)
            {
                if (col.transform == transform) continue;

                // Check angle
                Vector3 dirToTarget = (col.transform.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, dirToTarget);

                if (angle <= lockOnAngle)
                {
                    // Check line of sight
                    if (!IsObstructed(col.transform))
                    {
                        potentialTargets.Add(col.transform);
                    }
                }
            }
        }

        private Transform FindBestTarget()
        {
            if (potentialTargets.Count == 0) return null;

            Transform best = null;
            float bestScore = float.MaxValue;

            foreach (var target in potentialTargets)
            {
                // Score based on screen position (prefer center)
                Vector3 screenPos = Camera.main.WorldToViewportPoint(target.position);
                float distFromCenter = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), new Vector2(0.5f, 0.5f));

                // Also factor in world distance
                float worldDist = Vector3.Distance(transform.position, target.position);

                float score = distFromCenter * 100f + worldDist;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = target;
                }
            }

            return best;
        }

        public void LockOnTo(Transform target)
        {
            if (target == null) return;

            currentTarget = target;
            isLocked = true;
            obstacleTimer = 0f;

            CreateIndicator();

            TargetLocked?.Invoke(target);
            onTargetLocked?.Invoke(target);

            Debug.Log($"[LockOn] Locked onto {target.name}");
        }

        public void ReleaseLock()
        {
            if (!isLocked) return;

            currentTarget = null;
            isLocked = false;

            DestroyIndicator();

            TargetLost?.Invoke();
            onTargetLost?.Invoke();

            Debug.Log("[LockOn] Lock released");
        }

        public void SwitchTarget(int direction)
        {
            if (!isLocked) return;

            RefreshPotentialTargets();

            if (potentialTargets.Count <= 1) return;

            // Sort by horizontal screen position
            var sortedTargets = potentialTargets
                .OrderBy(t => Camera.main.WorldToViewportPoint(t.position).x)
                .ToList();

            int currentIndex = sortedTargets.IndexOf(currentTarget);
            if (currentIndex == -1) currentIndex = 0;

            int newIndex = (currentIndex + direction + sortedTargets.Count) % sortedTargets.Count;
            Transform newTarget = sortedTargets[newIndex];

            if (newTarget != currentTarget)
            {
                currentTarget = newTarget;
                TargetSwitched?.Invoke(newTarget);
                onTargetSwitched?.Invoke(newTarget);

                Debug.Log($"[LockOn] Switched to {newTarget.name}");
            }
        }

        private void SwitchToNextValidTarget()
        {
            RefreshPotentialTargets();

            foreach (var target in potentialTargets)
            {
                if (target != currentTarget && IsTargetValid(target))
                {
                    currentTarget = target;
                    TargetSwitched?.Invoke(target);
                    onTargetSwitched?.Invoke(target);
                    return;
                }
            }

            ReleaseLock();
        }

        private bool IsTargetValid(Transform target)
        {
            if (target == null) return false;
            if (!target.gameObject.activeInHierarchy) return false;

            // Check for health component
            var health = target.GetComponent<ILockOnTarget>();
            if (health != null && !health.CanBeLocked())
            {
                return false;
            }

            return true;
        }

        private bool IsObstructed(Transform target)
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 targetPos = target.position + lockOnOffset;
            Vector3 direction = targetPos - origin;

            return Physics.Raycast(origin, direction.normalized, direction.magnitude, obstacleLayer);
        }

        private void FaceTarget()
        {
            if (currentTarget == null) return;

            Vector3 direction = currentTarget.position - transform.position;
            direction.y = 0;

            if (direction.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }

        private void UpdateCameraLookAt()
        {
            if (currentTarget == null || cameraTransform == null) return;

            Vector3 lookPoint = currentTarget.position + lockOnOffset;
            Vector3 direction = lookPoint - cameraTransform.position;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, Time.deltaTime * cameraSmoothSpeed);
        }

        private void CreateIndicator()
        {
            DestroyIndicator();

            if (lockOnIndicatorPrefab != null && currentTarget != null)
            {
                currentIndicator = Instantiate(lockOnIndicatorPrefab, currentTarget);
                currentIndicator.transform.localPosition = indicatorOffset;
            }
        }

        private void DestroyIndicator()
        {
            if (currentIndicator != null)
            {
                Destroy(currentIndicator);
                currentIndicator = null;
            }
        }

        private void UpdateIndicator()
        {
            if (currentIndicator != null && currentTarget != null)
            {
                currentIndicator.transform.position = currentTarget.position + indicatorOffset;

                // Billboard effect
                if (Camera.main != null)
                {
                    currentIndicator.transform.LookAt(Camera.main.transform);
                }
            }
        }

        /// <summary>
        /// Get direction to current target.
        /// </summary>
        public Vector3 GetDirectionToTarget()
        {
            if (currentTarget == null) return transform.forward;
            return (currentTarget.position - transform.position).normalized;
        }

        /// <summary>
        /// Get all potential targets.
        /// </summary>
        public List<Transform> GetPotentialTargets()
        {
            RefreshPotentialTargets();
            return new List<Transform>(potentialTargets);
        }

        private void OnDrawGizmosSelected()
        {
            // Detection range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            // Lock angle
            Gizmos.color = Color.green;
            Vector3 leftDir = Quaternion.Euler(0, -lockOnAngle, 0) * transform.forward;
            Vector3 rightDir = Quaternion.Euler(0, lockOnAngle, 0) * transform.forward;
            Gizmos.DrawRay(transform.position, leftDir * detectionRange);
            Gizmos.DrawRay(transform.position, rightDir * detectionRange);

            // Current target
            if (currentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, currentTarget.position + lockOnOffset);
            }
        }
    }

    /// <summary>
    /// Interface for lockable targets.
    /// </summary>
    public interface ILockOnTarget
    {
        bool CanBeLocked();
        Vector3 GetLockOnPoint();
    }
}
