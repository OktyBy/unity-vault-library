using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace UnityVault.VFX
{
    /// <summary>
    /// Screen flash effect for damage feedback and impacts.
    /// </summary>
    public class ScreenFlash : MonoBehaviour
    {
        public static ScreenFlash Instance { get; private set; }

        [Header("UI Reference")]
        [SerializeField] private Image flashImage;
        [SerializeField] private Canvas flashCanvas;

        [Header("Default Settings")]
        [SerializeField] private Color defaultColor = new Color(1f, 0f, 0f, 0.5f);
        [SerializeField] private float defaultDuration = 0.2f;
        [SerializeField] private AnimationCurve defaultCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Presets")]
        [SerializeField] private FlashPreset damageFlash;
        [SerializeField] private FlashPreset healFlash;
        [SerializeField] private FlashPreset criticalFlash;
        [SerializeField] private FlashPreset deathFlash;
        [SerializeField] private FlashPreset levelUpFlash;

        private Coroutine flashCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SetupCanvas();
            InitializePresets();
        }

        private void SetupCanvas()
        {
            if (flashCanvas == null)
            {
                var canvasObj = new GameObject("FlashCanvas");
                canvasObj.transform.SetParent(transform);
                flashCanvas = canvasObj.AddComponent<Canvas>();
                flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                flashCanvas.sortingOrder = 999;
            }

            if (flashImage == null)
            {
                var imageObj = new GameObject("FlashImage");
                imageObj.transform.SetParent(flashCanvas.transform);
                flashImage = imageObj.AddComponent<Image>();
                flashImage.raycastTarget = false;

                var rect = flashImage.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            flashImage.color = Color.clear;
        }

        private void InitializePresets()
        {
            if (damageFlash == null)
                damageFlash = new FlashPreset { color = new Color(1f, 0f, 0f, 0.4f), duration = 0.15f };
            if (healFlash == null)
                healFlash = new FlashPreset { color = new Color(0f, 1f, 0.3f, 0.3f), duration = 0.3f };
            if (criticalFlash == null)
                criticalFlash = new FlashPreset { color = new Color(1f, 0.8f, 0f, 0.5f), duration = 0.2f };
            if (deathFlash == null)
                deathFlash = new FlashPreset { color = new Color(0f, 0f, 0f, 0.8f), duration = 0.5f };
            if (levelUpFlash == null)
                levelUpFlash = new FlashPreset { color = new Color(1f, 1f, 0.5f, 0.4f), duration = 0.4f };
        }

        public void Flash()
        {
            Flash(defaultColor, defaultDuration);
        }

        public void Flash(Color color, float duration = -1f)
        {
            if (duration < 0) duration = defaultDuration;

            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }

            flashCoroutine = StartCoroutine(FlashRoutine(color, duration, defaultCurve));
        }

        public void Flash(FlashPreset preset)
        {
            if (preset == null) return;

            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }

            var curve = preset.customCurve ?? defaultCurve;
            flashCoroutine = StartCoroutine(FlashRoutine(preset.color, preset.duration, curve));
        }

        public void FlashDamage() => Flash(damageFlash);
        public void FlashHeal() => Flash(healFlash);
        public void FlashCritical() => Flash(criticalFlash);
        public void FlashDeath() => Flash(deathFlash);
        public void FlashLevelUp() => Flash(levelUpFlash);

        private IEnumerator FlashRoutine(Color color, float duration, AnimationCurve curve)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float alpha = curve.Evaluate(t) * color.a;
                flashImage.color = new Color(color.r, color.g, color.b, alpha);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            flashImage.color = Color.clear;
            flashCoroutine = null;
        }

        public void SetSolidColor(Color color)
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }

            flashImage.color = color;
        }

        public void Clear()
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }

            flashImage.color = Color.clear;
        }

        public void FadeIn(Color color, float duration)
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }

            flashCoroutine = StartCoroutine(FadeRoutine(Color.clear, color, duration));
        }

        public void FadeOut(float duration)
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }

            flashCoroutine = StartCoroutine(FadeRoutine(flashImage.color, Color.clear, duration));
        }

        private IEnumerator FadeRoutine(Color from, Color to, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                flashImage.color = Color.Lerp(from, to, t);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            flashImage.color = to;
            flashCoroutine = null;
        }
    }

    [System.Serializable]
    public class FlashPreset
    {
        public string name;
        public Color color = Color.red;
        public float duration = 0.2f;
        public AnimationCurve customCurve;

        public FlashPreset() { }

        public FlashPreset(Color color, float duration)
        {
            this.color = color;
            this.duration = duration;
        }
    }
}
