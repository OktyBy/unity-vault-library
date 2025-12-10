using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityVault.Core
{
    /// <summary>
    /// Manages active status effects on a GameObject.
    /// </summary>
    public class StatusManager : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Settings")]
        [SerializeField] private bool immuneToDebuffs = false;
        [SerializeField] private bool immuneToStuns = false;

        [Header("Events")]
        [SerializeField] private UnityEvent<StatusEffect> onEffectApplied;
        [SerializeField] private UnityEvent<StatusEffect> onEffectRemoved;
        [SerializeField] private UnityEvent<StatusEffect> onEffectTick;

        #endregion

        #region Properties

        public IReadOnlyList<ActiveEffect> ActiveEffects => activeEffects;
        public bool IsStunned => HasEffectOfCategory(EffectCategory.Stun);
        public bool IsSlowed => HasEffectOfCategory(EffectCategory.Slow);
        public bool IsRooted => HasEffectOfCategory(EffectCategory.Root);
        public bool IsInvulnerable => HasEffectOfCategory(EffectCategory.Invulnerable);

        #endregion

        #region C# Events

        public event Action<StatusEffect> EffectApplied;
        public event Action<StatusEffect> EffectRemoved;
        public event Action<StatusEffect, float> EffectTicked; // effect, tickValue

        #endregion

        #region Private Fields

        private List<ActiveEffect> activeEffects = new List<ActiveEffect>();
        private HealthSystem healthSystem;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            healthSystem = GetComponent<HealthSystem>();
        }

        private void Update()
        {
            UpdateEffects();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Apply a status effect.
        /// </summary>
        public bool ApplyEffect(StatusEffect effect, GameObject source = null)
        {
            if (effect == null) return false;

            // Check immunities
            if (immuneToDebuffs && effect.effectType == EffectType.Debuff) return false;
            if (immuneToStuns && effect.category == EffectCategory.Stun) return false;
            if (IsInvulnerable && effect.effectType == EffectType.Debuff) return false;

            // Check for existing effect
            var existing = activeEffects.FirstOrDefault(e => e.Effect.effectId == effect.effectId);

            if (existing != null)
            {
                return HandleExistingEffect(existing, effect);
            }

            // Apply new effect
            var activeEffect = new ActiveEffect
            {
                Effect = effect,
                Source = source,
                StartTime = Time.time,
                RemainingDuration = effect.isPermanent ? float.MaxValue : effect.duration,
                CurrentStacks = 1,
                LastTickTime = Time.time
            };

            activeEffects.Add(activeEffect);
            SpawnVisualEffect(activeEffect);

            EffectApplied?.Invoke(effect);
            onEffectApplied?.Invoke(effect);

            return true;
        }

        /// <summary>
        /// Remove a specific effect.
        /// </summary>
        public bool RemoveEffect(string effectId)
        {
            var effect = activeEffects.FirstOrDefault(e => e.Effect.effectId == effectId);
            if (effect == null) return false;

            return RemoveEffect(effect);
        }

        /// <summary>
        /// Remove all effects of a type.
        /// </summary>
        public void RemoveEffectsOfType(EffectType type)
        {
            var toRemove = activeEffects.Where(e => e.Effect.effectType == type).ToList();
            foreach (var effect in toRemove)
            {
                RemoveEffect(effect);
            }
        }

        /// <summary>
        /// Remove all effects of a category.
        /// </summary>
        public void RemoveEffectsOfCategory(EffectCategory category)
        {
            var toRemove = activeEffects.Where(e => e.Effect.category == category).ToList();
            foreach (var effect in toRemove)
            {
                RemoveEffect(effect);
            }
        }

        /// <summary>
        /// Clear all effects.
        /// </summary>
        public void ClearAllEffects()
        {
            var toRemove = activeEffects.ToList();
            foreach (var effect in toRemove)
            {
                RemoveEffect(effect);
            }
        }

        /// <summary>
        /// Clear all debuffs.
        /// </summary>
        public void ClearDebuffs()
        {
            RemoveEffectsOfType(EffectType.Debuff);
        }

        /// <summary>
        /// Check if has a specific effect.
        /// </summary>
        public bool HasEffect(string effectId)
        {
            return activeEffects.Any(e => e.Effect.effectId == effectId);
        }

        /// <summary>
        /// Check if has any effect of category.
        /// </summary>
        public bool HasEffectOfCategory(EffectCategory category)
        {
            return activeEffects.Any(e => e.Effect.category == category);
        }

        /// <summary>
        /// Get total speed modifier from all effects.
        /// </summary>
        public float GetSpeedModifier()
        {
            float modifier = 0f;
            foreach (var effect in activeEffects)
            {
                modifier += effect.Effect.speedModifier * effect.CurrentStacks;
            }
            return Mathf.Max(-0.9f, modifier); // Cap at 90% slow
        }

        /// <summary>
        /// Get stack count of an effect.
        /// </summary>
        public int GetStackCount(string effectId)
        {
            var effect = activeEffects.FirstOrDefault(e => e.Effect.effectId == effectId);
            return effect?.CurrentStacks ?? 0;
        }

        #endregion

        #region Private Methods

        private void UpdateEffects()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var active = activeEffects[i];

                // Update duration
                if (!active.Effect.isPermanent)
                {
                    active.RemainingDuration -= Time.deltaTime;

                    if (active.RemainingDuration <= 0f)
                    {
                        RemoveEffect(active);
                        continue;
                    }
                }

                // Process ticks
                if (active.Effect.hasTick)
                {
                    if (Time.time >= active.LastTickTime + active.Effect.tickInterval)
                    {
                        ProcessTick(active);
                        active.LastTickTime = Time.time;
                    }
                }
            }
        }

        private void ProcessTick(ActiveEffect active)
        {
            float tickValue = active.Effect.tickValue * active.CurrentStacks;

            if (healthSystem != null)
            {
                if (active.Effect.tickIsDamage)
                {
                    healthSystem.TakeDamage(tickValue, ignoreArmor: true);
                }
                else
                {
                    healthSystem.Heal(tickValue);
                }
            }

            EffectTicked?.Invoke(active.Effect, tickValue);
            onEffectTick?.Invoke(active.Effect);
        }

        private bool HandleExistingEffect(ActiveEffect existing, StatusEffect newEffect)
        {
            switch (newEffect.stackBehavior)
            {
                case StackBehavior.RefreshDuration:
                    existing.RemainingDuration = newEffect.duration;
                    return true;

                case StackBehavior.AddDuration:
                    existing.RemainingDuration += newEffect.duration;
                    return true;

                case StackBehavior.IncreaseStacks:
                    if (existing.CurrentStacks < newEffect.maxStacks)
                    {
                        existing.CurrentStacks++;
                        existing.RemainingDuration = newEffect.duration;
                        return true;
                    }
                    else
                    {
                        existing.RemainingDuration = newEffect.duration;
                        return true;
                    }

                case StackBehavior.Replace:
                    existing.RemainingDuration = newEffect.duration;
                    existing.CurrentStacks = 1;
                    return true;

                default:
                    return false;
            }
        }

        private bool RemoveEffect(ActiveEffect active)
        {
            if (active.VisualInstance != null)
            {
                Destroy(active.VisualInstance);
            }

            activeEffects.Remove(active);

            EffectRemoved?.Invoke(active.Effect);
            onEffectRemoved?.Invoke(active.Effect);

            return true;
        }

        private void SpawnVisualEffect(ActiveEffect active)
        {
            if (active.Effect.visualEffectPrefab != null)
            {
                active.VisualInstance = Instantiate(
                    active.Effect.visualEffectPrefab,
                    transform.position,
                    Quaternion.identity,
                    transform
                );
            }
        }

        #endregion

        #region Data Class

        [Serializable]
        public class ActiveEffect
        {
            public StatusEffect Effect;
            public GameObject Source;
            public float StartTime;
            public float RemainingDuration;
            public int CurrentStacks;
            public float LastTickTime;
            public GameObject VisualInstance;
        }

        #endregion
    }
}
