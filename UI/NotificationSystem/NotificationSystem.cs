using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

namespace UnityVault.UI
{
    /// <summary>
    /// Notification system for popups, alerts, and toast messages.
    /// </summary>
    public class NotificationSystem : MonoBehaviour
    {
        public static NotificationSystem Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private GameObject notificationPrefab;
        [SerializeField] private GameObject toastPrefab;
        [SerializeField] private GameObject alertPrefab;

        [Header("Containers")]
        [SerializeField] private RectTransform notificationContainer;
        [SerializeField] private RectTransform toastContainer;
        [SerializeField] private RectTransform alertContainer;

        [Header("Settings")]
        [SerializeField] private int maxNotifications = 5;
        [SerializeField] private float defaultDuration = 3f;
        [SerializeField] private float fadeSpeed = 5f;
        [SerializeField] private float slideSpeed = 500f;

        [Header("Positioning")]
        [SerializeField] private NotificationPosition position = NotificationPosition.TopRight;
        [SerializeField] private float spacing = 10f;
        [SerializeField] private float padding = 20f;

        [Header("Audio")]
        [SerializeField] private AudioClip notificationSound;
        [SerializeField] private AudioClip alertSound;
        [SerializeField] private AudioClip successSound;
        [SerializeField] private AudioClip errorSound;

        // State
        private List<NotificationInstance> activeNotifications = new List<NotificationInstance>();
        private Queue<NotificationData> pendingNotifications = new Queue<NotificationData>();
        private AudioSource audioSource;

        // Events
        public event Action<NotificationData> NotificationShown;
        public event Action<NotificationData> NotificationDismissed;

        public enum NotificationPosition
        {
            TopLeft,
            TopCenter,
            TopRight,
            BottomLeft,
            BottomCenter,
            BottomRight
        }

        public enum NotificationType
        {
            Info,
            Success,
            Warning,
            Error,
            Achievement
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        private void Update()
        {
            UpdateNotifications();
            ProcessPendingNotifications();
        }

        private void UpdateNotifications()
        {
            for (int i = activeNotifications.Count - 1; i >= 0; i--)
            {
                var notif = activeNotifications[i];

                if (notif.instance == null)
                {
                    activeNotifications.RemoveAt(i);
                    continue;
                }

                // Update timer
                notif.timer -= Time.deltaTime;

                // Start fade out
                if (notif.timer <= 0 && notif.state == NotificationState.Showing)
                {
                    notif.state = NotificationState.FadingOut;
                }

                // Update animation
                UpdateNotificationAnimation(notif, i);

                // Remove when done
                if (notif.state == NotificationState.FadingOut && notif.alpha <= 0)
                {
                    RemoveNotification(i);
                }
            }
        }

        private void UpdateNotificationAnimation(NotificationInstance notif, int index)
        {
            var canvasGroup = notif.instance.GetComponent<CanvasGroup>();
            var rectTransform = notif.instance.GetComponent<RectTransform>();

            switch (notif.state)
            {
                case NotificationState.FadingIn:
                    notif.alpha = Mathf.MoveTowards(notif.alpha, 1f, fadeSpeed * Time.deltaTime);
                    if (notif.alpha >= 1f)
                    {
                        notif.state = NotificationState.Showing;
                    }
                    break;

                case NotificationState.FadingOut:
                    notif.alpha = Mathf.MoveTowards(notif.alpha, 0f, fadeSpeed * Time.deltaTime);
                    break;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = notif.alpha;
            }

            // Slide to target position
            Vector2 targetPos = CalculateTargetPosition(index);
            rectTransform.anchoredPosition = Vector2.MoveTowards(
                rectTransform.anchoredPosition,
                targetPos,
                slideSpeed * Time.deltaTime
            );
        }

        private Vector2 CalculateTargetPosition(int index)
        {
            float x = 0;
            float y = 0;

            switch (position)
            {
                case NotificationPosition.TopRight:
                    x = -padding;
                    y = -padding - index * (100 + spacing);
                    break;
                case NotificationPosition.TopLeft:
                    x = padding;
                    y = -padding - index * (100 + spacing);
                    break;
                case NotificationPosition.TopCenter:
                    x = 0;
                    y = -padding - index * (100 + spacing);
                    break;
                case NotificationPosition.BottomRight:
                    x = -padding;
                    y = padding + index * (100 + spacing);
                    break;
                case NotificationPosition.BottomLeft:
                    x = padding;
                    y = padding + index * (100 + spacing);
                    break;
                case NotificationPosition.BottomCenter:
                    x = 0;
                    y = padding + index * (100 + spacing);
                    break;
            }

            return new Vector2(x, y);
        }

        private void ProcessPendingNotifications()
        {
            while (pendingNotifications.Count > 0 && activeNotifications.Count < maxNotifications)
            {
                var data = pendingNotifications.Dequeue();
                CreateNotification(data);
            }
        }

        private void CreateNotification(NotificationData data)
        {
            GameObject prefab = GetPrefabForType(data.type);
            if (prefab == null)
            {
                prefab = notificationPrefab;
            }

            if (prefab == null) return;

            GameObject instance = Instantiate(prefab, notificationContainer);
            SetupNotificationUI(instance, data);

            var notifInstance = new NotificationInstance
            {
                data = data,
                instance = instance,
                timer = data.duration,
                alpha = 0,
                state = NotificationState.FadingIn
            };

            // Set initial position off-screen
            var rt = instance.GetComponent<RectTransform>();
            SetupAnchor(rt);
            rt.anchoredPosition = GetStartPosition();

            // Add canvas group for fading
            var canvasGroup = instance.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = instance.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 0;

            activeNotifications.Insert(0, notifInstance);

            // Play sound
            PlayNotificationSound(data.type);

            NotificationShown?.Invoke(data);
        }

        private void SetupAnchor(RectTransform rt)
        {
            switch (position)
            {
                case NotificationPosition.TopRight:
                    rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
                    break;
                case NotificationPosition.TopLeft:
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                    break;
                case NotificationPosition.TopCenter:
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
                    break;
                case NotificationPosition.BottomRight:
                    rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
                    break;
                case NotificationPosition.BottomLeft:
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                    break;
                case NotificationPosition.BottomCenter:
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
                    break;
            }
        }

        private Vector2 GetStartPosition()
        {
            return position switch
            {
                NotificationPosition.TopRight => new Vector2(300, -padding),
                NotificationPosition.TopLeft => new Vector2(-300, -padding),
                NotificationPosition.TopCenter => new Vector2(0, 200),
                NotificationPosition.BottomRight => new Vector2(300, padding),
                NotificationPosition.BottomLeft => new Vector2(-300, padding),
                NotificationPosition.BottomCenter => new Vector2(0, -200),
                _ => new Vector2(300, -padding)
            };
        }

        private void SetupNotificationUI(GameObject instance, NotificationData data)
        {
            // Title
            var titleText = instance.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
            {
                titleText.text = data.title;
            }

            // Message
            var messageText = instance.transform.Find("Message")?.GetComponent<TextMeshProUGUI>();
            if (messageText != null)
            {
                messageText.text = data.message;
            }

            // Icon
            var iconImage = instance.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImage != null && data.icon != null)
            {
                iconImage.sprite = data.icon;
                iconImage.gameObject.SetActive(true);
            }

            // Type-based coloring
            var background = instance.GetComponent<Image>();
            if (background != null)
            {
                background.color = GetColorForType(data.type);
            }

            // Close button
            var closeButton = instance.GetComponentInChildren<Button>();
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => DismissNotification(instance));
            }
        }

        private GameObject GetPrefabForType(NotificationType type)
        {
            return type switch
            {
                NotificationType.Warning or NotificationType.Error => alertPrefab,
                NotificationType.Achievement => toastPrefab,
                _ => notificationPrefab
            };
        }

        private Color GetColorForType(NotificationType type)
        {
            return type switch
            {
                NotificationType.Info => new Color(0.2f, 0.4f, 0.8f, 0.9f),
                NotificationType.Success => new Color(0.2f, 0.7f, 0.2f, 0.9f),
                NotificationType.Warning => new Color(0.8f, 0.6f, 0.1f, 0.9f),
                NotificationType.Error => new Color(0.8f, 0.2f, 0.2f, 0.9f),
                NotificationType.Achievement => new Color(0.6f, 0.4f, 0.8f, 0.9f),
                _ => new Color(0.2f, 0.2f, 0.2f, 0.9f)
            };
        }

        private void PlayNotificationSound(NotificationType type)
        {
            if (audioSource == null) return;

            AudioClip clip = type switch
            {
                NotificationType.Success => successSound,
                NotificationType.Error => errorSound,
                NotificationType.Warning or NotificationType.Error => alertSound,
                _ => notificationSound
            };

            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void RemoveNotification(int index)
        {
            var notif = activeNotifications[index];

            NotificationDismissed?.Invoke(notif.data);

            if (notif.instance != null)
            {
                Destroy(notif.instance);
            }

            activeNotifications.RemoveAt(index);
        }

        private void DismissNotification(GameObject instance)
        {
            var notif = activeNotifications.Find(n => n.instance == instance);
            if (notif != null)
            {
                notif.state = NotificationState.FadingOut;
            }
        }

        // Static methods for easy access
        public static void Show(string title, string message, NotificationType type = NotificationType.Info)
        {
            Show(new NotificationData
            {
                title = title,
                message = message,
                type = type,
                duration = Instance?.defaultDuration ?? 3f
            });
        }

        public static void Show(NotificationData data)
        {
            if (Instance == null) return;

            if (data.duration <= 0)
            {
                data.duration = Instance.defaultDuration;
            }

            Instance.pendingNotifications.Enqueue(data);
        }

        public static void ShowSuccess(string message)
        {
            Show("Success", message, NotificationType.Success);
        }

        public static void ShowError(string message)
        {
            Show("Error", message, NotificationType.Error);
        }

        public static void ShowWarning(string message)
        {
            Show("Warning", message, NotificationType.Warning);
        }

        public static void ShowAchievement(string title, string description, Sprite icon = null)
        {
            Show(new NotificationData
            {
                title = title,
                message = description,
                icon = icon,
                type = NotificationType.Achievement,
                duration = 5f
            });
        }

        public static void ClearAll()
        {
            if (Instance == null) return;

            foreach (var notif in Instance.activeNotifications)
            {
                if (notif.instance != null)
                {
                    Destroy(notif.instance);
                }
            }

            Instance.activeNotifications.Clear();
            Instance.pendingNotifications.Clear();
        }

        private enum NotificationState
        {
            FadingIn,
            Showing,
            FadingOut
        }

        private class NotificationInstance
        {
            public NotificationData data;
            public GameObject instance;
            public float timer;
            public float alpha;
            public NotificationState state;
        }
    }

    [Serializable]
    public class NotificationData
    {
        public string title;
        public string message;
        public Sprite icon;
        public NotificationSystem.NotificationType type;
        public float duration;
        public Action onClick;
    }
}
