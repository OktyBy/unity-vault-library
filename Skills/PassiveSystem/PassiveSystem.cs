using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Skills
{
    /// <summary>
    /// Passive skill/ability system for permanent bonuses.
    /// </summary>
    public class PassiveSystem : MonoBehaviour
    {
        [Header("Passive Slots")]
        [SerializeField] private int maxPassiveSlots = 4;
        [SerializeField] private List<PassiveSkill> availablePassives = new List<PassiveSkill>();

        [Header("Events")]
        [SerializeField] private UnityEvent<PassiveSkill> onPassiveActivated;
        [SerializeField] private UnityEvent<PassiveSkill> onPassiveDeactivated;
        [SerializeField] private UnityEvent<PassiveSkill> onPassiveUnlocked;

        // State
        private List<PassiveSkill> activePassives = new List<PassiveSkill>();
        private Dictionary<string, PassiveSkill> passiveMap = new Dictionary<string, PassiveSkill>();

        // Stat modifiers
        private Dictionary<StatType, float> flatBonuses = new Dictionary<StatType, float>();
        private Dictionary<StatType, float> percentBonuses = new Dictionary<StatType, float>();

        // Events
        public event Action<PassiveSkill> PassiveActivated;
        public event Action<PassiveSkill> PassiveDeactivated;
        public event Action<PassiveSkill> PassiveUnlocked;
        public event Action StatsChanged;

        public int ActivePassiveCount => activePassives.Count;
        public int MaxSlots => maxPassiveSlots;
        public bool HasFreeSlot => activePassives.Count < maxPassiveSlots;

        private void Awake()
        {
            // Build passive map
            foreach (var passive in availablePassives)
            {
                passiveMap[passive.id] = passive;
            }

            // Initialize stat dictionaries
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                flatBonuses[stat] = 0;
                percentBonuses[stat] = 0;
            }
        }

        /// <summary>
        /// Activate a passive skill.
        /// </summary>
        public bool ActivatePassive(string passiveId)
        {
            if (!passiveMap.TryGetValue(passiveId, out PassiveSkill passive))
            {
                Debug.LogWarning($"[Passive] Passive not found: {passiveId}");
                return false;
            }

            return ActivatePassive(passive);
        }

        /// <summary>
        /// Activate a passive skill.
        /// </summary>
        public bool ActivatePassive(PassiveSkill passive)
        {
            if (passive == null) return false;
            if (!passive.isUnlocked) return false;
            if (activePassives.Contains(passive)) return false;
            if (!HasFreeSlot) return false;

            activePassives.Add(passive);
            ApplyPassiveEffects(passive);

            PassiveActivated?.Invoke(passive);
            onPassiveActivated?.Invoke(passive);
            StatsChanged?.Invoke();

            Debug.Log($"[Passive] Activated: {passive.passiveName}");
            return true;
        }

        /// <summary>
        /// Deactivate a passive skill.
        /// </summary>
        public bool DeactivatePassive(string passiveId)
        {
            PassiveSkill passive = activePassives.Find(p => p.id == passiveId);
            if (passive == null) return false;

            return DeactivatePassive(passive);
        }

        /// <summary>
        /// Deactivate a passive skill.
        /// </summary>
        public bool DeactivatePassive(PassiveSkill passive)
        {
            if (passive == null) return false;
            if (!activePassives.Contains(passive)) return false;

            activePassives.Remove(passive);
            RemovePassiveEffects(passive);

            PassiveDeactivated?.Invoke(passive);
            onPassiveDeactivated?.Invoke(passive);
            StatsChanged?.Invoke();

            Debug.Log($"[Passive] Deactivated: {passive.passiveName}");
            return true;
        }

        private void ApplyPassiveEffects(PassiveSkill passive)
        {
            foreach (var effect in passive.effects)
            {
                if (effect.isPercent)
                {
                    percentBonuses[effect.statType] += effect.value;
                }
                else
                {
                    flatBonuses[effect.statType] += effect.value;
                }
            }
        }

        private void RemovePassiveEffects(PassiveSkill passive)
        {
            foreach (var effect in passive.effects)
            {
                if (effect.isPercent)
                {
                    percentBonuses[effect.statType] -= effect.value;
                }
                else
                {
                    flatBonuses[effect.statType] -= effect.value;
                }
            }
        }

        /// <summary>
        /// Get total flat bonus for a stat.
        /// </summary>
        public float GetFlatBonus(StatType stat)
        {
            return flatBonuses.TryGetValue(stat, out float bonus) ? bonus : 0;
        }

        /// <summary>
        /// Get total percent bonus for a stat.
        /// </summary>
        public float GetPercentBonus(StatType stat)
        {
            return percentBonuses.TryGetValue(stat, out float bonus) ? bonus : 0;
        }

        /// <summary>
        /// Calculate modified stat value.
        /// </summary>
        public float CalculateStat(StatType stat, float baseValue)
        {
            float flat = GetFlatBonus(stat);
            float percent = GetPercentBonus(stat);

            return (baseValue + flat) * (1f + percent / 100f);
        }

        /// <summary>
        /// Unlock a passive skill.
        /// </summary>
        public void UnlockPassive(string passiveId)
        {
            if (passiveMap.TryGetValue(passiveId, out PassiveSkill passive))
            {
                passive.isUnlocked = true;

                PassiveUnlocked?.Invoke(passive);
                onPassiveUnlocked?.Invoke(passive);

                Debug.Log($"[Passive] Unlocked: {passive.passiveName}");
            }
        }

        /// <summary>
        /// Check if passive is active.
        /// </summary>
        public bool IsPassiveActive(string passiveId)
        {
            return activePassives.Exists(p => p.id == passiveId);
        }

        /// <summary>
        /// Check if passive is unlocked.
        /// </summary>
        public bool IsPassiveUnlocked(string passiveId)
        {
            return passiveMap.TryGetValue(passiveId, out PassiveSkill passive) && passive.isUnlocked;
        }

        /// <summary>
        /// Get all active passives.
        /// </summary>
        public List<PassiveSkill> GetActivePassives()
        {
            return new List<PassiveSkill>(activePassives);
        }

        /// <summary>
        /// Get all unlocked passives.
        /// </summary>
        public List<PassiveSkill> GetUnlockedPassives()
        {
            return availablePassives.FindAll(p => p.isUnlocked);
        }

        /// <summary>
        /// Get all available passives.
        /// </summary>
        public List<PassiveSkill> GetAllPassives()
        {
            return new List<PassiveSkill>(availablePassives);
        }

        /// <summary>
        /// Set max passive slots.
        /// </summary>
        public void SetMaxSlots(int slots)
        {
            maxPassiveSlots = Mathf.Max(1, slots);

            // Deactivate excess passives
            while (activePassives.Count > maxPassiveSlots)
            {
                DeactivatePassive(activePassives[activePassives.Count - 1]);
            }
        }

        /// <summary>
        /// Check if character has a specific passive effect.
        /// </summary>
        public bool HasEffect(StatType stat)
        {
            return GetFlatBonus(stat) != 0 || GetPercentBonus(stat) != 0;
        }

        /// <summary>
        /// Get description of all active effects.
        /// </summary>
        public string GetActiveEffectsDescription()
        {
            string description = "";

            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                float flat = GetFlatBonus(stat);
                float percent = GetPercentBonus(stat);

                if (flat != 0)
                {
                    string sign = flat > 0 ? "+" : "";
                    description += $"{stat}: {sign}{flat}\n";
                }

                if (percent != 0)
                {
                    string sign = percent > 0 ? "+" : "";
                    description += $"{stat}: {sign}{percent}%\n";
                }
            }

            return description;
        }
    }

    [Serializable]
    public class PassiveSkill
    {
        public string id;
        public string passiveName;
        [TextArea]
        public string description;
        public Sprite icon;
        public int requiredLevel = 1;
        public bool isUnlocked = false;
        public List<PassiveEffect> effects = new List<PassiveEffect>();
        public List<string> requiredPassives = new List<string>();
    }

    [Serializable]
    public class PassiveEffect
    {
        public StatType statType;
        public float value;
        public bool isPercent;
    }

    public enum StatType
    {
        Health,
        Mana,
        Stamina,
        Damage,
        Defense,
        Speed,
        CritChance,
        CritDamage,
        AttackSpeed,
        CooldownReduction,
        HealthRegen,
        ManaRegen,
        StaminaRegen,
        Experience,
        Gold
    }
}
