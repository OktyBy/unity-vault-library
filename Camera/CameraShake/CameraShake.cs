using UnityEngine;
using System.Collections;

namespace UnityVault.Camera
{
    /// <summary>
    /// Camera shake system for impacts and effects.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("Default Settings")]
        [SerializeField] private float defaultDuration = 0.3f;
        [SerializeField] private float defaultMagnitude = 0.2f;
        [SerializeField] private float defaultRoughness = 3f;
        [SerializeField] private float decreaseFactor = 1f;

        [Header("Presets")]
        [SerializeField] private ShakePreset lightShake;
        [SerializeField] private ShakePreset mediumShake;
        [SerializeField] private ShakePreset heavyShake;
        [SerializeField] private ShakePreset explosionShake;

        // State
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Coroutine shakeCoroutine;
        private float currentMagnitude;

        public bool IsShaking => shakeCoroutine != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            originalPosition = transform.localPosition;
            originalRotation = transform.localRotation;

            InitializePresets();
        }

        private void InitializePresets()
        {
            if (lightShake == null)
            {
                lightShake = new ShakePreset { duration = 0.15f, magnitude = 0.1f, roughness = 3f };
            }
            if (mediumShake == null)
            {
                mediumShake = new ShakePreset { duration = 0.3f, magnitude = 0.2f, roughness = 4f };
            }
            if (heavyShake == null)
            {
                heavyShake = new ShakePreset { duration = 0.5f, magnitude = 0.4f, roughness = 5f };
            }
            if (explosionShake == null)
            {
                explosionShake = new ShakePreset { duration = 0.8f, magnitude = 0.6f, roughness = 6f, useRotation = true };
            }
        }

        public void Shake()
        {
            Shake(defaultDuration, defaultMagnitude, defaultRoughness);
        }

        public void Shake(float duration, float magnitude, float roughness = 3f)
        {
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
            }

            shakeCoroutine = StartCoroutine(ShakeRoutine(duration, magnitude, roughness, false));
        }

        public void Shake(ShakePreset preset)
        {
            if (preset == null) return;

            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
            }

            shakeCoroutine = StartCoroutine(ShakeRoutine(preset.duration, preset.magnitude, preset.roughness, preset.useRotation));
        }

        public void ShakeLight() => Shake(lightShake);
        public void ShakeMedium() => Shake(mediumShake);
        public void ShakeHeavy() => Shake(heavyShake);
        public void ShakeExplosion() => Shake(explosionShake);

        private IEnumerator ShakeRoutine(float duration, float magnitude, float roughness, bool useRotation)
        {
            float elapsed = 0f;
            currentMagnitude = magnitude;

            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * currentMagnitude;
                float y = Random.Range(-1f, 1f) * currentMagnitude;
                float z = 0f;

                // Apply Perlin noise for smoother shake
                float noiseX = (Mathf.PerlinNoise(Time.time * roughness, 0) - 0.5f) * 2f * currentMagnitude;
                float noiseY = (Mathf.PerlinNoise(0, Time.time * roughness) - 0.5f) * 2f * currentMagnitude;

                transform.localPosition = originalPosition + new Vector3(noiseX, noiseY, z);

                if (useRotation)
                {
                    float rotZ = Random.Range(-1f, 1f) * currentMagnitude * 5f;
                    transform.localRotation = originalRotation * Quaternion.Euler(0, 0, rotZ);
                }

                currentMagnitude = Mathf.Lerp(magnitude, 0f, elapsed / duration);

                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
            shakeCoroutine = null;
        }

        public void StopShake()
        {
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
                shakeCoroutine = null;
            }

            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
        }

        /// <summary>
        /// Shake based on distance from source.
        /// </summary>
        public void ShakeFromSource(Vector3 source, float maxDistance, ShakePreset preset)
        {
            float distance = Vector3.Distance(transform.position, source);
            if (distance > maxDistance) return;

            float falloff = 1f - (distance / maxDistance);
            float magnitude = preset.magnitude * falloff;

            Shake(preset.duration, magnitude, preset.roughness);
        }
    }

    [System.Serializable]
    public class ShakePreset
    {
        public string name;
        public float duration = 0.3f;
        public float magnitude = 0.2f;
        public float roughness = 3f;
        public bool useRotation = false;

        public ShakePreset() { }

        public ShakePreset(float duration, float magnitude, float roughness)
        {
            this.duration = duration;
            this.magnitude = magnitude;
            this.roughness = roughness;
        }
    }
}
