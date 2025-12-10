using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Input
{
    /// <summary>
    /// Input manager wrapper with action-based input handling.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Input Actions")]
        [SerializeField] private List<InputAction> inputActions = new List<InputAction>();

        private Dictionary<string, InputAction> actionMap = new Dictionary<string, InputAction>();
        private Dictionary<string, float> actionTimestamps = new Dictionary<string, float>();

        // Properties
        public Vector2 MoveInput => new Vector2(
            UnityEngine.Input.GetAxisRaw("Horizontal"),
            UnityEngine.Input.GetAxisRaw("Vertical")
        );

        public Vector2 LookInput => new Vector2(
            UnityEngine.Input.GetAxis("Mouse X"),
            UnityEngine.Input.GetAxis("Mouse Y")
        );

        public bool JumpPressed => GetActionDown("Jump");
        public bool AttackPressed => GetActionDown("Attack");
        public bool InteractPressed => GetActionDown("Interact");

        // Events
        public event Action<string> ActionTriggered;
        public event Action<string> ActionReleased;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            InitializeActions();
        }

        private void InitializeActions()
        {
            // Default actions
            AddDefaultActions();

            // Register custom actions
            foreach (var action in inputActions)
            {
                actionMap[action.actionName] = action;
            }
        }

        private void AddDefaultActions()
        {
            // Default keyboard/mouse bindings
            var defaults = new[]
            {
                new InputAction { actionName = "Jump", primaryKey = KeyCode.Space },
                new InputAction { actionName = "Attack", mouseButton = 0 },
                new InputAction { actionName = "SecondaryAttack", mouseButton = 1 },
                new InputAction { actionName = "Interact", primaryKey = KeyCode.E },
                new InputAction { actionName = "Inventory", primaryKey = KeyCode.I },
                new InputAction { actionName = "Pause", primaryKey = KeyCode.Escape },
                new InputAction { actionName = "Sprint", primaryKey = KeyCode.LeftShift },
                new InputAction { actionName = "Crouch", primaryKey = KeyCode.LeftControl },
                new InputAction { actionName = "Reload", primaryKey = KeyCode.R },
                new InputAction { actionName = "UseItem", primaryKey = KeyCode.Q },
            };

            foreach (var action in defaults)
            {
                if (!actionMap.ContainsKey(action.actionName))
                {
                    actionMap[action.actionName] = action;
                }
            }
        }

        private void Update()
        {
            foreach (var kvp in actionMap)
            {
                var action = kvp.Value;
                string actionName = kvp.Key;

                if (IsActionPressed(action))
                {
                    if (!actionTimestamps.ContainsKey(actionName) || actionTimestamps[actionName] == 0)
                    {
                        actionTimestamps[actionName] = Time.time;
                        action.onPressed?.Invoke();
                        ActionTriggered?.Invoke(actionName);
                    }
                    action.onHeld?.Invoke();
                }
                else
                {
                    if (actionTimestamps.ContainsKey(actionName) && actionTimestamps[actionName] > 0)
                    {
                        actionTimestamps[actionName] = 0;
                        action.onReleased?.Invoke();
                        ActionReleased?.Invoke(actionName);
                    }
                }
            }
        }

        private bool IsActionPressed(InputAction action)
        {
            // Check keyboard
            if (action.primaryKey != KeyCode.None && UnityEngine.Input.GetKey(action.primaryKey))
                return true;
            if (action.secondaryKey != KeyCode.None && UnityEngine.Input.GetKey(action.secondaryKey))
                return true;

            // Check mouse
            if (action.mouseButton >= 0 && UnityEngine.Input.GetMouseButton(action.mouseButton))
                return true;

            return false;
        }

        public bool GetAction(string actionName)
        {
            if (!actionMap.TryGetValue(actionName, out var action)) return false;
            return IsActionPressed(action);
        }

        public bool GetActionDown(string actionName)
        {
            if (!actionMap.TryGetValue(actionName, out var action)) return false;

            if (action.primaryKey != KeyCode.None && UnityEngine.Input.GetKeyDown(action.primaryKey))
                return true;
            if (action.secondaryKey != KeyCode.None && UnityEngine.Input.GetKeyDown(action.secondaryKey))
                return true;
            if (action.mouseButton >= 0 && UnityEngine.Input.GetMouseButtonDown(action.mouseButton))
                return true;

            return false;
        }

        public bool GetActionUp(string actionName)
        {
            if (!actionMap.TryGetValue(actionName, out var action)) return false;

            if (action.primaryKey != KeyCode.None && UnityEngine.Input.GetKeyUp(action.primaryKey))
                return true;
            if (action.secondaryKey != KeyCode.None && UnityEngine.Input.GetKeyUp(action.secondaryKey))
                return true;
            if (action.mouseButton >= 0 && UnityEngine.Input.GetMouseButtonUp(action.mouseButton))
                return true;

            return false;
        }

        public void RebindAction(string actionName, KeyCode newKey, bool isPrimary = true)
        {
            if (!actionMap.TryGetValue(actionName, out var action)) return;

            if (isPrimary)
            {
                action.primaryKey = newKey;
            }
            else
            {
                action.secondaryKey = newKey;
            }

            // Save binding
            PlayerPrefs.SetInt($"Input_{actionName}_{(isPrimary ? "Primary" : "Secondary")}", (int)newKey);
        }

        public void LoadBindings()
        {
            foreach (var kvp in actionMap)
            {
                string key = kvp.Key;
                var action = kvp.Value;

                if (PlayerPrefs.HasKey($"Input_{key}_Primary"))
                {
                    action.primaryKey = (KeyCode)PlayerPrefs.GetInt($"Input_{key}_Primary");
                }
                if (PlayerPrefs.HasKey($"Input_{key}_Secondary"))
                {
                    action.secondaryKey = (KeyCode)PlayerPrefs.GetInt($"Input_{key}_Secondary");
                }
            }
        }

        public void ResetBindings()
        {
            foreach (var kvp in actionMap)
            {
                PlayerPrefs.DeleteKey($"Input_{kvp.Key}_Primary");
                PlayerPrefs.DeleteKey($"Input_{kvp.Key}_Secondary");
            }

            actionMap.Clear();
            InitializeActions();
        }

        public InputAction GetInputAction(string actionName)
        {
            return actionMap.TryGetValue(actionName, out var action) ? action : null;
        }

        public void RegisterAction(InputAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.actionName)) return;
            actionMap[action.actionName] = action;
        }
    }

    [Serializable]
    public class InputAction
    {
        public string actionName;
        public KeyCode primaryKey = KeyCode.None;
        public KeyCode secondaryKey = KeyCode.None;
        public int mouseButton = -1; // -1 = none, 0 = left, 1 = right, 2 = middle

        [Header("Events")]
        public UnityEvent onPressed;
        public UnityEvent onHeld;
        public UnityEvent onReleased;
    }
}
