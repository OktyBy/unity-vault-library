using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Dialogue
{
    /// <summary>
    /// Faction reputation system for tracking player standing with different groups.
    /// </summary>
    public class ReputationSystem : MonoBehaviour
    {
        public static ReputationSystem Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int minReputation = -100;
        [SerializeField] private int maxReputation = 100;
        [SerializeField] private int defaultReputation = 0;
        [SerializeField] private bool saveOnChange = true;

        [Header("Factions")]
        [SerializeField] private List<Faction> factions = new List<Faction>();

        [Header("Reputation Thresholds")]
        [SerializeField] private ReputationTier[] tiers = new ReputationTier[]
        {
            new ReputationTier { tierName = "Hated", minValue = -100, maxValue = -50 },
            new ReputationTier { tierName = "Hostile", minValue = -49, maxValue = -25 },
            new ReputationTier { tierName = "Unfriendly", minValue = -24, maxValue = -1 },
            new ReputationTier { tierName = "Neutral", minValue = 0, maxValue = 24 },
            new ReputationTier { tierName = "Friendly", minValue = 25, maxValue = 49 },
            new ReputationTier { tierName = "Honored", minValue = 50, maxValue = 74 },
            new ReputationTier { tierName = "Revered", minValue = 75, maxValue = 99 },
            new ReputationTier { tierName = "Exalted", minValue = 100, maxValue = 100 }
        };

        [Header("Events")]
        [SerializeField] private UnityEvent<string, int> onReputationChanged;
        [SerializeField] private UnityEvent<string, ReputationTier> onTierChanged;
        [SerializeField] private UnityEvent<string, string> onFactionRelationChanged;

        // State
        private Dictionary<string, FactionData> factionData = new Dictionary<string, FactionData>();

        // Events
        public event Action<string, int, int> ReputationChanged; // faction, old, new
        public event Action<string, ReputationTier, ReputationTier> TierChanged;
        public event Action<string, string, FactionRelation> RelationChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeFactions();
        }

        private void InitializeFactions()
        {
            foreach (var faction in factions)
            {
                factionData[faction.factionId] = new FactionData
                {
                    faction = faction,
                    reputation = faction.startingReputation != 0 ? faction.startingReputation : defaultReputation
                };
            }
        }

        /// <summary>
        /// Add reputation to a faction.
        /// </summary>
        public void AddReputation(string factionId, int amount)
        {
            if (!factionData.TryGetValue(factionId, out FactionData data)) return;

            int oldValue = data.reputation;
            ReputationTier oldTier = GetTier(oldValue);

            data.reputation = Mathf.Clamp(data.reputation + amount, minReputation, maxReputation);

            ReputationTier newTier = GetTier(data.reputation);

            ReputationChanged?.Invoke(factionId, oldValue, data.reputation);
            onReputationChanged?.Invoke(factionId, data.reputation);

            if (oldTier != newTier)
            {
                TierChanged?.Invoke(factionId, oldTier, newTier);
                onTierChanged?.Invoke(factionId, newTier);
            }

            // Apply to allied/enemy factions
            ApplyRelatedReputationChanges(factionId, amount);

            if (saveOnChange)
            {
                Save();
            }

            Debug.Log($"[Reputation] {factionId}: {oldValue} -> {data.reputation} ({(amount >= 0 ? "+" : "")}{amount})");
        }

        /// <summary>
        /// Set reputation for a faction.
        /// </summary>
        public void SetReputation(string factionId, int value)
        {
            if (!factionData.TryGetValue(factionId, out FactionData data)) return;

            int oldValue = data.reputation;
            data.reputation = Mathf.Clamp(value, minReputation, maxReputation);

            ReputationChanged?.Invoke(factionId, oldValue, data.reputation);
            onReputationChanged?.Invoke(factionId, data.reputation);

            if (saveOnChange)
            {
                Save();
            }
        }

        /// <summary>
        /// Get reputation value.
        /// </summary>
        public int GetReputation(string factionId)
        {
            return factionData.TryGetValue(factionId, out FactionData data) ? data.reputation : defaultReputation;
        }

        /// <summary>
        /// Get reputation tier.
        /// </summary>
        public ReputationTier GetReputationTier(string factionId)
        {
            int rep = GetReputation(factionId);
            return GetTier(rep);
        }

        /// <summary>
        /// Get tier name.
        /// </summary>
        public string GetTierName(string factionId)
        {
            return GetReputationTier(factionId)?.tierName ?? "Unknown";
        }

        private ReputationTier GetTier(int reputation)
        {
            foreach (var tier in tiers)
            {
                if (reputation >= tier.minValue && reputation <= tier.maxValue)
                {
                    return tier;
                }
            }
            return tiers[tiers.Length / 2]; // Default to middle tier
        }

        /// <summary>
        /// Get faction relation to player.
        /// </summary>
        public FactionRelation GetRelation(string factionId)
        {
            int rep = GetReputation(factionId);

            if (rep <= -50) return FactionRelation.Hostile;
            if (rep < 0) return FactionRelation.Unfriendly;
            if (rep < 25) return FactionRelation.Neutral;
            if (rep < 75) return FactionRelation.Friendly;
            return FactionRelation.Allied;
        }

        /// <summary>
        /// Check if faction is hostile.
        /// </summary>
        public bool IsHostile(string factionId)
        {
            return GetRelation(factionId) == FactionRelation.Hostile;
        }

        /// <summary>
        /// Check if faction is friendly.
        /// </summary>
        public bool IsFriendly(string factionId)
        {
            var relation = GetRelation(factionId);
            return relation == FactionRelation.Friendly || relation == FactionRelation.Allied;
        }

        private void ApplyRelatedReputationChanges(string factionId, int amount)
        {
            if (!factionData.TryGetValue(factionId, out FactionData data)) return;

            Faction faction = data.faction;

            // Allied factions gain partial reputation
            if (faction.alliedFactions != null)
            {
                foreach (string alliedId in faction.alliedFactions)
                {
                    if (factionData.ContainsKey(alliedId))
                    {
                        int alliedAmount = Mathf.RoundToInt(amount * faction.alliedReputationShare);
                        AddReputationDirect(alliedId, alliedAmount);
                    }
                }
            }

            // Enemy factions lose reputation
            if (faction.enemyFactions != null)
            {
                foreach (string enemyId in faction.enemyFactions)
                {
                    if (factionData.ContainsKey(enemyId))
                    {
                        int enemyAmount = Mathf.RoundToInt(-amount * faction.enemyReputationShare);
                        AddReputationDirect(enemyId, enemyAmount);
                    }
                }
            }
        }

        private void AddReputationDirect(string factionId, int amount)
        {
            if (!factionData.TryGetValue(factionId, out FactionData data)) return;
            if (amount == 0) return;

            data.reputation = Mathf.Clamp(data.reputation + amount, minReputation, maxReputation);
        }

        /// <summary>
        /// Get faction data.
        /// </summary>
        public Faction GetFaction(string factionId)
        {
            return factionData.TryGetValue(factionId, out FactionData data) ? data.faction : null;
        }

        /// <summary>
        /// Get all factions.
        /// </summary>
        public List<Faction> GetAllFactions()
        {
            return new List<Faction>(factions);
        }

        /// <summary>
        /// Get factions by relation.
        /// </summary>
        public List<Faction> GetFactionsByRelation(FactionRelation relation)
        {
            List<Faction> result = new List<Faction>();

            foreach (var data in factionData.Values)
            {
                if (GetRelation(data.faction.factionId) == relation)
                {
                    result.Add(data.faction);
                }
            }

            return result;
        }

        /// <summary>
        /// Check reputation requirement.
        /// </summary>
        public bool MeetsRequirement(string factionId, int requiredReputation)
        {
            return GetReputation(factionId) >= requiredReputation;
        }

        /// <summary>
        /// Check tier requirement.
        /// </summary>
        public bool MeetsTierRequirement(string factionId, string requiredTier)
        {
            string currentTier = GetTierName(factionId);

            int currentIndex = Array.FindIndex(tiers, t => t.tierName == currentTier);
            int requiredIndex = Array.FindIndex(tiers, t => t.tierName == requiredTier);

            return currentIndex >= requiredIndex;
        }

        /// <summary>
        /// Save reputation data.
        /// </summary>
        public void Save()
        {
            ReputationSaveData saveData = new ReputationSaveData();

            foreach (var kvp in factionData)
            {
                saveData.reputations.Add(new ReputationEntry
                {
                    factionId = kvp.Key,
                    reputation = kvp.Value.reputation
                });
            }

            string json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString("ReputationData", json);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Load reputation data.
        /// </summary>
        public void Load()
        {
            string json = PlayerPrefs.GetString("ReputationData", "");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                ReputationSaveData saveData = JsonUtility.FromJson<ReputationSaveData>(json);

                foreach (var entry in saveData.reputations)
                {
                    if (factionData.TryGetValue(entry.factionId, out FactionData data))
                    {
                        data.reputation = entry.reputation;
                    }
                }

                Debug.Log("[Reputation] Loaded save data");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Reputation] Failed to load: {e.Message}");
            }
        }

        /// <summary>
        /// Reset all reputations.
        /// </summary>
        public void ResetAll()
        {
            foreach (var kvp in factionData)
            {
                kvp.Value.reputation = kvp.Value.faction.startingReputation != 0 ?
                    kvp.Value.faction.startingReputation : defaultReputation;
            }

            PlayerPrefs.DeleteKey("ReputationData");
        }
    }

    [Serializable]
    public class Faction
    {
        public string factionId;
        public string factionName;
        [TextArea]
        public string description;
        public Sprite icon;
        public Color factionColor = Color.white;
        public int startingReputation = 0;

        [Header("Related Factions")]
        public List<string> alliedFactions = new List<string>();
        public List<string> enemyFactions = new List<string>();
        [Range(0f, 1f)]
        public float alliedReputationShare = 0.5f;
        [Range(0f, 1f)]
        public float enemyReputationShare = 0.25f;
    }

    [Serializable]
    public class ReputationTier
    {
        public string tierName;
        public int minValue;
        public int maxValue;
        public Color tierColor = Color.white;
        public Sprite tierIcon;
    }

    public enum FactionRelation
    {
        Hostile,
        Unfriendly,
        Neutral,
        Friendly,
        Allied
    }

    [Serializable]
    public class FactionData
    {
        public Faction faction;
        public int reputation;
    }

    [Serializable]
    public class ReputationSaveData
    {
        public List<ReputationEntry> reputations = new List<ReputationEntry>();
    }

    [Serializable]
    public class ReputationEntry
    {
        public string factionId;
        public int reputation;
    }
}
