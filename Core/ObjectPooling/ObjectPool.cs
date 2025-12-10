using UnityEngine;
using System.Collections.Generic;

namespace UnityVault.Core
{
    /// <summary>
    /// Generic object pool for performance optimization.
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private GameObject prefab;
        [SerializeField] private int initialSize = 10;
        [SerializeField] private int maxSize = 100;
        [SerializeField] private bool expandable = true;

        private Queue<GameObject> pool = new Queue<GameObject>();
        private List<GameObject> activeObjects = new List<GameObject>();
        private Transform poolParent;

        public int AvailableCount => pool.Count;
        public int ActiveCount => activeObjects.Count;
        public int TotalCount => AvailableCount + ActiveCount;

        private void Awake()
        {
            poolParent = new GameObject($"Pool_{prefab.name}").transform;
            poolParent.SetParent(transform);

            for (int i = 0; i < initialSize; i++)
            {
                CreatePooledObject();
            }
        }

        public GameObject Get()
        {
            return Get(Vector3.zero, Quaternion.identity);
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject obj;

            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
            }
            else if (expandable && TotalCount < maxSize)
            {
                obj = CreatePooledObject();
            }
            else
            {
                Debug.LogWarning($"[ObjectPool] Pool exhausted for {prefab.name}");
                return null;
            }

            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);
            activeObjects.Add(obj);

            var poolable = obj.GetComponent<IPoolable>();
            poolable?.OnSpawn();

            return obj;
        }

        public T Get<T>(Vector3 position, Quaternion rotation) where T : Component
        {
            var obj = Get(position, rotation);
            return obj?.GetComponent<T>();
        }

        public void Return(GameObject obj)
        {
            if (obj == null) return;

            var poolable = obj.GetComponent<IPoolable>();
            poolable?.OnDespawn();

            obj.SetActive(false);
            obj.transform.SetParent(poolParent);

            activeObjects.Remove(obj);
            pool.Enqueue(obj);
        }

        public void ReturnAll()
        {
            for (int i = activeObjects.Count - 1; i >= 0; i--)
            {
                Return(activeObjects[i]);
            }
        }

        private GameObject CreatePooledObject()
        {
            var obj = Instantiate(prefab, poolParent);
            obj.SetActive(false);
            pool.Enqueue(obj);
            return obj;
        }

        public void Preload(int count)
        {
            int toCreate = Mathf.Min(count, maxSize - TotalCount);
            for (int i = 0; i < toCreate; i++)
            {
                CreatePooledObject();
            }
        }
    }

    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }
}
