using UnityEngine;
using System;

namespace UnityVault.Core
{
    /// <summary>
    /// ScriptableObject defining a status effect (buff/debuff).
    /// </summary>
    [CreateAssetMenu(fileName = "NewStatusEffect", menuName = "UnityVault/Status Effect")]
    public class StatusEffect : ScriptableObject
    {
        [Header("Basic Info")]
        public string effectId;
        public string effectName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("Type")]
        public EffectType effectType = EffectType.Buff;
        public EffectCategory category = EffectCategory.Generic;

        [Header("Duration")]
        public bool isPermanent = false;
        public float duration = 5f;
        public bool canStack = false;
        public int maxStacks = 1;
        public StackBehavior stackBehavior = StackBehavior.RefreshDuration;

        [Header("Effect Values")]
        public StatModifierType modifierType = StatModifierType.Flat;
        public float value = 10f;
        public float valuePerStack = 0f;

        [Header("Tick Damage/Heal")]
        public bool hasTick = false;
        public float tickInterval = 1f;
        public float tickValue = 5f;
        public bool tickIsDamage = true; // false = heal

        [Header("Movement")]
        public float speedModifier = 0f; // percentage: -0.5 = 50% slow

        [Header("Visual")]
        public GameObject visualEffectPrefab;
        public Color effectColor = Color.white;
    }

    public enum EffectType
    {
        Buff,
        Debuff,
        Neutral
    }

    public enum EffectCategory
    {
        Generic,
        Damage,
        Defense,
        Speed,
        Heal,
        DoT,      // Damage over Time
        HoT,      // Heal over Time
        Stun,
        Slow,
        Root,
        Silence,
        Invulnerable
    }

    public enum StatModifierType
    {
        Flat,
        Percentage,
        Multiplicative
    }

    public enum StackBehavior
    {
        RefreshDuration,
        AddDuration,
        IncreaseStacks,
        Replace
    }
}
