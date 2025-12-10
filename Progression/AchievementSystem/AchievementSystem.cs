using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Progression
{
    /// <summary>
    /// Achievement system for tracking player accomplishments.
    /// </summary>
    public class AchievementSystem : MonoBehaviour
    {
        public static AchievementSystem Instance { get; private set; }

        [Header("Achievements")]
        [SerializeField] private List<Achievement> achievements = new List<Achievement>();

        [Header("Settings")]
        [SerializeField] private bool saveOnUnlock = true;
        [SerializeField] private string saveKey = "Achievements";

        [Header("UI")]
        [SerializeField] private GameObject unlockNotificationPrefab;
        [SerializeField] private Transform notificationParent;
        [SerializeField] private float notificationDuration = 3f;

        [Header("Events")]
        [SerializeField] private UnityEvent<Achievement> onAchievementUnlocked;
        [SerializeField] private UnityEvent<Achievement, float> onAchievementProgress;

        // State
        private Dictionary<string, AchievementData> achievementData = new Dictionary<string, AchievementData>();

        // Events
        public event Action<Achievement> AchievementUnlocked;
        public event Action<Achievement, float> AchievementProgress;
        public event Action<int> TotalPointsChanged;
        public event Action AllAchievementsUnlocked;

        public int TotalPoints { get; private set; }
        public int UnlockedCount => GetUnlockedAchievements().Count;
        public int TotalCount => achievements.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeAchievements();
            Load();
        }

        private void InitializeAchievements()
        {
            foreach (var achievement in achievements)
            {
                achievementData[achievement.achievementId] = new AchievementData
                {
                    achievement = achievement,
                    isUnlocked = false,
                    currentProgress = 0,
                    unlockDate = null
                };
            }
        }

        /// <summary>
        /// Unlock an achievement.
        /// </summary>
        public void Unlock(string achievementId)
        {
            if (!achievementData.TryGetValue(achievementId, out AchievementData data)) return;
            if (data.isUnlocked) return;

            // Check prerequisites
            if (!CheckPrerequisites(data.achievement))
            {
                return;
            }

            data.isUnlocked = true;
            data.unlockDate = DateTime.Now;
            data.currentProgress = data.achievement.targetProgress;

            TotalPoints += data.achievement.points;

            // Show notification
            ShowUnlockNotification(data.achievement);

            AchievementUnlocked?.Invoke(data.achievement);
            onAchievementUnlocked?.Invoke(data.achievement);
            TotalPointsChanged?.Invoke(TotalPoints);

            Debug.Log($"[Achievement] Unlocked: {data.achievement.title}");

            // Check if all unlocked
            if (UnlockedCount >= TotalCount)
            {
                AllAchievementsUnlocked?.Invoke();
            }

            if (saveOnUnlock)
            {
                Save();
            }
        }

        /// <summary>
        /// Update achievement progress.
        /// </summary>
        public void SetProgress(string achievementId, float progress)
        {
            if (!achievementData.TryGetValue(achievementId, out AchievementData data)) return;
            if (data.isUnlocked) return;

            data.currentProgress = Mathf.Clamp(progress, 0, data.achievement.targetProgress);

            float percentage = data.currentProgress / data.achievement.targetProgress;

            AchievementProgress?.Invoke(data.achievement, percentage);
            onAchievementProgress?.Invoke(data.achievement, percentage);

            // Check if completed
            if (data.currentProgress >= data.achievement.targetProgress)
            {
                Unlock(achievementId);
            }
        }

        /// <summary>
        /// Add to achievement progress.
        /// </summary>
        public void AddProgress(string achievementId, float amount)
        {
            if (!achievementData.TryGetValue(achievementId, out AchievementData data)) return;

            SetProgress(achievementId, data.currentProgress + amount);
        }

        /// <summary>
        /// Increment achievement progress by 1.
        /// </summary>
        public void Increment(string achievementId)
        {
            AddProgress(achievementId, 1);
        }

        /// <summary>
        /// Check if achievement is unlocked.
        /// </summary>
        public bool IsUnlocked(string achievementId)
        {
            return achievementData.TryGetValue(achievementId, out AchievementData data) && data.isUnlocked;
        }

        /// <summary>
        /// Get achievement progress.
        /// </summary>
        public float GetProgress(string achievementId)
        {
            if (!achievementData.TryGetValue(achievementId, out AchievementData data))
            {
                return 0;
            }

            return data.currentProgress / data.achievement.targetProgress;
        }

        /// <summary>
        /// Get current progress value.
        /// </summary>
        public float GetProgressValue(string achievementId)
        {
            return achievementData.TryGetValue(achievementId, out AchievementData data) ?
                data.currentProgress : 0;
        }

        private bool CheckPrerequisites(Achievement achievement)
        {
            if (achievement.prerequisites == null || achievement.prerequisites.Count == 0)
            {
                return true;
            }

            foreach (string prereqId in achievement.prerequisites)
            {
                if (!IsUnlocked(prereqId))
                {
                    return false;
                }
            }

            return true;
        }

        private void ShowUnlockNotification(Achievement achievement)
        {
            if (unlockNotificationPrefab == null) return;

            Transform parent = notificationParent != null ? notificationParent : transform;
            GameObject notification = Instantiate(unlockNotificationPrefab, parent);

            AchievementNotification notif = notification.GetComponent<AchievementNotification>();
            if (notif != null)
            {
                notif.Setup(achievement);
            }

            Destroy(notification, notificationDuration);
        }

        /// <summary>
        /// Get achievement by ID.
        /// </summary>
        public Achievement GetAchievement(string achievementId)
        {
            return achievementData.TryGetValue(achievementId, out AchievementData data) ?
                data.achievement : null;
        }

        /// <summary>
        /// Get all achievements.
        /// </summary>
        public List<Achievement> GetAllAchievements()
        {
            return new List<Achievement>(achievements);
        }

        /// <summary>
        /// Get unlocked achievements.
        /// </summary>
        public List<Achievement> GetUnlockedAchievements()
        {
            List<Achievement> unlocked = new List<Achievement>();

            foreach (var data in achievementData.Values)
            {
                if (data.isUnlocked)
                {
                    unlocked.Add(data.achievement);
                }
            }

            return unlocked;
        }

        /// <summary>
        /// Get locked achievements.
        /// </summary>
        public List<Achievement> GetLockedAchievements()
        {
            List<Achievement> locked = new List<Achievement>();

            foreach (var data in achievementData.Values)
            {
                if (!data.isUnlocked)
                {
                    locked.Add(data.achievement);
                }
            }

            return locked;
        }

        /// <summary>
        /// Get achievements by category.
        /// </summary>
        public List<Achievement> GetAchievementsByCategory(AchievementCategory category)
        {
            List<Achievement> result = new List<Achievement>();

            foreach (var achievement in achievements)
            {
                if (achievement.category == category)
                {
                    result.Add(achievement);
                }
            }

            return result;
        }

        /// <summary>
        /// Get completion percentage.
        /// </summary>
        public float GetCompletionPercentage()
        {
            if (TotalCount == 0) return 0;
            return (float)UnlockedCount / TotalCount * 100f;
        }

        /// <summary>
        /// Save achievements to PlayerPrefs.
        /// </summary>
        public void Save()
        {
            AchievementSaveData saveData = new AchievementSaveData();

            foreach (var kvp in achievementData)
            {
                if (kvp.Value.isUnlocked || kvp.Value.currentProgress > 0)
                {
                    saveData.achievements.Add(new AchievementSaveEntry
                    {
                        achievementId = kvp.Key,
                        isUnlocked = kvp.Value.isUnlocked,
                        currentProgress = kvp.Value.currentProgress,
                        unlockDate = kvp.Value.unlockDate?.ToString("o") ?? ""
                    });
                }
            }

            saveData.totalPoints = TotalPoints;

            string json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString(saveKey, json);
            PlayerPrefs.Save();

            Debug.Log("[Achievement] Saved");
        }

        /// <summary>
        /// Load achievements from PlayerPrefs.
        /// </summary>
        public void Load()
        {
            string json = PlayerPrefs.GetString(saveKey, "");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                AchievementSaveData saveData = JsonUtility.FromJson<AchievementSaveData>(json);

                foreach (var entry in saveData.achievements)
                {
                    if (achievementData.TryGetValue(entry.achievementId, out AchievementData data))
                    {
                        data.isUnlocked = entry.isUnlocked;
                        data.currentProgress = entry.currentProgress;

                        if (!string.IsNullOrEmpty(entry.unlockDate))
                        {
                            data.unlockDate = DateTime.Parse(entry.unlockDate);
                        }
                    }
                }

                TotalPoints = saveData.totalPoints;

                Debug.Log("[Achievement] Loaded");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Achievement] Failed to load: {e.Message}");
            }
        }

        /// <summary>
        /// Reset all achievements.
        /// </summary>
        public void ResetAll()
        {
            foreach (var data in achievementData.Values)
            {
                data.isUnlocked = false;
                data.currentProgress = 0;
                data.unlockDate = null;
            }

            TotalPoints = 0;

            PlayerPrefs.DeleteKey(saveKey);
            Debug.Log("[Achievement] Reset all");
        }
    }

    [Serializable]
    public class Achievement
    {
        public string achievementId;
        public string title;
        [TextArea]
        public string description;
        public Sprite icon;
        public int points = 10;
        public AchievementCategory category;
        public AchievementRarity rarity = AchievementRarity.Common;

        [Header("Progress")]
        public float targetProgress = 1;
        public bool isHidden;

        [Header("Prerequisites")]
        public List<string> prerequisites = new List<string>();

        [Header("Rewards")]
        public List<AchievementReward> rewards = new List<AchievementReward>();
    }

    [Serializable]
    public class AchievementData
    {
        public Achievement achievement;
        public bool isUnlocked;
        public float currentProgress;
        public DateTime? unlockDate;
    }

    [Serializable]
    public class AchievementReward
    {
        public RewardType rewardType;
        public string rewardId;
        public int amount;
    }

    public enum AchievementCategory
    {
        General,
        Combat,
        Exploration,
        Collection,
        Social,
        Story,
        Challenge,
        Secret
    }

    public enum AchievementRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum RewardType
    {
        Currency,
        Item,
        Experience,
        Title,
        Cosmetic
    }

    [Serializable]
    public class AchievementSaveData
    {
        public List<AchievementSaveEntry> achievements = new List<AchievementSaveEntry>();
        public int totalPoints;
    }

    [Serializable]
    public class AchievementSaveEntry
    {
        public string achievementId;
        public bool isUnlocked;
        public float currentProgress;
        public string unlockDate;
    }

    /// <summary>
    /// Achievement notification UI component.
    /// </summary>
    public class AchievementNotification : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image iconImage;
        [SerializeField] private TMPro.TextMeshProUGUI titleText;
        [SerializeField] private TMPro.TextMeshProUGUI descriptionText;
        [SerializeField] private TMPro.TextMeshProUGUI pointsText;

        public void Setup(Achievement achievement)
        {
            if (iconImage != null && achievement.icon != null)
            {
                iconImage.sprite = achievement.icon;
            }

            if (titleText != null)
            {
                titleText.text = achievement.title;
            }

            if (descriptionText != null)
            {
                descriptionText.text = achievement.description;
            }

            if (pointsText != null)
            {
                pointsText.text = $"+{achievement.points} pts";
            }
        }
    }
}
