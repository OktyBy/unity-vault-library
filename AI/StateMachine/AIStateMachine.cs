using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace UnityVault.AI
{
    /// <summary>
    /// Finite State Machine for AI behavior.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class AIStateMachine : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private AIStateType startingState = AIStateType.Idle;
        [SerializeField] private float stateUpdateInterval = 0.1f;

        [Header("Detection")]
        [SerializeField] private float detectionRange = 10f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float fieldOfView = 120f;
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private LayerMask obstacleMask;

        [Header("Combat")]
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private float damage = 10f;

        [Header("Debug")]
        [SerializeField] private bool showDebug = true;

        // Components
        private NavMeshAgent agent;
        private Animator animator;

        // State
        private Dictionary<AIStateType, AIState> states = new Dictionary<AIStateType, AIState>();
        private AIState currentState;
        private AIStateType currentStateType;
        private Transform target;
        private float lastStateUpdate;
        private float lastAttackTime;

        // Properties
        public NavMeshAgent Agent => agent;
        public Animator Animator => animator;
        public Transform Target => target;
        public AIStateType CurrentStateType => currentStateType;
        public float DetectionRange => detectionRange;
        public float AttackRange => attackRange;
        public float Damage => damage;
        public bool CanAttack => Time.time >= lastAttackTime + attackCooldown;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();

            InitializeStates();
        }

        private void Start()
        {
            TransitionTo(startingState);
        }

        private void Update()
        {
            if (Time.time - lastStateUpdate >= stateUpdateInterval)
            {
                lastStateUpdate = Time.time;
                UpdateTarget();
                currentState?.Update(this);
            }

            currentState?.Tick(this);
        }

        private void InitializeStates()
        {
            states[AIStateType.Idle] = new IdleState();
            states[AIStateType.Patrol] = new PatrolState();
            states[AIStateType.Chase] = new ChaseState();
            states[AIStateType.Attack] = new AttackState();
            states[AIStateType.Flee] = new FleeState();
            states[AIStateType.Dead] = new DeadState();
        }

        public void TransitionTo(AIStateType newState)
        {
            if (!states.ContainsKey(newState)) return;

            currentState?.Exit(this);
            currentStateType = newState;
            currentState = states[newState];
            currentState.Enter(this);

            Debug.Log($"[AI] Transitioned to {newState}");
        }

        private void UpdateTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, targetMask);
            float closestDist = float.MaxValue;
            Transform closest = null;

            foreach (var hit in hits)
            {
                if (!IsInFieldOfView(hit.transform)) continue;
                if (!HasLineOfSight(hit.transform)) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = hit.transform;
                }
            }

            target = closest;
        }

        public bool IsInFieldOfView(Transform t)
        {
            Vector3 dirToTarget = (t.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToTarget);
            return angle <= fieldOfView / 2f;
        }

        public bool HasLineOfSight(Transform t)
        {
            Vector3 direction = t.position - transform.position;
            return !Physics.Raycast(transform.position + Vector3.up, direction.normalized, direction.magnitude, obstacleMask);
        }

        public bool IsTargetInRange(float range)
        {
            if (target == null) return false;
            return Vector3.Distance(transform.position, target.position) <= range;
        }

        public void SetDestination(Vector3 position)
        {
            agent.SetDestination(position);
        }

        public void StopMovement()
        {
            agent.ResetPath();
        }

        public void Attack()
        {
            if (!CanAttack || target == null) return;

            lastAttackTime = Time.time;

            var damageable = target.GetComponent<UnityVault.Core.IDamageable>();
            damageable?.TakeDamage(damage);

            animator?.SetTrigger("Attack");
        }

        public void Die()
        {
            TransitionTo(AIStateType.Dead);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebug) return;

            // Detection range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            // Attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Field of view
            Gizmos.color = Color.blue;
            Vector3 leftBound = Quaternion.Euler(0, -fieldOfView / 2, 0) * transform.forward * detectionRange;
            Vector3 rightBound = Quaternion.Euler(0, fieldOfView / 2, 0) * transform.forward * detectionRange;
            Gizmos.DrawLine(transform.position, transform.position + leftBound);
            Gizmos.DrawLine(transform.position, transform.position + rightBound);
        }
    }

    public enum AIStateType
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Flee,
        Dead
    }

    public abstract class AIState
    {
        public virtual void Enter(AIStateMachine ai) { }
        public virtual void Update(AIStateMachine ai) { }
        public virtual void Tick(AIStateMachine ai) { }
        public virtual void Exit(AIStateMachine ai) { }
    }

    public class IdleState : AIState
    {
        public override void Update(AIStateMachine ai)
        {
            if (ai.Target != null)
            {
                ai.TransitionTo(AIStateType.Chase);
            }
        }
    }

    public class PatrolState : AIState
    {
        public override void Update(AIStateMachine ai)
        {
            if (ai.Target != null)
            {
                ai.TransitionTo(AIStateType.Chase);
            }
        }
    }

    public class ChaseState : AIState
    {
        public override void Enter(AIStateMachine ai)
        {
            ai.Animator?.SetBool("IsRunning", true);
        }

        public override void Tick(AIStateMachine ai)
        {
            if (ai.Target != null)
            {
                ai.SetDestination(ai.Target.position);
            }
        }

        public override void Update(AIStateMachine ai)
        {
            if (ai.Target == null)
            {
                ai.TransitionTo(AIStateType.Idle);
                return;
            }

            if (ai.IsTargetInRange(ai.AttackRange))
            {
                ai.TransitionTo(AIStateType.Attack);
            }
        }

        public override void Exit(AIStateMachine ai)
        {
            ai.Animator?.SetBool("IsRunning", false);
        }
    }

    public class AttackState : AIState
    {
        public override void Enter(AIStateMachine ai)
        {
            ai.StopMovement();
        }

        public override void Update(AIStateMachine ai)
        {
            if (ai.Target == null)
            {
                ai.TransitionTo(AIStateType.Idle);
                return;
            }

            if (!ai.IsTargetInRange(ai.AttackRange))
            {
                ai.TransitionTo(AIStateType.Chase);
                return;
            }

            if (ai.CanAttack)
            {
                ai.Attack();
            }
        }
    }

    public class FleeState : AIState
    {
        public override void Tick(AIStateMachine ai)
        {
            if (ai.Target != null)
            {
                Vector3 fleeDir = (ai.transform.position - ai.Target.position).normalized;
                ai.SetDestination(ai.transform.position + fleeDir * 10f);
            }
        }
    }

    public class DeadState : AIState
    {
        public override void Enter(AIStateMachine ai)
        {
            ai.Agent.enabled = false;
            ai.Animator?.SetTrigger("Die");
        }
    }
}
