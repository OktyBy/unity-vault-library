using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityVault.Combat
{
    /// <summary>
    /// Melee combat system with combo attacks, hitboxes, and timing windows.
    /// </summary>
    public class MeleeCombat : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Attack Settings")]
        [SerializeField] private Transform attackPoint;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private LayerMask targetLayers;

        [Header("Combo System")]
        [SerializeField] private AttackData[] comboAttacks;
        [SerializeField] private float comboResetTime = 1f;

        [Header("Events")]
        [SerializeField] private UnityEvent<AttackData> onAttackStarted;
        [SerializeField] private UnityEvent<GameObject, float> onHitTarget; // target, damage
        [SerializeField] private UnityEvent onComboComplete;
        [SerializeField] private UnityEvent onComboReset;

        #endregion

        #region Properties

        public bool IsAttacking { get; private set; }
        public bool CanAttack => !IsAttacking && !isInCooldown;
        public int CurrentComboIndex => currentComboIndex;
        public AttackData CurrentAttack => currentComboIndex < comboAttacks.Length ? comboAttacks[currentComboIndex] : null;

        #endregion

        #region C# Events

        public event Action<AttackData> AttackStarted;
        public event Action<GameObject, float> HitTarget;
        public event Action ComboComplete;
        public event Action ComboReset;

        #endregion

        #region Private Fields

        private int currentComboIndex = 0;
        private float lastAttackTime;
        private bool isInCooldown;
        private bool comboWindowOpen;
        private Animator animator;
        private List<GameObject> hitTargets = new List<GameObject>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            animator = GetComponent<Animator>();

            if (attackPoint == null)
            {
                attackPoint = transform;
            }
        }

        private void Update()
        {
            // Check combo reset
            if (!IsAttacking && currentComboIndex > 0)
            {
                if (Time.time - lastAttackTime > comboResetTime)
                {
                    ResetCombo();
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempt to perform an attack.
        /// </summary>
        public bool Attack()
        {
            if (!CanAttack) return false;

            if (comboAttacks == null || comboAttacks.Length == 0)
            {
                Debug.LogWarning("[MeleeCombat] No attacks configured!");
                return false;
            }

            var attackData = comboAttacks[currentComboIndex];
            StartCoroutine(PerformAttack(attackData));
            return true;
        }

        /// <summary>
        /// Force reset the combo.
        /// </summary>
        public void ResetCombo()
        {
            currentComboIndex = 0;
            comboWindowOpen = false;
            ComboReset?.Invoke();
            onComboReset?.Invoke();
        }

        /// <summary>
        /// Check for hits in the attack area.
        /// </summary>
        public void CheckHits()
        {
            Collider[] hits = Physics.OverlapSphere(attackPoint.position, attackRange, targetLayers);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                if (hitTargets.Contains(hit.gameObject)) continue;

                hitTargets.Add(hit.gameObject);
                ProcessHit(hit.gameObject);
            }
        }

        /// <summary>
        /// Called from animation event to enable hitbox.
        /// </summary>
        public void EnableHitbox()
        {
            hitTargets.Clear();
            CheckHits();
        }

        /// <summary>
        /// Called from animation event to disable hitbox.
        /// </summary>
        public void DisableHitbox()
        {
            hitTargets.Clear();
        }

        #endregion

        #region Private Methods

        private IEnumerator PerformAttack(AttackData attack)
        {
            IsAttacking = true;
            lastAttackTime = Time.time;

            // Trigger animation
            if (animator != null && !string.IsNullOrEmpty(attack.animationTrigger))
            {
                animator.SetTrigger(attack.animationTrigger);
            }

            AttackStarted?.Invoke(attack);
            onAttackStarted?.Invoke(attack);

            // Wait for startup
            yield return new WaitForSeconds(attack.startupTime);

            // Active frames - check for hits
            hitTargets.Clear();
            float activeTime = 0f;

            while (activeTime < attack.activeTime)
            {
                CheckHits();
                activeTime += Time.deltaTime;
                yield return null;
            }

            // Recovery
            yield return new WaitForSeconds(attack.recoveryTime);

            IsAttacking = false;

            // Advance combo
            currentComboIndex++;
            if (currentComboIndex >= comboAttacks.Length)
            {
                // Combo complete
                ComboComplete?.Invoke();
                onComboComplete?.Invoke();
                currentComboIndex = 0;

                // Cooldown after full combo
                StartCoroutine(Cooldown(attack.cooldown));
            }
            else
            {
                // Open combo window
                comboWindowOpen = true;
            }
        }

        private void ProcessHit(GameObject target)
        {
            var attack = comboAttacks[currentComboIndex];
            float damage = attack.baseDamage;

            // Apply damage if target has health
            var damageable = target.GetComponent<UnityVault.Core.IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }

            // Apply knockback
            var rb = target.GetComponent<Rigidbody>();
            if (rb != null && attack.knockbackForce > 0)
            {
                Vector3 knockbackDir = (target.transform.position - transform.position).normalized;
                knockbackDir.y = 0.2f; // Slight upward
                rb.AddForce(knockbackDir * attack.knockbackForce, ForceMode.Impulse);
            }

            // Play hit effect
            if (attack.hitEffect != null)
            {
                Instantiate(attack.hitEffect, target.transform.position, Quaternion.identity);
            }

            // Play hit sound
            if (attack.hitSound != null)
            {
                AudioSource.PlayClipAtPoint(attack.hitSound, target.transform.position);
            }

            HitTarget?.Invoke(target, damage);
            onHitTarget?.Invoke(target, damage);
        }

        private IEnumerator Cooldown(float duration)
        {
            isInCooldown = true;
            yield return new WaitForSeconds(duration);
            isInCooldown = false;
        }

        #endregion

        #region Editor

        private void OnDrawGizmosSelected()
        {
            if (attackPoint == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }

        #endregion
    }

    /// <summary>
    /// Data for a single attack in a combo.
    /// </summary>
    [System.Serializable]
    public class AttackData
    {
        [Header("Info")]
        public string attackName;
        public string animationTrigger;

        [Header("Damage")]
        public float baseDamage = 10f;
        public DamageType damageType = DamageType.Physical;
        public float knockbackForce = 5f;

        [Header("Timing")]
        public float startupTime = 0.1f;  // Time before attack becomes active
        public float activeTime = 0.2f;    // Time attack can hit
        public float recoveryTime = 0.3f;  // Time after attack before can act
        public float cooldown = 0.5f;      // Cooldown after combo

        [Header("Effects")]
        public GameObject hitEffect;
        public AudioClip swingSound;
        public AudioClip hitSound;

        public float TotalDuration => startupTime + activeTime + recoveryTime;
    }

    public enum DamageType
    {
        Physical,
        Fire,
        Ice,
        Lightning,
        Poison,
        Holy,
        Dark,
        True // Ignores defense
    }
}
