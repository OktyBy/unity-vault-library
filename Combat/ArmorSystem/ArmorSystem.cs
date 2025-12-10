using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Combat
{
    /// <summary>
    /// Armor system with defense, resistance, and durability mechanics.
    /// </summary>
    public class ArmorSystem : MonoBehaviour
    {
        [Header("Base Defense")]
        [SerializeField] private float baseDefense = 10f;
        [SerializeField] private float defenseMultiplier = 1f;

        [Header("Damage Reduction")]
        [SerializeField] private float flatDamageReduction = 0f;
        [SerializeField] private float percentDamageReduction = 0f;
        [SerializeField] private float maxDamageReduction = 0.8f; // Cap at 80%

        [Header("Resistances")]
        [SerializeField] private List<DamageResistance> resistances = new List<DamageResistance>();

        [Header("Durability")]
        [SerializeField] private bool useDurability = true;
        [SerializeField] private float maxDurability = 100f;
        [SerializeField] private float durabilityLossPerHit = 1f;
        [SerializeField] private float brokenDefenseMultiplier = 0.5f;

        [Header("Events")]
        [SerializeField] private UnityEvent<float, float> onDamageReduced;
        [SerializeField] private UnityEvent<float> onDurabilityChanged;
        [SerializeField] private UnityEvent onArmorBroken;
        [SerializeField] private UnityEvent onArmorRepaired;

        // State
        private float currentDurability;
        private bool isBroken;
        private Dictionary<DamageType, float> resistanceMap = new Dictionary<DamageType, float>();

        // Properties
        public float CurrentDefense => CalculateTotalDefense();
        public float CurrentDurability => currentDurability;
        public float DurabilityPercent => currentDurability / maxDurability;
        public bool IsBroken => isBroken;
        public float BaseDefense => baseDefense;

        // Events
        public event Action<float, float> DamageReduced; // original, reduced
        public event Action<float> DurabilityChanged;
        public event Action ArmorBroken;
        public event Action ArmorRepaired;

        private void Awake()
        {
            currentDurability = maxDurability;
            BuildResistanceMap();
        }

        private void BuildResistanceMap()
        {
            resistanceMap.Clear();
            foreach (var resistance in resistances)
            {
                resistanceMap[resistance.damageType] = resistance.resistancePercent;
            }
        }

        private float CalculateTotalDefense()
        {
            float defense = baseDefense * defenseMultiplier;

            if (isBroken)
            {
                defense *= brokenDefenseMultiplier;
            }

            return defense;
        }

        /// <summary>
        /// Process incoming damage through armor.
        /// Returns the final damage after reduction.
        /// </summary>
        public float ProcessDamage(float incomingDamage, DamageType damageType = DamageType.Physical)
        {
            float originalDamage = incomingDamage;

            // Apply resistance
            float resistance = GetResistance(damageType);
            incomingDamage *= (1f - resistance);

            // Apply flat reduction
            incomingDamage -= flatDamageReduction;
            incomingDamage = Mathf.Max(0, incomingDamage);

            // Apply defense-based reduction
            float defenseReduction = CalculateDefenseReduction(incomingDamage);
            incomingDamage -= defenseReduction;

            // Apply percent reduction
            incomingDamage *= (1f - percentDamageReduction);

            // Apply cap
            float totalReduction = 1f - (incomingDamage / originalDamage);
            if (totalReduction > maxDamageReduction)
            {
                incomingDamage = originalDamage * (1f - maxDamageReduction);
            }

            // Ensure minimum damage
            incomingDamage = Mathf.Max(1f, incomingDamage);

            // Reduce durability
            if (useDurability && !isBroken)
            {
                ReduceDurability(durabilityLossPerHit);
            }

            float reducedAmount = originalDamage - incomingDamage;
            DamageReduced?.Invoke(originalDamage, incomingDamage);
            onDamageReduced?.Invoke(originalDamage, incomingDamage);

            Debug.Log($"[Armor] Reduced {reducedAmount:F1} damage ({damageType}): {originalDamage:F1} -> {incomingDamage:F1}");

            return incomingDamage;
        }

        private float CalculateDefenseReduction(float damage)
        {
            // Defense formula: reduction = defense / (defense + damage)
            float defense = CurrentDefense;
            float reductionPercent = defense / (defense + damage + 100f);
            return damage * reductionPercent;
        }

        public float GetResistance(DamageType damageType)
        {
            if (resistanceMap.TryGetValue(damageType, out float resistance))
            {
                return Mathf.Clamp01(resistance);
            }
            return 0f;
        }

        public void SetResistance(DamageType damageType, float value)
        {
            resistanceMap[damageType] = Mathf.Clamp(value, -1f, 1f); // Negative = vulnerability

            // Update serialized list
            var existing = resistances.Find(r => r.damageType == damageType);
            if (existing != null)
            {
                existing.resistancePercent = value;
            }
            else
            {
                resistances.Add(new DamageResistance { damageType = damageType, resistancePercent = value });
            }
        }

        public void AddResistance(DamageType damageType, float amount)
        {
            float current = GetResistance(damageType);
            SetResistance(damageType, current + amount);
        }

        private void ReduceDurability(float amount)
        {
            if (!useDurability || isBroken) return;

            currentDurability = Mathf.Max(0, currentDurability - amount);
            DurabilityChanged?.Invoke(currentDurability);
            onDurabilityChanged?.Invoke(currentDurability);

            if (currentDurability <= 0 && !isBroken)
            {
                BreakArmor();
            }
        }

        private void BreakArmor()
        {
            isBroken = true;
            ArmorBroken?.Invoke();
            onArmorBroken?.Invoke();
            Debug.Log("[Armor] Armor broken!");
        }

        public void RepairArmor(float amount)
        {
            if (!useDurability) return;

            bool wasBroken = isBroken;
            currentDurability = Mathf.Min(maxDurability, currentDurability + amount);

            if (currentDurability > 0 && isBroken)
            {
                isBroken = false;
                ArmorRepaired?.Invoke();
                onArmorRepaired?.Invoke();
                Debug.Log("[Armor] Armor repaired!");
            }

            DurabilityChanged?.Invoke(currentDurability);
            onDurabilityChanged?.Invoke(currentDurability);
        }

        public void FullRepair()
        {
            RepairArmor(maxDurability);
        }

        // Stat modifiers
        public void SetBaseDefense(float value) => baseDefense = value;
        public void AddBaseDefense(float amount) => baseDefense += amount;
        public void SetDefenseMultiplier(float value) => defenseMultiplier = value;
        public void SetFlatReduction(float value) => flatDamageReduction = value;
        public void SetPercentReduction(float value) => percentDamageReduction = Mathf.Clamp01(value);

        /// <summary>
        /// Get armor stats for UI display.
        /// </summary>
        public ArmorStats GetStats()
        {
            return new ArmorStats
            {
                defense = CurrentDefense,
                flatReduction = flatDamageReduction,
                percentReduction = percentDamageReduction,
                durability = currentDurability,
                maxDurability = maxDurability,
                isBroken = isBroken,
                resistances = new Dictionary<DamageType, float>(resistanceMap)
            };
        }
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
        True // Ignores armor
    }

    [Serializable]
    public class DamageResistance
    {
        public DamageType damageType;
        [Range(-1f, 1f)]
        public float resistancePercent; // Negative = vulnerability
    }

    public struct ArmorStats
    {
        public float defense;
        public float flatReduction;
        public float percentReduction;
        public float durability;
        public float maxDurability;
        public bool isBroken;
        public Dictionary<DamageType, float> resistances;
    }
}
