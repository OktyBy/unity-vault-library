using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.AI
{
    /// <summary>
    /// Boss AI system with phases, patterns, and special attacks.
    /// </summary>
    public class BossAI : MonoBehaviour
    {
        [Header("Boss Settings")]
        [SerializeField] private string bossName = "Boss";
        [SerializeField] private List<BossPhase> phases = new List<BossPhase>();
        [SerializeField] private float enrageTimer = 300f; // 5 minutes
        [SerializeField] private bool enrageOnLowHealth = true;
        [SerializeField] private float enrageHealthThreshold = 0.1f;

        [Header("Combat")]
        [SerializeField] private Transform target;
        [SerializeField] private float attackRange = 5f;
        [SerializeField] private float baseDamage = 20f;
        [SerializeField] private float attackCooldown = 2f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float rotationSpeed = 5f;

        [Header("Events")]
        [SerializeField] private UnityEvent onBossFightStarted;
        [SerializeField] private UnityEvent<int> onPhaseChanged;
        [SerializeField] private UnityEvent<BossAttack> onAttackStarted;
        [SerializeField] private UnityEvent onEnraged;
        [SerializeField] private UnityEvent onBossDefeated;

        // State
        private BossState currentState = BossState.Idle;
        private int currentPhaseIndex = 0;
        private float fightStartTime;
        private float lastAttackTime;
        private bool isEnraged;
        private bool fightStarted;
        private float currentHealthPercent = 1f;

        // Components
        private Core.HealthSystem healthSystem;
        private Animator animator;

        // Properties
        public BossState State => currentState;
        public int CurrentPhase => currentPhaseIndex + 1;
        public BossPhase CurrentPhaseData => currentPhaseIndex < phases.Count ? phases[currentPhaseIndex] : null;
        public bool IsEnraged => isEnraged;
        public bool IsFighting => fightStarted;
        public float FightDuration => fightStarted ? Time.time - fightStartTime : 0f;
        public string BossName => bossName;

        // Events
        public event Action BossFightStarted;
        public event Action<int> PhaseChanged;
        public event Action<BossAttack> AttackStarted;
        public event Action Enraged;
        public event Action BossDefeated;

        public enum BossState
        {
            Idle,
            Approaching,
            Attacking,
            SpecialAttack,
            PhaseTransition,
            Stunned,
            Defeated
        }

        private void Awake()
        {
            healthSystem = GetComponent<Core.HealthSystem>();
            animator = GetComponent<Animator>();

            if (healthSystem != null)
            {
                healthSystem.OnDamage += OnDamageTaken;
                healthSystem.OnDeath += OnDefeated;
            }

            // Default phase if none defined
            if (phases.Count == 0)
            {
                phases.Add(new BossPhase
                {
                    phaseName = "Phase 1",
                    healthThreshold = 0f,
                    attacks = new List<BossAttack>
                    {
                        new BossAttack { attackName = "Basic Attack", damage = baseDamage }
                    }
                });
            }
        }

        private void Update()
        {
            if (!fightStarted || currentState == BossState.Defeated) return;

            UpdateHealthPercent();
            CheckPhaseTransition();
            CheckEnrage();
            UpdateBehavior();
        }

        private void UpdateHealthPercent()
        {
            if (healthSystem != null)
            {
                currentHealthPercent = healthSystem.CurrentHealth / healthSystem.MaxHealth;
            }
        }

        private void CheckPhaseTransition()
        {
            // Check if should transition to next phase
            for (int i = currentPhaseIndex + 1; i < phases.Count; i++)
            {
                if (currentHealthPercent <= phases[i].healthThreshold)
                {
                    TransitionToPhase(i);
                    break;
                }
            }
        }

        private void CheckEnrage()
        {
            if (isEnraged) return;

            bool shouldEnrage = false;

            // Timer enrage
            if (FightDuration >= enrageTimer)
            {
                shouldEnrage = true;
            }

            // Low health enrage
            if (enrageOnLowHealth && currentHealthPercent <= enrageHealthThreshold)
            {
                shouldEnrage = true;
            }

            if (shouldEnrage)
            {
                TriggerEnrage();
            }
        }

        private void UpdateBehavior()
        {
            if (target == null) return;

            float distance = Vector3.Distance(transform.position, target.position);
            var phase = CurrentPhaseData;

            switch (currentState)
            {
                case BossState.Idle:
                    if (distance > attackRange)
                    {
                        currentState = BossState.Approaching;
                    }
                    else if (Time.time - lastAttackTime >= GetAttackCooldown())
                    {
                        SelectAndExecuteAttack();
                    }
                    break;

                case BossState.Approaching:
                    MoveTowardsTarget();
                    if (distance <= attackRange)
                    {
                        currentState = BossState.Idle;
                    }
                    break;

                case BossState.Attacking:
                case BossState.SpecialAttack:
                case BossState.PhaseTransition:
                    // Handled by coroutines
                    break;
            }

            // Always face target
            FaceTarget();
        }

        private void MoveTowardsTarget()
        {
            if (target == null) return;

            Vector3 direction = (target.position - transform.position).normalized;
            float speed = isEnraged ? moveSpeed * 1.5f : moveSpeed;
            transform.position += direction * speed * Time.deltaTime;
        }

        private void FaceTarget()
        {
            if (target == null) return;

            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0;

            if (direction.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        private float GetAttackCooldown()
        {
            float cooldown = attackCooldown;
            if (CurrentPhaseData != null)
            {
                cooldown *= CurrentPhaseData.attackSpeedMultiplier;
            }
            if (isEnraged)
            {
                cooldown *= 0.7f;
            }
            return cooldown;
        }

        private void SelectAndExecuteAttack()
        {
            var phase = CurrentPhaseData;
            if (phase == null || phase.attacks.Count == 0) return;

            // Select attack based on weights
            BossAttack selectedAttack = SelectWeightedAttack(phase.attacks);

            if (selectedAttack != null)
            {
                StartCoroutine(ExecuteAttack(selectedAttack));
            }
        }

        private BossAttack SelectWeightedAttack(List<BossAttack> attacks)
        {
            float totalWeight = 0f;
            foreach (var attack in attacks)
            {
                totalWeight += attack.weight;
            }

            float random = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var attack in attacks)
            {
                cumulative += attack.weight;
                if (random <= cumulative)
                {
                    return attack;
                }
            }

            return attacks[0];
        }

        private System.Collections.IEnumerator ExecuteAttack(BossAttack attack)
        {
            currentState = attack.isSpecial ? BossState.SpecialAttack : BossState.Attacking;
            lastAttackTime = Time.time;

            AttackStarted?.Invoke(attack);
            onAttackStarted?.Invoke(attack);

            Debug.Log($"[BossAI] {bossName} uses {attack.attackName}!");

            // Telegraph
            if (animator != null && !string.IsNullOrEmpty(attack.animationTrigger))
            {
                animator.SetTrigger(attack.animationTrigger);
            }

            yield return new WaitForSeconds(attack.telegraphTime);

            // Execute attack
            if (attack.isAOE)
            {
                ExecuteAOEAttack(attack);
            }
            else
            {
                ExecuteSingleTargetAttack(attack);
            }

            // Recovery
            yield return new WaitForSeconds(attack.recoveryTime);

            currentState = BossState.Idle;
        }

        private void ExecuteSingleTargetAttack(BossAttack attack)
        {
            if (target == null) return;

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance <= attack.range)
            {
                var targetHealth = target.GetComponent<Core.HealthSystem>();
                if (targetHealth != null)
                {
                    float damage = CalculateDamage(attack);
                    targetHealth.TakeDamage(damage);
                }
            }
        }

        private void ExecuteAOEAttack(BossAttack attack)
        {
            Vector3 center = attack.aoeAtSelf ? transform.position : (target != null ? target.position : transform.position);

            Collider[] hits = Physics.OverlapSphere(center, attack.aoeRadius);
            foreach (var hit in hits)
            {
                if (hit.transform == transform) continue;

                var health = hit.GetComponent<Core.HealthSystem>();
                if (health != null)
                {
                    float damage = CalculateDamage(attack);
                    health.TakeDamage(damage);
                }
            }

            Debug.Log($"[BossAI] AOE attack hit {hits.Length} targets");
        }

        private float CalculateDamage(BossAttack attack)
        {
            float damage = attack.damage;

            if (CurrentPhaseData != null)
            {
                damage *= CurrentPhaseData.damageMultiplier;
            }

            if (isEnraged)
            {
                damage *= 1.5f;
            }

            return damage;
        }

        private void TransitionToPhase(int phaseIndex)
        {
            StartCoroutine(PhaseTransitionCoroutine(phaseIndex));
        }

        private System.Collections.IEnumerator PhaseTransitionCoroutine(int phaseIndex)
        {
            currentState = BossState.PhaseTransition;

            Debug.Log($"[BossAI] {bossName} transitioning to phase {phaseIndex + 1}!");

            // Phase transition animation/invulnerability
            if (animator != null)
            {
                animator.SetTrigger("PhaseTransition");
            }

            yield return new WaitForSeconds(2f);

            currentPhaseIndex = phaseIndex;

            PhaseChanged?.Invoke(currentPhaseIndex + 1);
            onPhaseChanged?.Invoke(currentPhaseIndex + 1);

            // Apply phase modifiers
            var phase = CurrentPhaseData;
            if (phase != null)
            {
                moveSpeed *= phase.moveSpeedMultiplier;
            }

            currentState = BossState.Idle;
        }

        private void TriggerEnrage()
        {
            isEnraged = true;

            Enraged?.Invoke();
            onEnraged?.Invoke();

            Debug.Log($"[BossAI] {bossName} has ENRAGED!");

            if (animator != null)
            {
                animator.SetBool("Enraged", true);
            }
        }

        private void OnDamageTaken(float damage)
        {
            // Interrupt attack on heavy damage?
            // Add stagger mechanics here
        }

        private void OnDefeated()
        {
            currentState = BossState.Defeated;
            fightStarted = false;

            BossDefeated?.Invoke();
            onBossDefeated?.Invoke();

            Debug.Log($"[BossAI] {bossName} has been DEFEATED!");

            if (animator != null)
            {
                animator.SetTrigger("Death");
            }
        }

        // Public methods
        public void StartFight(Transform playerTarget = null)
        {
            if (fightStarted) return;

            target = playerTarget ?? GameObject.FindGameObjectWithTag("Player")?.transform;
            fightStarted = true;
            fightStartTime = Time.time;
            currentState = BossState.Idle;

            BossFightStarted?.Invoke();
            onBossFightStarted?.Invoke();

            Debug.Log($"[BossAI] {bossName} fight started!");
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void Stun(float duration)
        {
            StartCoroutine(StunCoroutine(duration));
        }

        private System.Collections.IEnumerator StunCoroutine(float duration)
        {
            var previousState = currentState;
            currentState = BossState.Stunned;

            Debug.Log($"[BossAI] {bossName} stunned for {duration}s!");

            yield return new WaitForSeconds(duration);

            if (currentState == BossState.Stunned)
            {
                currentState = BossState.Idle;
            }
        }

        public void ForcePhase(int phase)
        {
            if (phase > 0 && phase <= phases.Count)
            {
                TransitionToPhase(phase - 1);
            }
        }

        public void ForceEnrage()
        {
            TriggerEnrage();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            if (target != null)
            {
                Gizmos.color = currentState == BossState.Attacking ? Color.red : Color.yellow;
                Gizmos.DrawLine(transform.position, target.position);
            }
        }
    }

    [Serializable]
    public class BossPhase
    {
        public string phaseName = "Phase";
        [Range(0f, 1f)]
        public float healthThreshold = 0.5f; // Trigger phase at this health %
        public float damageMultiplier = 1f;
        public float attackSpeedMultiplier = 1f;
        public float moveSpeedMultiplier = 1f;
        public List<BossAttack> attacks = new List<BossAttack>();
        public string transitionAnimation;
    }

    [Serializable]
    public class BossAttack
    {
        public string attackName = "Attack";
        public float damage = 10f;
        public float range = 3f;
        public float weight = 1f; // Selection weight
        public float telegraphTime = 0.5f;
        public float recoveryTime = 1f;
        public bool isSpecial = false;
        public bool isAOE = false;
        public float aoeRadius = 5f;
        public bool aoeAtSelf = true;
        public string animationTrigger;
        public AudioClip soundEffect;
    }
}
