using UnityEngine;
using System;
using System.Collections.Generic;

namespace UnityVault.Audio
{
    /// <summary>
    /// 3D spatial audio system with positioning and occlusion.
    /// </summary>
    public class Audio3D : MonoBehaviour
    {
        public static Audio3D Instance { get; private set; }

        [Header("Pool Settings")]
        [SerializeField] private int poolSize = 20;
        [SerializeField] private int maxConcurrentSounds = 15;

        [Header("3D Settings")]
        [SerializeField] private float defaultMinDistance = 1f;
        [SerializeField] private float defaultMaxDistance = 50f;
        [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

        [Header("Occlusion")]
        [SerializeField] private bool enableOcclusion = true;
        [SerializeField] private LayerMask occlusionLayers;
        [SerializeField] private float occlusionDampening = 0.5f;
        [SerializeField] private float occlusionLowPassFreq = 1000f;
        [SerializeField] private float occlusionCheckInterval = 0.1f;

        [Header("Distance Culling")]
        [SerializeField] private float cullDistance = 100f;
        [SerializeField] private bool enableCulling = true;

        [Header("Listener")]
        [SerializeField] private Transform listenerTransform;

        // Pool
        private List<Audio3DSource> sourcePool = new List<Audio3DSource>();
        private List<Audio3DSource> activeSources = new List<Audio3DSource>();
        private float lastOcclusionCheck;

        // Events
        public event Action<AudioClip, Vector3> SoundPlayed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            CreatePool();
            FindListener();
        }

        private void CreatePool()
        {
            for (int i = 0; i < poolSize; i++)
            {
                CreatePooledSource();
            }
        }

        private Audio3DSource CreatePooledSource()
        {
            GameObject sourceObj = new GameObject($"3DSource_{sourcePool.Count}");
            sourceObj.transform.SetParent(transform);

            AudioSource audioSource = sourceObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = rolloffMode;
            audioSource.minDistance = defaultMinDistance;
            audioSource.maxDistance = defaultMaxDistance;

            AudioLowPassFilter lowPass = null;
            if (enableOcclusion)
            {
                lowPass = sourceObj.AddComponent<AudioLowPassFilter>();
                lowPass.cutoffFrequency = 22000;
            }

            var source = new Audio3DSource
            {
                audioSource = audioSource,
                lowPassFilter = lowPass,
                gameObject = sourceObj
            };

            sourcePool.Add(source);
            sourceObj.SetActive(false);

            return source;
        }

        private void FindListener()
        {
            if (listenerTransform == null)
            {
                AudioListener listener = FindObjectOfType<AudioListener>();
                if (listener != null)
                {
                    listenerTransform = listener.transform;
                }
            }
        }

        private void Update()
        {
            UpdateActiveSources();

            if (enableOcclusion && Time.time - lastOcclusionCheck >= occlusionCheckInterval)
            {
                lastOcclusionCheck = Time.time;
                UpdateOcclusion();
            }
        }

        private void UpdateActiveSources()
        {
            for (int i = activeSources.Count - 1; i >= 0; i--)
            {
                var source = activeSources[i];

                if (!source.audioSource.isPlaying)
                {
                    ReturnToPool(source);
                    activeSources.RemoveAt(i);
                    continue;
                }

                // Update position for following sources
                if (source.followTarget != null)
                {
                    source.gameObject.transform.position = source.followTarget.position;
                }

                // Distance culling
                if (enableCulling && listenerTransform != null)
                {
                    float distance = Vector3.Distance(source.gameObject.transform.position, listenerTransform.position);
                    if (distance > cullDistance)
                    {
                        source.audioSource.Stop();
                    }
                }
            }
        }

        private void UpdateOcclusion()
        {
            if (listenerTransform == null) return;

            foreach (var source in activeSources)
            {
                if (source.lowPassFilter == null) continue;

                Vector3 direction = listenerTransform.position - source.gameObject.transform.position;
                float distance = direction.magnitude;

                bool occluded = Physics.Raycast(
                    source.gameObject.transform.position,
                    direction.normalized,
                    distance,
                    occlusionLayers
                );

                if (occluded)
                {
                    source.audioSource.volume = source.baseVolume * occlusionDampening;
                    source.lowPassFilter.cutoffFrequency = Mathf.Lerp(
                        source.lowPassFilter.cutoffFrequency,
                        occlusionLowPassFreq,
                        Time.deltaTime * 5f
                    );
                }
                else
                {
                    source.audioSource.volume = source.baseVolume;
                    source.lowPassFilter.cutoffFrequency = Mathf.Lerp(
                        source.lowPassFilter.cutoffFrequency,
                        22000f,
                        Time.deltaTime * 5f
                    );
                }
            }
        }

        /// <summary>
        /// Play a 3D sound at a position.
        /// </summary>
        public Audio3DHandle PlayAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
        {
            return PlayAtPosition(clip, position, volume, defaultMinDistance, defaultMaxDistance);
        }

        /// <summary>
        /// Play a 3D sound at a position with custom distances.
        /// </summary>
        public Audio3DHandle PlayAtPosition(AudioClip clip, Vector3 position, float volume, float minDist, float maxDist)
        {
            if (clip == null) return null;
            if (activeSources.Count >= maxConcurrentSounds) return null;

            // Distance check
            if (enableCulling && listenerTransform != null)
            {
                float distance = Vector3.Distance(position, listenerTransform.position);
                if (distance > cullDistance) return null;
            }

            Audio3DSource source = GetAvailableSource();
            if (source == null) return null;

            source.gameObject.SetActive(true);
            source.gameObject.transform.position = position;
            source.audioSource.clip = clip;
            source.audioSource.volume = volume;
            source.audioSource.minDistance = minDist;
            source.audioSource.maxDistance = maxDist;
            source.audioSource.loop = false;
            source.baseVolume = volume;
            source.followTarget = null;

            source.audioSource.Play();
            activeSources.Add(source);

            SoundPlayed?.Invoke(clip, position);

            return new Audio3DHandle(source);
        }

        /// <summary>
        /// Play a 3D sound attached to a transform.
        /// </summary>
        public Audio3DHandle PlayAttached(AudioClip clip, Transform target, float volume = 1f)
        {
            if (target == null) return null;

            var handle = PlayAtPosition(clip, target.position, volume);
            if (handle != null)
            {
                handle.source.followTarget = target;
            }

            return handle;
        }

        /// <summary>
        /// Play a looping 3D sound at a position.
        /// </summary>
        public Audio3DHandle PlayLoopAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
        {
            var handle = PlayAtPosition(clip, position, volume);
            if (handle != null)
            {
                handle.source.audioSource.loop = true;
            }

            return handle;
        }

        /// <summary>
        /// Play a looping 3D sound attached to a transform.
        /// </summary>
        public Audio3DHandle PlayLoopAttached(AudioClip clip, Transform target, float volume = 1f)
        {
            var handle = PlayAttached(clip, target, volume);
            if (handle != null)
            {
                handle.source.audioSource.loop = true;
            }

            return handle;
        }

        private Audio3DSource GetAvailableSource()
        {
            foreach (var source in sourcePool)
            {
                if (!activeSources.Contains(source))
                {
                    return source;
                }
            }

            // Create new if pool is exhausted
            if (sourcePool.Count < poolSize * 2)
            {
                return CreatePooledSource();
            }

            return null;
        }

        private void ReturnToPool(Audio3DSource source)
        {
            source.audioSource.Stop();
            source.audioSource.clip = null;
            source.followTarget = null;
            source.gameObject.SetActive(false);

            if (source.lowPassFilter != null)
            {
                source.lowPassFilter.cutoffFrequency = 22000;
            }
        }

        /// <summary>
        /// Stop all 3D sounds.
        /// </summary>
        public void StopAll()
        {
            foreach (var source in activeSources)
            {
                ReturnToPool(source);
            }
            activeSources.Clear();
        }

        /// <summary>
        /// Set the listener transform.
        /// </summary>
        public void SetListener(Transform listener)
        {
            listenerTransform = listener;
        }

        /// <summary>
        /// Get active sound count.
        /// </summary>
        public int GetActiveSoundCount() => activeSources.Count;
    }

    public class Audio3DSource
    {
        public AudioSource audioSource;
        public AudioLowPassFilter lowPassFilter;
        public GameObject gameObject;
        public Transform followTarget;
        public float baseVolume;
    }

    public class Audio3DHandle
    {
        internal Audio3DSource source;

        public Audio3DHandle(Audio3DSource source)
        {
            this.source = source;
        }

        public bool IsPlaying => source?.audioSource?.isPlaying ?? false;

        public void Stop()
        {
            source?.audioSource?.Stop();
        }

        public void SetVolume(float volume)
        {
            if (source != null)
            {
                source.baseVolume = volume;
                source.audioSource.volume = volume;
            }
        }

        public void SetPitch(float pitch)
        {
            if (source?.audioSource != null)
            {
                source.audioSource.pitch = pitch;
            }
        }

        public void SetPosition(Vector3 position)
        {
            if (source?.gameObject != null)
            {
                source.gameObject.transform.position = position;
            }
        }
    }
}
