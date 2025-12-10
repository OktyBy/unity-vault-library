using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Dialogue
{
    /// <summary>
    /// NPC relationship and affinity system.
    /// </summary>
    public class RelationshipSystem : MonoBehaviour
    {
        public static RelationshipSystem Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int minAffinity = 0;
        [SerializeField] private int maxAffinity = 100;
        [SerializeField] private int defaultAffinity = 50;

        [Header("NPCs")]
        [SerializeField] private List<NPCRelationship> npcs = new List<NPCRelationship>();

        [Header("Affinity Levels")]
        [SerializeField] private AffinityLevel[] affinityLevels = new AffinityLevel[]
        {
            new AffinityLevel { levelName = "Stranger", minValue = 0, maxValue = 19 },
            new AffinityLevel { levelName = "Acquaintance", minValue = 20, maxValue = 39 },
            new AffinityLevel { levelName = "Friend", minValue = 40, maxValue = 59 },
            new AffinityLevel { levelName = "Close Friend", minValue = 60, maxValue = 79 },
            new AffinityLevel { levelName = "Best Friend", minValue = 80, maxValue = 89 },
            new AffinityLevel { levelName = "Soulmate", minValue = 90, maxValue = 100 }
        };

        [Header("Events")]
        [SerializeField] private UnityEvent<string, int> onAffinityChanged;
        [SerializeField] private UnityEvent<string, AffinityLevel> onLevelChanged;
        [SerializeField] private UnityEvent<string> onMaxAffinityReached;

        // State
        private Dictionary<string, NPCData> npcData = new Dictionary<string, NPCData>();

        // Events
        public event Action<string, int, int> AffinityChanged;
        public event Action<string, AffinityLevel, AffinityLevel> LevelChanged;
        public event Action<string> MaxAffinityReached;
        public event Action<string, string> GiftGiven;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeNPCs();
        }

        private void InitializeNPCs()
        {
            foreach (var npc in npcs)
            {
                npcData[npc.npcId] = new NPCData
                {
                    relationship = npc,
                    affinity = npc.startingAffinity > 0 ? npc.startingAffinity : defaultAffinity,
                    giftsGivenToday = 0,
                    lastGiftDate = ""
                };
            }
        }

        /// <summary>
        /// Add affinity to NPC.
        /// </summary>
        public void AddAffinity(string npcId, int amount)
        {
            if (!npcData.TryGetValue(npcId, out NPCData data)) return;

            int oldValue = data.affinity;
            AffinityLevel oldLevel = GetAffinityLevel(oldValue);

            data.affinity = Mathf.Clamp(data.affinity + amount, minAffinity, maxAffinity);

            AffinityLevel newLevel = GetAffinityLevel(data.affinity);

            AffinityChanged?.Invoke(npcId, oldValue, data.affinity);
            onAffinityChanged?.Invoke(npcId, data.affinity);

            if (oldLevel.levelName != newLevel.levelName)
            {
                LevelChanged?.Invoke(npcId, oldLevel, newLevel);
                onLevelChanged?.Invoke(npcId, newLevel);
                Debug.Log($"[Relationship] {data.relationship.npcName}: Level up to {newLevel.levelName}!");
            }

            if (data.affinity >= maxAffinity && oldValue < maxAffinity)
            {
                MaxAffinityReached?.Invoke(npcId);
                onMaxAffinityReached?.Invoke(npcId);
            }

            Debug.Log($"[Relationship] {data.relationship.npcName}: {oldValue} -> {data.affinity}");
        }

        /// <summary>
        /// Set affinity for NPC.
        /// </summary>
        public void SetAffinity(string npcId, int value)
        {
            if (!npcData.TryGetValue(npcId, out NPCData data)) return;

            int oldValue = data.affinity;
            data.affinity = Mathf.Clamp(value, minAffinity, maxAffinity);

            AffinityChanged?.Invoke(npcId, oldValue, data.affinity);
            onAffinityChanged?.Invoke(npcId, data.affinity);
        }

        /// <summary>
        /// Get affinity value.
        /// </summary>
        public int GetAffinity(string npcId)
        {
            return npcData.TryGetValue(npcId, out NPCData data) ? data.affinity : defaultAffinity;
        }

        /// <summary>
        /// Get affinity level.
        /// </summary>
        public AffinityLevel GetAffinityLevel(string npcId)
        {
            int affinity = GetAffinity(npcId);
            return GetAffinityLevel(affinity);
        }

        private AffinityLevel GetAffinityLevel(int affinity)
        {
            foreach (var level in affinityLevels)
            {
                if (affinity >= level.minValue && affinity <= level.maxValue)
                {
                    return level;
                }
            }
            return affinityLevels[0];
        }

        /// <summary>
        /// Give gift to NPC.
        /// </summary>
        public bool GiveGift(string npcId, string itemId)
        {
            if (!npcData.TryGetValue(npcId, out NPCData data)) return false;

            NPCRelationship npc = data.relationship;

            // Check daily gift limit
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (data.lastGiftDate != today)
            {
                data.lastGiftDate = today;
                data.giftsGivenToday = 0;
            }

            if (data.giftsGivenToday >= npc.maxGiftsPerDay)
            {
                Debug.Log($"[Relationship] {npc.npcName} can't receive more gifts today");
                return false;
            }

            // Calculate affinity change
            int affinityChange = CalculateGiftAffinity(npc, itemId);

            AddAffinity(npcId, affinityChange);
            data.giftsGivenToday++;

            GiftGiven?.Invoke(npcId, itemId);

            Debug.Log($"[Relationship] Gave {itemId} to {npc.npcName}: {(affinityChange >= 0 ? "+" : "")}{affinityChange}");

            return true;
        }

        private int CalculateGiftAffinity(NPCRelationship npc, string itemId)
        {
            // Check loved items
            if (npc.lovedItems != null && npc.lovedItems.Contains(itemId))
            {
                return npc.lovedItemBonus;
            }

            // Check liked items
            if (npc.likedItems != null && npc.likedItems.Contains(itemId))
            {
                return npc.likedItemBonus;
            }

            // Check disliked items
            if (npc.dislikedItems != null && npc.dislikedItems.Contains(itemId))
            {
                return npc.dislikedItemPenalty;
            }

            // Check hated items
            if (npc.hatedItems != null && npc.hatedItems.Contains(itemId))
            {
                return npc.hatedItemPenalty;
            }

            // Neutral gift
            return npc.neutralItemBonus;
        }

        /// <summary>
        /// Talk to NPC.
        /// </summary>
        public void TalkTo(string npcId)
        {
            if (!npcData.TryGetValue(npcId, out NPCData data)) return;

            // Small affinity gain from talking
            AddAffinity(npcId, data.relationship.talkAffinityBonus);
        }

        /// <summary>
        /// Complete quest for NPC.
        /// </summary>
        public void CompleteQuestFor(string npcId, int affinityBonus = 10)
        {
            AddAffinity(npcId, affinityBonus);
        }

        /// <summary>
        /// Check if NPC likes player enough.
        /// </summary>
        public bool MeetsAffinityRequirement(string npcId, int required)
        {
            return GetAffinity(npcId) >= required;
        }

        /// <summary>
        /// Check if at specific affinity level.
        /// </summary>
        public bool IsAtLevel(string npcId, string levelName)
        {
            AffinityLevel current = GetAffinityLevel(npcId);
            return current.levelName == levelName;
        }

        /// <summary>
        /// Check if at or above level.
        /// </summary>
        public bool IsAtOrAboveLevel(string npcId, string levelName)
        {
            int currentAffinity = GetAffinity(npcId);
            int targetIndex = Array.FindIndex(affinityLevels, l => l.levelName == levelName);

            if (targetIndex < 0) return false;

            return currentAffinity >= affinityLevels[targetIndex].minValue;
        }

        /// <summary>
        /// Get NPC relationship data.
        /// </summary>
        public NPCRelationship GetNPC(string npcId)
        {
            return npcData.TryGetValue(npcId, out NPCData data) ? data.relationship : null;
        }

        /// <summary>
        /// Get all NPCs.
        /// </summary>
        public List<NPCRelationship> GetAllNPCs()
        {
            return new List<NPCRelationship>(npcs);
        }

        /// <summary>
        /// Get NPCs at affinity level.
        /// </summary>
        public List<NPCRelationship> GetNPCsAtLevel(string levelName)
        {
            List<NPCRelationship> result = new List<NPCRelationship>();

            foreach (var data in npcData.Values)
            {
                if (GetAffinityLevel(data.affinity).levelName == levelName)
                {
                    result.Add(data.relationship);
                }
            }

            return result;
        }

        /// <summary>
        /// Save relationship data.
        /// </summary>
        public void Save()
        {
            RelationshipSaveData saveData = new RelationshipSaveData();

            foreach (var kvp in npcData)
            {
                saveData.relationships.Add(new RelationshipEntry
                {
                    npcId = kvp.Key,
                    affinity = kvp.Value.affinity,
                    giftsGivenToday = kvp.Value.giftsGivenToday,
                    lastGiftDate = kvp.Value.lastGiftDate
                });
            }

            string json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString("RelationshipData", json);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Load relationship data.
        /// </summary>
        public void Load()
        {
            string json = PlayerPrefs.GetString("RelationshipData", "");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                RelationshipSaveData saveData = JsonUtility.FromJson<RelationshipSaveData>(json);

                foreach (var entry in saveData.relationships)
                {
                    if (npcData.TryGetValue(entry.npcId, out NPCData data))
                    {
                        data.affinity = entry.affinity;
                        data.giftsGivenToday = entry.giftsGivenToday;
                        data.lastGiftDate = entry.lastGiftDate;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Relationship] Failed to load: {e.Message}");
            }
        }

        /// <summary>
        /// Reset all relationships.
        /// </summary>
        public void ResetAll()
        {
            foreach (var kvp in npcData)
            {
                kvp.Value.affinity = kvp.Value.relationship.startingAffinity > 0 ?
                    kvp.Value.relationship.startingAffinity : defaultAffinity;
                kvp.Value.giftsGivenToday = 0;
                kvp.Value.lastGiftDate = "";
            }

            PlayerPrefs.DeleteKey("RelationshipData");
        }
    }

    [Serializable]
    public class NPCRelationship
    {
        public string npcId;
        public string npcName;
        [TextArea]
        public string description;
        public Sprite portrait;
        public int startingAffinity = 50;

        [Header("Gift Preferences")]
        public List<string> lovedItems = new List<string>();
        public List<string> likedItems = new List<string>();
        public List<string> dislikedItems = new List<string>();
        public List<string> hatedItems = new List<string>();

        [Header("Gift Values")]
        public int lovedItemBonus = 20;
        public int likedItemBonus = 10;
        public int neutralItemBonus = 3;
        public int dislikedItemPenalty = -5;
        public int hatedItemPenalty = -15;
        public int maxGiftsPerDay = 2;

        [Header("Interaction")]
        public int talkAffinityBonus = 1;

        [Header("Unlocks")]
        public List<RelationshipUnlock> unlocks = new List<RelationshipUnlock>();
    }

    [Serializable]
    public class AffinityLevel
    {
        public string levelName;
        public int minValue;
        public int maxValue;
        public Color levelColor = Color.white;
    }

    [Serializable]
    public class RelationshipUnlock
    {
        public string unlockId;
        public string unlockName;
        public int requiredAffinity;
        public UnlockType unlockType;
    }

    public enum UnlockType
    {
        Dialogue,
        Quest,
        Item,
        Event,
        Romance
    }

    [Serializable]
    public class NPCData
    {
        public NPCRelationship relationship;
        public int affinity;
        public int giftsGivenToday;
        public string lastGiftDate;
    }

    [Serializable]
    public class RelationshipSaveData
    {
        public List<RelationshipEntry> relationships = new List<RelationshipEntry>();
    }

    [Serializable]
    public class RelationshipEntry
    {
        public string npcId;
        public int affinity;
        public int giftsGivenToday;
        public string lastGiftDate;
    }
}
