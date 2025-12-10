using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.AI
{
    /// <summary>
    /// AI combat behavior with attack patterns and decision making.
    /// </summary>
    public class AICombat : MonoBehaviour
    {
        [Header("Combat Settings")]
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float optimalRange = 1.5f;
        [SerializeField] private float retreatRange = 1f;
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("Attack Patterns")]
        [SerializeField] private List<AttackPattern> attackPatterns = new List<AttackPattern>();
        [SerializeField] private bool randomizePatterns = true;
        [SerializeField] private float patternChangeInterval = 5f;

        [Header("Behavior")]
        [SerializeField] private CombatStyle combatStyle = CombatStyle.Aggressive;
        [SerializeField] private float aggressionLevel = 0.7f;
        [SerializeField] private float defensiveHealthThreshold = 0.3f;

        [Header("Timing")]
        [SerializeField] private float reactionTime = 0.2f;
        [SerializeField] private float telegraphTime = 0.5f;
        [SerializeField] private float recoveryTime = 0.8f;

        [Header("Strafing")]
        [SerializeField] private bool enableStrafing = true;
        [SerializeField] private float strafeSpeed = 3f;
        [SerializeField] private float strafeChangeInterval = 2f;

        [Header("Events")]
        [SerializeField] private UnityEvent<AttackPattern> onAttackStarted;
        [SerializeField] private UnityEvent onAttackEnded;
        [SerializeField] private UnityEvent<int> onComboStep;
        [SerializeField] private UnityEvent onBlock;
        [SerializeField] private UnityEvent onDodge;

        // State
        private CombatState currentState = CombatState.Idle;
        private Transform target;
        private float lastAttackTime;
        private float lastPatternChange;
        private float lastStrafeChange;
        private int currentPatternIndex;
        private int currentComboStep;
        private float strafeDirection = 1f;
        private AttackPattern currentPattern;

        // Components
        private Animator animator;

        // Properties
        public CombatState State => currentState;
        public bool IsAttacking => currentState == CombatState.Attacking;
        public bool CanAttack => currentState == CombatState.Idle && Time.time - lastAttackTime >= attackCooldown;
        public float DistanceToTarget => target != null ? Vector3.Distance(transform.position, target.position) : float.MaxValue;
        public bool InAttackRange => DistanceToTarget <= attackRange;

        // Events
        public event Action<AttackPattern> AttackStarted;
        public event Action AttackEnded;
        public event Action<int> ComboStepExecuted;
        public event Action BlockPerformed;
        public event Action DodgePerformed;

        public enum CombatState
        {
            Idle,
            Approaching,
            Attacking,
            Recovering,
            Blocking,
            Dodging,
            Retreating,
            Strafing
        }

        public enum CombatStyle
        {
            Aggressive,
            Defensive,
            Balanced,
            Berserker,
            Tactical
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();

            if (attackPatterns.Count == 0)
            {
                // Default pattern
                attackPatterns.Add(new AttackPattern
                {
                    patternName = "Basic Attack",
                    damage = 10f,
                    comboLength = 1,
                    attackSpeed = 1f
                });
            }

            currentPattern = attackPatterns[0];
        }

        private void Update()
        {
            if (target == null) return;

            UpdateCombatBehavior();
            UpdateStrafing();
        }

        private void UpdateCombatBehavior()
        {
            float distance = DistanceToTarget;

            switch (currentState)
            {
                case CombatState.Idle:
                    DecideNextAction(distance);
                    break;

                case CombatState.Approaching:
                    if (distance <= optimalRange)
                    {
                        currentState = CombatState.Idle;
                    }
                    break;

                case CombatState.Retreating:
                    if (distance >= optimalRange)
                    {
                        currentState = CombatState.Idle;
                    }
                    break;

                case CombatState.Attacking:
                    // Handled by animation events or coroutine
                    break;

                case CombatState.Recovering:
                    // Wait for recovery to end
                    break;
            }

            // Pattern cycling
            if (Time.time - lastPatternChange >= patternChangeInterval)
            {
                ChangePattern();
            }
        }

        private void DecideNextAction(float distance)
        {
            // Get health percentage if available
            float healthPercent = 1f;
            var health = GetComponent<Core.HealthSystem>();
            if (health != null)
            {
                healthPercent = health.CurrentHealth / health.MaxHealth;
            }

            // Defensive behavior at low health
            if (healthPercent <= defensiveHealthThreshold)
            {
                if (UnityEngine.Random.value > 0.5f)
                {
                    StartRetreat();
                    return;
                }
            }

            // Distance-based decisions
            if (distance > attackRange)
            {
                StartApproach();
            }
            else if (distance < retreatRange && combatStyle != CombatStyle.Berserker)
            {
                if (UnityEngine.Random.value > aggressionLevel)
                {
                    StartRetreat();
                }
                else
                {
                    TryAttack();
                }
            }
            else if (CanAttack)
            {
                // Decide to attack or wait based on style
                float attackChance = GetAttackChance();
                if (UnityEngine.Random.value <= attackChance)
                {
                    TryAttack();
                }
                else if (enableStrafing)
                {
                    currentState = CombatState.Strafing;
                }
            }
        }

        private float GetAttackChance()
        {
            return combatStyle switch
            {
                CombatStyle.Aggressive => 0.8f,
                CombatStyle.Defensive => 0.3f,
                CombatStyle.Balanced => 0.5f,
                CombatStyle.Berserker => 0.95f,
                CombatStyle.Tactical => 0.4f,
                _ => 0.5f
            };
        }

        private void StartApproach()
        {
            currentState = CombatState.Approaching;
        }

        private void StartRetreat()
        {
            currentState = CombatState.Retreating;
        }

        public void TryAttack()
        {
            if (!CanAttack || !InAttackRange) return;

            StartCoroutine(ExecuteAttack());
        }

        private System.Collections.IEnumerator ExecuteAttack()
        {
            currentState = CombatState.Attacking;
            lastAttackTime = Time.time;
            currentComboStep = 0;

            AttackStarted?.Invoke(currentPattern);
            onAttackStarted?.Invoke(currentPattern);

            // Telegraph (windup)
            if (animator != null)
            {
                animator.SetTrigger("Attack");
            }

            yield return new WaitForSeconds(telegraphTime);

            // Execute combo
            for (int i = 0; i < currentPattern.comboLength; i++)
            {
                if (target == null || DistanceToTarget > attackRange * 1.2f)
                {
                    break;
                }

                currentComboStep = i + 1;
                ComboStepExecuted?.Invoke(currentComboStep);
                onComboStep?.Invoke(currentComboStep);

                // Deal damage
                DealDamage(currentPattern.damage * currentPattern.comboMultipliers[Mathf.Min(i, currentPattern.comboMultipliers.Length - 1)]);

                yield return new WaitForSeconds(currentPattern.timeBetweenHits / currentPattern.attackSpeed);
            }

            // Recovery
            currentState = CombatState.Recovering;

            AttackEnded?.Invoke();
            onAttackEnded?.Invoke();

            yield return new WaitForSeconds(recoveryTime);

            currentState = CombatState.Idle;
        }

        private void DealDamage(float damage)
        {
            if (target == null) return;

            var targetHealth = target.GetComponent<Core.HealthSystem>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(damage);
                Debug.Log($"[AICombat] Dealt {damage} damage to {target.name}");
            }
        }

        private void ChangePattern()
        {
            lastPatternChange = Time.time;

            if (randomizePatterns && attackPatterns.Count > 1)
            {
                int newIndex;
                do
                {
                    newIndex = UnityEngine.Random.Range(0, attackPatterns.Count);
                } while (newIndex == currentPatternIndex);

                currentPatternIndex = newIndex;
            }
            else
            {
                currentPatternIndex = (currentPatternIndex + 1) % attackPatterns.Count;
            }

            currentPattern = attackPatterns[currentPatternIndex];
        }

        private void UpdateStrafing()
        {
            if (currentState != CombatState.Strafing && currentState != CombatState.Idle) return;
            if (!enableStrafing || target == null) return;

            // Change strafe direction periodically
            if (Time.time - lastStrafeChange >= strafeChangeInterval)
            {
                lastStrafeChange = Time.time;
                strafeDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f;
            }

            // Calculate strafe movement
            if (currentState == CombatState.Strafing)
            {
                Vector3 dirToTarget = (target.position - transform.position).normalized;
                Vector3 strafeDir = Vector3.Cross(dirToTarget, Vector3.up) * strafeDirection;

                transform.position += strafeDir * strafeSpeed * Time.deltaTime;

                // Face target
                Quaternion lookRot = Quaternion.LookRotation(dirToTarget);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 10f * Time.deltaTime);

                // Check if should attack
                if (CanAttack && UnityEngine.Random.value > 0.98f)
                {
                    TryAttack();
                }
            }
        }

        /// <summary>
        /// Attempt to block incoming attack.
        /// </summary>
        public bool TryBlock()
        {
            if (currentState != CombatState.Idle) return false;

            float blockChance = combatStyle switch
            {
                CombatStyle.Defensive => 0.7f,
                CombatStyle.Tactical => 0.5f,
                CombatStyle.Balanced => 0.3f,
                _ => 0.1f
            };

            if (UnityEngine.Random.value <= blockChance)
            {
                StartCoroutine(PerformBlock());
                return true;
            }

            return false;
        }

        private System.Collections.IEnumerator PerformBlock()
        {
            currentState = CombatState.Blocking;

            if (animator != null)
            {
                animator.SetTrigger("Block");
            }

            BlockPerformed?.Invoke();
            onBlock?.Invoke();

            yield return new WaitForSeconds(0.5f);

            currentState = CombatState.Idle;
        }

        /// <summary>
        /// Attempt to dodge.
        /// </summary>
        public bool TryDodge()
        {
            if (currentState != CombatState.Idle) return false;

            float dodgeChance = combatStyle switch
            {
                CombatStyle.Tactical => 0.6f,
                CombatStyle.Defensive => 0.4f,
                CombatStyle.Balanced => 0.3f,
                _ => 0.1f
            };

            if (UnityEngine.Random.value <= dodgeChance)
            {
                StartCoroutine(PerformDodge());
                return true;
            }

            return false;
        }

        private System.Collections.IEnumerator PerformDodge()
        {
            currentState = CombatState.Dodging;

            Vector3 dodgeDir = (UnityEngine.Random.value > 0.5f ? 1 : -1) * transform.right;
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + dodgeDir * 2f;

            if (animator != null)
            {
                animator.SetTrigger("Dodge");
            }

            DodgePerformed?.Invoke();
            onDodge?.Invoke();

            float elapsed = 0;
            float duration = 0.3f;
            while (elapsed < duration)
            {
                transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            currentState = CombatState.Idle;
        }

        // Public methods
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetCombatStyle(CombatStyle style)
        {
            combatStyle = style;
        }

        public void SetAggressionLevel(float level)
        {
            aggressionLevel = Mathf.Clamp01(level);
        }

        public void ForceAttack()
        {
            if (target != null && currentState == CombatState.Idle)
            {
                TryAttack();
            }
        }

        public void CancelAttack()
        {
            StopAllCoroutines();
            currentState = CombatState.Idle;
        }

        /// <summary>
        /// Get movement direction for this frame.
        /// </summary>
        public Vector3 GetMovementDirection()
        {
            if (target == null) return Vector3.zero;

            Vector3 dirToTarget = (target.position - transform.position).normalized;

            return currentState switch
            {
                CombatState.Approaching => dirToTarget,
                CombatState.Retreating => -dirToTarget,
                CombatState.Strafing => Vector3.Cross(dirToTarget, Vector3.up) * strafeDirection,
                _ => Vector3.zero
            };
        }

        private void OnDrawGizmosSelected()
        {
            // Attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Optimal range
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, optimalRange);

            // Retreat range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, retreatRange);

            // Target line
            if (target != null)
            {
                Gizmos.color = IsAttacking ? Color.red : Color.blue;
                Gizmos.DrawLine(transform.position, target.position);
            }
        }
    }

    [Serializable]
    public class AttackPattern
    {
        public string patternName = "Attack";
        public float damage = 10f;
        public int comboLength = 1;
        public float attackSpeed = 1f;
        public float[] comboMultipliers = { 1f };
        public float timeBetweenHits = 0.3f;
        public string animationTrigger;
        public AudioClip attackSound;
    }
}
