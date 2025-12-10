using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace UnityVault.Audio
{
    /// <summary>
    /// Central audio management system.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string masterVolumeParam = "MasterVolume";
        [SerializeField] private string musicVolumeParam = "MusicVolume";
        [SerializeField] private string sfxVolumeParam = "SFXVolume";

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private int sfxPoolSize = 10;

        [Header("Settings")]
        [SerializeField] private float defaultMasterVolume = 1f;
        [SerializeField] private float defaultMusicVolume = 0.8f;
        [SerializeField] private float defaultSFXVolume = 1f;

        // Pools
        private List<AudioSource> sfxPool = new List<AudioSource>();
        private int currentPoolIndex;

        // Volume cache
        private float masterVolume;
        private float musicVolume;
        private float sfxVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializePool();
            LoadVolumeSettings();
        }

        private void InitializePool()
        {
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }

            for (int i = 0; i < sfxPoolSize; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                sfxPool.Add(source);
            }
        }

        private void LoadVolumeSettings()
        {
            masterVolume = PlayerPrefs.GetFloat("MasterVolume", defaultMasterVolume);
            musicVolume = PlayerPrefs.GetFloat("MusicVolume", defaultMusicVolume);
            sfxVolume = PlayerPrefs.GetFloat("SFXVolume", defaultSFXVolume);

            ApplyVolumeSettings();
        }

        private void ApplyVolumeSettings()
        {
            SetMixerVolume(masterVolumeParam, masterVolume);
            SetMixerVolume(musicVolumeParam, musicVolume);
            SetMixerVolume(sfxVolumeParam, sfxVolume);
        }

        private void SetMixerVolume(string param, float volume)
        {
            if (audioMixer != null)
            {
                float db = volume > 0.001f ? Mathf.Log10(volume) * 20f : -80f;
                audioMixer.SetFloat(param, db);
            }
        }

        #region Volume Control

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                SetMixerVolume(masterVolumeParam, masterVolume);
                PlayerPrefs.SetFloat("MasterVolume", masterVolume);
            }
        }

        public float MusicVolume
        {
            get => musicVolume;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                SetMixerVolume(musicVolumeParam, musicVolume);
                PlayerPrefs.SetFloat("MusicVolume", musicVolume);
            }
        }

        public float SFXVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                SetMixerVolume(sfxVolumeParam, sfxVolume);
                PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
            }
        }

        #endregion

        #region SFX Playback

        public void PlaySFX(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;

            var source = GetPooledSource();
            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f;
            source.spatialBlend = 0f;
            source.Play();
        }

        public void PlaySFX(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;

            var source = GetPooledSource();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 0f;
            source.Play();
        }

        public void PlaySFXAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, volume * sfxVolume);
        }

        public void PlaySFX3D(AudioClip clip, Vector3 position, float volume = 1f, float minDistance = 1f, float maxDistance = 50f)
        {
            if (clip == null) return;

            var source = GetPooledSource();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.transform.position = position;
            source.Play();
        }

        public void PlayRandomSFX(AudioClip[] clips, float volume = 1f)
        {
            if (clips == null || clips.Length == 0) return;
            PlaySFX(clips[Random.Range(0, clips.Length)], volume);
        }

        private AudioSource GetPooledSource()
        {
            currentPoolIndex = (currentPoolIndex + 1) % sfxPool.Count;
            return sfxPool[currentPoolIndex];
        }

        #endregion

        #region Music Playback

        public void PlayMusic(AudioClip clip, bool loop = true, float fadeTime = 1f)
        {
            if (musicSource == null) return;

            if (fadeTime > 0 && musicSource.isPlaying)
            {
                StartCoroutine(CrossfadeMusic(clip, loop, fadeTime));
            }
            else
            {
                musicSource.clip = clip;
                musicSource.loop = loop;
                musicSource.Play();
            }
        }

        public void StopMusic(float fadeTime = 1f)
        {
            if (musicSource == null) return;

            if (fadeTime > 0)
            {
                StartCoroutine(FadeOutMusic(fadeTime));
            }
            else
            {
                musicSource.Stop();
            }
        }

        public void PauseMusic()
        {
            musicSource?.Pause();
        }

        public void ResumeMusic()
        {
            musicSource?.UnPause();
        }

        private System.Collections.IEnumerator CrossfadeMusic(AudioClip newClip, bool loop, float fadeTime)
        {
            float startVolume = musicSource.volume;

            // Fade out
            while (musicSource.volume > 0)
            {
                musicSource.volume -= startVolume * Time.deltaTime / fadeTime;
                yield return null;
            }

            // Switch clip
            musicSource.clip = newClip;
            musicSource.loop = loop;
            musicSource.Play();

            // Fade in
            while (musicSource.volume < startVolume)
            {
                musicSource.volume += startVolume * Time.deltaTime / fadeTime;
                yield return null;
            }
        }

        private System.Collections.IEnumerator FadeOutMusic(float fadeTime)
        {
            float startVolume = musicSource.volume;

            while (musicSource.volume > 0)
            {
                musicSource.volume -= startVolume * Time.deltaTime / fadeTime;
                yield return null;
            }

            musicSource.Stop();
            musicSource.volume = startVolume;
        }

        #endregion

        public void StopAllSounds()
        {
            musicSource?.Stop();
            foreach (var source in sfxPool)
            {
                source.Stop();
            }
        }
    }
}
