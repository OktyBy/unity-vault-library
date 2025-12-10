using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace UnityVault.Audio
{
    /// <summary>
    /// Music playback system with crossfade and playlist support.
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sourceA;
        [SerializeField] private AudioSource sourceB;

        [Header("Settings")]
        [SerializeField] private float defaultCrossfadeTime = 2f;
        [SerializeField] private float defaultVolume = 0.8f;
        [SerializeField] private bool playOnStart = false;
        [SerializeField] private MusicTrack startingTrack;

        [Header("Playlist")]
        [SerializeField] private List<MusicTrack> playlist = new List<MusicTrack>();
        [SerializeField] private PlaylistMode playlistMode = PlaylistMode.Sequential;
        [SerializeField] private bool autoPlay = true;

        // State
        private AudioSource activeSource;
        private MusicTrack currentTrack;
        private int currentPlaylistIndex = -1;
        private bool isTransitioning;
        private Coroutine transitionCoroutine;

        public bool IsPlaying => activeSource != null && activeSource.isPlaying;
        public MusicTrack CurrentTrack => currentTrack;
        public float Volume
        {
            get => defaultVolume;
            set
            {
                defaultVolume = Mathf.Clamp01(value);
                if (activeSource != null && !isTransitioning)
                {
                    activeSource.volume = defaultVolume;
                }
            }
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

            InitializeSources();
        }

        private void Start()
        {
            if (playOnStart && startingTrack != null)
            {
                Play(startingTrack);
            }
        }

        private void Update()
        {
            if (autoPlay && !isTransitioning && activeSource != null)
            {
                if (!activeSource.isPlaying && playlist.Count > 0)
                {
                    PlayNext();
                }
            }
        }

        private void InitializeSources()
        {
            if (sourceA == null)
            {
                sourceA = gameObject.AddComponent<AudioSource>();
            }
            if (sourceB == null)
            {
                sourceB = gameObject.AddComponent<AudioSource>();
            }

            ConfigureSource(sourceA);
            ConfigureSource(sourceB);

            activeSource = sourceA;
        }

        private void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 0f;
            source.spatialBlend = 0f;
        }

        public void Play(MusicTrack track, float fadeTime = -1f)
        {
            if (track == null || track.clip == null) return;

            if (fadeTime < 0) fadeTime = defaultCrossfadeTime;

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(TransitionTo(track, fadeTime));
        }

        public void Play(AudioClip clip, float fadeTime = -1f)
        {
            var track = new MusicTrack { clip = clip };
            Play(track, fadeTime);
        }

        public void PlayFromPlaylist(int index)
        {
            if (index < 0 || index >= playlist.Count) return;

            currentPlaylistIndex = index;
            Play(playlist[index]);
        }

        public void PlayNext()
        {
            if (playlist.Count == 0) return;

            switch (playlistMode)
            {
                case PlaylistMode.Sequential:
                    currentPlaylistIndex = (currentPlaylistIndex + 1) % playlist.Count;
                    break;

                case PlaylistMode.Shuffle:
                    int nextIndex;
                    do
                    {
                        nextIndex = Random.Range(0, playlist.Count);
                    } while (nextIndex == currentPlaylistIndex && playlist.Count > 1);
                    currentPlaylistIndex = nextIndex;
                    break;

                case PlaylistMode.Loop:
                    // Keep same track
                    break;
            }

            Play(playlist[currentPlaylistIndex]);
        }

        public void PlayPrevious()
        {
            if (playlist.Count == 0) return;

            currentPlaylistIndex--;
            if (currentPlaylistIndex < 0)
            {
                currentPlaylistIndex = playlist.Count - 1;
            }

            Play(playlist[currentPlaylistIndex]);
        }

        public void Stop(float fadeTime = -1f)
        {
            if (fadeTime < 0) fadeTime = defaultCrossfadeTime;

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(FadeOut(fadeTime));
        }

        public void Pause()
        {
            activeSource?.Pause();
        }

        public void Resume()
        {
            activeSource?.UnPause();
        }

        private IEnumerator TransitionTo(MusicTrack track, float fadeTime)
        {
            isTransitioning = true;

            AudioSource newSource = (activeSource == sourceA) ? sourceB : sourceA;
            AudioSource oldSource = activeSource;

            // Setup new source
            newSource.clip = track.clip;
            newSource.volume = 0f;
            newSource.loop = track.loop;
            newSource.Play();

            float targetVolume = track.volume * defaultVolume;

            // Crossfade
            float elapsed = 0f;
            float oldStartVolume = oldSource.volume;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeTime;

                newSource.volume = Mathf.Lerp(0f, targetVolume, t);
                oldSource.volume = Mathf.Lerp(oldStartVolume, 0f, t);

                yield return null;
            }

            // Cleanup
            oldSource.Stop();
            oldSource.volume = 0f;

            newSource.volume = targetVolume;
            activeSource = newSource;
            currentTrack = track;
            isTransitioning = false;
        }

        private IEnumerator FadeOut(float fadeTime)
        {
            isTransitioning = true;
            float startVolume = activeSource.volume;
            float elapsed = 0f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                activeSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeTime);
                yield return null;
            }

            activeSource.Stop();
            activeSource.volume = 0f;
            currentTrack = null;
            isTransitioning = false;
        }

        public void AddToPlaylist(MusicTrack track)
        {
            playlist.Add(track);
        }

        public void ClearPlaylist()
        {
            playlist.Clear();
            currentPlaylistIndex = -1;
        }
    }

    [System.Serializable]
    public class MusicTrack
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public bool loop = false;
        public string[] tags;
    }

    public enum PlaylistMode
    {
        Sequential,
        Shuffle,
        Loop
    }
}
