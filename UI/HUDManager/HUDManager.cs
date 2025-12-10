using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace UnityVault.UI
{
    /// <summary>
    /// Central HUD manager for all UI elements.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        public static HUDManager Instance { get; private set; }

        [Header("HUD Groups")]
        [SerializeField] private CanvasGroup gameplayHUD;
        [SerializeField] private CanvasGroup combatHUD;
        [SerializeField] private CanvasGroup inventoryHUD;
        [SerializeField] private CanvasGroup dialogueHUD;
        [SerializeField] private CanvasGroup pauseHUD;

        [Header("Animation")]
        [SerializeField] private float fadeSpeed = 5f;
        [SerializeField] private bool animateTransitions = true;

        [Header("Safe Area")]
        [SerializeField] private bool respectSafeArea = true;
        [SerializeField] private RectTransform safeAreaContainer;

        // Registered HUD elements
        private Dictionary<string, HUDElement> registeredElements = new Dictionary<string, HUDElement>();
        private Dictionary<string, CanvasGroup> hudGroups = new Dictionary<string, CanvasGroup>();

        // State
        private HUDState currentState = HUDState.Gameplay;
        private List<string> activeOverlays = new List<string>();

        // Events
        public event Action<HUDState, HUDState> StateChanged;
        public event Action<string, bool> ElementVisibilityChanged;

        public enum HUDState
        {
            Gameplay,
            Combat,
            Inventory,
            Dialogue,
            Pause,
            Cinematic,
            Hidden
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

            InitializeGroups();
            ApplySafeArea();
        }

        private void InitializeGroups()
        {
            if (gameplayHUD != null) hudGroups["gameplay"] = gameplayHUD;
            if (combatHUD != null) hudGroups["combat"] = combatHUD;
            if (inventoryHUD != null) hudGroups["inventory"] = inventoryHUD;
            if (dialogueHUD != null) hudGroups["dialogue"] = dialogueHUD;
            if (pauseHUD != null) hudGroups["pause"] = pauseHUD;
        }

        private void ApplySafeArea()
        {
            if (!respectSafeArea || safeAreaContainer == null) return;

            Rect safeArea = Screen.safeArea;
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            safeAreaContainer.anchorMin = anchorMin;
            safeAreaContainer.anchorMax = anchorMax;
        }

        private void Update()
        {
            UpdateAnimations();
        }

        private void UpdateAnimations()
        {
            if (!animateTransitions) return;

            foreach (var kvp in hudGroups)
            {
                if (kvp.Value == null) continue;

                float targetAlpha = ShouldGroupBeVisible(kvp.Key) ? 1f : 0f;
                kvp.Value.alpha = Mathf.MoveTowards(kvp.Value.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
                kvp.Value.interactable = kvp.Value.alpha > 0.5f;
                kvp.Value.blocksRaycasts = kvp.Value.alpha > 0.5f;
            }
        }

        private bool ShouldGroupBeVisible(string groupName)
        {
            // Check overlays first
            if (activeOverlays.Contains(groupName))
            {
                return true;
            }

            // Check state-based visibility
            return currentState switch
            {
                HUDState.Gameplay => groupName == "gameplay",
                HUDState.Combat => groupName == "gameplay" || groupName == "combat",
                HUDState.Inventory => groupName == "inventory",
                HUDState.Dialogue => groupName == "dialogue",
                HUDState.Pause => groupName == "pause",
                HUDState.Cinematic => false,
                HUDState.Hidden => false,
                _ => groupName == "gameplay"
            };
        }

        /// <summary>
        /// Set the current HUD state.
        /// </summary>
        public static void SetState(HUDState newState)
        {
            if (Instance == null) return;

            HUDState oldState = Instance.currentState;
            Instance.currentState = newState;

            if (!Instance.animateTransitions)
            {
                Instance.ApplyStateImmediate();
            }

            Instance.StateChanged?.Invoke(oldState, newState);
            Debug.Log($"[HUD] State changed: {oldState} -> {newState}");
        }

        private void ApplyStateImmediate()
        {
            foreach (var kvp in hudGroups)
            {
                if (kvp.Value == null) continue;

                bool visible = ShouldGroupBeVisible(kvp.Key);
                kvp.Value.alpha = visible ? 1f : 0f;
                kvp.Value.interactable = visible;
                kvp.Value.blocksRaycasts = visible;
            }
        }

        /// <summary>
        /// Show a HUD overlay without changing state.
        /// </summary>
        public static void ShowOverlay(string overlayName)
        {
            if (Instance == null) return;

            if (!Instance.activeOverlays.Contains(overlayName))
            {
                Instance.activeOverlays.Add(overlayName);
            }
        }

        /// <summary>
        /// Hide a HUD overlay.
        /// </summary>
        public static void HideOverlay(string overlayName)
        {
            if (Instance == null) return;
            Instance.activeOverlays.Remove(overlayName);
        }

        /// <summary>
        /// Register a HUD element for management.
        /// </summary>
        public static void RegisterElement(string id, GameObject element, string group = "gameplay")
        {
            if (Instance == null) return;

            var hudElement = new HUDElement
            {
                id = id,
                gameObject = element,
                group = group,
                canvasGroup = element.GetComponent<CanvasGroup>()
            };

            if (hudElement.canvasGroup == null)
            {
                hudElement.canvasGroup = element.AddComponent<CanvasGroup>();
            }

            Instance.registeredElements[id] = hudElement;
        }

        /// <summary>
        /// Unregister a HUD element.
        /// </summary>
        public static void UnregisterElement(string id)
        {
            if (Instance == null) return;
            Instance.registeredElements.Remove(id);
        }

        /// <summary>
        /// Show a specific HUD element.
        /// </summary>
        public static void ShowElement(string id)
        {
            if (Instance == null) return;

            if (Instance.registeredElements.TryGetValue(id, out HUDElement element))
            {
                element.isVisible = true;
                element.gameObject.SetActive(true);
                Instance.ElementVisibilityChanged?.Invoke(id, true);
            }
        }

        /// <summary>
        /// Hide a specific HUD element.
        /// </summary>
        public static void HideElement(string id)
        {
            if (Instance == null) return;

            if (Instance.registeredElements.TryGetValue(id, out HUDElement element))
            {
                element.isVisible = false;
                element.gameObject.SetActive(false);
                Instance.ElementVisibilityChanged?.Invoke(id, false);
            }
        }

        /// <summary>
        /// Toggle a specific HUD element.
        /// </summary>
        public static void ToggleElement(string id)
        {
            if (Instance == null) return;

            if (Instance.registeredElements.TryGetValue(id, out HUDElement element))
            {
                if (element.isVisible)
                {
                    HideElement(id);
                }
                else
                {
                    ShowElement(id);
                }
            }
        }

        /// <summary>
        /// Get a registered HUD element.
        /// </summary>
        public static T GetElement<T>(string id) where T : Component
        {
            if (Instance == null) return null;

            if (Instance.registeredElements.TryGetValue(id, out HUDElement element))
            {
                return element.gameObject.GetComponent<T>();
            }

            return null;
        }

        /// <summary>
        /// Add a custom HUD group.
        /// </summary>
        public static void AddGroup(string groupName, CanvasGroup group)
        {
            if (Instance == null) return;
            Instance.hudGroups[groupName] = group;
        }

        /// <summary>
        /// Show all HUD elements.
        /// </summary>
        public static void ShowAll()
        {
            SetState(HUDState.Gameplay);
        }

        /// <summary>
        /// Hide all HUD elements.
        /// </summary>
        public static void HideAll()
        {
            SetState(HUDState.Hidden);
        }

        /// <summary>
        /// Check if a specific element is visible.
        /// </summary>
        public static bool IsElementVisible(string id)
        {
            if (Instance == null) return false;

            if (Instance.registeredElements.TryGetValue(id, out HUDElement element))
            {
                return element.isVisible;
            }

            return false;
        }

        /// <summary>
        /// Get current HUD state.
        /// </summary>
        public static HUDState GetCurrentState()
        {
            return Instance?.currentState ?? HUDState.Gameplay;
        }

        /// <summary>
        /// Trigger screen flash effect.
        /// </summary>
        public static void FlashScreen(Color color, float duration = 0.1f)
        {
            if (Instance == null) return;
            Instance.StartCoroutine(Instance.ScreenFlashCoroutine(color, duration));
        }

        private System.Collections.IEnumerator ScreenFlashCoroutine(Color color, float duration)
        {
            // Would need a flash image reference
            yield return new WaitForSeconds(duration);
        }

        private class HUDElement
        {
            public string id;
            public GameObject gameObject;
            public CanvasGroup canvasGroup;
            public string group;
            public bool isVisible = true;
        }
    }
}
