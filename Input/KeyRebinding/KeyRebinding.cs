using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

namespace UnityVault.Input
{
    /// <summary>
    /// Key rebinding system for customizable controls.
    /// </summary>
    public class KeyRebindingManager : MonoBehaviour
    {
        public static KeyRebindingManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private string saveKey = "KeyBindings";
        [SerializeField] private bool saveOnChange = true;

        [Header("Default Bindings")]
        [SerializeField] private List<KeyBinding> defaultBindings = new List<KeyBinding>();

        [Header("Events")]
        [SerializeField] private UnityEvent<string, KeyCode> onKeyRebound;
        [SerializeField] private UnityEvent onBindingsReset;

        // State
        private Dictionary<string, KeyBinding> bindings = new Dictionary<string, KeyBinding>();
        private bool isListening;
        private string listeningAction;
        private Action<KeyCode> onKeyPressed;

        // Events
        public event Action<string, KeyCode> KeyRebound;
        public event Action BindingsReset;
        public event Action<string> ListeningStarted;
        public event Action ListeningCancelled;

        public bool IsListening => isListening;
        public string ListeningAction => listeningAction;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeBindings();
            LoadBindings();
        }

        private void InitializeBindings()
        {
            foreach (var binding in defaultBindings)
            {
                bindings[binding.actionName] = new KeyBinding
                {
                    actionName = binding.actionName,
                    displayName = binding.displayName,
                    primaryKey = binding.primaryKey,
                    secondaryKey = binding.secondaryKey,
                    category = binding.category,
                    canRebind = binding.canRebind
                };
            }
        }

        private void Update()
        {
            if (!isListening) return;

            // Check for key press
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (UnityEngine.Input.GetKeyDown(key))
                {
                    // Skip invalid keys
                    if (key == KeyCode.Escape)
                    {
                        CancelListening();
                        return;
                    }

                    if (IsValidKey(key))
                    {
                        OnKeyDetected(key);
                        return;
                    }
                }
            }
        }

        private bool IsValidKey(KeyCode key)
        {
            // Exclude mouse buttons and some system keys
            if (key == KeyCode.None) return false;
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) return false;

            return true;
        }

        /// <summary>
        /// Start listening for key input.
        /// </summary>
        public void StartListening(string actionName, Action<KeyCode> callback = null)
        {
            if (!bindings.TryGetValue(actionName, out KeyBinding binding)) return;
            if (!binding.canRebind) return;

            isListening = true;
            listeningAction = actionName;
            onKeyPressed = callback;

            ListeningStarted?.Invoke(actionName);

            Debug.Log($"[KeyRebinding] Listening for: {actionName}");
        }

        /// <summary>
        /// Cancel key listening.
        /// </summary>
        public void CancelListening()
        {
            isListening = false;
            listeningAction = null;
            onKeyPressed = null;

            ListeningCancelled?.Invoke();
        }

        private void OnKeyDetected(KeyCode key)
        {
            if (string.IsNullOrEmpty(listeningAction)) return;

            SetBinding(listeningAction, key);

            onKeyPressed?.Invoke(key);
            onKeyPressed = null;

            isListening = false;
            listeningAction = null;
        }

        /// <summary>
        /// Set key binding.
        /// </summary>
        public void SetBinding(string actionName, KeyCode key, bool isSecondary = false)
        {
            if (!bindings.TryGetValue(actionName, out KeyBinding binding)) return;

            // Check for conflicts
            string conflict = FindConflict(key, actionName);
            if (!string.IsNullOrEmpty(conflict))
            {
                // Clear the conflicting binding
                if (bindings[conflict].primaryKey == key)
                    bindings[conflict].primaryKey = KeyCode.None;
                else if (bindings[conflict].secondaryKey == key)
                    bindings[conflict].secondaryKey = KeyCode.None;
            }

            if (isSecondary)
            {
                binding.secondaryKey = key;
            }
            else
            {
                binding.primaryKey = key;
            }

            KeyRebound?.Invoke(actionName, key);
            onKeyRebound?.Invoke(actionName, key);

            if (saveOnChange)
            {
                SaveBindings();
            }

            Debug.Log($"[KeyRebinding] Set {actionName} to {key}");
        }

        /// <summary>
        /// Get key binding.
        /// </summary>
        public KeyCode GetBinding(string actionName, bool secondary = false)
        {
            if (!bindings.TryGetValue(actionName, out KeyBinding binding))
            {
                return KeyCode.None;
            }

            return secondary ? binding.secondaryKey : binding.primaryKey;
        }

        /// <summary>
        /// Check if action key is pressed.
        /// </summary>
        public bool GetKey(string actionName)
        {
            if (!bindings.TryGetValue(actionName, out KeyBinding binding))
            {
                return false;
            }

            return UnityEngine.Input.GetKey(binding.primaryKey) ||
                   UnityEngine.Input.GetKey(binding.secondaryKey);
        }

        /// <summary>
        /// Check if action key was pressed this frame.
        /// </summary>
        public bool GetKeyDown(string actionName)
        {
            if (!bindings.TryGetValue(actionName, out KeyBinding binding))
            {
                return false;
            }

            return UnityEngine.Input.GetKeyDown(binding.primaryKey) ||
                   UnityEngine.Input.GetKeyDown(binding.secondaryKey);
        }

        /// <summary>
        /// Check if action key was released this frame.
        /// </summary>
        public bool GetKeyUp(string actionName)
        {
            if (!bindings.TryGetValue(actionName, out KeyBinding binding))
            {
                return false;
            }

            return UnityEngine.Input.GetKeyUp(binding.primaryKey) ||
                   UnityEngine.Input.GetKeyUp(binding.secondaryKey);
        }

        /// <summary>
        /// Find conflicting action.
        /// </summary>
        public string FindConflict(KeyCode key, string excludeAction = null)
        {
            foreach (var kvp in bindings)
            {
                if (kvp.Key == excludeAction) continue;

                if (kvp.Value.primaryKey == key || kvp.Value.secondaryKey == key)
                {
                    return kvp.Key;
                }
            }

            return null;
        }

        /// <summary>
        /// Reset all bindings to default.
        /// </summary>
        public void ResetToDefault()
        {
            foreach (var defaultBinding in defaultBindings)
            {
                if (bindings.TryGetValue(defaultBinding.actionName, out KeyBinding binding))
                {
                    binding.primaryKey = defaultBinding.primaryKey;
                    binding.secondaryKey = defaultBinding.secondaryKey;
                }
            }

            SaveBindings();

            BindingsReset?.Invoke();
            onBindingsReset?.Invoke();

            Debug.Log("[KeyRebinding] Reset to defaults");
        }

        /// <summary>
        /// Reset single binding to default.
        /// </summary>
        public void ResetBinding(string actionName)
        {
            var defaultBinding = defaultBindings.Find(b => b.actionName == actionName);
            if (defaultBinding == null) return;

            if (bindings.TryGetValue(actionName, out KeyBinding binding))
            {
                binding.primaryKey = defaultBinding.primaryKey;
                binding.secondaryKey = defaultBinding.secondaryKey;

                if (saveOnChange)
                {
                    SaveBindings();
                }
            }
        }

        /// <summary>
        /// Save bindings to PlayerPrefs.
        /// </summary>
        public void SaveBindings()
        {
            KeyBindingData data = new KeyBindingData();

            foreach (var kvp in bindings)
            {
                data.bindings.Add(new KeyBindingSave
                {
                    actionName = kvp.Key,
                    primaryKey = (int)kvp.Value.primaryKey,
                    secondaryKey = (int)kvp.Value.secondaryKey
                });
            }

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(saveKey, json);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Load bindings from PlayerPrefs.
        /// </summary>
        public void LoadBindings()
        {
            string json = PlayerPrefs.GetString(saveKey, "");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                KeyBindingData data = JsonUtility.FromJson<KeyBindingData>(json);

                foreach (var save in data.bindings)
                {
                    if (bindings.TryGetValue(save.actionName, out KeyBinding binding))
                    {
                        binding.primaryKey = (KeyCode)save.primaryKey;
                        binding.secondaryKey = (KeyCode)save.secondaryKey;
                    }
                }

                Debug.Log("[KeyRebinding] Loaded bindings");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[KeyRebinding] Failed to load bindings: {e.Message}");
            }
        }

        /// <summary>
        /// Get all bindings.
        /// </summary>
        public List<KeyBinding> GetAllBindings()
        {
            return new List<KeyBinding>(bindings.Values);
        }

        /// <summary>
        /// Get bindings by category.
        /// </summary>
        public List<KeyBinding> GetBindingsByCategory(string category)
        {
            List<KeyBinding> result = new List<KeyBinding>();

            foreach (var binding in bindings.Values)
            {
                if (binding.category == category)
                {
                    result.Add(binding);
                }
            }

            return result;
        }

        /// <summary>
        /// Get key display name.
        /// </summary>
        public static string GetKeyDisplayName(KeyCode key)
        {
            if (key == KeyCode.None) return "None";

            // Handle special cases
            return key switch
            {
                KeyCode.Return => "Enter",
                KeyCode.LeftShift => "L-Shift",
                KeyCode.RightShift => "R-Shift",
                KeyCode.LeftControl => "L-Ctrl",
                KeyCode.RightControl => "R-Ctrl",
                KeyCode.LeftAlt => "L-Alt",
                KeyCode.RightAlt => "R-Alt",
                KeyCode.BackQuote => "`",
                KeyCode.Alpha0 => "0",
                KeyCode.Alpha1 => "1",
                KeyCode.Alpha2 => "2",
                KeyCode.Alpha3 => "3",
                KeyCode.Alpha4 => "4",
                KeyCode.Alpha5 => "5",
                KeyCode.Alpha6 => "6",
                KeyCode.Alpha7 => "7",
                KeyCode.Alpha8 => "8",
                KeyCode.Alpha9 => "9",
                _ => key.ToString()
            };
        }
    }

    [Serializable]
    public class KeyBinding
    {
        public string actionName;
        public string displayName;
        public KeyCode primaryKey = KeyCode.None;
        public KeyCode secondaryKey = KeyCode.None;
        public string category = "General";
        public bool canRebind = true;
    }

    [Serializable]
    public class KeyBindingData
    {
        public List<KeyBindingSave> bindings = new List<KeyBindingSave>();
    }

    [Serializable]
    public class KeyBindingSave
    {
        public string actionName;
        public int primaryKey;
        public int secondaryKey;
    }

    /// <summary>
    /// UI component for rebinding a single key.
    /// </summary>
    public class KeyRebindButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI actionNameText;
        [SerializeField] private TextMeshProUGUI keyText;
        [SerializeField] private Button rebindButton;
        [SerializeField] private Button resetButton;

        [Header("Settings")]
        [SerializeField] private string actionName;
        [SerializeField] private bool isSecondaryKey = false;
        [SerializeField] private string listeningText = "Press any key...";

        private void Start()
        {
            if (rebindButton != null)
            {
                rebindButton.onClick.AddListener(OnRebindClicked);
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(OnResetClicked);
            }

            UpdateDisplay();

            if (KeyRebindingManager.Instance != null)
            {
                KeyRebindingManager.Instance.KeyRebound += OnKeyRebound;
            }
        }

        private void OnDestroy()
        {
            if (KeyRebindingManager.Instance != null)
            {
                KeyRebindingManager.Instance.KeyRebound -= OnKeyRebound;
            }
        }

        public void SetAction(string action, bool secondary = false)
        {
            actionName = action;
            isSecondaryKey = secondary;
            UpdateDisplay();
        }

        private void OnRebindClicked()
        {
            if (KeyRebindingManager.Instance == null) return;

            if (keyText != null)
            {
                keyText.text = listeningText;
            }

            KeyRebindingManager.Instance.StartListening(actionName, OnKeyDetected);
        }

        private void OnKeyDetected(KeyCode key)
        {
            KeyRebindingManager.Instance?.SetBinding(actionName, key, isSecondaryKey);
            UpdateDisplay();
        }

        private void OnResetClicked()
        {
            KeyRebindingManager.Instance?.ResetBinding(actionName);
            UpdateDisplay();
        }

        private void OnKeyRebound(string action, KeyCode key)
        {
            if (action == actionName)
            {
                UpdateDisplay();
            }
        }

        private void UpdateDisplay()
        {
            if (KeyRebindingManager.Instance == null) return;

            KeyCode key = KeyRebindingManager.Instance.GetBinding(actionName, isSecondaryKey);

            if (keyText != null)
            {
                keyText.text = KeyRebindingManager.GetKeyDisplayName(key);
            }
        }
    }
}
