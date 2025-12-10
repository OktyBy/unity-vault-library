using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.AI
{
    /// <summary>
    /// Companion AI that follows and assists the player.
    /// </summary>
    public class CompanionAI : MonoBehaviour
    {
        [Header("Owner Reference")]
        [SerializeField] private Transform owner;
        [SerializeField] private string ownerTag = "Player";
        [SerializeField] private bool autoFindOwner = true;

        [Header("Follow Settings")]
        [SerializeField] private float followDistance = 3f;
        [SerializeField] private float maxDistance = 15f;
        [SerializeField] private float followSpeed = 5f;
        [SerializeField] private float teleportDistance = 25f;
        [SerializeField] private Vector3 followOffset = new Vector3(-1f, 0, -1f);

        [Header("Combat Assist")]
        [SerializeField] private bool assistInCombat = true;
        [SerializeField] private float assistRange = 10f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackDamage = 10f;
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("Behavior")]
        [SerializeField] private CompanionMode currentMode = CompanionMode.Follow;
        [SerializeField] private float idleWanderRadius = 3f;
        [SerializeField] private float interactionDistance = 2f;

        [Header("Abilities")]
        [SerializeField] private List<CompanionAbility> abilities = new List<CompanionAbility>();
        [SerializeField] private bool autoUseAbilities = true;

        [Header("Events")]
        [SerializeField] private UnityEvent<CompanionMode> onModeChanged;
        [SerializeField] private UnityEvent<Transform> onCombatTargetChanged;
        [SerializeField] private UnityEvent onOwnerInDanger;
        [SerializeField] private UnityEvent<CompanionAbility> onAbilityUsed;

        // State
        private CompanionState currentState = CompanionState.Idle;
        private Transform combatTarget;
        private float lastAttackTime;
        private Vector3 wanderTarget;
        private float wanderTimer;

        // Components
        private UnityEngine.AI.NavMeshAgent navAgent;
        private Animator animator;

        // Properties
        public Transform Owner => owner;
        public CompanionMode Mode => currentMode;
        public CompanionState State => currentState;
        public Transform CombatTarget => combatTarget;
        public float DistanceToOwner => owner != null ? Vector3.Distance(transform.position, owner.position) : 0f;
        public bool IsInCombat => currentState == CompanionState.Combat;

        // Events
        public event Action<CompanionMode> ModeChanged;
        public event Action<Transform> CombatTargetChanged;
        public event Action OwnerInDanger;
        public event Action<CompanionAbility> AbilityUsed;

        public enum CompanionMode
        {
            Follow,
            Stay,
            Defend,
            Aggressive,
            Passive
        }

        public enum CompanionState
        {
            Idle,
            Following,
            Combat,
            UsingAbility,
            Wandering,
            Returning
        }

        private void Awake()
        {
            navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            animator = GetComponent<Animator>();
        }

        private void Start()
        {
            if (autoFindOwner && owner == null)
            {
                FindOwner();
            }
        }

        private void Update()
        {
            if (owner == null)
            {
                if (autoFindOwner) FindOwner();
                return;
            }

            UpdateState();
            UpdateBehavior();
            CheckAbilities();
        }

        private void FindOwner()
        {
            GameObject ownerObj = GameObject.FindGameObjectWithTag(ownerTag);
            if (ownerObj != null)
            {
                owner = ownerObj.transform;
            }
        }

        private void UpdateState()
        {
            float distanceToOwner = DistanceToOwner;

            // Teleport if too far
            if (distanceToOwner > teleportDistance)
            {
                TeleportToOwner();
                return;
            }

            // Check for combat
            if (assistInCombat && currentMode != CompanionMode.Passive)
            {
                UpdateCombatTarget();
            }

            // Determine state based on mode
            switch (currentMode)
            {
                case CompanionMode.Follow:
                    if (combatTarget != null && currentMode != CompanionMode.Passive)
                    {
                        currentState = CompanionState.Combat;
                    }
                    else if (distanceToOwner > followDistance)
                    {
                        currentState = CompanionState.Following;
                    }
                    else
                    {
                        currentState = CompanionState.Idle;
                    }
                    break;

                case CompanionMode.Stay:
                    if (combatTarget != null && currentMode == CompanionMode.Defend)
                    {
                        currentState = CompanionState.Combat;
                    }
                    else
                    {
                        currentState = CompanionState.Idle;
                    }
                    break;

                case CompanionMode.Defend:
                    if (combatTarget != null)
                    {
                        currentState = CompanionState.Combat;
                    }
                    else if (distanceToOwner > followDistance * 2f)
                    {
                        currentState = CompanionState.Returning;
                    }
                    else
                    {
                        currentState = CompanionState.Idle;
                    }
                    break;

                case CompanionMode.Aggressive:
                    if (combatTarget != null)
                    {
                        currentState = CompanionState.Combat;
                    }
                    else
                    {
                        currentState = CompanionState.Following;
                    }
                    break;

                case CompanionMode.Passive:
                    currentState = distanceToOwner > followDistance ? CompanionState.Following : CompanionState.Idle;
                    break;
            }
        }

        private void UpdateBehavior()
        {
            switch (currentState)
            {
                case CompanionState.Idle:
                    UpdateIdle();
                    break;

                case CompanionState.Following:
                    UpdateFollowing();
                    break;

                case CompanionState.Combat:
                    UpdateCombat();
                    break;

                case CompanionState.Wandering:
                    UpdateWandering();
                    break;

                case CompanionState.Returning:
                    UpdateReturning();
                    break;
            }

            UpdateAnimation();
        }

        private void UpdateIdle()
        {
            // Occasional wandering
            wanderTimer -= Time.deltaTime;
            if (wanderTimer <= 0)
            {
                wanderTimer = UnityEngine.Random.Range(3f, 8f);

                if (UnityEngine.Random.value > 0.7f)
                {
                    StartWandering();
                }
            }

            // Face owner
            if (owner != null)
            {
                FaceTarget(owner.position);
            }
        }

        private void UpdateFollowing()
        {
            if (owner == null) return;

            Vector3 targetPos = GetFollowPosition();

            if (navAgent != null && navAgent.enabled)
            {
                navAgent.speed = followSpeed;
                navAgent.SetDestination(targetPos);
            }
            else
            {
                Vector3 direction = (targetPos - transform.position).normalized;
                transform.position += direction * followSpeed * Time.deltaTime;
            }

            FaceTarget(owner.position);
        }

        private Vector3 GetFollowPosition()
        {
            // Position behind and to the side of owner
            Vector3 offset = owner.TransformDirection(followOffset);
            return owner.position + offset;
        }

        private void UpdateCombat()
        {
            if (combatTarget == null)
            {
                currentState = CompanionState.Idle;
                return;
            }

            float distance = Vector3.Distance(transform.position, combatTarget.position);

            if (distance > attackRange)
            {
                // Move towards target
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.speed = followSpeed * 1.2f;
                    navAgent.SetDestination(combatTarget.position);
                }
                else
                {
                    Vector3 direction = (combatTarget.position - transform.position).normalized;
                    transform.position += direction * followSpeed * 1.2f * Time.deltaTime;
                }
            }
            else
            {
                // Attack
                if (Time.time - lastAttackTime >= attackCooldown)
                {
                    Attack();
                }
            }

            FaceTarget(combatTarget.position);
        }

        private void UpdateWandering()
        {
            if (navAgent != null && navAgent.enabled)
            {
                if (navAgent.remainingDistance < 0.5f)
                {
                    currentState = CompanionState.Idle;
                }
            }
            else
            {
                float dist = Vector3.Distance(transform.position, wanderTarget);
                if (dist < 0.5f)
                {
                    currentState = CompanionState.Idle;
                }
                else
                {
                    Vector3 direction = (wanderTarget - transform.position).normalized;
                    transform.position += direction * followSpeed * 0.5f * Time.deltaTime;
                }
            }
        }

        private void UpdateReturning()
        {
            if (owner == null) return;

            Vector3 targetPos = GetFollowPosition();

            if (navAgent != null && navAgent.enabled)
            {
                navAgent.speed = followSpeed * 1.5f;
                navAgent.SetDestination(targetPos);
            }

            if (DistanceToOwner <= followDistance)
            {
                currentState = CompanionState.Idle;
            }
        }

        private void StartWandering()
        {
            if (owner == null) return;

            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * idleWanderRadius;
            wanderTarget = owner.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            if (navAgent != null && navAgent.enabled)
            {
                navAgent.SetDestination(wanderTarget);
            }

            currentState = CompanionState.Wandering;
        }

        private void UpdateCombatTarget()
        {
            // Look for threats to owner
            Collider[] enemies = Physics.OverlapSphere(owner.position, assistRange);
            Transform closestEnemy = null;
            float closestDist = float.MaxValue;

            foreach (var col in enemies)
            {
                if (col.transform == transform || col.transform == owner) continue;

                // Check if it's an enemy (you might want to use tags or layers)
                var health = col.GetComponent<Core.HealthSystem>();
                if (health != null)
                {
                    float dist = Vector3.Distance(owner.position, col.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestEnemy = col.transform;
                    }
                }
            }

            if (closestEnemy != combatTarget)
            {
                combatTarget = closestEnemy;
                CombatTargetChanged?.Invoke(combatTarget);
                onCombatTargetChanged?.Invoke(combatTarget);
            }
        }

        private void Attack()
        {
            if (combatTarget == null) return;

            lastAttackTime = Time.time;

            var targetHealth = combatTarget.GetComponent<Core.HealthSystem>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(attackDamage);
            }

            if (animator != null)
            {
                animator.SetTrigger("Attack");
            }

            Debug.Log($"[CompanionAI] Attacked {combatTarget.name} for {attackDamage} damage");
        }

        private void CheckAbilities()
        {
            if (!autoUseAbilities || currentState == CompanionState.UsingAbility) return;

            foreach (var ability in abilities)
            {
                if (ability.CanUse() && ShouldUseAbility(ability))
                {
                    UseAbility(ability);
                    break;
                }
            }
        }

        private bool ShouldUseAbility(CompanionAbility ability)
        {
            switch (ability.triggerCondition)
            {
                case AbilityTrigger.OwnerLowHealth:
                    var ownerHealth = owner?.GetComponent<Core.HealthSystem>();
                    if (ownerHealth != null)
                    {
                        return ownerHealth.CurrentHealth / ownerHealth.MaxHealth <= ability.triggerThreshold;
                    }
                    break;

                case AbilityTrigger.InCombat:
                    return IsInCombat;

                case AbilityTrigger.Manual:
                    return false;
            }

            return false;
        }

        private void UseAbility(CompanionAbility ability)
        {
            StartCoroutine(UseAbilityCoroutine(ability));
        }

        private System.Collections.IEnumerator UseAbilityCoroutine(CompanionAbility ability)
        {
            currentState = CompanionState.UsingAbility;

            ability.Use();

            AbilityUsed?.Invoke(ability);
            onAbilityUsed?.Invoke(ability);

            Debug.Log($"[CompanionAI] Used ability: {ability.abilityName}");

            yield return new WaitForSeconds(ability.castTime);

            // Apply ability effect
            if (ability.targetOwner && owner != null)
            {
                var ownerHealth = owner.GetComponent<Core.HealthSystem>();
                if (ownerHealth != null && ability.healAmount > 0)
                {
                    ownerHealth.Heal(ability.healAmount);
                }
            }

            currentState = CompanionState.Idle;
        }

        private void TeleportToOwner()
        {
            if (owner == null) return;

            Vector3 targetPos = GetFollowPosition();

            if (navAgent != null)
            {
                navAgent.Warp(targetPos);
            }
            else
            {
                transform.position = targetPos;
            }

            Debug.Log("[CompanionAI] Teleported to owner");
        }

        private void FaceTarget(Vector3 targetPos)
        {
            Vector3 direction = (targetPos - transform.position).normalized;
            direction.y = 0;

            if (direction.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
            }
        }

        private void UpdateAnimation()
        {
            if (animator == null) return;

            float speed = 0f;
            if (navAgent != null)
            {
                speed = navAgent.velocity.magnitude / followSpeed;
            }

            animator.SetFloat("Speed", speed);
            animator.SetBool("InCombat", IsInCombat);
        }

        // Public methods
        public void SetMode(CompanionMode mode)
        {
            currentMode = mode;
            ModeChanged?.Invoke(mode);
            onModeChanged?.Invoke(mode);
            Debug.Log($"[CompanionAI] Mode changed to: {mode}");
        }

        public void SetOwner(Transform newOwner)
        {
            owner = newOwner;
        }

        public void CommandAttack(Transform target)
        {
            combatTarget = target;
            currentState = CompanionState.Combat;
        }

        public void CommandFollow()
        {
            SetMode(CompanionMode.Follow);
            combatTarget = null;
        }

        public void CommandStay()
        {
            SetMode(CompanionMode.Stay);
        }

        public void TriggerAbility(int index)
        {
            if (index >= 0 && index < abilities.Count)
            {
                UseAbility(abilities[index]);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Follow distance
            if (owner != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(owner.position, followDistance);

                // Follow position
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(GetFollowPosition(), 0.3f);
            }

            // Assist range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, assistRange);

            // Attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }

    public enum AbilityTrigger
    {
        Manual,
        OwnerLowHealth,
        InCombat,
        OnCooldown
    }

    [Serializable]
    public class CompanionAbility
    {
        public string abilityName = "Ability";
        public float cooldown = 30f;
        public float castTime = 1f;
        public AbilityTrigger triggerCondition = AbilityTrigger.Manual;
        public float triggerThreshold = 0.3f;
        public bool targetOwner = true;
        public float healAmount = 0f;
        public float damageAmount = 0f;

        private float lastUseTime = -999f;

        public bool CanUse() => Time.time - lastUseTime >= cooldown;

        public void Use()
        {
            lastUseTime = Time.time;
        }
    }
}
