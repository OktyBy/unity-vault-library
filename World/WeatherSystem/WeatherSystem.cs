using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.World
{
    /// <summary>
    /// Weather system with rain, snow, fog, and dynamic transitions.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        [Header("Weather Presets")]
        [SerializeField] private List<WeatherPreset> presets = new List<WeatherPreset>();
        [SerializeField] private WeatherPreset defaultWeather;

        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private ParticleSystem snowParticles;
        [SerializeField] private ParticleSystem fogParticles;

        [Header("Lighting")]
        [SerializeField] private Light sunLight;
        [SerializeField] private float minLightIntensity = 0.2f;
        [SerializeField] private float maxLightIntensity = 1f;

        [Header("Transition")]
        [SerializeField] private float transitionDuration = 10f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Randomization")]
        [SerializeField] private bool enableRandomWeather = false;
        [SerializeField] private float minWeatherDuration = 60f;
        [SerializeField] private float maxWeatherDuration = 300f;

        [Header("Events")]
        [SerializeField] private UnityEvent<WeatherType> onWeatherChanged;
        [SerializeField] private UnityEvent<WeatherType, WeatherType> onWeatherTransitionStart;

        // State
        private WeatherPreset currentWeather;
        private WeatherPreset targetWeather;
        private float transitionProgress;
        private bool isTransitioning;
        private float weatherTimer;

        // Events
        public event Action<WeatherType> WeatherChanged;
        public event Action<WeatherType, WeatherType> TransitionStarted;

        public WeatherType CurrentWeatherType => currentWeather?.weatherType ?? WeatherType.Clear;
        public bool IsTransitioning => isTransitioning;
        public float TransitionProgress => transitionProgress;

        public enum WeatherType
        {
            Clear,
            Cloudy,
            Overcast,
            Rain,
            HeavyRain,
            Thunderstorm,
            Snow,
            Blizzard,
            Fog,
            Sandstorm
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (defaultWeather != null)
            {
                SetWeatherImmediate(defaultWeather);
            }

            if (enableRandomWeather)
            {
                weatherTimer = UnityEngine.Random.Range(minWeatherDuration, maxWeatherDuration);
            }
        }

        private void Update()
        {
            if (isTransitioning)
            {
                UpdateTransition();
            }

            if (enableRandomWeather)
            {
                UpdateRandomWeather();
            }
        }

        private void UpdateTransition()
        {
            transitionProgress += Time.deltaTime / transitionDuration;

            if (transitionProgress >= 1f)
            {
                transitionProgress = 1f;
                isTransitioning = false;
                currentWeather = targetWeather;

                WeatherChanged?.Invoke(currentWeather.weatherType);
                onWeatherChanged?.Invoke(currentWeather.weatherType);
            }

            float t = transitionCurve.Evaluate(transitionProgress);
            ApplyWeatherLerp(currentWeather, targetWeather, t);
        }

        private void UpdateRandomWeather()
        {
            if (isTransitioning) return;

            weatherTimer -= Time.deltaTime;

            if (weatherTimer <= 0)
            {
                weatherTimer = UnityEngine.Random.Range(minWeatherDuration, maxWeatherDuration);

                // Pick random weather
                if (presets.Count > 0)
                {
                    WeatherPreset newWeather = presets[UnityEngine.Random.Range(0, presets.Count)];
                    SetWeather(newWeather.weatherType);
                }
            }
        }

        private void ApplyWeatherLerp(WeatherPreset from, WeatherPreset to, float t)
        {
            // Lighting
            if (sunLight != null)
            {
                sunLight.intensity = Mathf.Lerp(from.sunIntensity, to.sunIntensity, t);
                sunLight.color = Color.Lerp(from.sunColor, to.sunColor, t);
            }

            // Fog
            RenderSettings.fog = from.enableFog || to.enableFog;
            RenderSettings.fogColor = Color.Lerp(from.fogColor, to.fogColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(from.fogDensity, to.fogDensity, t);

            // Ambient
            RenderSettings.ambientLight = Color.Lerp(from.ambientColor, to.ambientColor, t);
            RenderSettings.ambientIntensity = Mathf.Lerp(from.ambientIntensity, to.ambientIntensity, t);

            // Particles
            UpdateParticleSystem(rainParticles, from.rainIntensity, to.rainIntensity, t);
            UpdateParticleSystem(snowParticles, from.snowIntensity, to.snowIntensity, t);
            UpdateParticleSystem(fogParticles, from.fogParticleIntensity, to.fogParticleIntensity, t);
        }

        private void UpdateParticleSystem(ParticleSystem ps, float fromRate, float toRate, float t)
        {
            if (ps == null) return;

            float rate = Mathf.Lerp(fromRate, toRate, t);

            var emission = ps.emission;
            emission.rateOverTime = rate;

            if (rate > 0 && !ps.isPlaying)
            {
                ps.Play();
            }
            else if (rate <= 0 && ps.isPlaying)
            {
                ps.Stop();
            }
        }

        /// <summary>
        /// Set weather with transition.
        /// </summary>
        public void SetWeather(WeatherType type)
        {
            WeatherPreset preset = GetPreset(type);
            if (preset != null)
            {
                SetWeather(preset);
            }
        }

        /// <summary>
        /// Set weather with custom preset and transition.
        /// </summary>
        public void SetWeather(WeatherPreset preset)
        {
            if (preset == null || preset == currentWeather) return;

            targetWeather = preset;
            isTransitioning = true;
            transitionProgress = 0;

            TransitionStarted?.Invoke(currentWeather?.weatherType ?? WeatherType.Clear, preset.weatherType);
            onWeatherTransitionStart?.Invoke(currentWeather?.weatherType ?? WeatherType.Clear, preset.weatherType);

            Debug.Log($"[Weather] Transitioning to: {preset.weatherType}");
        }

        /// <summary>
        /// Set weather immediately without transition.
        /// </summary>
        public void SetWeatherImmediate(WeatherType type)
        {
            WeatherPreset preset = GetPreset(type);
            if (preset != null)
            {
                SetWeatherImmediate(preset);
            }
        }

        /// <summary>
        /// Set weather immediately with custom preset.
        /// </summary>
        public void SetWeatherImmediate(WeatherPreset preset)
        {
            if (preset == null) return;

            currentWeather = preset;
            targetWeather = preset;
            isTransitioning = false;

            ApplyWeatherLerp(preset, preset, 1f);

            WeatherChanged?.Invoke(preset.weatherType);
            onWeatherChanged?.Invoke(preset.weatherType);

            Debug.Log($"[Weather] Set immediately to: {preset.weatherType}");
        }

        private WeatherPreset GetPreset(WeatherType type)
        {
            foreach (var preset in presets)
            {
                if (preset.weatherType == type)
                {
                    return preset;
                }
            }
            return defaultWeather;
        }

        /// <summary>
        /// Get current weather intensity (0-1).
        /// </summary>
        public float GetWeatherIntensity()
        {
            if (currentWeather == null) return 0;

            return currentWeather.weatherType switch
            {
                WeatherType.Rain => currentWeather.rainIntensity / 1000f,
                WeatherType.HeavyRain or WeatherType.Thunderstorm => 1f,
                WeatherType.Snow => currentWeather.snowIntensity / 500f,
                WeatherType.Blizzard => 1f,
                WeatherType.Fog => currentWeather.fogDensity * 10f,
                _ => 0f
            };
        }

        /// <summary>
        /// Check if it's raining.
        /// </summary>
        public bool IsRaining()
        {
            return currentWeather?.rainIntensity > 0;
        }

        /// <summary>
        /// Check if it's snowing.
        /// </summary>
        public bool IsSnowing()
        {
            return currentWeather?.snowIntensity > 0;
        }

        /// <summary>
        /// Set transition duration.
        /// </summary>
        public void SetTransitionDuration(float duration)
        {
            transitionDuration = Mathf.Max(0.1f, duration);
        }

        /// <summary>
        /// Enable/disable random weather.
        /// </summary>
        public void SetRandomWeather(bool enabled)
        {
            enableRandomWeather = enabled;
            if (enabled)
            {
                weatherTimer = UnityEngine.Random.Range(minWeatherDuration, maxWeatherDuration);
            }
        }
    }

    [CreateAssetMenu(fileName = "WeatherPreset", menuName = "UnityVault/World/Weather Preset")]
    public class WeatherPreset : ScriptableObject
    {
        public string presetName = "New Weather";
        public WeatherSystem.WeatherType weatherType;

        [Header("Lighting")]
        public float sunIntensity = 1f;
        public Color sunColor = Color.white;
        public Color ambientColor = Color.gray;
        public float ambientIntensity = 1f;

        [Header("Fog")]
        public bool enableFog = false;
        public Color fogColor = Color.gray;
        public float fogDensity = 0.01f;

        [Header("Precipitation")]
        public float rainIntensity = 0;
        public float snowIntensity = 0;
        public float fogParticleIntensity = 0;

        [Header("Wind")]
        public float windStrength = 0;
        public Vector3 windDirection = Vector3.right;

        [Header("Audio")]
        public AudioClip ambientLoop;
        public float audioVolume = 1f;
    }
}
