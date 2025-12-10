using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Core
{
    /// <summary>
    /// RPG stat system with base values and modifiers.
    /// </summary>
    public class StatSystem : MonoBehaviour
    {
        [Header("Base Stats")]
        [SerializeField] private List<StatDefinition> baseStats = new List<StatDefinition>();

        [Header("Events")]
        [SerializeField] private UnityEvent<StatType, float> onStatChanged;

        private Dictionary<StatType, Stat> stats = new Dictionary<StatType, Stat>();

        public event Action<StatType, float> StatChanged;

        private void Awake()
        {
            InitializeStats();
        }

        private void InitializeStats()
        {
            foreach (var def in baseStats)
            {
                stats[def.type] = new Stat(def.baseValue);
            }
        }

        public float GetStatValue(StatType type)
        {
            return stats.TryGetValue(type, out var stat) ? stat.Value : 0f;
        }

        public float GetBaseValue(StatType type)
        {
            return stats.TryGetValue(type, out var stat) ? stat.BaseValue : 0f;
        }

        public void SetBaseValue(StatType type, float value)
        {
            if (stats.TryGetValue(type, out var stat))
            {
                stat.BaseValue = value;
                NotifyStatChanged(type);
            }
        }

        public void AddModifier(StatType type, StatModifier modifier)
        {
            if (stats.TryGetValue(type, out var stat))
            {
                stat.AddModifier(modifier);
                NotifyStatChanged(type);
            }
        }

        public void RemoveModifier(StatType type, StatModifier modifier)
        {
            if (stats.TryGetValue(type, out var stat))
            {
                stat.RemoveModifier(modifier);
                NotifyStatChanged(type);
            }
        }

        public void RemoveModifiersBySource(object source)
        {
            foreach (var kvp in stats)
            {
                kvp.Value.RemoveModifiersBySource(source);
                NotifyStatChanged(kvp.Key);
            }
        }

        private void NotifyStatChanged(StatType type)
        {
            float value = GetStatValue(type);
            StatChanged?.Invoke(type, value);
            onStatChanged?.Invoke(type, value);
        }
    }

    [Serializable]
    public class StatDefinition
    {
        public StatType type;
        public float baseValue;
    }

    public enum StatType
    {
        Strength,
        Dexterity,
        Intelligence,
        Vitality,
        Luck,
        MaxHealth,
        MaxMana,
        Attack,
        Defense,
        Speed,
        CritChance,
        CritDamage
    }

    public class Stat
    {
        public float BaseValue { get; set; }
        private List<StatModifier> modifiers = new List<StatModifier>();
        private bool isDirty = true;
        private float cachedValue;

        public float Value
        {
            get
            {
                if (isDirty)
                {
                    cachedValue = CalculateValue();
                    isDirty = false;
                }
                return cachedValue;
            }
        }

        public Stat(float baseValue)
        {
            BaseValue = baseValue;
        }

        public void AddModifier(StatModifier modifier)
        {
            modifiers.Add(modifier);
            modifiers.Sort((a, b) => a.Order.CompareTo(b.Order));
            isDirty = true;
        }

        public void RemoveModifier(StatModifier modifier)
        {
            modifiers.Remove(modifier);
            isDirty = true;
        }

        public void RemoveModifiersBySource(object source)
        {
            modifiers.RemoveAll(m => m.Source == source);
            isDirty = true;
        }

        private float CalculateValue()
        {
            float finalValue = BaseValue;
            float sumPercentAdd = 0;

            foreach (var mod in modifiers)
            {
                switch (mod.Type)
                {
                    case ModifierType.Flat:
                        finalValue += mod.Value;
                        break;
                    case ModifierType.PercentAdd:
                        sumPercentAdd += mod.Value;
                        break;
                    case ModifierType.PercentMult:
                        finalValue *= 1 + mod.Value;
                        break;
                }
            }

            finalValue *= 1 + sumPercentAdd;
            return Mathf.Max(0, finalValue);
        }
    }

    [Serializable]
    public class StatModifier
    {
        public float Value;
        public ModifierType Type;
        public int Order;
        public object Source;

        public StatModifier(float value, ModifierType type, int order = 0, object source = null)
        {
            Value = value;
            Type = type;
            Order = order;
            Source = source;
        }
    }

    public enum ModifierType
    {
        Flat = 100,
        PercentAdd = 200,
        PercentMult = 300
    }
}
