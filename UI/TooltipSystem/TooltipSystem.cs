using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

namespace UnityVault.UI
{
    /// <summary>
    /// Tooltip system for displaying hover information.
    /// </summary>
    public class TooltipSystem : MonoBehaviour
    {
        public static TooltipSystem Instance { get; private set; }

        [Header("Tooltip UI")]
        [SerializeField] private RectTransform tooltipPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Layout")]
        [SerializeField] private float padding = 10f;
        [SerializeField] private float maxWidth = 300f;
        [SerializeField] private Vector2 offset = new Vector2(15, -15);
        [SerializeField] private TooltipPivot pivotMode = TooltipPivot.Auto;

        [Header("Animation")]
        [SerializeField] private float showDelay = 0.5f;
        [SerializeField] private float fadeSpeed = 10f;
        [SerializeField] private bool useAnimation = true;

        [Header("Style")]
        [SerializeField] private TooltipStyle defaultStyle;
        [SerializeField] private TooltipStyle rarityCommon;
        [SerializeField] private TooltipStyle rarityUncommon;
        [SerializeField] private TooltipStyle rarityRare;
        [SerializeField] private TooltipStyle rarityEpic;
        [SerializeField] private TooltipStyle rarityLegendary;

        // State
        private float hoverTimer;
        private bool isShowing;
        private bool isHovering;
        private TooltipData currentData;
        private float targetAlpha;

        // Canvas reference
        private Canvas parentCanvas;
        private RectTransform canvasRect;

        public enum TooltipPivot
        {
            Auto,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
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

            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                canvasRect = parentCanvas.GetComponent<RectTransform>();
            }

            if (canvasGroup == null && tooltipPanel != null)
            {
                canvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = tooltipPanel.gameObject.AddComponent<CanvasGroup>();
                }
            }

            Hide();
        }

        private void Update()
        {
            if (isHovering)
            {
                hoverTimer += Time.unscaledDeltaTime;

                if (hoverTimer >= showDelay && !isShowing)
                {
                    ShowInternal();
                }
            }

            UpdatePosition();
            UpdateFade();
        }

        private void UpdatePosition()
        {
            if (tooltipPanel == null || !isShowing) return;

            Vector2 mousePos = Input.mousePosition;

            // Convert to canvas space
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, mousePos, parentCanvas?.worldCamera, out Vector2 localPoint);

            // Calculate pivot based on screen position
            Vector2 pivot = CalculatePivot(mousePos);
            tooltipPanel.pivot = pivot;

            // Apply offset based on pivot
            Vector2 finalOffset = offset;
            if (pivot.x > 0.5f) finalOffset.x = -offset.x;
            if (pivot.y < 0.5f) finalOffset.y = -offset.y;

            tooltipPanel.anchoredPosition = localPoint + finalOffset;
        }

        private Vector2 CalculatePivot(Vector2 screenPos)
        {
            if (pivotMode != TooltipPivot.Auto)
            {
                return pivotMode switch
                {
                    TooltipPivot.TopLeft => new Vector2(0, 1),
                    TooltipPivot.TopRight => new Vector2(1, 1),
                    TooltipPivot.BottomLeft => new Vector2(0, 0),
                    TooltipPivot.BottomRight => new Vector2(1, 0),
                    _ => new Vector2(0, 1)
                };
            }

            // Auto pivot - keep tooltip on screen
            float x = screenPos.x > Screen.width * 0.5f ? 1 : 0;
            float y = screenPos.y > Screen.height * 0.5f ? 1 : 0;

            return new Vector2(x, y);
        }

        private void UpdateFade()
        {
            if (canvasGroup == null) return;

            if (useAnimation)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
            }
            else
            {
                canvasGroup.alpha = targetAlpha;
            }
        }

        /// <summary>
        /// Show tooltip with simple text.
        /// </summary>
        public static void Show(string title, string description = "")
        {
            if (Instance == null) return;

            Instance.SetContent(new TooltipData
            {
                title = title,
                description = description
            });

            Instance.isHovering = true;
            Instance.hoverTimer = 0;
        }

        /// <summary>
        /// Show tooltip with full data.
        /// </summary>
        public static void Show(TooltipData data)
        {
            if (Instance == null) return;

            Instance.SetContent(data);
            Instance.isHovering = true;
            Instance.hoverTimer = 0;
        }

        /// <summary>
        /// Show tooltip immediately (no delay).
        /// </summary>
        public static void ShowImmediate(string title, string description = "")
        {
            Show(title, description);
            if (Instance != null)
            {
                Instance.hoverTimer = Instance.showDelay;
            }
        }

        /// <summary>
        /// Hide the tooltip.
        /// </summary>
        public static void Hide()
        {
            if (Instance == null) return;

            Instance.isHovering = false;
            Instance.hoverTimer = 0;
            Instance.isShowing = false;
            Instance.targetAlpha = 0;

            if (Instance.tooltipPanel != null)
            {
                Instance.tooltipPanel.gameObject.SetActive(false);
            }
        }

        private void SetContent(TooltipData data)
        {
            currentData = data;

            if (titleText != null)
            {
                titleText.text = data.title;
                titleText.gameObject.SetActive(!string.IsNullOrEmpty(data.title));
            }

            if (descriptionText != null)
            {
                descriptionText.text = data.description;
                descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(data.description));
            }

            if (iconImage != null)
            {
                iconImage.sprite = data.icon;
                iconImage.gameObject.SetActive(data.icon != null);
            }

            // Apply style
            TooltipStyle style = GetStyleForData(data);
            ApplyStyle(style);

            // Force layout rebuild
            if (tooltipPanel != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);
            }
        }

        private TooltipStyle GetStyleForData(TooltipData data)
        {
            if (data.customStyle != null)
            {
                return data.customStyle;
            }

            return data.rarity switch
            {
                ItemRarity.Common => rarityCommon ?? defaultStyle,
                ItemRarity.Uncommon => rarityUncommon ?? defaultStyle,
                ItemRarity.Rare => rarityRare ?? defaultStyle,
                ItemRarity.Epic => rarityEpic ?? defaultStyle,
                ItemRarity.Legendary => rarityLegendary ?? defaultStyle,
                _ => defaultStyle
            };
        }

        private void ApplyStyle(TooltipStyle style)
        {
            if (style == null) return;

            if (backgroundImage != null)
            {
                backgroundImage.color = style.backgroundColor;
            }

            if (titleText != null)
            {
                titleText.color = style.titleColor;
            }

            if (descriptionText != null)
            {
                descriptionText.color = style.descriptionColor;
            }
        }

        private void ShowInternal()
        {
            isShowing = true;
            targetAlpha = 1;

            if (tooltipPanel != null)
            {
                tooltipPanel.gameObject.SetActive(true);
            }

            UpdatePosition();
        }

        /// <summary>
        /// Set show delay.
        /// </summary>
        public static void SetDelay(float delay)
        {
            if (Instance != null)
            {
                Instance.showDelay = delay;
            }
        }
    }

    [Serializable]
    public class TooltipData
    {
        public string title;
        public string description;
        public Sprite icon;
        public ItemRarity rarity = ItemRarity.Common;
        public TooltipStyle customStyle;
    }

    [Serializable]
    public class TooltipStyle
    {
        public Color backgroundColor = new Color(0, 0, 0, 0.9f);
        public Color titleColor = Color.white;
        public Color descriptionColor = new Color(0.8f, 0.8f, 0.8f);
        public Color borderColor = Color.gray;
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// Attach to UI elements to show tooltips on hover.
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string title;
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private ItemRarity rarity;

        public void OnPointerEnter(PointerEventData eventData)
        {
            TooltipSystem.Show(new TooltipData
            {
                title = title,
                description = description,
                icon = icon,
                rarity = rarity
            });
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipSystem.Hide();
        }

        public void SetContent(string title, string description)
        {
            this.title = title;
            this.description = description;
        }

        public void SetRarity(ItemRarity rarity)
        {
            this.rarity = rarity;
        }
    }
}
