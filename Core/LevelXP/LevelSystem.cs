using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Core
{
    /// <summary>
    /// Experience and leveling system.
    /// </summary>
    public class LevelSystem : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int startingLevel = 1;
        [SerializeField] private int maxLevel = 100;
        [SerializeField] private ExperienceCurve experienceCurve = ExperienceCurve.Quadratic;
        [SerializeField] private int baseXPRequired = 100;
        [SerializeField] private float growthFactor = 1.5f;

        [Header("Current State")]
        [SerializeField] private int currentLevel;
        [SerializeField] private int currentXP;
        [SerializeField] private int totalXP;

        [Header("Events")]
        [SerializeField] private UnityEvent<int> onLevelUp;
        [SerializeField] private UnityEvent<int, int> onXPGained; // current, max

        public int Level => currentLevel;
        public int CurrentXP => currentXP;
        public int TotalXP => totalXP;
        public int XPToNextLevel => GetXPForLevel(currentLevel + 1) - GetTotalXPForLevel(currentLevel);
        public float LevelProgress => (float)currentXP / XPToNextLevel;
        public bool IsMaxLevel => currentLevel >= maxLevel;

        public event Action<int> LeveledUp;
        public event Action<int, int> XPGained;

        private void Awake()
        {
            currentLevel = startingLevel;
            currentXP = 0;
            totalXP = GetTotalXPForLevel(startingLevel);
        }

        public void AddXP(int amount)
        {
            if (IsMaxLevel || amount <= 0) return;

            currentXP += amount;
            totalXP += amount;

            XPGained?.Invoke(currentXP, XPToNextLevel);
            onXPGained?.Invoke(currentXP, XPToNextLevel);

            while (currentXP >= XPToNextLevel && !IsMaxLevel)
            {
                currentXP -= XPToNextLevel;
                LevelUp();
            }

            if (IsMaxLevel)
            {
                currentXP = 0;
            }
        }

        private void LevelUp()
        {
            currentLevel++;
            LeveledUp?.Invoke(currentLevel);
            onLevelUp?.Invoke(currentLevel);
            Debug.Log($"[LevelSystem] Level Up! Now level {currentLevel}");
        }

        public void SetLevel(int level)
        {
            currentLevel = Mathf.Clamp(level, 1, maxLevel);
            currentXP = 0;
            totalXP = GetTotalXPForLevel(currentLevel);
        }

        public int GetXPForLevel(int level)
        {
            if (level <= 1) return 0;

            switch (experienceCurve)
            {
                case ExperienceCurve.Linear:
                    return baseXPRequired * level;

                case ExperienceCurve.Quadratic:
                    return Mathf.RoundToInt(baseXPRequired * Mathf.Pow(level, growthFactor));

                case ExperienceCurve.Exponential:
                    return Mathf.RoundToInt(baseXPRequired * Mathf.Pow(growthFactor, level - 1));

                default:
                    return baseXPRequired * level;
            }
        }

        public int GetTotalXPForLevel(int level)
        {
            int total = 0;
            for (int i = 1; i <= level; i++)
            {
                total += GetXPForLevel(i);
            }
            return total;
        }

        public int GetLevelFromTotalXP(int xp)
        {
            int level = 1;
            int totalRequired = 0;

            while (level < maxLevel)
            {
                totalRequired += GetXPForLevel(level + 1);
                if (xp < totalRequired) break;
                level++;
            }

            return level;
        }
    }

    public enum ExperienceCurve
    {
        Linear,
        Quadratic,
        Exponential
    }
}
