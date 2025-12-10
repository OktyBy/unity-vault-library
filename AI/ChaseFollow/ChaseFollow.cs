using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.AI
{
    /// <summary>
    /// AI chase and follow behavior with NavMesh support.
    /// </summary>
    public class ChaseFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private string targetTag = "Player";
        [SerializeField] private bool autoFindTarget = true;

        [Header("Chase Settings")]
        [SerializeField] private float chaseSpeed = 5f;
        [SerializeField] private float chaseRange = 15f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float giveUpDistance = 25f;

        [Header("Follow Settings")]
        [SerializeField] private float followDistance = 3f;
        [SerializeField] private float followSpeed = 4f;
        [SerializeField] private bool maintainDistance = true;

        [Header("Movement")]
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float acceleration = 8f;
        [SerializeField] private bool useNavMesh = true;
        [SerializeField] private float pathUpdateInterval = 0.2f;
        [SerializeField] private float stoppingDistance = 0.5f;

        [Header("Prediction")]
        [SerializeField] private bool predictMovement = true;
        [SerializeField] private float predictionTime = 0.5f;

        [Header("Events")]
        [SerializeField] private UnityEvent onChaseStarted;
        [SerializeField] private UnityEvent onChaseLost;
        [SerializeField] private UnityEvent onTargetReached;
        [SerializeField] private UnityEvent onTargetInAttackRange;

        // State
        private ChaseState currentState = ChaseState.Idle;
        private float lastPathUpdate;
        private Vector3 currentVelocity;
        private Vector3 lastTargetPosition;

        // Components
        private UnityEngine.AI.NavMeshAgent navAgent;
        private Rigidbody rb;
        private CharacterController charController;

        // Properties
        public ChaseState State => currentState;
        public Transform Target => target;
        public float DistanceToTarget => target != null ? Vector3.Distance(transform.position, target.position) : float.MaxValue;
        public bool IsChasing => currentState == ChaseState.Chasing;
        public bool IsFollowing => currentState == ChaseState.Following;
        public bool IsInAttackRange => DistanceToTarget <= attackRange;

        // Events
        public event Action ChaseStarted;
        public event Action ChaseLost;
        public event Action TargetReached;
        public event Action TargetInAttackRange;

        public enum ChaseState
        {
            Idle,
            Chasing,
            Following,
            Attacking,
            Returning
        }

        private void Awake()
        {
            navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            rb = GetComponent<Rigidbody>();
            charController = GetComponent<CharacterController>();

            if (navAgent != null)
            {
                navAgent.stoppingDistance = stoppingDistance;
            }
        }

        private void Start()
        {
            if (autoFindTarget && target == null)
            {
                FindTarget();
            }
        }

        private void Update()
        {
            if (target == null)
            {
                if (autoFindTarget)
                {
                    FindTarget();
                }
                return;
            }

            UpdateState();
            UpdateMovement();
        }

        private void FindTarget()
        {
            GameObject found = GameObject.FindGameObjectWithTag(targetTag);
            if (found != null)
            {
                target = found.transform;
            }
        }

        private void UpdateState()
        {
            float distance = DistanceToTarget;

            switch (currentState)
            {
                case ChaseState.Idle:
                    if (distance <= chaseRange)
                    {
                        StartChase();
                    }
                    break;

                case ChaseState.Chasing:
                    if (distance > giveUpDistance)
                    {
                        LoseTarget();
                    }
                    else if (distance <= attackRange)
                    {
                        EnterAttackRange();
                    }
                    break;

                case ChaseState.Following:
                    if (distance > giveUpDistance)
                    {
                        LoseTarget();
                    }
                    break;

                case ChaseState.Attacking:
                    if (distance > attackRange * 1.2f)
                    {
                        currentState = ChaseState.Chasing;
                    }
                    break;
            }
        }

        private void UpdateMovement()
        {
            if (currentState == ChaseState.Idle || currentState == ChaseState.Attacking)
            {
                StopMovement();
                return;
            }

            // Update path periodically
            if (Time.time - lastPathUpdate >= pathUpdateInterval)
            {
                lastPathUpdate = Time.time;
                UpdatePath();
            }

            // Non-NavMesh movement
            if (!useNavMesh || navAgent == null || !navAgent.enabled)
            {
                MoveWithoutNavMesh();
            }

            // Face target
            FaceTarget();
        }

        private void UpdatePath()
        {
            if (target == null) return;

            Vector3 targetPos = GetTargetPosition();

            if (useNavMesh && navAgent != null && navAgent.enabled)
            {
                navAgent.speed = currentState == ChaseState.Chasing ? chaseSpeed : followSpeed;
                navAgent.SetDestination(targetPos);
            }

            lastTargetPosition = target.position;
        }

        private Vector3 GetTargetPosition()
        {
            Vector3 targetPos = target.position;

            // Predict movement
            if (predictMovement && lastTargetPosition != Vector3.zero)
            {
                Vector3 targetVelocity = (target.position - lastTargetPosition) / pathUpdateInterval;
                targetPos += targetVelocity * predictionTime;
            }

            // Maintain follow distance
            if (currentState == ChaseState.Following && maintainDistance)
            {
                Vector3 dirFromTarget = (transform.position - target.position).normalized;
                targetPos = target.position + dirFromTarget * followDistance;
            }

            return targetPos;
        }

        private void MoveWithoutNavMesh()
        {
            if (target == null) return;

            Vector3 targetPos = GetTargetPosition();
            Vector3 direction = (targetPos - transform.position).normalized;
            float speed = currentState == ChaseState.Chasing ? chaseSpeed : followSpeed;

            // Accelerate
            Vector3 targetVelocity = direction * speed;
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * Time.deltaTime);

            // Apply movement
            if (charController != null)
            {
                charController.Move(currentVelocity * Time.deltaTime);
            }
            else if (rb != null)
            {
                rb.MovePosition(transform.position + currentVelocity * Time.deltaTime);
            }
            else
            {
                transform.position += currentVelocity * Time.deltaTime;
            }
        }

        private void FaceTarget()
        {
            if (target == null) return;

            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0;

            if (direction.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        private void StopMovement()
        {
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
            }
            currentVelocity = Vector3.zero;
        }

        private void StartChase()
        {
            currentState = ChaseState.Chasing;

            if (navAgent != null)
            {
                navAgent.isStopped = false;
            }

            ChaseStarted?.Invoke();
            onChaseStarted?.Invoke();
            Debug.Log("[ChaseFollow] Chase started!");
        }

        private void LoseTarget()
        {
            currentState = ChaseState.Idle;
            StopMovement();

            ChaseLost?.Invoke();
            onChaseLost?.Invoke();
            Debug.Log("[ChaseFollow] Lost target");
        }

        private void EnterAttackRange()
        {
            currentState = ChaseState.Attacking;
            StopMovement();

            TargetInAttackRange?.Invoke();
            onTargetInAttackRange?.Invoke();
            Debug.Log("[ChaseFollow] Target in attack range!");
        }

        // Public methods
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            lastTargetPosition = Vector3.zero;
        }

        public void StartChasing()
        {
            if (target != null)
            {
                StartChase();
            }
        }

        public void StartFollowing()
        {
            if (target != null)
            {
                currentState = ChaseState.Following;
                if (navAgent != null)
                {
                    navAgent.isStopped = false;
                }
            }
        }

        public void Stop()
        {
            currentState = ChaseState.Idle;
            StopMovement();
        }

        public void SetChaseSpeed(float speed) => chaseSpeed = speed;
        public void SetFollowSpeed(float speed) => followSpeed = speed;
        public void SetChaseRange(float range) => chaseRange = range;
        public void SetAttackRange(float range) => attackRange = range;

        /// <summary>
        /// Check if can see target (line of sight).
        /// </summary>
        public bool CanSeeTarget(LayerMask obstacleLayer)
        {
            if (target == null) return false;

            Vector3 direction = target.position - transform.position;
            return !Physics.Raycast(transform.position + Vector3.up, direction.normalized, direction.magnitude, obstacleLayer);
        }

        private void OnDrawGizmosSelected()
        {
            // Chase range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, chaseRange);

            // Attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Give up distance
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, giveUpDistance);

            // Follow distance
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, followDistance);

            // Line to target
            if (target != null)
            {
                Gizmos.color = IsChasing ? Color.red : Color.blue;
                Gizmos.DrawLine(transform.position, target.position);
            }
        }
    }
}
