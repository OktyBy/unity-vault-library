using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Dialogue
{
    /// <summary>
    /// Branching narrative system for story choices and consequences.
    /// </summary>
    public class BranchingNarrative : MonoBehaviour
    {
        public static BranchingNarrative Instance { get; private set; }

        [Header("Story Data")]
        [SerializeField] private List<StoryChapter> chapters = new List<StoryChapter>();
        [SerializeField] private string startingChapterId;

        [Header("Events")]
        [SerializeField] private UnityEvent<StoryNode> onNodeEntered;
        [SerializeField] private UnityEvent<StoryChoice> onChoiceMade;
        [SerializeField] private UnityEvent<string> onFlagSet;
        [SerializeField] private UnityEvent<StoryChapter> onChapterStarted;
        [SerializeField] private UnityEvent<StoryChapter> onChapterCompleted;

        // State
        private Dictionary<string, StoryChapter> chapterMap = new Dictionary<string, StoryChapter>();
        private Dictionary<string, StoryNode> nodeMap = new Dictionary<string, StoryNode>();
        private Dictionary<string, bool> storyFlags = new Dictionary<string, bool>();
        private Dictionary<string, int> storyVariables = new Dictionary<string, int>();
        private List<string> choiceHistory = new List<string>();

        private StoryChapter currentChapter;
        private StoryNode currentNode;

        // Events
        public event Action<StoryNode> NodeEntered;
        public event Action<StoryChoice> ChoiceMade;
        public event Action<string, bool> FlagChanged;
        public event Action<string, int> VariableChanged;
        public event Action<StoryChapter> ChapterStarted;
        public event Action<StoryChapter> ChapterCompleted;
        public event Action StoryEnded;

        public StoryChapter CurrentChapter => currentChapter;
        public StoryNode CurrentNode => currentNode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildMaps();
        }

        private void BuildMaps()
        {
            foreach (var chapter in chapters)
            {
                chapterMap[chapter.chapterId] = chapter;

                if (chapter.nodes != null)
                {
                    foreach (var node in chapter.nodes)
                    {
                        nodeMap[node.nodeId] = node;
                    }
                }
            }
        }

        /// <summary>
        /// Start the story.
        /// </summary>
        public void StartStory()
        {
            if (string.IsNullOrEmpty(startingChapterId))
            {
                if (chapters.Count > 0)
                {
                    StartChapter(chapters[0].chapterId);
                }
                return;
            }

            StartChapter(startingChapterId);
        }

        /// <summary>
        /// Start a specific chapter.
        /// </summary>
        public void StartChapter(string chapterId)
        {
            if (!chapterMap.TryGetValue(chapterId, out StoryChapter chapter))
            {
                Debug.LogWarning($"[Narrative] Chapter not found: {chapterId}");
                return;
            }

            currentChapter = chapter;

            ChapterStarted?.Invoke(chapter);
            onChapterStarted?.Invoke(chapter);

            // Start at first node
            if (chapter.nodes != null && chapter.nodes.Count > 0)
            {
                EnterNode(chapter.nodes[0].nodeId);
            }

            Debug.Log($"[Narrative] Started chapter: {chapter.chapterName}");
        }

        /// <summary>
        /// Enter a story node.
        /// </summary>
        public void EnterNode(string nodeId)
        {
            if (!nodeMap.TryGetValue(nodeId, out StoryNode node))
            {
                Debug.LogWarning($"[Narrative] Node not found: {nodeId}");
                return;
            }

            currentNode = node;

            // Execute node actions
            ExecuteNodeActions(node);

            NodeEntered?.Invoke(node);
            onNodeEntered?.Invoke(node);

            // Auto-advance if no choices
            if (node.autoAdvance && !string.IsNullOrEmpty(node.nextNodeId))
            {
                EnterNode(node.nextNodeId);
            }

            Debug.Log($"[Narrative] Entered node: {node.nodeId}");
        }

        private void ExecuteNodeActions(StoryNode node)
        {
            if (node.actions == null) return;

            foreach (var action in node.actions)
            {
                ExecuteAction(action);
            }
        }

        private void ExecuteAction(StoryAction action)
        {
            switch (action.actionType)
            {
                case StoryActionType.SetFlag:
                    SetFlag(action.targetId, action.boolValue);
                    break;
                case StoryActionType.SetVariable:
                    SetVariable(action.targetId, action.intValue);
                    break;
                case StoryActionType.AddVariable:
                    AddToVariable(action.targetId, action.intValue);
                    break;
                case StoryActionType.TriggerEvent:
                    Debug.Log($"[Narrative] Triggered event: {action.targetId}");
                    break;
            }
        }

        /// <summary>
        /// Make a choice.
        /// </summary>
        public void MakeChoice(int choiceIndex)
        {
            if (currentNode == null || currentNode.choices == null) return;
            if (choiceIndex < 0 || choiceIndex >= currentNode.choices.Count) return;

            StoryChoice choice = currentNode.choices[choiceIndex];
            MakeChoice(choice);
        }

        /// <summary>
        /// Make a choice.
        /// </summary>
        public void MakeChoice(StoryChoice choice)
        {
            if (choice == null) return;

            // Record choice
            choiceHistory.Add(choice.choiceId);

            // Execute choice consequences
            if (choice.consequences != null)
            {
                foreach (var action in choice.consequences)
                {
                    ExecuteAction(action);
                }
            }

            ChoiceMade?.Invoke(choice);
            onChoiceMade?.Invoke(choice);

            // Navigate to next node
            if (!string.IsNullOrEmpty(choice.nextNodeId))
            {
                EnterNode(choice.nextNodeId);
            }
            else if (choice.endChapter)
            {
                EndChapter();
            }

            Debug.Log($"[Narrative] Made choice: {choice.choiceText}");
        }

        /// <summary>
        /// Get available choices for current node.
        /// </summary>
        public List<StoryChoice> GetAvailableChoices()
        {
            if (currentNode == null || currentNode.choices == null)
            {
                return new List<StoryChoice>();
            }

            List<StoryChoice> available = new List<StoryChoice>();

            foreach (var choice in currentNode.choices)
            {
                if (MeetsRequirements(choice.requirements))
                {
                    available.Add(choice);
                }
            }

            return available;
        }

        private bool MeetsRequirements(List<StoryRequirement> requirements)
        {
            if (requirements == null || requirements.Count == 0) return true;

            foreach (var req in requirements)
            {
                if (!CheckRequirement(req))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CheckRequirement(StoryRequirement req)
        {
            switch (req.requirementType)
            {
                case RequirementType.FlagSet:
                    return GetFlag(req.targetId) == req.boolValue;

                case RequirementType.FlagNotSet:
                    return GetFlag(req.targetId) != req.boolValue;

                case RequirementType.VariableEquals:
                    return GetVariable(req.targetId) == req.intValue;

                case RequirementType.VariableGreater:
                    return GetVariable(req.targetId) > req.intValue;

                case RequirementType.VariableLess:
                    return GetVariable(req.targetId) < req.intValue;

                case RequirementType.ChoiceMade:
                    return choiceHistory.Contains(req.targetId);

                case RequirementType.ChoiceNotMade:
                    return !choiceHistory.Contains(req.targetId);

                default:
                    return true;
            }
        }

        /// <summary>
        /// End current chapter.
        /// </summary>
        public void EndChapter()
        {
            if (currentChapter == null) return;

            ChapterCompleted?.Invoke(currentChapter);
            onChapterCompleted?.Invoke(currentChapter);

            // Start next chapter
            if (!string.IsNullOrEmpty(currentChapter.nextChapterId))
            {
                StartChapter(currentChapter.nextChapterId);
            }
            else
            {
                StoryEnded?.Invoke();
                Debug.Log("[Narrative] Story ended");
            }
        }

        /// <summary>
        /// Set a story flag.
        /// </summary>
        public void SetFlag(string flagId, bool value)
        {
            storyFlags[flagId] = value;

            FlagChanged?.Invoke(flagId, value);
            onFlagSet?.Invoke(flagId);

            Debug.Log($"[Narrative] Flag {flagId} = {value}");
        }

        /// <summary>
        /// Get a story flag.
        /// </summary>
        public bool GetFlag(string flagId)
        {
            return storyFlags.TryGetValue(flagId, out bool value) && value;
        }

        /// <summary>
        /// Set a story variable.
        /// </summary>
        public void SetVariable(string varId, int value)
        {
            storyVariables[varId] = value;
            VariableChanged?.Invoke(varId, value);
        }

        /// <summary>
        /// Add to a story variable.
        /// </summary>
        public void AddToVariable(string varId, int amount)
        {
            int current = GetVariable(varId);
            SetVariable(varId, current + amount);
        }

        /// <summary>
        /// Get a story variable.
        /// </summary>
        public int GetVariable(string varId)
        {
            return storyVariables.TryGetValue(varId, out int value) ? value : 0;
        }

        /// <summary>
        /// Check if choice was made.
        /// </summary>
        public bool WasChoiceMade(string choiceId)
        {
            return choiceHistory.Contains(choiceId);
        }

        /// <summary>
        /// Get choice history.
        /// </summary>
        public List<string> GetChoiceHistory()
        {
            return new List<string>(choiceHistory);
        }

        /// <summary>
        /// Save narrative state.
        /// </summary>
        public NarrativeSaveData GetSaveData()
        {
            return new NarrativeSaveData
            {
                currentChapterId = currentChapter?.chapterId ?? "",
                currentNodeId = currentNode?.nodeId ?? "",
                flags = new Dictionary<string, bool>(storyFlags),
                variables = new Dictionary<string, int>(storyVariables),
                choiceHistory = new List<string>(choiceHistory)
            };
        }

        /// <summary>
        /// Load narrative state.
        /// </summary>
        public void LoadSaveData(NarrativeSaveData data)
        {
            if (data == null) return;

            storyFlags = new Dictionary<string, bool>(data.flags);
            storyVariables = new Dictionary<string, int>(data.variables);
            choiceHistory = new List<string>(data.choiceHistory);

            if (!string.IsNullOrEmpty(data.currentChapterId))
            {
                currentChapter = chapterMap.TryGetValue(data.currentChapterId, out var ch) ? ch : null;
            }

            if (!string.IsNullOrEmpty(data.currentNodeId))
            {
                EnterNode(data.currentNodeId);
            }
        }

        /// <summary>
        /// Reset story state.
        /// </summary>
        public void ResetStory()
        {
            storyFlags.Clear();
            storyVariables.Clear();
            choiceHistory.Clear();
            currentChapter = null;
            currentNode = null;
        }
    }

    [Serializable]
    public class StoryChapter
    {
        public string chapterId;
        public string chapterName;
        [TextArea]
        public string chapterDescription;
        public List<StoryNode> nodes = new List<StoryNode>();
        public string nextChapterId;
    }

    [Serializable]
    public class StoryNode
    {
        public string nodeId;
        public string speakerName;
        [TextArea(3, 10)]
        public string dialogueText;
        public Sprite speakerPortrait;
        public AudioClip voiceLine;

        [Header("Choices")]
        public List<StoryChoice> choices = new List<StoryChoice>();

        [Header("Auto Advance")]
        public bool autoAdvance;
        public string nextNodeId;
        public float autoAdvanceDelay = 0f;

        [Header("Actions")]
        public List<StoryAction> actions = new List<StoryAction>();
    }

    [Serializable]
    public class StoryChoice
    {
        public string choiceId;
        public string choiceText;
        public string nextNodeId;
        public bool endChapter;

        [Header("Requirements")]
        public List<StoryRequirement> requirements = new List<StoryRequirement>();

        [Header("Consequences")]
        public List<StoryAction> consequences = new List<StoryAction>();
    }

    [Serializable]
    public class StoryRequirement
    {
        public RequirementType requirementType;
        public string targetId;
        public bool boolValue;
        public int intValue;
    }

    [Serializable]
    public class StoryAction
    {
        public StoryActionType actionType;
        public string targetId;
        public bool boolValue;
        public int intValue;
    }

    public enum RequirementType
    {
        FlagSet,
        FlagNotSet,
        VariableEquals,
        VariableGreater,
        VariableLess,
        ChoiceMade,
        ChoiceNotMade
    }

    public enum StoryActionType
    {
        SetFlag,
        SetVariable,
        AddVariable,
        TriggerEvent
    }

    [Serializable]
    public class NarrativeSaveData
    {
        public string currentChapterId;
        public string currentNodeId;
        public Dictionary<string, bool> flags;
        public Dictionary<string, int> variables;
        public List<string> choiceHistory;
    }
}
