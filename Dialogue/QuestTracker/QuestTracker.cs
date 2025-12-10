using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System;
using System.Collections.Generic;

namespace UnityVault.Dialogue
{
    /// <summary>
    /// Quest tracker UI for displaying active quests.
    /// </summary>
    public class QuestTracker : MonoBehaviour
    {
        public static QuestTracker Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private RectTransform trackerContainer;
        [SerializeField] private GameObject questEntryPrefab;
        [SerializeField] private GameObject objectiveEntryPrefab;

        [Header("Settings")]
        [SerializeField] private int maxTrackedQuests = 3;
        [SerializeField] private bool autoTrackNewQuests = true;
        [SerializeField] private bool showCompletedObjectives = true;
        [SerializeField] private float completionDisplayTime = 2f;

        [Header("Animation")]
        [SerializeField] private float fadeSpeed = 5f;
        [SerializeField] private bool animateUpdates = true;

        [Header("Colors")]
        [SerializeField] private Color activeObjectiveColor = Color.white;
        [SerializeField] private Color completedObjectiveColor = Color.green;
        [SerializeField] private Color failedObjectiveColor = Color.red;

        [Header("Events")]
        [SerializeField] private UnityEvent<TrackedQuest> onQuestTracked;
        [SerializeField] private UnityEvent<TrackedQuest> onQuestUntracked;
        [SerializeField] private UnityEvent<string> onObjectiveUpdated;

        // State
        private List<TrackedQuest> trackedQuests = new List<TrackedQuest>();
        private Dictionary<string, QuestTrackerEntry> questEntries = new Dictionary<string, QuestTrackerEntry>();

        // Events
        public event Action<TrackedQuest> QuestTracked;
        public event Action<TrackedQuest> QuestUntracked;
        public event Action<string, int, int> ObjectiveUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        /// <summary>
        /// Track a quest.
        /// </summary>
        public void TrackQuest(TrackedQuest quest)
        {
            if (quest == null) return;
            if (trackedQuests.Contains(quest)) return;

            // Remove oldest if at max
            while (trackedQuests.Count >= maxTrackedQuests)
            {
                UntrackQuest(trackedQuests[0]);
            }

            trackedQuests.Add(quest);
            CreateQuestEntry(quest);

            QuestTracked?.Invoke(quest);
            onQuestTracked?.Invoke(quest);

            Debug.Log($"[QuestTracker] Tracking: {quest.questName}");
        }

        /// <summary>
        /// Untrack a quest.
        /// </summary>
        public void UntrackQuest(TrackedQuest quest)
        {
            if (quest == null) return;
            if (!trackedQuests.Contains(quest)) return;

            trackedQuests.Remove(quest);
            RemoveQuestEntry(quest.questId);

            QuestUntracked?.Invoke(quest);
            onQuestUntracked?.Invoke(quest);

            Debug.Log($"[QuestTracker] Untracked: {quest.questName}");
        }

        /// <summary>
        /// Untrack by quest ID.
        /// </summary>
        public void UntrackQuest(string questId)
        {
            TrackedQuest quest = trackedQuests.Find(q => q.questId == questId);
            if (quest != null)
            {
                UntrackQuest(quest);
            }
        }

        private void CreateQuestEntry(TrackedQuest quest)
        {
            if (questEntryPrefab == null || trackerContainer == null) return;

            GameObject entryObj = Instantiate(questEntryPrefab, trackerContainer);
            QuestTrackerEntry entry = entryObj.GetComponent<QuestTrackerEntry>();

            if (entry == null)
            {
                entry = entryObj.AddComponent<QuestTrackerEntry>();
            }

            entry.Setup(quest, this);
            questEntries[quest.questId] = entry;
        }

        private void RemoveQuestEntry(string questId)
        {
            if (questEntries.TryGetValue(questId, out QuestTrackerEntry entry))
            {
                if (entry != null && entry.gameObject != null)
                {
                    Destroy(entry.gameObject);
                }
                questEntries.Remove(questId);
            }
        }

        /// <summary>
        /// Update objective progress.
        /// </summary>
        public void UpdateObjective(string questId, string objectiveId, int current, int target)
        {
            if (questEntries.TryGetValue(questId, out QuestTrackerEntry entry))
            {
                entry.UpdateObjective(objectiveId, current, target);
            }

            ObjectiveUpdated?.Invoke(objectiveId, current, target);
            onObjectiveUpdated?.Invoke(objectiveId);
        }

        /// <summary>
        /// Mark objective as complete.
        /// </summary>
        public void CompleteObjective(string questId, string objectiveId)
        {
            if (questEntries.TryGetValue(questId, out QuestTrackerEntry entry))
            {
                entry.CompleteObjective(objectiveId);

                if (!showCompletedObjectives)
                {
                    StartCoroutine(RemoveObjectiveAfterDelay(entry, objectiveId));
                }
            }
        }

        private System.Collections.IEnumerator RemoveObjectiveAfterDelay(QuestTrackerEntry entry, string objectiveId)
        {
            yield return new WaitForSeconds(completionDisplayTime);
            entry.RemoveObjective(objectiveId);
        }

        /// <summary>
        /// Mark quest as complete.
        /// </summary>
        public void CompleteQuest(string questId)
        {
            TrackedQuest quest = trackedQuests.Find(q => q.questId == questId);
            if (quest != null)
            {
                if (questEntries.TryGetValue(questId, out QuestTrackerEntry entry))
                {
                    entry.ShowCompletion();
                }

                StartCoroutine(UntrackAfterDelay(quest));
            }
        }

        private System.Collections.IEnumerator UntrackAfterDelay(TrackedQuest quest)
        {
            yield return new WaitForSeconds(completionDisplayTime);
            UntrackQuest(quest);
        }

        /// <summary>
        /// Check if quest is tracked.
        /// </summary>
        public bool IsTracked(string questId)
        {
            return trackedQuests.Exists(q => q.questId == questId);
        }

        /// <summary>
        /// Get tracked quest count.
        /// </summary>
        public int GetTrackedCount() => trackedQuests.Count;

        /// <summary>
        /// Clear all tracked quests.
        /// </summary>
        public void ClearAll()
        {
            foreach (var entry in questEntries.Values)
            {
                if (entry != null && entry.gameObject != null)
                {
                    Destroy(entry.gameObject);
                }
            }

            questEntries.Clear();
            trackedQuests.Clear();
        }

        /// <summary>
        /// Show/hide tracker.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (trackerContainer != null)
            {
                trackerContainer.gameObject.SetActive(visible);
            }
        }

        // Getter for colors
        public Color GetObjectiveColor(ObjectiveState state)
        {
            return state switch
            {
                ObjectiveState.Active => activeObjectiveColor,
                ObjectiveState.Completed => completedObjectiveColor,
                ObjectiveState.Failed => failedObjectiveColor,
                _ => activeObjectiveColor
            };
        }
    }

    /// <summary>
    /// Individual quest tracker entry UI.
    /// </summary>
    public class QuestTrackerEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI questNameText;
        [SerializeField] private RectTransform objectiveContainer;
        [SerializeField] private CanvasGroup canvasGroup;

        private TrackedQuest quest;
        private QuestTracker tracker;
        private Dictionary<string, ObjectiveEntry> objectiveEntries = new Dictionary<string, ObjectiveEntry>();

        public void Setup(TrackedQuest quest, QuestTracker tracker)
        {
            this.quest = quest;
            this.tracker = tracker;

            if (questNameText != null)
            {
                questNameText.text = quest.questName;
            }

            // Create objective entries
            foreach (var objective in quest.objectives)
            {
                CreateObjectiveEntry(objective);
            }
        }

        private void CreateObjectiveEntry(QuestObjective objective)
        {
            if (objectiveContainer == null) return;

            GameObject objEntry = new GameObject($"Objective_{objective.objectiveId}");
            objEntry.transform.SetParent(objectiveContainer, false);

            TextMeshProUGUI text = objEntry.AddComponent<TextMeshProUGUI>();
            text.fontSize = 14;
            text.color = tracker.GetObjectiveColor(ObjectiveState.Active);

            UpdateObjectiveText(text, objective);

            objectiveEntries[objective.objectiveId] = new ObjectiveEntry
            {
                gameObject = objEntry,
                text = text,
                objective = objective
            };
        }

        private void UpdateObjectiveText(TextMeshProUGUI text, QuestObjective objective)
        {
            if (objective.showProgress && objective.targetCount > 1)
            {
                text.text = $"• {objective.description} ({objective.currentCount}/{objective.targetCount})";
            }
            else
            {
                text.text = $"• {objective.description}";
            }
        }

        public void UpdateObjective(string objectiveId, int current, int target)
        {
            if (objectiveEntries.TryGetValue(objectiveId, out ObjectiveEntry entry))
            {
                entry.objective.currentCount = current;
                entry.objective.targetCount = target;
                UpdateObjectiveText(entry.text, entry.objective);
            }
        }

        public void CompleteObjective(string objectiveId)
        {
            if (objectiveEntries.TryGetValue(objectiveId, out ObjectiveEntry entry))
            {
                entry.objective.state = ObjectiveState.Completed;
                entry.text.color = tracker.GetObjectiveColor(ObjectiveState.Completed);
                entry.text.text = $"✓ {entry.objective.description}";
            }
        }

        public void RemoveObjective(string objectiveId)
        {
            if (objectiveEntries.TryGetValue(objectiveId, out ObjectiveEntry entry))
            {
                Destroy(entry.gameObject);
                objectiveEntries.Remove(objectiveId);
            }
        }

        public void ShowCompletion()
        {
            if (questNameText != null)
            {
                questNameText.text = $"✓ {quest.questName} - COMPLETE!";
                questNameText.color = tracker.GetObjectiveColor(ObjectiveState.Completed);
            }
        }

        private class ObjectiveEntry
        {
            public GameObject gameObject;
            public TextMeshProUGUI text;
            public QuestObjective objective;
        }
    }

    [Serializable]
    public class TrackedQuest
    {
        public string questId;
        public string questName;
        public List<QuestObjective> objectives = new List<QuestObjective>();
    }

    [Serializable]
    public class QuestObjective
    {
        public string objectiveId;
        public string description;
        public int currentCount;
        public int targetCount = 1;
        public bool showProgress = true;
        public ObjectiveState state = ObjectiveState.Active;
    }

    public enum ObjectiveState
    {
        Active,
        Completed,
        Failed
    }
}
