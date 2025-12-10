using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Dialogue
{
    /// <summary>
    /// Dialogue system with branching conversations.
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float typingSpeed = 0.05f;
        [SerializeField] private bool autoAdvance = false;
        [SerializeField] private float autoAdvanceDelay = 2f;

        [Header("Events")]
        [SerializeField] private UnityEvent<DialogueSO> onDialogueStarted;
        [SerializeField] private UnityEvent onDialogueEnded;
        [SerializeField] private UnityEvent<DialogueNode> onNodeChanged;
        [SerializeField] private UnityEvent<string> onTextDisplayed;

        // State
        private DialogueSO currentDialogue;
        private DialogueNode currentNode;
        private bool isDialogueActive;
        private bool isTyping;
        private string displayedText;

        // Properties
        public bool IsDialogueActive => isDialogueActive;
        public DialogueNode CurrentNode => currentNode;
        public string DisplayedText => displayedText;

        // Events
        public event Action<DialogueSO> DialogueStarted;
        public event Action DialogueEnded;
        public event Action<DialogueNode> NodeChanged;
        public event Action<string> TextDisplayed;
        public event Action<List<DialogueChoice>> ChoicesPresented;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void StartDialogue(DialogueSO dialogue)
        {
            if (dialogue == null || dialogue.nodes.Count == 0) return;

            currentDialogue = dialogue;
            isDialogueActive = true;

            DialogueStarted?.Invoke(dialogue);
            onDialogueStarted?.Invoke(dialogue);

            GoToNode(dialogue.startNodeId);
        }

        public void GoToNode(string nodeId)
        {
            var node = currentDialogue.GetNode(nodeId);
            if (node == null)
            {
                EndDialogue();
                return;
            }

            currentNode = node;
            NodeChanged?.Invoke(node);
            onNodeChanged?.Invoke(node);

            DisplayText(node.text);

            if (node.choices.Count > 0)
            {
                ChoicesPresented?.Invoke(node.choices);
            }
            else if (autoAdvance)
            {
                Invoke(nameof(Advance), autoAdvanceDelay);
            }
        }

        public void SelectChoice(int choiceIndex)
        {
            if (currentNode == null || choiceIndex < 0 || choiceIndex >= currentNode.choices.Count)
                return;

            var choice = currentNode.choices[choiceIndex];

            // Execute choice action if any
            choice.onSelected?.Invoke();

            // Go to next node
            GoToNode(choice.nextNodeId);
        }

        public void Advance()
        {
            if (!isDialogueActive) return;

            if (isTyping)
            {
                // Skip typing animation
                CompleteTyping();
                return;
            }

            if (currentNode.choices.Count == 0)
            {
                if (!string.IsNullOrEmpty(currentNode.nextNodeId))
                {
                    GoToNode(currentNode.nextNodeId);
                }
                else
                {
                    EndDialogue();
                }
            }
        }

        public void EndDialogue()
        {
            isDialogueActive = false;
            currentDialogue = null;
            currentNode = null;

            DialogueEnded?.Invoke();
            onDialogueEnded?.Invoke();
        }

        private void DisplayText(string text)
        {
            if (typingSpeed > 0)
            {
                StartCoroutine(TypeText(text));
            }
            else
            {
                displayedText = text;
                TextDisplayed?.Invoke(text);
                onTextDisplayed?.Invoke(text);
            }
        }

        private System.Collections.IEnumerator TypeText(string text)
        {
            isTyping = true;
            displayedText = "";

            foreach (char c in text)
            {
                displayedText += c;
                TextDisplayed?.Invoke(displayedText);
                onTextDisplayed?.Invoke(displayedText);
                yield return new WaitForSeconds(typingSpeed);
            }

            isTyping = false;
        }

        private void CompleteTyping()
        {
            StopAllCoroutines();
            isTyping = false;
            displayedText = currentNode.text;
            TextDisplayed?.Invoke(displayedText);
            onTextDisplayed?.Invoke(displayedText);
        }
    }

    /// <summary>
    /// Dialogue ScriptableObject containing conversation data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDialogue", menuName = "UnityVault/Dialogue/Dialogue")]
    public class DialogueSO : ScriptableObject
    {
        public string dialogueId;
        public string dialogueName;
        public string startNodeId;
        public List<DialogueNode> nodes = new List<DialogueNode>();

        public DialogueNode GetNode(string nodeId)
        {
            return nodes.Find(n => n.nodeId == nodeId);
        }
    }

    [System.Serializable]
    public class DialogueNode
    {
        public string nodeId;
        public string speakerName;
        public Sprite speakerPortrait;
        [TextArea(3, 5)]
        public string text;
        public string nextNodeId;
        public List<DialogueChoice> choices = new List<DialogueChoice>();
        public AudioClip voiceLine;
    }

    [System.Serializable]
    public class DialogueChoice
    {
        public string text;
        public string nextNodeId;
        public bool requiresCondition;
        public string conditionId;
        public UnityEvent onSelected;
    }
}
