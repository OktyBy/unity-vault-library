using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System;
using System.Collections.Generic;

namespace UnityVault.Multiplayer
{
    /// <summary>
    /// Multiplayer chat system.
    /// </summary>
    public class ChatSystem : MonoBehaviour
    {
        public static ChatSystem Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject chatPanel;
        [SerializeField] private TMP_InputField chatInput;
        [SerializeField] private ScrollRect chatScroll;
        [SerializeField] private RectTransform messageContainer;
        [SerializeField] private GameObject messagePrefab;

        [Header("Settings")]
        [SerializeField] private int maxMessages = 100;
        [SerializeField] private float messageLifetime = 0f; // 0 = permanent
        [SerializeField] private bool autoScroll = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.Return;
        [SerializeField] private KeyCode sendKey = KeyCode.Return;

        [Header("Chat Channels")]
        [SerializeField] private bool enableChannels = true;
        [SerializeField] private ChatChannel[] channels;

        [Header("Formatting")]
        [SerializeField] private string timestampFormat = "[HH:mm]";
        [SerializeField] private bool showTimestamp = true;
        [SerializeField] private Color systemMessageColor = Color.yellow;
        [SerializeField] private Color localPlayerColor = Color.cyan;

        [Header("Events")]
        [SerializeField] private UnityEvent<ChatMessage> onMessageReceived;
        [SerializeField] private UnityEvent<ChatMessage> onMessageSent;

        // State
        private List<ChatMessage> messages = new List<ChatMessage>();
        private List<GameObject> messageObjects = new List<GameObject>();
        private ChatChannel currentChannel;
        private bool isInputActive;
        private string localPlayerId;
        private string localPlayerName;

        // Events
        public event Action<ChatMessage> MessageReceived;
        public event Action<ChatMessage> MessageSent;
        public event Action<ChatChannel> ChannelChanged;

        public bool IsInputActive => isInputActive;
        public ChatChannel CurrentChannel => currentChannel;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (channels != null && channels.Length > 0)
            {
                currentChannel = channels[0];
            }
        }

        private void Start()
        {
            if (chatInput != null)
            {
                chatInput.onSubmit.AddListener(OnInputSubmit);
            }

            HideInput();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                if (isInputActive)
                {
                    SendCurrentMessage();
                }
                else
                {
                    ShowInput();
                }
            }

            if (isInputActive && Input.GetKeyDown(KeyCode.Escape))
            {
                HideInput();
            }
        }

        /// <summary>
        /// Set local player info.
        /// </summary>
        public void SetLocalPlayer(string playerId, string playerName)
        {
            localPlayerId = playerId;
            localPlayerName = playerName;
        }

        /// <summary>
        /// Show chat input.
        /// </summary>
        public void ShowInput()
        {
            isInputActive = true;

            if (chatInput != null)
            {
                chatInput.gameObject.SetActive(true);
                chatInput.ActivateInputField();
                chatInput.Select();
            }
        }

        /// <summary>
        /// Hide chat input.
        /// </summary>
        public void HideInput()
        {
            isInputActive = false;

            if (chatInput != null)
            {
                chatInput.text = "";
                chatInput.DeactivateInputField();
                chatInput.gameObject.SetActive(false);
            }
        }

        private void OnInputSubmit(string text)
        {
            SendCurrentMessage();
        }

        /// <summary>
        /// Send current input message.
        /// </summary>
        public void SendCurrentMessage()
        {
            if (chatInput == null) return;

            string text = chatInput.text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                // Check for commands
                if (text.StartsWith("/"))
                {
                    ProcessCommand(text);
                }
                else
                {
                    SendMessage(text);
                }
            }

            HideInput();
        }

        /// <summary>
        /// Send a chat message.
        /// </summary>
        public void SendMessage(string text, ChatChannel channel = null)
        {
            if (string.IsNullOrEmpty(text)) return;

            ChatMessage message = new ChatMessage
            {
                messageId = Guid.NewGuid().ToString(),
                senderId = localPlayerId,
                senderName = localPlayerName,
                text = text,
                timestamp = DateTime.Now,
                channel = channel ?? currentChannel,
                messageType = ChatMessageType.Player
            };

            // Add locally
            AddMessage(message);

            // Send to network
            // TODO: NetworkManager.Instance.SendChatMessage(message);

            MessageSent?.Invoke(message);
            onMessageSent?.Invoke(message);
        }

        /// <summary>
        /// Receive a chat message from network.
        /// </summary>
        public void ReceiveMessage(ChatMessage message)
        {
            if (message == null) return;

            AddMessage(message);

            MessageReceived?.Invoke(message);
            onMessageReceived?.Invoke(message);
        }

        /// <summary>
        /// Add a system message.
        /// </summary>
        public void AddSystemMessage(string text)
        {
            ChatMessage message = new ChatMessage
            {
                messageId = Guid.NewGuid().ToString(),
                senderId = "system",
                senderName = "System",
                text = text,
                timestamp = DateTime.Now,
                messageType = ChatMessageType.System
            };

            AddMessage(message);
        }

        /// <summary>
        /// Add player join message.
        /// </summary>
        public void AddPlayerJoinMessage(string playerName)
        {
            AddSystemMessage($"{playerName} has joined the game");
        }

        /// <summary>
        /// Add player leave message.
        /// </summary>
        public void AddPlayerLeaveMessage(string playerName)
        {
            AddSystemMessage($"{playerName} has left the game");
        }

        private void AddMessage(ChatMessage message)
        {
            messages.Add(message);

            // Limit message count
            while (messages.Count > maxMessages)
            {
                messages.RemoveAt(0);
                if (messageObjects.Count > 0)
                {
                    Destroy(messageObjects[0]);
                    messageObjects.RemoveAt(0);
                }
            }

            // Create UI element
            CreateMessageUI(message);

            // Auto scroll
            if (autoScroll && chatScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                chatScroll.verticalNormalizedPosition = 0f;
            }
        }

        private void CreateMessageUI(ChatMessage message)
        {
            if (messageContainer == null || messagePrefab == null) return;

            GameObject msgObj = Instantiate(messagePrefab, messageContainer);
            TextMeshProUGUI text = msgObj.GetComponentInChildren<TextMeshProUGUI>();

            if (text != null)
            {
                text.text = FormatMessage(message);
                text.color = GetMessageColor(message);
            }

            messageObjects.Add(msgObj);

            // Auto-destroy if lifetime set
            if (messageLifetime > 0)
            {
                Destroy(msgObj, messageLifetime);
            }
        }

        private string FormatMessage(ChatMessage message)
        {
            string formatted = "";

            if (showTimestamp)
            {
                formatted += message.timestamp.ToString(timestampFormat) + " ";
            }

            if (enableChannels && message.channel != null)
            {
                formatted += $"[{message.channel.channelName}] ";
            }

            if (message.messageType == ChatMessageType.System)
            {
                formatted += message.text;
            }
            else
            {
                formatted += $"<b>{message.senderName}</b>: {message.text}";
            }

            return formatted;
        }

        private Color GetMessageColor(ChatMessage message)
        {
            if (message.messageType == ChatMessageType.System)
            {
                return systemMessageColor;
            }

            if (message.senderId == localPlayerId)
            {
                return localPlayerColor;
            }

            if (enableChannels && message.channel != null)
            {
                return message.channel.channelColor;
            }

            return Color.white;
        }

        private void ProcessCommand(string command)
        {
            string[] parts = command.Substring(1).Split(' ');
            string cmd = parts[0].ToLower();

            switch (cmd)
            {
                case "whisper":
                case "w":
                    if (parts.Length >= 3)
                    {
                        string target = parts[1];
                        string msg = string.Join(" ", parts, 2, parts.Length - 2);
                        SendWhisper(target, msg);
                    }
                    break;

                case "channel":
                case "ch":
                    if (parts.Length >= 2)
                    {
                        SwitchChannel(parts[1]);
                    }
                    break;

                case "clear":
                    ClearChat();
                    break;

                default:
                    AddSystemMessage($"Unknown command: {cmd}");
                    break;
            }
        }

        /// <summary>
        /// Send whisper to specific player.
        /// </summary>
        public void SendWhisper(string targetPlayer, string text)
        {
            ChatMessage message = new ChatMessage
            {
                messageId = Guid.NewGuid().ToString(),
                senderId = localPlayerId,
                senderName = localPlayerName,
                targetId = targetPlayer,
                text = text,
                timestamp = DateTime.Now,
                messageType = ChatMessageType.Whisper
            };

            // Add locally with whisper format
            ChatMessage localCopy = message;
            localCopy.text = $"[To {targetPlayer}]: {text}";
            AddMessage(localCopy);

            // TODO: Send to target player
            MessageSent?.Invoke(message);
        }

        /// <summary>
        /// Switch chat channel.
        /// </summary>
        public void SwitchChannel(string channelName)
        {
            if (!enableChannels || channels == null) return;

            foreach (var channel in channels)
            {
                if (channel.channelName.Equals(channelName, StringComparison.OrdinalIgnoreCase))
                {
                    currentChannel = channel;
                    ChannelChanged?.Invoke(channel);
                    AddSystemMessage($"Switched to {channel.channelName} channel");
                    return;
                }
            }

            AddSystemMessage($"Channel not found: {channelName}");
        }

        /// <summary>
        /// Clear chat messages.
        /// </summary>
        public void ClearChat()
        {
            messages.Clear();

            foreach (var obj in messageObjects)
            {
                Destroy(obj);
            }
            messageObjects.Clear();
        }

        /// <summary>
        /// Toggle chat panel visibility.
        /// </summary>
        public void ToggleChat()
        {
            if (chatPanel != null)
            {
                chatPanel.SetActive(!chatPanel.activeSelf);
            }
        }

        /// <summary>
        /// Show chat panel.
        /// </summary>
        public void ShowChat()
        {
            if (chatPanel != null)
            {
                chatPanel.SetActive(true);
            }
        }

        /// <summary>
        /// Hide chat panel.
        /// </summary>
        public void HideChat()
        {
            if (chatPanel != null)
            {
                chatPanel.SetActive(false);
            }
            HideInput();
        }
    }

    [Serializable]
    public class ChatMessage
    {
        public string messageId;
        public string senderId;
        public string senderName;
        public string targetId;
        public string text;
        public DateTime timestamp;
        public ChatChannel channel;
        public ChatMessageType messageType;
    }

    [Serializable]
    public class ChatChannel
    {
        public string channelId;
        public string channelName;
        public Color channelColor = Color.white;
        public bool isDefault;
        public bool requirePermission;
    }

    public enum ChatMessageType
    {
        Player,
        System,
        Whisper,
        Team,
        Global
    }
}
