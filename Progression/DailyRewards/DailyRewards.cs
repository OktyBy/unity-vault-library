using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

namespace UnityVault.Progression
{
    /// <summary>
    /// Daily rewards system with streak bonuses.
    /// </summary>
    public class DailyRewards : MonoBehaviour
    {
        public static DailyRewards Instance { get; private set; }

        [Header("Rewards")]
        [SerializeField] private List<DailyReward> rewards = new List<DailyReward>();
        [SerializeField] private bool loopRewards = true;
        [SerializeField] private int streakBonusThreshold = 7;
        [SerializeField] private float streakBonusMultiplier = 1.5f;

        [Header("Settings")]
        [SerializeField] private int resetHour = 0; // UTC hour for daily reset
        [SerializeField] private bool requireConsecutiveDays = true;
        [SerializeField] private int maxMissedDays = 1; // Days before streak resets

        [Header("UI References")]
        [SerializeField] private GameObject rewardsPanel;
        [SerializeField] private RectTransform rewardsContainer;
        [SerializeField] private GameObject rewardSlotPrefab;
        [SerializeField] private Button claimButton;
        [SerializeField] private TextMeshProUGUI streakText;
        [SerializeField] private TextMeshProUGUI nextRewardText;

        [Header("Events")]
        [SerializeField] private UnityEvent<DailyReward> onRewardClaimed;
        [SerializeField] private UnityEvent<int> onStreakUpdated;
        [SerializeField] private UnityEvent onStreakReset;

        // State
        private int currentStreak;
        private int currentDay;
        private DateTime lastClaimDate;
        private bool hasClaimedToday;
        private List<DailyRewardSlot> rewardSlots = new List<DailyRewardSlot>();

        // Events
        public event Action<DailyReward, int> RewardClaimed; // reward, day
        public event Action<int> StreakUpdated;
        public event Action StreakReset;
        public event Action<TimeSpan> TimeUntilNextReward;

        public int CurrentStreak => currentStreak;
        public int CurrentDay => currentDay;
        public bool CanClaim => !hasClaimedToday;
        public bool HasClaimedToday => hasClaimedToday;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Load();
        }

        private void Start()
        {
            CheckDailyReset();
            SetupUI();
            UpdateUI();
        }

        private void Update()
        {
            // Check for daily reset
            if (IsNewDay())
            {
                CheckDailyReset();
                UpdateUI();
            }
        }

        private bool IsNewDay()
        {
            DateTime now = DateTime.UtcNow;
            DateTime resetTime = new DateTime(now.Year, now.Month, now.Day, resetHour, 0, 0);

            if (now.Hour < resetHour)
            {
                resetTime = resetTime.AddDays(-1);
            }

            return lastClaimDate < resetTime && now >= resetTime && hasClaimedToday;
        }

        private void CheckDailyReset()
        {
            DateTime now = DateTime.UtcNow;
            DateTime lastReset = GetLastResetTime();

            // Check if it's a new day
            if (lastClaimDate.Date < lastReset.Date)
            {
                hasClaimedToday = false;

                // Check streak
                if (requireConsecutiveDays)
                {
                    int daysSinceLastClaim = (int)(lastReset.Date - lastClaimDate.Date).TotalDays;

                    if (daysSinceLastClaim > maxMissedDays + 1)
                    {
                        // Streak broken
                        ResetStreak();
                    }
                }
            }
        }

        private DateTime GetLastResetTime()
        {
            DateTime now = DateTime.UtcNow;
            DateTime resetTime = new DateTime(now.Year, now.Month, now.Day, resetHour, 0, 0);

            if (now.Hour < resetHour)
            {
                resetTime = resetTime.AddDays(-1);
            }

            return resetTime;
        }

        private void SetupUI()
        {
            if (claimButton != null)
            {
                claimButton.onClick.AddListener(ClaimReward);
            }

            if (rewardsContainer != null && rewardSlotPrefab != null)
            {
                // Create reward slots
                for (int i = 0; i < rewards.Count; i++)
                {
                    GameObject slotObj = Instantiate(rewardSlotPrefab, rewardsContainer);
                    DailyRewardSlot slot = slotObj.GetComponent<DailyRewardSlot>();

                    if (slot != null)
                    {
                        slot.Setup(i + 1, rewards[i]);
                        rewardSlots.Add(slot);
                    }
                }
            }
        }

        private void UpdateUI()
        {
            // Update streak text
            if (streakText != null)
            {
                streakText.text = $"Streak: {currentStreak} days";
            }

            // Update next reward timer
            if (nextRewardText != null)
            {
                if (hasClaimedToday)
                {
                    TimeSpan timeUntilReset = GetTimeUntilNextReward();
                    nextRewardText.text = $"Next reward in: {timeUntilReset.Hours:D2}:{timeUntilReset.Minutes:D2}:{timeUntilReset.Seconds:D2}";
                }
                else
                {
                    nextRewardText.text = "Reward available!";
                }
            }

            // Update claim button
            if (claimButton != null)
            {
                claimButton.interactable = CanClaim;
            }

            // Update reward slots
            for (int i = 0; i < rewardSlots.Count; i++)
            {
                bool isClaimed = i < currentDay;
                bool isCurrent = i == currentDay && !hasClaimedToday;
                bool isLocked = i > currentDay || (i == currentDay && hasClaimedToday);

                rewardSlots[i].UpdateState(isClaimed, isCurrent, isLocked);
            }
        }

        /// <summary>
        /// Claim today's reward.
        /// </summary>
        public void ClaimReward()
        {
            if (hasClaimedToday)
            {
                Debug.Log("[DailyRewards] Already claimed today");
                return;
            }

            int rewardIndex = currentDay;
            if (loopRewards && rewards.Count > 0)
            {
                rewardIndex = currentDay % rewards.Count;
            }

            if (rewardIndex >= rewards.Count)
            {
                Debug.Log("[DailyRewards] No more rewards");
                return;
            }

            DailyReward reward = rewards[rewardIndex];

            // Apply streak bonus
            float multiplier = 1f;
            if (currentStreak >= streakBonusThreshold)
            {
                multiplier = streakBonusMultiplier;
            }

            // Give reward
            GiveReward(reward, multiplier);

            // Update state
            hasClaimedToday = true;
            lastClaimDate = DateTime.UtcNow;
            currentDay++;
            currentStreak++;

            RewardClaimed?.Invoke(reward, currentDay);
            onRewardClaimed?.Invoke(reward);

            StreakUpdated?.Invoke(currentStreak);
            onStreakUpdated?.Invoke(currentStreak);

            Save();
            UpdateUI();

            Debug.Log($"[DailyRewards] Claimed day {currentDay} reward. Streak: {currentStreak}");
        }

        private void GiveReward(DailyReward reward, float multiplier)
        {
            int amount = Mathf.RoundToInt(reward.amount * multiplier);

            switch (reward.rewardType)
            {
                case DailyRewardType.Currency:
                    // TODO: Add currency through your currency system
                    Debug.Log($"[DailyRewards] +{amount} {reward.rewardId}");
                    break;

                case DailyRewardType.Item:
                    // TODO: Add item through your inventory system
                    Debug.Log($"[DailyRewards] +{amount} {reward.rewardId}");
                    break;

                case DailyRewardType.Experience:
                    // TODO: Add XP through your level system
                    Debug.Log($"[DailyRewards] +{amount} XP");
                    break;

                case DailyRewardType.Premium:
                    // TODO: Add premium currency
                    Debug.Log($"[DailyRewards] +{amount} premium currency");
                    break;
            }
        }

        /// <summary>
        /// Reset streak.
        /// </summary>
        public void ResetStreak()
        {
            currentStreak = 0;
            currentDay = 0;

            StreakReset?.Invoke();
            onStreakReset?.Invoke();

            Save();
            UpdateUI();

            Debug.Log("[DailyRewards] Streak reset");
        }

        /// <summary>
        /// Get time until next reward.
        /// </summary>
        public TimeSpan GetTimeUntilNextReward()
        {
            DateTime now = DateTime.UtcNow;
            DateTime nextReset = new DateTime(now.Year, now.Month, now.Day, resetHour, 0, 0);

            if (now.Hour >= resetHour)
            {
                nextReset = nextReset.AddDays(1);
            }

            return nextReset - now;
        }

        /// <summary>
        /// Get today's reward (without claiming).
        /// </summary>
        public DailyReward GetTodaysReward()
        {
            int rewardIndex = currentDay;
            if (loopRewards && rewards.Count > 0)
            {
                rewardIndex = currentDay % rewards.Count;
            }

            if (rewardIndex < rewards.Count)
            {
                return rewards[rewardIndex];
            }

            return null;
        }

        /// <summary>
        /// Show rewards panel.
        /// </summary>
        public void ShowPanel()
        {
            if (rewardsPanel != null)
            {
                rewardsPanel.SetActive(true);
                UpdateUI();
            }
        }

        /// <summary>
        /// Hide rewards panel.
        /// </summary>
        public void HidePanel()
        {
            if (rewardsPanel != null)
            {
                rewardsPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Save daily rewards data.
        /// </summary>
        public void Save()
        {
            DailyRewardsSaveData data = new DailyRewardsSaveData
            {
                currentStreak = currentStreak,
                currentDay = currentDay,
                lastClaimDate = lastClaimDate.ToString("o"),
                hasClaimedToday = hasClaimedToday
            };

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString("DailyRewards", json);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Load daily rewards data.
        /// </summary>
        public void Load()
        {
            string json = PlayerPrefs.GetString("DailyRewards", "");
            if (string.IsNullOrEmpty(json))
            {
                lastClaimDate = DateTime.MinValue;
                return;
            }

            try
            {
                DailyRewardsSaveData data = JsonUtility.FromJson<DailyRewardsSaveData>(json);
                currentStreak = data.currentStreak;
                currentDay = data.currentDay;
                lastClaimDate = DateTime.Parse(data.lastClaimDate);
                hasClaimedToday = data.hasClaimedToday;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DailyRewards] Failed to load: {e.Message}");
                lastClaimDate = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Reset all daily rewards data.
        /// </summary>
        public void ResetAll()
        {
            currentStreak = 0;
            currentDay = 0;
            hasClaimedToday = false;
            lastClaimDate = DateTime.MinValue;

            PlayerPrefs.DeleteKey("DailyRewards");
            UpdateUI();
        }
    }

    [Serializable]
    public class DailyReward
    {
        public string rewardId;
        public string rewardName;
        [TextArea]
        public string description;
        public Sprite icon;
        public DailyRewardType rewardType;
        public int amount;
        public bool isBonus;
    }

    public enum DailyRewardType
    {
        Currency,
        Item,
        Experience,
        Premium
    }

    [Serializable]
    public class DailyRewardsSaveData
    {
        public int currentStreak;
        public int currentDay;
        public string lastClaimDate;
        public bool hasClaimedToday;
    }

    /// <summary>
    /// UI slot for daily reward.
    /// </summary>
    public class DailyRewardSlot : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private GameObject claimedOverlay;
        [SerializeField] private GameObject currentHighlight;
        [SerializeField] private GameObject lockedOverlay;

        private int day;
        private DailyReward reward;

        public void Setup(int dayNumber, DailyReward dailyReward)
        {
            day = dayNumber;
            reward = dailyReward;

            if (dayText != null)
            {
                dayText.text = $"Day {day}";
            }

            if (iconImage != null && reward.icon != null)
            {
                iconImage.sprite = reward.icon;
            }

            if (amountText != null)
            {
                amountText.text = $"x{reward.amount}";
            }
        }

        public void UpdateState(bool claimed, bool current, bool locked)
        {
            if (claimedOverlay != null)
                claimedOverlay.SetActive(claimed);

            if (currentHighlight != null)
                currentHighlight.SetActive(current);

            if (lockedOverlay != null)
                lockedOverlay.SetActive(locked && !claimed);
        }
    }
}
