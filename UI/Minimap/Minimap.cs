using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace UnityVault.UI
{
    /// <summary>
    /// Minimap system with icons, rotation, and zoom.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private Transform player;
        [SerializeField] private RawImage minimapImage;
        [SerializeField] private Image playerIcon;
        [SerializeField] private RectTransform iconContainer;

        [Header("Settings")]
        [SerializeField] private float defaultZoom = 50f;
        [SerializeField] private float minZoom = 20f;
        [SerializeField] private float maxZoom = 100f;
        [SerializeField] private float zoomSpeed = 10f;
        [SerializeField] private float height = 100f;

        [Header("Rotation")]
        [SerializeField] private bool rotateWithPlayer = true;
        [SerializeField] private bool rotateMap = true; // false = rotate player icon instead

        [Header("Icon Settings")]
        [SerializeField] private float iconScale = 1f;
        [SerializeField] private bool showOffscreenIcons = true;
        [SerializeField] private float edgePadding = 10f;

        [Header("Render")]
        [SerializeField] private LayerMask minimapLayers;
        [SerializeField] private int renderTextureSize = 256;

        // Runtime
        private float currentZoom;
        private RenderTexture renderTexture;
        private Dictionary<Transform, MinimapIcon> trackedObjects = new Dictionary<Transform, MinimapIcon>();
        private List<Transform> toRemove = new List<Transform>();

        // Properties
        public float CurrentZoom => currentZoom;
        public float ZoomPercent => (currentZoom - minZoom) / (maxZoom - minZoom);

        private void Awake()
        {
            currentZoom = defaultZoom;
            SetupCamera();
        }

        private void Start()
        {
            if (player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    player = playerObj.transform;
                }
            }
        }

        private void SetupCamera()
        {
            if (minimapCamera == null)
            {
                // Create minimap camera
                GameObject camObj = new GameObject("MinimapCamera");
                minimapCamera = camObj.AddComponent<Camera>();
                minimapCamera.orthographic = true;
                minimapCamera.cullingMask = minimapLayers;
            }

            // Create render texture
            renderTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16);
            minimapCamera.targetTexture = renderTexture;

            if (minimapImage != null)
            {
                minimapImage.texture = renderTexture;
            }

            minimapCamera.orthographicSize = currentZoom;
        }

        private void LateUpdate()
        {
            if (player == null) return;

            UpdateCameraPosition();
            UpdateCameraRotation();
            UpdateIcons();
        }

        private void UpdateCameraPosition()
        {
            Vector3 newPos = player.position;
            newPos.y = player.position.y + height;
            minimapCamera.transform.position = newPos;
        }

        private void UpdateCameraRotation()
        {
            if (rotateWithPlayer && rotateMap)
            {
                // Rotate camera with player
                minimapCamera.transform.rotation = Quaternion.Euler(90f, player.eulerAngles.y, 0f);

                // Player icon stays pointing up
                if (playerIcon != null)
                {
                    playerIcon.transform.localRotation = Quaternion.identity;
                }
            }
            else
            {
                // Fixed north-up map
                minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                // Rotate player icon
                if (playerIcon != null && rotateWithPlayer)
                {
                    playerIcon.transform.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
                }
            }
        }

        private void UpdateIcons()
        {
            toRemove.Clear();

            foreach (var kvp in trackedObjects)
            {
                Transform target = kvp.Key;
                MinimapIcon icon = kvp.Value;

                if (target == null || icon.iconTransform == null)
                {
                    toRemove.Add(target);
                    continue;
                }

                UpdateIconPosition(target, icon);
            }

            // Remove destroyed objects
            foreach (var target in toRemove)
            {
                if (trackedObjects.TryGetValue(target, out MinimapIcon icon))
                {
                    if (icon.iconTransform != null)
                    {
                        Destroy(icon.iconTransform.gameObject);
                    }
                }
                trackedObjects.Remove(target);
            }
        }

        private void UpdateIconPosition(Transform target, MinimapIcon icon)
        {
            // Calculate position relative to player
            Vector3 offset = target.position - player.position;

            // Convert to minimap coordinates
            float halfSize = currentZoom;
            float x = offset.x / halfSize;
            float y = offset.z / halfSize;

            // Apply rotation if map doesn't rotate
            if (!rotateMap && rotateWithPlayer)
            {
                float angle = player.eulerAngles.y * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float newX = x * cos - y * sin;
                float newY = x * sin + y * cos;
                x = newX;
                y = newY;
            }

            // Check if in bounds
            float distance = Mathf.Sqrt(x * x + y * y);
            bool inBounds = distance <= 1f;

            if (!inBounds && showOffscreenIcons)
            {
                // Clamp to edge
                float angle = Mathf.Atan2(y, x);
                x = Mathf.Cos(angle);
                y = Mathf.Sin(angle);

                // Apply edge padding
                RectTransform rt = icon.iconTransform;
                float maxX = (minimapImage.rectTransform.rect.width / 2f) - edgePadding;
                float maxY = (minimapImage.rectTransform.rect.height / 2f) - edgePadding;
                x *= maxX / (minimapImage.rectTransform.rect.width / 2f);
                y *= maxY / (minimapImage.rectTransform.rect.height / 2f);
            }

            // Set position
            if (inBounds || showOffscreenIcons)
            {
                icon.iconTransform.gameObject.SetActive(true);

                Vector2 mapSize = minimapImage.rectTransform.rect.size;
                icon.iconTransform.anchoredPosition = new Vector2(x * mapSize.x / 2f, y * mapSize.y / 2f);

                // Rotate icon if needed
                if (icon.rotateWithTarget)
                {
                    float iconRotation = -target.eulerAngles.y;
                    if (!rotateMap && rotateWithPlayer)
                    {
                        iconRotation += player.eulerAngles.y;
                    }
                    icon.iconTransform.localRotation = Quaternion.Euler(0, 0, iconRotation);
                }
            }
            else
            {
                icon.iconTransform.gameObject.SetActive(false);
            }
        }

        // Public methods
        public void RegisterObject(Transform target, Sprite iconSprite, Color color, bool rotate = false)
        {
            if (trackedObjects.ContainsKey(target)) return;
            if (iconContainer == null) return;

            // Create icon
            GameObject iconObj = new GameObject($"Icon_{target.name}");
            iconObj.transform.SetParent(iconContainer, false);

            Image img = iconObj.AddComponent<Image>();
            img.sprite = iconSprite;
            img.color = color;

            RectTransform rt = iconObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(20, 20) * iconScale;

            MinimapIcon icon = new MinimapIcon
            {
                iconTransform = rt,
                image = img,
                rotateWithTarget = rotate
            };

            trackedObjects[target] = icon;
        }

        public void UnregisterObject(Transform target)
        {
            if (trackedObjects.TryGetValue(target, out MinimapIcon icon))
            {
                if (icon.iconTransform != null)
                {
                    Destroy(icon.iconTransform.gameObject);
                }
                trackedObjects.Remove(target);
            }
        }

        public void SetZoom(float zoom)
        {
            currentZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            if (minimapCamera != null)
            {
                minimapCamera.orthographicSize = currentZoom;
            }
        }

        public void ZoomIn()
        {
            SetZoom(currentZoom - zoomSpeed);
        }

        public void ZoomOut()
        {
            SetZoom(currentZoom + zoomSpeed);
        }

        public void SetPlayer(Transform playerTransform)
        {
            player = playerTransform;
        }

        public void SetRotateWithPlayer(bool rotate)
        {
            rotateWithPlayer = rotate;
        }

        public void UpdateIconColor(Transform target, Color color)
        {
            if (trackedObjects.TryGetValue(target, out MinimapIcon icon))
            {
                icon.image.color = color;
            }
        }

        public void UpdateIconSprite(Transform target, Sprite sprite)
        {
            if (trackedObjects.TryGetValue(target, out MinimapIcon icon))
            {
                icon.image.sprite = sprite;
            }
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
            }
        }

        private class MinimapIcon
        {
            public RectTransform iconTransform;
            public Image image;
            public bool rotateWithTarget;
        }
    }
}
