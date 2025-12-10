using UnityEngine;
using UnityEngine.UI;

namespace UnityVault.UI
{
    /// <summary>
    /// Floating damage number display.
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float floatSpeed = 1f;
        [SerializeField] private float floatHeight = 50f;
        [SerializeField] private float lifetime = 1f;
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Scale")]
        [SerializeField] private float startScale = 0.5f;
        [SerializeField] private float maxScale = 1.2f;
        [SerializeField] private float endScale = 0.8f;
        [SerializeField] private AnimationCurve scaleCurve;

        [Header("Fade")]
        [SerializeField] private float fadeStartTime = 0.5f;

        // Components
        private Text textComponent;
        private CanvasGroup canvasGroup;

        // State
        private Vector3 startPosition;
        private float elapsedTime;
        private bool isInitialized;

        private void Awake()
        {
            textComponent = GetComponentInChildren<Text>();
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (scaleCurve == null || scaleCurve.length == 0)
            {
                scaleCurve = new AnimationCurve(
                    new Keyframe(0, startScale),
                    new Keyframe(0.2f, maxScale),
                    new Keyframe(1, endScale)
                );
            }
        }

        public void Initialize(float damage, bool isCritical = false, DamageType damageType = DamageType.Normal)
        {
            startPosition = transform.position;
            elapsedTime = 0f;
            isInitialized = true;

            // Set text
            string text = Mathf.RoundToInt(damage).ToString();
            if (isCritical)
            {
                text = $"CRIT!\n{text}";
            }

            if (textComponent != null)
            {
                textComponent.text = text;
                textComponent.color = GetColorForType(damageType, isCritical);
            }

            // Reset visual state
            canvasGroup.alpha = 1f;
            transform.localScale = Vector3.one * startScale;
        }

        private void Update()
        {
            if (!isInitialized) return;

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / lifetime;

            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // Movement
            float yOffset = movementCurve.Evaluate(t) * floatHeight;
            transform.position = startPosition + Vector3.up * yOffset;

            // Scale
            float scale = scaleCurve.Evaluate(t);
            transform.localScale = Vector3.one * scale;

            // Fade
            if (t > fadeStartTime)
            {
                float fadeT = (t - fadeStartTime) / (1f - fadeStartTime);
                canvasGroup.alpha = 1f - fadeT;
            }
        }

        private Color GetColorForType(DamageType type, bool isCritical)
        {
            if (isCritical) return new Color(1f, 0.8f, 0f); // Gold

            switch (type)
            {
                case DamageType.Normal: return Color.white;
                case DamageType.Fire: return new Color(1f, 0.4f, 0.2f);
                case DamageType.Ice: return new Color(0.4f, 0.8f, 1f);
                case DamageType.Lightning: return new Color(1f, 1f, 0.4f);
                case DamageType.Poison: return new Color(0.4f, 1f, 0.4f);
                case DamageType.Heal: return new Color(0.4f, 1f, 0.6f);
                default: return Color.white;
            }
        }
    }

    public enum DamageType
    {
        Normal,
        Fire,
        Ice,
        Lightning,
        Poison,
        Heal
    }

    /// <summary>
    /// Spawns damage numbers at world positions.
    /// </summary>
    public class DamageNumberSpawner : MonoBehaviour
    {
        public static DamageNumberSpawner Instance { get; private set; }

        [SerializeField] private DamageNumber prefab;
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private Vector2 randomOffset = new Vector2(20f, 10f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void SpawnDamageNumber(Vector3 worldPosition, float damage, bool isCritical = false, DamageType type = DamageType.Normal)
        {
            if (prefab == null || worldCanvas == null) return;

            // Convert world position to screen position
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);

            // Add random offset
            screenPos.x += Random.Range(-randomOffset.x, randomOffset.x);
            screenPos.y += Random.Range(-randomOffset.y, randomOffset.y);

            var instance = Instantiate(prefab, worldCanvas.transform);
            instance.transform.position = screenPos;
            instance.Initialize(damage, isCritical, type);
        }
    }
}
