using UnityEngine;
using System.Collections;

namespace UnityVault.VFX
{
    /// <summary>
    /// Hit stop / frame freeze effect for impactful hits.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        [Header("Default Settings")]
        [SerializeField] private float defaultDuration = 0.1f;
        [SerializeField] private float defaultTimeScale = 0f;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Presets")]
        [SerializeField] private HitStopPreset lightHit;
        [SerializeField] private HitStopPreset mediumHit;
        [SerializeField] private HitStopPreset heavyHit;
        [SerializeField] private HitStopPreset criticalHit;

        private Coroutine hitStopCoroutine;
        private float originalTimeScale;

        public bool IsActive => hitStopCoroutine != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializePresets();
        }

        private void InitializePresets()
        {
            if (lightHit == null)
                lightHit = new HitStopPreset { duration = 0.03f, timeScale = 0.1f };
            if (mediumHit == null)
                mediumHit = new HitStopPreset { duration = 0.06f, timeScale = 0.05f };
            if (heavyHit == null)
                heavyHit = new HitStopPreset { duration = 0.1f, timeScale = 0f };
            if (criticalHit == null)
                criticalHit = new HitStopPreset { duration = 0.15f, timeScale = 0f };
        }

        public void Stop(float duration = -1f, float timeScale = -1f)
        {
            if (duration < 0) duration = defaultDuration;
            if (timeScale < 0) timeScale = defaultTimeScale;

            if (hitStopCoroutine != null)
            {
                StopCoroutine(hitStopCoroutine);
            }

            hitStopCoroutine = StartCoroutine(HitStopRoutine(duration, timeScale));
        }

        public void Stop(HitStopPreset preset)
        {
            if (preset == null) return;
            Stop(preset.duration, preset.timeScale);
        }

        public void StopLight() => Stop(lightHit);
        public void StopMedium() => Stop(mediumHit);
        public void StopHeavy() => Stop(heavyHit);
        public void StopCritical() => Stop(criticalHit);

        private IEnumerator HitStopRoutine(float duration, float targetTimeScale)
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = targetTimeScale;

            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(duration);
            }
            else
            {
                yield return new WaitForSeconds(duration);
            }

            Time.timeScale = originalTimeScale;
            hitStopCoroutine = null;
        }

        public void Cancel()
        {
            if (hitStopCoroutine != null)
            {
                StopCoroutine(hitStopCoroutine);
                Time.timeScale = originalTimeScale;
                hitStopCoroutine = null;
            }
        }

        private void OnDestroy()
        {
            // Ensure time scale is reset
            if (hitStopCoroutine != null)
            {
                Time.timeScale = 1f;
            }
        }
    }

    [System.Serializable]
    public class HitStopPreset
    {
        public string name;
        public float duration = 0.1f;
        [Range(0f, 1f)] public float timeScale = 0f;

        public HitStopPreset() { }

        public HitStopPreset(float duration, float timeScale)
        {
            this.duration = duration;
            this.timeScale = timeScale;
        }
    }
}
