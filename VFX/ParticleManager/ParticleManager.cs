using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.VFX
{
    /// <summary>
    /// Particle effect manager with object pooling.
    /// </summary>
    public class ParticleManager : MonoBehaviour
    {
        public static ParticleManager Instance { get; private set; }

        [Header("Pool Settings")]
        [SerializeField] private int defaultPoolSize = 10;
        [SerializeField] private bool autoExpand = true;
        [SerializeField] private int maxPoolSize = 50;

        [Header("Particle Presets")]
        [SerializeField] private List<ParticlePreset> presets = new List<ParticlePreset>();

        [Header("Events")]
        [SerializeField] private UnityEvent<string> onEffectSpawned;

        // Pools
        private Dictionary<string, Queue<PooledParticle>> pools = new Dictionary<string, Queue<PooledParticle>>();
        private Dictionary<string, ParticlePreset> presetMap = new Dictionary<string, ParticlePreset>();
        private List<PooledParticle> activeParticles = new List<PooledParticle>();

        // Events
        public event Action<string, Vector3> EffectSpawned;
        public event Action<string> EffectReturned;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializePools();
        }

        private void InitializePools()
        {
            foreach (var preset in presets)
            {
                if (preset.prefab == null) continue;

                presetMap[preset.effectName] = preset;
                pools[preset.effectName] = new Queue<PooledParticle>();

                int poolSize = preset.poolSize > 0 ? preset.poolSize : defaultPoolSize;

                for (int i = 0; i < poolSize; i++)
                {
                    CreatePooledParticle(preset);
                }
            }
        }

        private PooledParticle CreatePooledParticle(ParticlePreset preset)
        {
            GameObject obj = Instantiate(preset.prefab, transform);
            obj.SetActive(false);

            PooledParticle pooled = obj.GetComponent<PooledParticle>();
            if (pooled == null)
            {
                pooled = obj.AddComponent<PooledParticle>();
            }

            pooled.Initialize(preset.effectName, this);
            pools[preset.effectName].Enqueue(pooled);

            return pooled;
        }

        /// <summary>
        /// Spawn a particle effect at position.
        /// </summary>
        public PooledParticle Spawn(string effectName, Vector3 position)
        {
            return Spawn(effectName, position, Quaternion.identity);
        }

        /// <summary>
        /// Spawn a particle effect at position with rotation.
        /// </summary>
        public PooledParticle Spawn(string effectName, Vector3 position, Quaternion rotation)
        {
            if (!pools.TryGetValue(effectName, out Queue<PooledParticle> pool))
            {
                Debug.LogWarning($"[ParticleManager] Effect not found: {effectName}");
                return null;
            }

            PooledParticle particle = null;

            if (pool.Count > 0)
            {
                particle = pool.Dequeue();
            }
            else if (autoExpand && presetMap.TryGetValue(effectName, out ParticlePreset preset))
            {
                int currentCount = GetTotalCount(effectName);
                if (currentCount < maxPoolSize)
                {
                    particle = CreatePooledParticle(preset);
                    pool.Dequeue(); // Remove from pool immediately
                }
            }

            if (particle == null)
            {
                Debug.LogWarning($"[ParticleManager] Pool exhausted for: {effectName}");
                return null;
            }

            particle.transform.position = position;
            particle.transform.rotation = rotation;
            particle.gameObject.SetActive(true);
            particle.Play();

            activeParticles.Add(particle);

            EffectSpawned?.Invoke(effectName, position);
            onEffectSpawned?.Invoke(effectName);

            return particle;
        }

        /// <summary>
        /// Spawn effect attached to transform.
        /// </summary>
        public PooledParticle SpawnAttached(string effectName, Transform parent, Vector3 localOffset = default)
        {
            PooledParticle particle = Spawn(effectName, parent.position + localOffset, parent.rotation);
            if (particle != null)
            {
                particle.AttachTo(parent, localOffset);
            }
            return particle;
        }

        /// <summary>
        /// Return particle to pool.
        /// </summary>
        public void ReturnToPool(PooledParticle particle)
        {
            if (particle == null) return;

            particle.Stop();
            particle.Detach();
            particle.gameObject.SetActive(false);

            if (pools.TryGetValue(particle.EffectName, out Queue<PooledParticle> pool))
            {
                pool.Enqueue(particle);
            }

            activeParticles.Remove(particle);
            EffectReturned?.Invoke(particle.EffectName);
        }

        /// <summary>
        /// Stop all active effects.
        /// </summary>
        public void StopAll()
        {
            foreach (var particle in new List<PooledParticle>(activeParticles))
            {
                ReturnToPool(particle);
            }
        }

        /// <summary>
        /// Stop all effects of specific type.
        /// </summary>
        public void StopAll(string effectName)
        {
            foreach (var particle in new List<PooledParticle>(activeParticles))
            {
                if (particle.EffectName == effectName)
                {
                    ReturnToPool(particle);
                }
            }
        }

        /// <summary>
        /// Register a new particle preset at runtime.
        /// </summary>
        public void RegisterPreset(ParticlePreset preset)
        {
            if (preset == null || preset.prefab == null) return;
            if (presetMap.ContainsKey(preset.effectName)) return;

            presetMap[preset.effectName] = preset;
            pools[preset.effectName] = new Queue<PooledParticle>();

            int poolSize = preset.poolSize > 0 ? preset.poolSize : defaultPoolSize;
            for (int i = 0; i < poolSize; i++)
            {
                CreatePooledParticle(preset);
            }

            Debug.Log($"[ParticleManager] Registered: {preset.effectName}");
        }

        /// <summary>
        /// Get active particle count.
        /// </summary>
        public int GetActiveCount() => activeParticles.Count;

        /// <summary>
        /// Get active count for specific effect.
        /// </summary>
        public int GetActiveCount(string effectName)
        {
            int count = 0;
            foreach (var p in activeParticles)
            {
                if (p.EffectName == effectName) count++;
            }
            return count;
        }

        /// <summary>
        /// Get total count (active + pooled) for effect.
        /// </summary>
        public int GetTotalCount(string effectName)
        {
            int pooled = pools.TryGetValue(effectName, out var pool) ? pool.Count : 0;
            return pooled + GetActiveCount(effectName);
        }

        /// <summary>
        /// Check if effect exists.
        /// </summary>
        public bool HasEffect(string effectName)
        {
            return presetMap.ContainsKey(effectName);
        }

        /// <summary>
        /// Get all registered effect names.
        /// </summary>
        public List<string> GetEffectNames()
        {
            return new List<string>(presetMap.Keys);
        }

        private void Update()
        {
            // Auto-return finished particles
            for (int i = activeParticles.Count - 1; i >= 0; i--)
            {
                if (activeParticles[i].IsFinished)
                {
                    ReturnToPool(activeParticles[i]);
                }
            }
        }
    }

    /// <summary>
    /// Pooled particle component.
    /// </summary>
    public class PooledParticle : MonoBehaviour
    {
        private ParticleSystem[] particleSystems;
        private ParticleManager manager;
        private Transform attachedTo;
        private Vector3 attachOffset;
        private float autoReturnTime;
        private float spawnTime;

        public string EffectName { get; private set; }
        public bool IsPlaying => particleSystems.Length > 0 && particleSystems[0].isPlaying;
        public bool IsFinished
        {
            get
            {
                if (autoReturnTime > 0 && Time.time - spawnTime >= autoReturnTime)
                    return true;

                foreach (var ps in particleSystems)
                {
                    if (ps.isPlaying || ps.particleCount > 0)
                        return false;
                }
                return true;
            }
        }

        public void Initialize(string effectName, ParticleManager mgr)
        {
            EffectName = effectName;
            manager = mgr;
            particleSystems = GetComponentsInChildren<ParticleSystem>();
        }

        public void Play()
        {
            spawnTime = Time.time;

            foreach (var ps in particleSystems)
            {
                ps.Clear();
                ps.Play();
            }
        }

        public void Stop()
        {
            foreach (var ps in particleSystems)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void AttachTo(Transform parent, Vector3 offset)
        {
            attachedTo = parent;
            attachOffset = offset;
        }

        public void Detach()
        {
            attachedTo = null;
            transform.SetParent(manager.transform);
        }

        public void SetAutoReturn(float time)
        {
            autoReturnTime = time;
        }

        public void ReturnToPool()
        {
            if (manager != null)
            {
                manager.ReturnToPool(this);
            }
        }

        private void LateUpdate()
        {
            if (attachedTo != null)
            {
                transform.position = attachedTo.position + attachOffset;
                transform.rotation = attachedTo.rotation;
            }
        }
    }

    [Serializable]
    public class ParticlePreset
    {
        public string effectName;
        public GameObject prefab;
        public int poolSize = 10;
        public float defaultDuration = 0f;
        public ParticleCategory category;
    }

    public enum ParticleCategory
    {
        Combat,
        Environment,
        UI,
        Movement,
        Interaction
    }
}
