using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System;
using System.Collections.Generic;

namespace UnityVault.Dialogue
{
    /// <summary>
    /// Journal and quest log system.
    /// </summary>
    public class JournalSystem : MonoBehaviour
    {
        public static JournalSystem Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject journalPanel;
        [SerializeField] private RectTransform questListContainer;
        [SerializeField] private RectTransform entryListContainer;
        [SerializeField] private GameObject questButtonPrefab;
        [SerializeField] private GameObject entryPrefab;

        [Header("Detail Panel")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI objectivesText;
        [SerializeField] private TextMeshProUGUI rewardsText;
        [SerializeField] private Image questIcon;

        [Header("Tabs")]
        [SerializeField] private Button activeQuestsTab;
        [SerializeField] private Button completedQuestsTab;
        [SerializeField] private Button notesTab;
        [SerializeField] private Button loreTab;

        [Header("Settings")]
        [SerializeField] private KeyCode toggleKey = KeyCode.J;
        [SerializeField] private bool pauseOnOpen = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onJournalOpened;
        [SerializeField] private UnityEvent onJournalClosed;
        [SerializeField] private UnityEvent<JournalEntry> onEntrySelected;

        // Data
        private List<JournalEntry> activeQuests = new List<JournalEntry>();
        private List<JournalEntry> completedQuests = new List<JournalEntry>();
        private List<JournalEntry> notes = new List<JournalEntry>();
        private List<JournalEntry> loreEntries = new List<JournalEntry>();

        // State
        private JournalTab currentTab = JournalTab.ActiveQuests;
        private JournalEntry selectedEntry;
        private bool isOpen;

        // Events
        public event Action JournalOpened;
        public event Action JournalClosed;
        public event Action<JournalEntry> EntrySelected;
        public event Action<JournalEntry> EntryAdded;

        public bool IsOpen => isOpen;

        public enum JournalTab
        {
            ActiveQuests,
            CompletedQuests,
            Notes,
            Lore
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            SetupTabs();
        }

        private void Start()
        {
            Close();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }
        }

        private void SetupTabs()
        {
            if (activeQuestsTab != null)
            {
                activeQuestsTab.onClick.AddListener(() => SwitchTab(JournalTab.ActiveQuests));
            }

            if (completedQuestsTab != null)
            {
                completedQuestsTab.onClick.AddListener(() => SwitchTab(JournalTab.CompletedQuests));
            }

            if (notesTab != null)
            {
                notesTab.onClick.AddListener(() => SwitchTab(JournalTab.Notes));
            }

            if (loreTab != null)
            {
                loreTab.onClick.AddListener(() => SwitchTab(JournalTab.Lore));
            }
        }

        /// <summary>
        /// Toggle journal visibility.
        /// </summary>
        public void Toggle()
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>
        /// Open the journal.
        /// </summary>
        public void Open()
        {
            isOpen = true;

            if (journalPanel != null)
            {
                journalPanel.SetActive(true);
            }

            if (pauseOnOpen)
            {
                Time.timeScale = 0;
            }

            RefreshCurrentTab();

            JournalOpened?.Invoke();
            onJournalOpened?.Invoke();
        }

        /// <summary>
        /// Close the journal.
        /// </summary>
        public void Close()
        {
            isOpen = false;

            if (journalPanel != null)
            {
                journalPanel.SetActive(false);
            }

            if (pauseOnOpen)
            {
                Time.timeScale = 1;
            }

            JournalClosed?.Invoke();
            onJournalClosed?.Invoke();
        }

        /// <summary>
        /// Switch to a different tab.
        /// </summary>
        public void SwitchTab(JournalTab tab)
        {
            currentTab = tab;
            RefreshCurrentTab();
        }

        private void RefreshCurrentTab()
        {
            ClearList();

            List<JournalEntry> entries = GetEntriesForTab(currentTab);

            foreach (var entry in entries)
            {
                CreateEntryButton(entry);
            }

            // Select first entry
            if (entries.Count > 0)
            {
                SelectEntry(entries[0]);
            }
            else
            {
                ClearDetailPanel();
            }
        }

        private List<JournalEntry> GetEntriesForTab(JournalTab tab)
        {
            return tab switch
            {
                JournalTab.ActiveQuests => activeQuests,
                JournalTab.CompletedQuests => completedQuests,
                JournalTab.Notes => notes,
                JournalTab.Lore => loreEntries,
                _ => activeQuests
            };
        }

        private void ClearList()
        {
            if (questListContainer != null)
            {
                foreach (Transform child in questListContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            if (entryListContainer != null)
            {
                foreach (Transform child in entryListContainer)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void CreateEntryButton(JournalEntry entry)
        {
            RectTransform container = currentTab == JournalTab.ActiveQuests || currentTab == JournalTab.CompletedQuests
                ? questListContainer : entryListContainer;

            if (container == null) return;

            GameObject buttonObj = questButtonPrefab != null
                ? Instantiate(questButtonPrefab, container)
                : new GameObject($"Entry_{entry.id}");

            if (questButtonPrefab == null)
            {
                buttonObj.transform.SetParent(container, false);
            }

            Button button = buttonObj.GetComponent<Button>();
            if (button == null)
            {
                button = buttonObj.AddComponent<Button>();
            }

            TextMeshProUGUI text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text == null)
            {
                text = buttonObj.AddComponent<TextMeshProUGUI>();
            }

            text.text = entry.title;
            button.onClick.AddListener(() => SelectEntry(entry));
        }

        private void SelectEntry(JournalEntry entry)
        {
            selectedEntry = entry;
            UpdateDetailPanel(entry);

            EntrySelected?.Invoke(entry);
            onEntrySelected?.Invoke(entry);
        }

        private void UpdateDetailPanel(JournalEntry entry)
        {
            if (titleText != null)
            {
                titleText.text = entry.title;
            }

            if (descriptionText != null)
            {
                descriptionText.text = entry.description;
            }

            if (objectivesText != null && entry.objectives != null)
            {
                string objectives = "";
                foreach (var obj in entry.objectives)
                {
                    string marker = obj.isCompleted ? "✓" : "○";
                    objectives += $"{marker} {obj.text}\n";
                }
                objectivesText.text = objectives;
            }

            if (rewardsText != null && entry.rewards != null)
            {
                rewardsText.text = string.Join("\n", entry.rewards);
            }

            if (questIcon != null)
            {
                questIcon.sprite = entry.icon;
                questIcon.gameObject.SetActive(entry.icon != null);
            }
        }

        private void ClearDetailPanel()
        {
            if (titleText != null) titleText.text = "";
            if (descriptionText != null) descriptionText.text = "Select an entry";
            if (objectivesText != null) objectivesText.text = "";
            if (rewardsText != null) rewardsText.text = "";
            if (questIcon != null) questIcon.gameObject.SetActive(false);
        }

        /// <summary>
        /// Add a quest to the journal.
        /// </summary>
        public void AddQuest(JournalEntry quest)
        {
            if (quest == null) return;
            if (activeQuests.Exists(q => q.id == quest.id)) return;

            quest.type = EntryType.Quest;
            activeQuests.Add(quest);

            EntryAdded?.Invoke(quest);

            if (isOpen && currentTab == JournalTab.ActiveQuests)
            {
                RefreshCurrentTab();
            }

            Debug.Log($"[Journal] Quest added: {quest.title}");
        }

        /// <summary>
        /// Complete a quest.
        /// </summary>
        public void CompleteQuest(string questId)
        {
            JournalEntry quest = activeQuests.Find(q => q.id == questId);
            if (quest != null)
            {
                activeQuests.Remove(quest);
                quest.isCompleted = true;
                completedQuests.Add(quest);

                if (isOpen)
                {
                    RefreshCurrentTab();
                }

                Debug.Log($"[Journal] Quest completed: {quest.title}");
            }
        }

        /// <summary>
        /// Update quest objective.
        /// </summary>
        public void UpdateQuestObjective(string questId, string objectiveId, bool completed)
        {
            JournalEntry quest = activeQuests.Find(q => q.id == questId);
            if (quest != null && quest.objectives != null)
            {
                var obj = quest.objectives.Find(o => o.id == objectiveId);
                if (obj != null)
                {
                    obj.isCompleted = completed;

                    if (isOpen && selectedEntry?.id == questId)
                    {
                        UpdateDetailPanel(quest);
                    }
                }
            }
        }

        /// <summary>
        /// Add a note.
        /// </summary>
        public void AddNote(JournalEntry note)
        {
            if (note == null) return;

            note.type = EntryType.Note;
            notes.Add(note);

            EntryAdded?.Invoke(note);
            Debug.Log($"[Journal] Note added: {note.title}");
        }

        /// <summary>
        /// Add a lore entry.
        /// </summary>
        public void AddLore(JournalEntry lore)
        {
            if (lore == null) return;
            if (loreEntries.Exists(l => l.id == lore.id)) return;

            lore.type = EntryType.Lore;
            loreEntries.Add(lore);

            EntryAdded?.Invoke(lore);
            Debug.Log($"[Journal] Lore discovered: {lore.title}");
        }

        /// <summary>
        /// Get quest by ID.
        /// </summary>
        public JournalEntry GetQuest(string questId)
        {
            return activeQuests.Find(q => q.id == questId)
                ?? completedQuests.Find(q => q.id == questId);
        }

        /// <summary>
        /// Check if quest is active.
        /// </summary>
        public bool IsQuestActive(string questId)
        {
            return activeQuests.Exists(q => q.id == questId);
        }

        /// <summary>
        /// Check if quest is completed.
        /// </summary>
        public bool IsQuestCompleted(string questId)
        {
            return completedQuests.Exists(q => q.id == questId);
        }

        /// <summary>
        /// Get active quest count.
        /// </summary>
        public int GetActiveQuestCount() => activeQuests.Count;

        /// <summary>
        /// Get completed quest count.
        /// </summary>
        public int GetCompletedQuestCount() => completedQuests.Count;
    }

    [Serializable]
    public class JournalEntry
    {
        public string id;
        public string title;
        [TextArea(3, 10)]
        public string description;
        public Sprite icon;
        public EntryType type;
        public bool isCompleted;
        public List<JournalObjective> objectives;
        public List<string> rewards;
        public string category;
        public int sortOrder;
    }

    [Serializable]
    public class JournalObjective
    {
        public string id;
        public string text;
        public bool isCompleted;
        public bool isOptional;
    }

    public enum EntryType
    {
        Quest,
        Note,
        Lore
    }
}
