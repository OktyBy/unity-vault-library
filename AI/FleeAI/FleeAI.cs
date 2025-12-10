using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.AI
{
    /// <summary>
    /// AI flee behavior - run away from threats.
    /// </summary>
    public class FleeAI : MonoBehaviour
    {
        [Header("Threat Detection")]
        [SerializeField] private Transform primaryThreat;
        [SerializeField] private float threatDetectionRange = 10f;
        [SerializeField] private LayerMask threatLayer;
        [SerializeField] private string threatTag = "Player";

        [Header("Flee Settings")]
        [SerializeField] private float fleeSpeed = 6f;
        [SerializeField] private float safeDistance = 15f;
        [SerializeField] private float panicDistance = 5f; // Extra fast when very close
        [SerializeField] private float panicSpeedMultiplier = 1.5f;

        [Header("Behavior")]
        [SerializeField] private FleeMode fleeMode = FleeMode.DirectAway;
        [SerializeField] private bool seekCover = true;
        [SerializeField] private LayerMask coverLayer;
        [SerializeField] private float coverSearchRadius = 10f;

        [Header("Movement")]
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private bool useNavMesh = true;
        [SerializeField] private float pathUpdateInterval = 0.3f;

        [Header("Recovery")]
        [SerializeField] private float calmDownTime = 3f;
        [SerializeField] private bool returnToOrigin = false;
        [SerializeField] private float returnDelay = 5f;

        [Header("Events")]
        [SerializeField] private UnityEvent onFleeStarted;
        [SerializeField] private UnityEvent onFleeStopped;
        [SerializeField] private UnityEvent onReachedSafety;
        [SerializeField] private UnityEvent onPanic;
        [SerializeField] private UnityEvent onCoverFound;

        // State
        private FleeState currentState = FleeState.Idle;
        private Vector3 originPosition;
        private Vector3 fleeDestination;
        private float lastPathUpdate;
        private float safeTimer;
        private List<Transform> nearbyThreats = new List<Transform>();

        // Components
        private UnityEngine.AI.NavMeshAgent navAgent;
        private Rigidbody rb;

        // Properties
        public FleeState State => currentState;
        public bool IsFleeing => currentState == FleeState.Fleeing || currentState == FleeState.Panicking;
        public float DistanceToThreat => primaryThreat != null ? Vector3.Distance(transform.position, primaryThreat.position) : float.MaxValue;
        public bool IsSafe => DistanceToThreat >= safeDistance;

        // Events
        public event Action FleeStarted;
        public event Action FleeStopped;
        public event Action ReachedSafety;
        public event Action Panicking;
        public event Action CoverFound;

        public enum FleeState
        {
            Idle,
            Fleeing,
            Panicking,
            SeekingCover,
            InCover,
            Returning
        }

        public enum FleeMode
        {
            DirectAway,      // Straight away from threat
            SmartFlee,       // Consider obstacles and find best path
            ZigZag,          // Unpredictable movement
            TowardsCover     // Always try to find cover
        }

        private void Awake()
        {
            navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            rb = GetComponent<Rigidbody>();
            originPosition = transform.position;
        }

        private void Update()
        {
            DetectThreats();
            UpdateState();
            UpdateMovement();
        }

        private void DetectThreats()
        {
            nearbyThreats.Clear();

            Collider[] colliders = Physics.OverlapSphere(transform.position, threatDetectionRange, threatLayer);

            foreach (var col in colliders)
            {
                if (col.CompareTag(threatTag))
                {
                    nearbyThreats.Add(col.transform);
                }
            }

            // Set primary threat to closest
            if (nearbyThreats.Count > 0)
            {
                float closestDist = float.MaxValue;
                Transform closest = null;

                foreach (var threat in nearbyThreats)
                {
                    float dist = Vector3.Distance(transform.position, threat.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = threat;
                    }
                }

                primaryThreat = closest;
            }
        }

        private void UpdateState()
        {
            float threatDistance = DistanceToThreat;

            switch (currentState)
            {
                case FleeState.Idle:
                    if (threatDistance <= threatDetectionRange)
                    {
                        StartFlee();
                    }
                    break;

                case FleeState.Fleeing:
                    if (threatDistance <= panicDistance)
                    {
                        EnterPanic();
                    }
                    else if (threatDistance >= safeDistance)
                    {
                        safeTimer += Time.deltaTime;
                        if (safeTimer >= calmDownTime)
                        {
                            ReachSafety();
                        }
                    }
                    else
                    {
                        safeTimer = 0f;
                    }
                    break;

                case FleeState.Panicking:
                    if (threatDistance > panicDistance * 1.5f)
                    {
                        currentState = FleeState.Fleeing;
                    }
                    break;

                case FleeState.InCover:
                    if (threatDistance <= panicDistance)
                    {
                        // Threat found cover, flee again
                        StartFlee();
                    }
                    else if (threatDistance >= safeDistance)
                    {
                        safeTimer += Time.deltaTime;
                        if (safeTimer >= calmDownTime)
                        {
                            ReachSafety();
                        }
                    }
                    break;

                case FleeState.Returning:
                    if (threatDistance <= threatDetectionRange)
                    {
                        StartFlee();
                    }
                    else if (Vector3.Distance(transform.position, originPosition) < 1f)
                    {
                        currentState = FleeState.Idle;
                    }
                    break;
            }
        }

        private void UpdateMovement()
        {
            if (currentState == FleeState.Idle || currentState == FleeState.InCover)
            {
                StopMovement();
                return;
            }

            if (Time.time - lastPathUpdate >= pathUpdateInterval)
            {
                lastPathUpdate = Time.time;
                CalculateFleeDestination();
            }

            MoveToDestination();
        }

        private void CalculateFleeDestination()
        {
            if (primaryThreat == null) return;

            Vector3 directionAway = (transform.position - primaryThreat.position).normalized;

            switch (fleeMode)
            {
                case FleeMode.DirectAway:
                    fleeDestination = transform.position + directionAway * safeDistance;
                    break;

                case FleeMode.SmartFlee:
                    fleeDestination = FindSmartFleePoint(directionAway);
                    break;

                case FleeMode.ZigZag:
                    float zigzag = Mathf.Sin(Time.time * 3f) * 45f;
                    Vector3 zigzagDir = Quaternion.Euler(0, zigzag, 0) * directionAway;
                    fleeDestination = transform.position + zigzagDir * safeDistance;
                    break;

                case FleeMode.TowardsCover:
                    if (seekCover)
                    {
                        Vector3? cover = FindCover();
                        if (cover.HasValue)
                        {
                            fleeDestination = cover.Value;
                            break;
                        }
                    }
                    fleeDestination = transform.position + directionAway * safeDistance;
                    break;
            }

            // Validate destination
            if (useNavMesh && navAgent != null)
            {
                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(fleeDestination, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    fleeDestination = hit.position;
                }
            }
        }

        private Vector3 FindSmartFleePoint(Vector3 baseDirection)
        {
            // Try multiple directions and pick best
            float[] angles = { 0, 30, -30, 60, -60, 90, -90 };
            Vector3 bestPoint = transform.position + baseDirection * safeDistance;
            float bestScore = 0;

            foreach (float angle in angles)
            {
                Vector3 dir = Quaternion.Euler(0, angle, 0) * baseDirection;
                Vector3 point = transform.position + dir * safeDistance;

                float score = CalculateFleePointScore(point);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPoint = point;
                }
            }

            return bestPoint;
        }

        private float CalculateFleePointScore(Vector3 point)
        {
            float score = 0;

            // Distance from threat
            if (primaryThreat != null)
            {
                score += Vector3.Distance(point, primaryThreat.position);
            }

            // Check if reachable
            if (useNavMesh)
            {
                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(point, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    score += 10;
                }
                else
                {
                    score -= 50; // Penalty for unreachable
                }
            }

            // Check for cover nearby
            if (seekCover)
            {
                Collider[] covers = Physics.OverlapSphere(point, 3f, coverLayer);
                score += covers.Length * 5;
            }

            return score;
        }

        private Vector3? FindCover()
        {
            Collider[] covers = Physics.OverlapSphere(transform.position, coverSearchRadius, coverLayer);

            if (covers.Length == 0) return null;

            Vector3? bestCover = null;
            float bestScore = float.MinValue;

            foreach (var cover in covers)
            {
                Vector3 coverPos = cover.ClosestPoint(transform.position);

                // Score based on distance from threat and distance from self
                float distFromThreat = primaryThreat != null ? Vector3.Distance(coverPos, primaryThreat.position) : 0;
                float distFromSelf = Vector3.Distance(coverPos, transform.position);

                float score = distFromThreat - distFromSelf * 0.5f;

                // Check line of sight to threat
                if (primaryThreat != null)
                {
                    Vector3 dirToThreat = primaryThreat.position - coverPos;
                    if (Physics.Raycast(coverPos, dirToThreat.normalized, dirToThreat.magnitude, coverLayer))
                    {
                        score += 20; // Behind cover from threat
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCover = coverPos;
                }
            }

            if (bestCover.HasValue)
            {
                CoverFound?.Invoke();
                onCoverFound?.Invoke();
            }

            return bestCover;
        }

        private void MoveToDestination()
        {
            float speed = currentState == FleeState.Panicking ? fleeSpeed * panicSpeedMultiplier : fleeSpeed;

            if (useNavMesh && navAgent != null && navAgent.enabled)
            {
                navAgent.speed = speed;
                navAgent.SetDestination(fleeDestination);
            }
            else
            {
                Vector3 direction = (fleeDestination - transform.position).normalized;
                Vector3 movement = direction * speed * Time.deltaTime;

                if (rb != null)
                {
                    rb.MovePosition(transform.position + movement);
                }
                else
                {
                    transform.position += movement;
                }
            }

            // Face away from threat
            if (primaryThreat != null)
            {
                Vector3 lookDir = transform.position - primaryThreat.position;
                lookDir.y = 0;
                if (lookDir.magnitude > 0.1f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                }
            }
        }

        private void StopMovement()
        {
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
            }
        }

        private void StartFlee()
        {
            currentState = FleeState.Fleeing;
            safeTimer = 0f;

            if (navAgent != null)
            {
                navAgent.isStopped = false;
            }

            FleeStarted?.Invoke();
            onFleeStarted?.Invoke();
            Debug.Log("[FleeAI] Fleeing!");
        }

        private void EnterPanic()
        {
            currentState = FleeState.Panicking;

            Panicking?.Invoke();
            onPanic?.Invoke();
            Debug.Log("[FleeAI] PANIC!");
        }

        private void ReachSafety()
        {
            currentState = returnToOrigin ? FleeState.Returning : FleeState.Idle;
            StopMovement();

            if (returnToOrigin && navAgent != null)
            {
                navAgent.isStopped = false;
                navAgent.SetDestination(originPosition);
            }

            ReachedSafety?.Invoke();
            onReachedSafety?.Invoke();
            FleeStopped?.Invoke();
            onFleeStopped?.Invoke();

            Debug.Log("[FleeAI] Reached safety");
        }

        // Public methods
        public void SetThreat(Transform threat)
        {
            primaryThreat = threat;
        }

        public void ForceFlee()
        {
            StartFlee();
        }

        public void StopFleeing()
        {
            currentState = FleeState.Idle;
            StopMovement();
            FleeStopped?.Invoke();
            onFleeStopped?.Invoke();
        }

        public void SetFleeSpeed(float speed) => fleeSpeed = speed;
        public void SetSafeDistance(float distance) => safeDistance = distance;

        private void OnDrawGizmosSelected()
        {
            // Threat detection range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, threatDetectionRange);

            // Safe distance
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, safeDistance);

            // Panic distance
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, panicDistance);

            // Flee destination
            if (IsFleeing)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, fleeDestination);
                Gizmos.DrawSphere(fleeDestination, 0.5f);
            }

            // Cover search radius
            if (seekCover)
            {
                Gizmos.color = new Color(0, 1, 0, 0.2f);
                Gizmos.DrawWireSphere(transform.position, coverSearchRadius);
            }
        }
    }
}
