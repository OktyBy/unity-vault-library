using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Combat
{
    /// <summary>
    /// Damage type system for elemental and physical damage with resistances.
    /// </summary>
    public class DamageTypeSystem : MonoBehaviour
    {
        [Header("Damage Configuration")]
        [SerializeField] private List<DamageTypeConfig> damageConfigs = new List<DamageTypeConfig>();

        [Header("Effectiveness Matrix")]
        [SerializeField] private List<TypeEffectiveness> effectivenessMatrix = new List<TypeEffectiveness>();

        [Header("Events")]
        [SerializeField] private UnityEvent<DamageInstance> onDamageProcessed;
        [SerializeField] private UnityEvent<ElementalDamageType, float> onResistanceChanged;

        // Runtime data
        private Dictionary<ElementalDamageType, DamageTypeConfig> configMap = new Dictionary<ElementalDamageType, DamageTypeConfig>();
        private Dictionary<(ElementalDamageType, ElementalDamageType), float> effectivenessMap = new Dictionary<(ElementalDamageType, ElementalDamageType), float>();

        // Events
        public event Action<DamageInstance> DamageProcessed;
        public event Action<ElementalDamageType, float> ResistanceChanged;

        private void Awake()
        {
            InitializeMaps();
        }

        private void InitializeMaps()
        {
            configMap.Clear();
            foreach (var config in damageConfigs)
            {
                configMap[config.damageType] = config;
            }

            effectivenessMap.Clear();
            foreach (var eff in effectivenessMatrix)
            {
                effectivenessMap[(eff.attackType, eff.defenseType)] = eff.multiplier;
            }

            // Set up default effectiveness if empty
            if (effectivenessMap.Count == 0)
            {
                SetupDefaultEffectiveness();
            }
        }

        private void SetupDefaultEffectiveness()
        {
            // Fire vs Ice = 1.5x (super effective)
            effectivenessMap[(ElementalDamageType.Fire, ElementalDamageType.Ice)] = 1.5f;
            effectivenessMap[(ElementalDamageType.Ice, ElementalDamageType.Fire)] = 0.5f;

            // Fire vs Fire = 0.5x (not effective)
            effectivenessMap[(ElementalDamageType.Fire, ElementalDamageType.Fire)] = 0.5f;
            effectivenessMap[(ElementalDamageType.Ice, ElementalDamageType.Ice)] = 0.5f;

            // Lightning vs Water = 2x
            effectivenessMap[(ElementalDamageType.Lightning, ElementalDamageType.Water)] = 2f;

            // Poison vs Poison = 0x (immune)
            effectivenessMap[(ElementalDamageType.Poison, ElementalDamageType.Poison)] = 0f;

            // Holy vs Dark = 1.5x (both ways)
            effectivenessMap[(ElementalDamageType.Holy, ElementalDamageType.Dark)] = 1.5f;
            effectivenessMap[(ElementalDamageType.Dark, ElementalDamageType.Holy)] = 1.5f;
        }

        /// <summary>
        /// Process damage with type effectiveness.
        /// </summary>
        public DamageInstance ProcessDamage(float baseDamage, ElementalDamageType attackType, ElementalDamageType targetAffinity = ElementalDamageType.None)
        {
            float multiplier = GetEffectiveness(attackType, targetAffinity);
            float finalDamage = baseDamage * multiplier;

            var result = new DamageInstance
            {
                baseDamage = baseDamage,
                finalDamage = finalDamage,
                damageType = attackType,
                targetAffinity = targetAffinity,
                effectiveness = multiplier,
                effectivenessLevel = GetEffectivenessLevel(multiplier)
            };

            DamageProcessed?.Invoke(result);
            onDamageProcessed?.Invoke(result);

            LogDamageResult(result);

            return result;
        }

        /// <summary>
        /// Process multi-type damage (e.g., fire + physical).
        /// </summary>
        public DamageInstance ProcessMultiTypeDamage(Dictionary<ElementalDamageType, float> damageByType, ElementalDamageType targetAffinity = ElementalDamageType.None)
        {
            float totalDamage = 0f;
            float totalBase = 0f;

            foreach (var kvp in damageByType)
            {
                float multiplier = GetEffectiveness(kvp.Key, targetAffinity);
                totalDamage += kvp.Value * multiplier;
                totalBase += kvp.Value;
            }

            return new DamageInstance
            {
                baseDamage = totalBase,
                finalDamage = totalDamage,
                damageType = ElementalDamageType.None, // Multi-type
                targetAffinity = targetAffinity,
                effectiveness = totalDamage / totalBase,
                effectivenessLevel = GetEffectivenessLevel(totalDamage / totalBase)
            };
        }

        public float GetEffectiveness(ElementalDamageType attackType, ElementalDamageType defenseType)
        {
            // Physical and True damage are not affected by type
            if (attackType == ElementalDamageType.Physical || attackType == ElementalDamageType.True)
                return 1f;

            // No affinity = neutral
            if (defenseType == ElementalDamageType.None)
                return 1f;

            if (effectivenessMap.TryGetValue((attackType, defenseType), out float multiplier))
            {
                return multiplier;
            }

            return 1f; // Default neutral
        }

        private EffectivenessLevel GetEffectivenessLevel(float multiplier)
        {
            if (multiplier <= 0f) return EffectivenessLevel.Immune;
            if (multiplier < 0.5f) return EffectivenessLevel.Resistant;
            if (multiplier < 1f) return EffectivenessLevel.NotEffective;
            if (multiplier == 1f) return EffectivenessLevel.Neutral;
            if (multiplier < 1.5f) return EffectivenessLevel.Effective;
            return EffectivenessLevel.SuperEffective;
        }

        private void LogDamageResult(DamageInstance result)
        {
            string effectText = result.effectivenessLevel switch
            {
                EffectivenessLevel.Immune => "IMMUNE!",
                EffectivenessLevel.Resistant => "Resistant...",
                EffectivenessLevel.NotEffective => "Not very effective...",
                EffectivenessLevel.Neutral => "",
                EffectivenessLevel.Effective => "Effective!",
                EffectivenessLevel.SuperEffective => "SUPER EFFECTIVE!",
                _ => ""
            };

            if (!string.IsNullOrEmpty(effectText))
            {
                Debug.Log($"[DamageType] {result.damageType} vs {result.targetAffinity}: {effectText} ({result.effectiveness:F1}x)");
            }
        }

        /// <summary>
        /// Set type effectiveness.
        /// </summary>
        public void SetEffectiveness(ElementalDamageType attackType, ElementalDamageType defenseType, float multiplier)
        {
            effectivenessMap[(attackType, defenseType)] = multiplier;

            // Update serialized list
            var existing = effectivenessMatrix.Find(e => e.attackType == attackType && e.defenseType == defenseType);
            if (existing != null)
            {
                existing.multiplier = multiplier;
            }
            else
            {
                effectivenessMatrix.Add(new TypeEffectiveness
                {
                    attackType = attackType,
                    defenseType = defenseType,
                    multiplier = multiplier
                });
            }
        }

        /// <summary>
        /// Get damage color for UI feedback.
        /// </summary>
        public Color GetDamageColor(ElementalDamageType type)
        {
            return type switch
            {
                ElementalDamageType.Physical => Color.white,
                ElementalDamageType.Fire => new Color(1f, 0.4f, 0.1f),
                ElementalDamageType.Ice => new Color(0.4f, 0.8f, 1f),
                ElementalDamageType.Lightning => new Color(1f, 1f, 0.3f),
                ElementalDamageType.Poison => new Color(0.5f, 1f, 0.3f),
                ElementalDamageType.Water => new Color(0.2f, 0.5f, 1f),
                ElementalDamageType.Earth => new Color(0.6f, 0.4f, 0.2f),
                ElementalDamageType.Wind => new Color(0.7f, 1f, 0.7f),
                ElementalDamageType.Holy => new Color(1f, 1f, 0.8f),
                ElementalDamageType.Dark => new Color(0.4f, 0.2f, 0.5f),
                ElementalDamageType.True => Color.red,
                _ => Color.white
            };
        }

        /// <summary>
        /// Get damage icon name for UI.
        /// </summary>
        public string GetDamageIconName(ElementalDamageType type)
        {
            return $"icon_damage_{type.ToString().ToLower()}";
        }
    }

    public enum ElementalDamageType
    {
        None,
        Physical,
        Fire,
        Ice,
        Lightning,
        Poison,
        Water,
        Earth,
        Wind,
        Holy,
        Dark,
        True // Ignores all resistances
    }

    public enum EffectivenessLevel
    {
        Immune,
        Resistant,
        NotEffective,
        Neutral,
        Effective,
        SuperEffective
    }

    [Serializable]
    public class DamageTypeConfig
    {
        public ElementalDamageType damageType;
        public Color displayColor = Color.white;
        public string iconName;
        public bool canCrit = true;
        public bool affectedByArmor = true;
    }

    [Serializable]
    public class TypeEffectiveness
    {
        public ElementalDamageType attackType;
        public ElementalDamageType defenseType;
        [Range(0f, 3f)]
        public float multiplier = 1f;
    }

    [Serializable]
    public struct DamageInstance
    {
        public float baseDamage;
        public float finalDamage;
        public ElementalDamageType damageType;
        public ElementalDamageType targetAffinity;
        public float effectiveness;
        public EffectivenessLevel effectivenessLevel;
    }
}
