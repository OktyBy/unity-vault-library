using UnityEngine;
using System.Collections.Generic;

namespace UnityVault.Core
{
    /// <summary>
    /// Central manager for all object pools.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        [System.Serializable]
        public class PoolConfig
        {
            public string poolId;
            public GameObject prefab;
            public int initialSize = 10;
            public int maxSize = 100;
        }

        [SerializeField] private List<PoolConfig> poolConfigs = new List<PoolConfig>();

        private Dictionary<string, ObjectPool> pools = new Dictionary<string, ObjectPool>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializePools();
        }

        private void InitializePools()
        {
            foreach (var config in poolConfigs)
            {
                CreatePool(config);
            }
        }

        private void CreatePool(PoolConfig config)
        {
            if (pools.ContainsKey(config.poolId))
            {
                Debug.LogWarning($"[PoolManager] Pool already exists: {config.poolId}");
                return;
            }

            var poolObj = new GameObject($"Pool_{config.poolId}");
            poolObj.transform.SetParent(transform);

            var pool = poolObj.AddComponent<ObjectPool>();
            // Pool will initialize itself in Awake

            pools[config.poolId] = pool;
        }

        public GameObject Spawn(string poolId, Vector3 position, Quaternion rotation)
        {
            if (!pools.TryGetValue(poolId, out var pool))
            {
                Debug.LogError($"[PoolManager] Pool not found: {poolId}");
                return null;
            }

            return pool.Get(position, rotation);
        }

        public void Despawn(string poolId, GameObject obj)
        {
            if (!pools.TryGetValue(poolId, out var pool))
            {
                Debug.LogError($"[PoolManager] Pool not found: {poolId}");
                return;
            }

            pool.Return(obj);
        }

        public ObjectPool GetPool(string poolId)
        {
            pools.TryGetValue(poolId, out var pool);
            return pool;
        }

        public void PreloadAll()
        {
            foreach (var config in poolConfigs)
            {
                if (pools.TryGetValue(config.poolId, out var pool))
                {
                    pool.Preload(config.initialSize);
                }
            }
        }

        public void ReturnAllToPool(string poolId)
        {
            if (pools.TryGetValue(poolId, out var pool))
            {
                pool.ReturnAll();
            }
        }
    }
}
