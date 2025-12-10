using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityVault.Dialogue
{
    /// <summary>
    /// Quest system with objectives and rewards.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int maxActiveQuests = 10;
        [SerializeField] private bool trackObjectives = true;

        [Header("Events")]
        [SerializeField] private UnityEvent<QuestSO> onQuestStarted;
        [SerializeField] private UnityEvent<QuestSO> onQuestCompleted;
        [SerializeField] private UnityEvent<QuestSO> onQuestFailed;
        [SerializeField] private UnityEvent<QuestObjective> onObjectiveCompleted;

        // State
        private Dictionary<string, QuestInstance> activeQuests = new Dictionary<string, QuestInstance>();
        private List<string> completedQuestIds = new List<string>();

        // Properties
        public IReadOnlyDictionary<string, QuestInstance> ActiveQuests => activeQuests;
        public IReadOnlyList<string> CompletedQuestIds => completedQuestIds;

        // Events
        public event Action<QuestSO> QuestStarted;
        public event Action<QuestSO> QuestCompleted;
        public event Action<QuestSO> QuestFailed;
        public event Action<QuestObjective> ObjectiveCompleted;
        public event Action<QuestObjective, int> ObjectiveUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool StartQuest(QuestSO quest)
        {
            if (quest == null) return false;
            if (activeQuests.ContainsKey(quest.questId)) return false;
            if (completedQuestIds.Contains(quest.questId) && !quest.isRepeatable) return false;
            if (activeQuests.Count >= maxActiveQuests) return false;

            var instance = new QuestInstance(quest);
            activeQuests[quest.questId] = instance;

            QuestStarted?.Invoke(quest);
            onQuestStarted?.Invoke(quest);

            Debug.Log($"[Quest] Started: {quest.questName}");
            return true;
        }

        public void UpdateObjective(string questId, string objectiveId, int amount = 1)
        {
            if (!activeQuests.TryGetValue(questId, out var instance)) return;

            var objective = instance.objectives.FirstOrDefault(o => o.objectiveId == objectiveId);
            if (objective == null || objective.isCompleted) return;

            objective.currentAmount += amount;
            ObjectiveUpdated?.Invoke(objective, objective.currentAmount);

            if (objective.currentAmount >= objective.targetAmount)
            {
                CompleteObjective(instance, objective);
            }
        }

        public void UpdateObjectiveByType(string objectiveType, string targetId, int amount = 1)
        {
            foreach (var quest in activeQuests.Values)
            {
                foreach (var objective in quest.objectives)
                {
                    if (objective.objectiveType == objectiveType &&
                        (string.IsNullOrEmpty(objective.targetId) || objective.targetId == targetId) &&
                        !objective.isCompleted)
                    {
                        objective.currentAmount += amount;
                        ObjectiveUpdated?.Invoke(objective, objective.currentAmount);

                        if (objective.currentAmount >= objective.targetAmount)
                        {
                            CompleteObjective(quest, objective);
                        }
                    }
                }
            }
        }

        private void CompleteObjective(QuestInstance quest, QuestObjective objective)
        {
            objective.isCompleted = true;
            ObjectiveCompleted?.Invoke(objective);
            onObjectiveCompleted?.Invoke(objective);

            Debug.Log($"[Quest] Objective completed: {objective.description}");

            if (quest.objectives.All(o => o.isCompleted || o.isOptional))
            {
                CompleteQuest(quest.quest.questId);
            }
        }

        public void CompleteQuest(string questId)
        {
            if (!activeQuests.TryGetValue(questId, out var instance)) return;

            activeQuests.Remove(questId);

            if (!instance.quest.isRepeatable)
            {
                completedQuestIds.Add(questId);
            }

            // Grant rewards
            GrantRewards(instance.quest);

            QuestCompleted?.Invoke(instance.quest);
            onQuestCompleted?.Invoke(instance.quest);

            Debug.Log($"[Quest] Completed: {instance.quest.questName}");
        }

        public void FailQuest(string questId)
        {
            if (!activeQuests.TryGetValue(questId, out var instance)) return;

            activeQuests.Remove(questId);

            QuestFailed?.Invoke(instance.quest);
            onQuestFailed?.Invoke(instance.quest);

            Debug.Log($"[Quest] Failed: {instance.quest.questName}");
        }

        public void AbandonQuest(string questId)
        {
            activeQuests.Remove(questId);
        }

        private void GrantRewards(QuestSO quest)
        {
            foreach (var reward in quest.rewards)
            {
                reward.Grant();
            }
        }

        public bool IsQuestActive(string questId)
        {
            return activeQuests.ContainsKey(questId);
        }

        public bool IsQuestCompleted(string questId)
        {
            return completedQuestIds.Contains(questId);
        }

        public QuestInstance GetQuest(string questId)
        {
            activeQuests.TryGetValue(questId, out var instance);
            return instance;
        }
    }

    [CreateAssetMenu(fileName = "NewQuest", menuName = "UnityVault/Quest/Quest")]
    public class QuestSO : ScriptableObject
    {
        [Header("Basic Info")]
        public string questId;
        public string questName;
        [TextArea] public string description;
        public QuestType questType = QuestType.Main;
        public Sprite icon;

        [Header("Objectives")]
        public List<QuestObjective> objectives = new List<QuestObjective>();

        [Header("Rewards")]
        public List<QuestReward> rewards = new List<QuestReward>();

        [Header("Settings")]
        public bool isRepeatable = false;
        public bool autoComplete = true;
        public string[] prerequisiteQuestIds;
    }

    [System.Serializable]
    public class QuestObjective
    {
        public string objectiveId;
        public string description;
        public string objectiveType; // "kill", "collect", "talk", "reach"
        public string targetId;
        public int targetAmount = 1;
        public int currentAmount = 0;
        public bool isOptional = false;
        public bool isCompleted = false;

        public float Progress => (float)currentAmount / targetAmount;
    }

    [System.Serializable]
    public class QuestReward
    {
        public RewardType type;
        public string itemId;
        public int amount;

        public void Grant()
        {
            Debug.Log($"[Quest] Reward granted: {type} x{amount}");
            // Implement actual reward granting based on type
        }
    }

    public enum QuestType
    {
        Main,
        Side,
        Daily,
        Weekly,
        Event
    }

    public enum RewardType
    {
        Experience,
        Gold,
        Item,
        Skill,
        Reputation
    }

    public class QuestInstance
    {
        public QuestSO quest;
        public List<QuestObjective> objectives;
        public DateTime startTime;

        public QuestInstance(QuestSO quest)
        {
            this.quest = quest;
            this.objectives = quest.objectives.Select(o => new QuestObjective
            {
                objectiveId = o.objectiveId,
                description = o.description,
                objectiveType = o.objectiveType,
                targetId = o.targetId,
                targetAmount = o.targetAmount,
                currentAmount = 0,
                isOptional = o.isOptional,
                isCompleted = false
            }).ToList();
            this.startTime = DateTime.Now;
        }
    }
}
