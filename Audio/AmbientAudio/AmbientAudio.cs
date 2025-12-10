using UnityEngine;
using System;
using System.Collections.Generic;

namespace UnityVault.Audio
{
    /// <summary>
    /// Ambient audio system with zones and environmental sounds.
    /// </summary>
    public class AmbientAudio : MonoBehaviour
    {
        public static AmbientAudio Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource primarySource;
        [SerializeField] private AudioSource secondarySource;
        [SerializeField] private int poolSize = 5;

        [Header("Crossfade")]
        [SerializeField] private float crossfadeDuration = 2f;
        [SerializeField] private AnimationCurve crossfadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Global Settings")]
        [SerializeField] private float masterVolume = 1f;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private AmbientPreset defaultPreset;

        // State
        private AmbientPreset currentPreset;
        private List<AudioSource> soundPool = new List<AudioSource>();
        private List<LoopingSound> activeLoops = new List<LoopingSound>();
        private bool isCrossfading;
        private float crossfadeProgress;

        // Events
        public event Action<AmbientPreset> PresetChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SetupAudioSources();
            CreateSoundPool();
        }

        private void Start()
        {
            if (playOnStart && defaultPreset != null)
            {
                SetPreset(defaultPreset);
            }
        }

        private void SetupAudioSources()
        {
            if (primarySource == null)
            {
                primarySource = gameObject.AddComponent<AudioSource>();
                primarySource.loop = true;
                primarySource.playOnAwake = false;
            }

            if (secondarySource == null)
            {
                secondarySource = gameObject.AddComponent<AudioSource>();
                secondarySource.loop = true;
                secondarySource.playOnAwake = false;
            }
        }

        private void CreateSoundPool()
        {
            for (int i = 0; i < poolSize; i++)
            {
                GameObject poolObj = new GameObject($"AmbientPool_{i}");
                poolObj.transform.SetParent(transform);

                AudioSource source = poolObj.AddComponent<AudioSource>();
                source.playOnAwake = false;

                soundPool.Add(source);
            }
        }

        private void Update()
        {
            if (isCrossfading)
            {
                UpdateCrossfade();
            }

            UpdateRandomSounds();
        }

        private void UpdateCrossfade()
        {
            crossfadeProgress += Time.deltaTime / crossfadeDuration;

            float t = crossfadeCurve.Evaluate(crossfadeProgress);

            primarySource.volume = t * masterVolume;
            secondarySource.volume = (1f - t) * masterVolume;

            if (crossfadeProgress >= 1f)
            {
                isCrossfading = false;
                secondarySource.Stop();
            }
        }

        private void UpdateRandomSounds()
        {
            if (currentPreset == null) return;

            foreach (var sound in currentPreset.randomSounds)
            {
                sound.timer -= Time.deltaTime;

                if (sound.timer <= 0)
                {
                    PlayRandomSound(sound);
                    sound.timer = UnityEngine.Random.Range(sound.minInterval, sound.maxInterval);
                }
            }
        }

        private void PlayRandomSound(RandomAmbientSound sound)
        {
            if (sound.clips == null || sound.clips.Length == 0) return;

            AudioSource source = GetAvailableSource();
            if (source == null) return;

            AudioClip clip = sound.clips[UnityEngine.Random.Range(0, sound.clips.Length)];

            source.clip = clip;
            source.volume = UnityEngine.Random.Range(sound.minVolume, sound.maxVolume) * masterVolume;
            source.pitch = UnityEngine.Random.Range(sound.minPitch, sound.maxPitch);
            source.spatialBlend = 0; // 2D

            source.Play();
        }

        private AudioSource GetAvailableSource()
        {
            foreach (var source in soundPool)
            {
                if (!source.isPlaying)
                {
                    return source;
                }
            }
            return null;
        }

        /// <summary>
        /// Set ambient preset with crossfade.
        /// </summary>
        public void SetPreset(AmbientPreset preset, bool immediate = false)
        {
            if (preset == null) return;

            AmbientPreset oldPreset = currentPreset;
            currentPreset = preset;

            // Stop active loops
            StopAllLoops();

            if (immediate || oldPreset == null)
            {
                // Immediate switch
                primarySource.clip = preset.mainLoop;
                primarySource.volume = masterVolume;
                primarySource.Play();
            }
            else
            {
                // Crossfade
                AudioSource temp = primarySource;
                primarySource = secondarySource;
                secondarySource = temp;

                primarySource.clip = preset.mainLoop;
                primarySource.volume = 0;
                primarySource.Play();

                isCrossfading = true;
                crossfadeProgress = 0;
            }

            // Start additional loops
            foreach (var loop in preset.additionalLoops)
            {
                StartLoop(loop);
            }

            // Initialize random sound timers
            foreach (var sound in preset.randomSounds)
            {
                sound.timer = UnityEngine.Random.Range(0, sound.minInterval);
            }

            PresetChanged?.Invoke(preset);
            Debug.Log($"[Ambient] Preset changed to: {preset.presetName}");
        }

        private void StartLoop(AmbientLoop loop)
        {
            AudioSource source = GetAvailableSource();
            if (source == null) return;

            source.clip = loop.clip;
            source.volume = loop.volume * masterVolume;
            source.loop = true;
            source.Play();

            activeLoops.Add(new LoopingSound
            {
                source = source,
                loop = loop
            });
        }

        private void StopAllLoops()
        {
            foreach (var looping in activeLoops)
            {
                if (looping.source != null)
                {
                    looping.source.Stop();
                }
            }
            activeLoops.Clear();
        }

        /// <summary>
        /// Play a one-shot ambient sound.
        /// </summary>
        public void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            AudioSource source = GetAvailableSource();
            if (source == null || clip == null) return;

            source.PlayOneShot(clip, volume * masterVolume);
        }

        /// <summary>
        /// Set master volume.
        /// </summary>
        public void SetVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);

            if (primarySource != null && !isCrossfading)
            {
                primarySource.volume = masterVolume;
            }

            foreach (var loop in activeLoops)
            {
                loop.source.volume = loop.loop.volume * masterVolume;
            }
        }

        /// <summary>
        /// Pause all ambient audio.
        /// </summary>
        public void Pause()
        {
            primarySource?.Pause();
            secondarySource?.Pause();

            foreach (var loop in activeLoops)
            {
                loop.source?.Pause();
            }
        }

        /// <summary>
        /// Resume all ambient audio.
        /// </summary>
        public void Resume()
        {
            primarySource?.UnPause();
            secondarySource?.UnPause();

            foreach (var loop in activeLoops)
            {
                loop.source?.UnPause();
            }
        }

        /// <summary>
        /// Stop all ambient audio.
        /// </summary>
        public void StopAll()
        {
            primarySource?.Stop();
            secondarySource?.Stop();
            StopAllLoops();

            foreach (var source in soundPool)
            {
                source.Stop();
            }
        }

        private class LoopingSound
        {
            public AudioSource source;
            public AmbientLoop loop;
        }
    }

    [CreateAssetMenu(fileName = "AmbientPreset", menuName = "UnityVault/Audio/Ambient Preset")]
    public class AmbientPreset : ScriptableObject
    {
        public string presetName = "New Ambient";
        public AudioClip mainLoop;
        [Range(0, 1)] public float mainVolume = 1f;

        public List<AmbientLoop> additionalLoops = new List<AmbientLoop>();
        public List<RandomAmbientSound> randomSounds = new List<RandomAmbientSound>();
    }

    [Serializable]
    public class AmbientLoop
    {
        public AudioClip clip;
        [Range(0, 1)] public float volume = 0.5f;
    }

    [Serializable]
    public class RandomAmbientSound
    {
        public AudioClip[] clips;
        public float minInterval = 5f;
        public float maxInterval = 15f;
        [Range(0, 1)] public float minVolume = 0.3f;
        [Range(0, 1)] public float maxVolume = 0.7f;
        [Range(0.5f, 2f)] public float minPitch = 0.9f;
        [Range(0.5f, 2f)] public float maxPitch = 1.1f;

        [NonSerialized] public float timer;
    }
}
