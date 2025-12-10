using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Core
{
    /// <summary>
    /// Complete health management system with damage, healing, and death events.
    /// Supports damage modifiers, invincibility frames, and health regeneration.
    /// </summary>
    public class HealthSystem : MonoBehaviour, IDamageable
    {
        #region Serialized Fields

        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;
        [SerializeField] private bool startAtMaxHealth = true;

        [Header("Defense")]
        [SerializeField] private float armor = 0f;
        [SerializeField] private float damageReduction = 0f; // 0-1 percentage

        [Header("Regeneration")]
        [SerializeField] private bool enableRegen = false;
        [SerializeField] private float regenRate = 1f; // HP per second
        [SerializeField] private float regenDelay = 3f; // Seconds after taking damage

        [Header("Invincibility")]
        [SerializeField] private bool hasInvincibilityFrames = false;
        [SerializeField] private float invincibilityDuration = 0.5f;

        [Header("Events")]
        [SerializeField] private UnityEvent<float, float> onHealthChanged; // current, max
        [SerializeField] private UnityEvent<float> onDamageTaken; // damage amount
        [SerializeField] private UnityEvent<float> onHealed; // heal amount
        [SerializeField] private UnityEvent onDeath;
        [SerializeField] private UnityEvent onRevive;

        #endregion

        #region Properties

        public float MaxHealth
        {
            get => maxHealth;
            set
            {
                maxHealth = Mathf.Max(1f, value);
                currentHealth = Mathf.Min(currentHealth, maxHealth);
                OnHealthChanged();
            }
        }

        public float CurrentHealth
        {
            get => currentHealth;
            private set
            {
                currentHealth = Mathf.Clamp(value, 0f, maxHealth);
                OnHealthChanged();
            }
        }

        public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f;
        public bool IsAlive => currentHealth > 0f;
        public bool IsDead => currentHealth <= 0f;
        public bool IsFullHealth => Mathf.Approximately(currentHealth, maxHealth);
        public bool IsInvincible { get; private set; }

        public float Armor
        {
            get => armor;
            set => armor = Mathf.Max(0f, value);
        }

        public float DamageReduction
        {
            get => damageReduction;
            set => damageReduction = Mathf.Clamp01(value);
        }

        #endregion

        #region C# Events

        public event Action<float, float> HealthChanged; // current, max
        public event Action<float> DamageTaken; // damage amount
        public event Action<float> Healed; // heal amount
        public event Action Death;
        public event Action Revived;

        #endregion

        #region Private Fields

        private float lastDamageTime;
        private float invincibilityEndTime;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (startAtMaxHealth)
            {
                currentHealth = maxHealth;
            }
        }

        private void Update()
        {
            // Handle invincibility timer
            if (IsInvincible && Time.time >= invincibilityEndTime)
            {
                IsInvincible = false;
            }

            // Handle regeneration
            if (enableRegen && IsAlive && !IsFullHealth)
            {
                if (Time.time >= lastDamageTime + regenDelay)
                {
                    Heal(regenRate * Time.deltaTime, ignoreEvents: true);
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Apply damage to the health system.
        /// </summary>
        /// <param name="damage">Raw damage amount</param>
        /// <param name="ignoreArmor">If true, bypasses armor calculation</param>
        /// <param name="ignoreInvincibility">If true, bypasses invincibility frames</param>
        /// <returns>Actual damage dealt</returns>
        public float TakeDamage(float damage, bool ignoreArmor = false, bool ignoreInvincibility = false)
        {
            if (IsDead) return 0f;

            // Check invincibility
            if (IsInvincible && !ignoreInvincibility)
            {
                return 0f;
            }

            // Calculate actual damage
            float actualDamage = damage;

            if (!ignoreArmor)
            {
                // Apply armor (flat reduction)
                actualDamage = Mathf.Max(0f, actualDamage - armor);

                // Apply damage reduction (percentage)
                actualDamage *= (1f - damageReduction);
            }

            // Apply damage
            CurrentHealth -= actualDamage;
            lastDamageTime = Time.time;

            // Trigger invincibility
            if (hasInvincibilityFrames && !ignoreInvincibility)
            {
                IsInvincible = true;
                invincibilityEndTime = Time.time + invincibilityDuration;
            }

            // Fire events
            DamageTaken?.Invoke(actualDamage);
            onDamageTaken?.Invoke(actualDamage);

            // Check for death
            if (IsDead)
            {
                HandleDeath();
            }

            return actualDamage;
        }

        /// <summary>
        /// Heal the entity.
        /// </summary>
        /// <param name="amount">Amount to heal</param>
        /// <param name="canOverheal">If true, can exceed max health</param>
        /// <param name="ignoreEvents">If true, won't fire heal events (for regen)</param>
        /// <returns>Actual amount healed</returns>
        public float Heal(float amount, bool canOverheal = false, bool ignoreEvents = false)
        {
            if (IsDead) return 0f;

            float previousHealth = currentHealth;

            if (canOverheal)
            {
                currentHealth += amount;
                OnHealthChanged();
            }
            else
            {
                CurrentHealth += amount;
            }

            float actualHeal = currentHealth - previousHealth;

            if (!ignoreEvents && actualHeal > 0f)
            {
                Healed?.Invoke(actualHeal);
                onHealed?.Invoke(actualHeal);
            }

            return actualHeal;
        }

        /// <summary>
        /// Set health to a specific value.
        /// </summary>
        public void SetHealth(float value)
        {
            CurrentHealth = value;

            if (IsDead)
            {
                HandleDeath();
            }
        }

        /// <summary>
        /// Restore health to maximum.
        /// </summary>
        public void RestoreFullHealth()
        {
            float healAmount = maxHealth - currentHealth;
            CurrentHealth = maxHealth;

            if (healAmount > 0f)
            {
                Healed?.Invoke(healAmount);
                onHealed?.Invoke(healAmount);
            }
        }

        /// <summary>
        /// Instantly kill the entity.
        /// </summary>
        public void Kill()
        {
            if (IsDead) return;

            CurrentHealth = 0f;
            HandleDeath();
        }

        /// <summary>
        /// Revive the entity with specified health.
        /// </summary>
        /// <param name="healthPercent">Health percentage to revive with (0-1)</param>
        public void Revive(float healthPercent = 1f)
        {
            if (IsAlive) return;

            CurrentHealth = maxHealth * Mathf.Clamp01(healthPercent);

            Revived?.Invoke();
            onRevive?.Invoke();
        }

        /// <summary>
        /// Add temporary invincibility.
        /// </summary>
        public void AddInvincibility(float duration)
        {
            IsInvincible = true;
            invincibilityEndTime = Mathf.Max(invincibilityEndTime, Time.time + duration);
        }

        /// <summary>
        /// Remove invincibility immediately.
        /// </summary>
        public void RemoveInvincibility()
        {
            IsInvincible = false;
        }

        /// <summary>
        /// Modify max health and optionally scale current health.
        /// </summary>
        public void ModifyMaxHealth(float newMaxHealth, bool scaleCurrentHealth = true)
        {
            float healthRatio = HealthPercentage;
            maxHealth = Mathf.Max(1f, newMaxHealth);

            if (scaleCurrentHealth)
            {
                currentHealth = maxHealth * healthRatio;
            }
            else
            {
                currentHealth = Mathf.Min(currentHealth, maxHealth);
            }

            OnHealthChanged();
        }

        #endregion

        #region IDamageable Implementation

        float IDamageable.TakeDamage(float damage)
        {
            return TakeDamage(damage);
        }

        bool IDamageable.IsAlive => IsAlive;

        #endregion

        #region Private Methods

        private void OnHealthChanged()
        {
            HealthChanged?.Invoke(currentHealth, maxHealth);
            onHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void HandleDeath()
        {
            Death?.Invoke();
            onDeath?.Invoke();
        }

        #endregion

        #region Editor Helpers

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            armor = Mathf.Max(0f, armor);
            damageReduction = Mathf.Clamp01(damageReduction);
            regenRate = Mathf.Max(0f, regenRate);
            regenDelay = Mathf.Max(0f, regenDelay);
            invincibilityDuration = Mathf.Max(0f, invincibilityDuration);
        }

        #endregion
    }
}
