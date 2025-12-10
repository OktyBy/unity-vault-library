using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Combat
{
    /// <summary>
    /// Critical hit system with chance, multiplier, and various modifiers.
    /// </summary>
    public class CriticalHit : MonoBehaviour
    {
        [Header("Base Critical Stats")]
        [SerializeField] private float baseCritChance = 0.05f; // 5%
        [SerializeField] private float baseCritMultiplier = 2f; // 200% damage

        [Header("Bonus Stats")]
        [SerializeField] private float bonusCritChance = 0f;
        [SerializeField] private float bonusCritMultiplier = 0f;

        [Header("Caps")]
        [SerializeField] private float maxCritChance = 1f; // 100%
        [SerializeField] private float maxCritMultiplier = 10f;

        [Header("Conditional Bonuses")]
        [SerializeField] private float backStabBonus = 0.5f; // +50% crit chance from behind
        [SerializeField] private float lowHealthBonus = 0.25f; // +25% when target low HP
        [SerializeField] private float lowHealthThreshold = 0.3f; // Below 30% HP

        [Header("Events")]
        [SerializeField] private UnityEvent<CriticalHitResult> onCriticalHit;
        [SerializeField] private UnityEvent onNormalHit;

        // Properties
        public float CritChance => Mathf.Min(baseCritChance + bonusCritChance, maxCritChance);
        public float CritMultiplier => Mathf.Min(baseCritMultiplier + bonusCritMultiplier, maxCritMultiplier);
        public float BaseCritChance => baseCritChance;
        public float BaseCritMultiplier => baseCritMultiplier;

        // Events
        public event Action<CriticalHitResult> CriticalHitOccurred;
        public event Action NormalHitOccurred;

        /// <summary>
        /// Process damage with critical hit chance.
        /// Returns the final damage and whether it was critical.
        /// </summary>
        public CriticalHitResult ProcessDamage(float baseDamage)
        {
            return ProcessDamage(baseDamage, CritChance, CritMultiplier);
        }

        /// <summary>
        /// Process damage with custom critical chance/multiplier.
        /// </summary>
        public CriticalHitResult ProcessDamage(float baseDamage, float critChance, float critMultiplier)
        {
            bool isCrit = RollCritical(critChance);

            var result = new CriticalHitResult
            {
                baseDamage = baseDamage,
                finalDamage = isCrit ? baseDamage * critMultiplier : baseDamage,
                isCritical = isCrit,
                critChance = critChance,
                critMultiplier = critMultiplier
            };

            if (isCrit)
            {
                CriticalHitOccurred?.Invoke(result);
                onCriticalHit?.Invoke(result);
                Debug.Log($"[Crit] CRITICAL HIT! {baseDamage:F0} -> {result.finalDamage:F0} ({critMultiplier:F1}x)");
            }
            else
            {
                NormalHitOccurred?.Invoke();
                onNormalHit?.Invoke();
            }

            return result;
        }

        /// <summary>
        /// Process damage with contextual bonuses (backstab, low health target, etc.)
        /// </summary>
        public CriticalHitResult ProcessDamageContextual(
            float baseDamage,
            Transform attacker,
            Transform target,
            float targetHealthPercent = 1f)
        {
            float totalCritChance = CritChance;
            float totalCritMultiplier = CritMultiplier;

            // Backstab bonus
            if (attacker != null && target != null)
            {
                if (IsBackstab(attacker, target))
                {
                    totalCritChance += backStabBonus;
                    Debug.Log("[Crit] Backstab bonus applied!");
                }
            }

            // Low health bonus
            if (targetHealthPercent <= lowHealthThreshold)
            {
                totalCritChance += lowHealthBonus;
                Debug.Log("[Crit] Low health bonus applied!");
            }

            // Apply caps
            totalCritChance = Mathf.Min(totalCritChance, maxCritChance);
            totalCritMultiplier = Mathf.Min(totalCritMultiplier, maxCritMultiplier);

            return ProcessDamage(baseDamage, totalCritChance, totalCritMultiplier);
        }

        private bool IsBackstab(Transform attacker, Transform target)
        {
            Vector3 toAttacker = (attacker.position - target.position).normalized;
            float dot = Vector3.Dot(target.forward, toAttacker);
            return dot > 0.5f; // Attacking from behind
        }

        private bool RollCritical(float chance)
        {
            return UnityEngine.Random.value <= chance;
        }

        /// <summary>
        /// Guaranteed critical hit.
        /// </summary>
        public CriticalHitResult ProcessGuaranteedCrit(float baseDamage)
        {
            return ProcessDamage(baseDamage, 1f, CritMultiplier);
        }

        /// <summary>
        /// Process with custom multiplier (for abilities).
        /// </summary>
        public CriticalHitResult ProcessWithMultiplierBonus(float baseDamage, float bonusMultiplier)
        {
            return ProcessDamage(baseDamage, CritChance, CritMultiplier + bonusMultiplier);
        }

        // Stat modifiers
        public void SetBaseCritChance(float value) => baseCritChance = Mathf.Clamp01(value);
        public void SetBaseCritMultiplier(float value) => baseCritMultiplier = Mathf.Max(1f, value);
        public void AddBonusCritChance(float amount) => bonusCritChance += amount;
        public void AddBonusCritMultiplier(float amount) => bonusCritMultiplier += amount;
        public void SetBonusCritChance(float value) => bonusCritChance = value;
        public void SetBonusCritMultiplier(float value) => bonusCritMultiplier = value;

        public void ResetBonuses()
        {
            bonusCritChance = 0f;
            bonusCritMultiplier = 0f;
        }

        /// <summary>
        /// Apply temporary crit buff.
        /// </summary>
        public void ApplyCritBuff(float chanceBonus, float multiplierBonus, float duration)
        {
            bonusCritChance += chanceBonus;
            bonusCritMultiplier += multiplierBonus;

            // Schedule removal
            StartCoroutine(RemoveBuffAfterDelay(chanceBonus, multiplierBonus, duration));
        }

        private System.Collections.IEnumerator RemoveBuffAfterDelay(float chance, float multiplier, float delay)
        {
            yield return new WaitForSeconds(delay);
            bonusCritChance -= chance;
            bonusCritMultiplier -= multiplier;
        }

        /// <summary>
        /// Get display text for UI.
        /// </summary>
        public string GetCritChanceDisplay()
        {
            return $"{CritChance * 100:F1}%";
        }

        public string GetCritMultiplierDisplay()
        {
            return $"{CritMultiplier * 100:F0}%";
        }
    }

    [Serializable]
    public struct CriticalHitResult
    {
        public float baseDamage;
        public float finalDamage;
        public bool isCritical;
        public float critChance;
        public float critMultiplier;

        public float DamageIncrease => finalDamage - baseDamage;
    }
}
