using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System;

namespace UnityVault.Input
{
    /// <summary>
    /// Mobile touch joystick control.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;

        [Header("Settings")]
        [SerializeField] private float handleRange = 1f;
        [SerializeField] private float deadZone = 0.1f;
        [SerializeField] private JoystickType joystickType = JoystickType.Fixed;

        [Header("Events")]
        [SerializeField] private UnityEvent<Vector2> onValueChanged;

        private Vector2 input = Vector2.zero;
        private Canvas canvas;
        private Camera renderCamera;

        public Vector2 Input => input;
        public float Horizontal => input.x;
        public float Vertical => input.y;
        public bool IsPressed { get; private set; }

        public event Action<Vector2> ValueChanged;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                renderCamera = canvas.worldCamera;
            }

            Vector2 center = new Vector2(0.5f, 0.5f);
            background.pivot = center;
            handle.anchorMin = center;
            handle.anchorMax = center;
            handle.pivot = center;
            handle.anchoredPosition = Vector2.zero;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;

            if (joystickType == JoystickType.Floating)
            {
                background.anchoredPosition = ScreenPointToAnchoredPosition(eventData.position);
            }

            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background,
                eventData.position,
                renderCamera,
                out position
            );

            position = position / (background.sizeDelta / 2f);

            input = position.magnitude > handleRange ?
                position.normalized * handleRange :
                position;

            if (input.magnitude < deadZone)
            {
                input = Vector2.zero;
            }

            handle.anchoredPosition = input * (background.sizeDelta / 2f) * handleRange;

            ValueChanged?.Invoke(input);
            onValueChanged?.Invoke(input);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsPressed = false;
            input = Vector2.zero;
            handle.anchoredPosition = Vector2.zero;

            ValueChanged?.Invoke(input);
            onValueChanged?.Invoke(input);
        }

        private Vector2 ScreenPointToAnchoredPosition(Vector2 screenPosition)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                screenPosition,
                renderCamera,
                out Vector2 localPoint))
            {
                return localPoint;
            }
            return Vector2.zero;
        }
    }

    public enum JoystickType
    {
        Fixed,
        Floating,
        Dynamic
    }

    /// <summary>
    /// Virtual button for touch input.
    /// </summary>
    public class VirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Settings")]
        [SerializeField] private string buttonName = "Fire";
        [SerializeField] private bool holdable = true;

        [Header("Visual")]
        [SerializeField] private UnityEngine.UI.Image buttonImage;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color pressedColor = new Color(0.8f, 0.8f, 0.8f);

        [Header("Events")]
        [SerializeField] private UnityEvent onPressed;
        [SerializeField] private UnityEvent onReleased;
        [SerializeField] private UnityEvent onHeld;

        public bool IsPressed { get; private set; }
        public string ButtonName => buttonName;

        public event Action Pressed;
        public event Action Released;
        public event Action Held;

        private void Update()
        {
            if (IsPressed && holdable)
            {
                Held?.Invoke();
                onHeld?.Invoke();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;

            if (buttonImage != null)
            {
                buttonImage.color = pressedColor;
            }

            Pressed?.Invoke();
            onPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsPressed = false;

            if (buttonImage != null)
            {
                buttonImage.color = normalColor;
            }

            Released?.Invoke();
            onReleased?.Invoke();
        }
    }

    /// <summary>
    /// Touch input manager for mobile games.
    /// </summary>
    public class TouchInputManager : MonoBehaviour
    {
        public static TouchInputManager Instance { get; private set; }

        [Header("Joysticks")]
        [SerializeField] private VirtualJoystick moveJoystick;
        [SerializeField] private VirtualJoystick lookJoystick;

        [Header("Buttons")]
        [SerializeField] private VirtualButton[] virtualButtons;

        public Vector2 MoveInput => moveJoystick != null ? moveJoystick.Input : Vector2.zero;
        public Vector2 LookInput => lookJoystick != null ? lookJoystick.Input : Vector2.zero;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool GetButton(string buttonName)
        {
            foreach (var button in virtualButtons)
            {
                if (button.ButtonName == buttonName)
                {
                    return button.IsPressed;
                }
            }
            return false;
        }

        public VirtualButton GetVirtualButton(string buttonName)
        {
            foreach (var button in virtualButtons)
            {
                if (button.ButtonName == buttonName)
                {
                    return button;
                }
            }
            return null;
        }

        public void ShowControls()
        {
            gameObject.SetActive(true);
        }

        public void HideControls()
        {
            gameObject.SetActive(false);
        }
    }
}
