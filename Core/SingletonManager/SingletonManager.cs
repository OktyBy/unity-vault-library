using UnityEngine;
using System;
using System.Collections.Generic;

namespace UnityVault.Core
{
    /// <summary>
    /// Service locator and singleton manager for centralized system access.
    /// </summary>
    public class ServiceLocator : MonoBehaviour
    {
        private static ServiceLocator _instance;
        public static ServiceLocator Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[ServiceLocator]");
                    _instance = go.AddComponent<ServiceLocator>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private Dictionary<Type, object> services = new Dictionary<Type, object>();
        private Dictionary<Type, MonoBehaviour> monoServices = new Dictionary<Type, MonoBehaviour>();

        // Events
        public event Action<Type> ServiceRegistered;
        public event Action<Type> ServiceUnregistered;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Register a service.
        /// </summary>
        public void Register<T>(T service) where T : class
        {
            Type type = typeof(T);

            if (services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Service already registered: {type.Name}");
                return;
            }

            services[type] = service;

            if (service is MonoBehaviour mono)
            {
                monoServices[type] = mono;
            }

            ServiceRegistered?.Invoke(type);
            Debug.Log($"[ServiceLocator] Registered: {type.Name}");
        }

        /// <summary>
        /// Unregister a service.
        /// </summary>
        public void Unregister<T>() where T : class
        {
            Type type = typeof(T);

            if (services.Remove(type))
            {
                monoServices.Remove(type);
                ServiceUnregistered?.Invoke(type);
                Debug.Log($"[ServiceLocator] Unregistered: {type.Name}");
            }
        }

        /// <summary>
        /// Get a registered service.
        /// </summary>
        public T Get<T>() where T : class
        {
            Type type = typeof(T);

            if (services.TryGetValue(type, out object service))
            {
                return service as T;
            }

            Debug.LogWarning($"[ServiceLocator] Service not found: {type.Name}");
            return null;
        }

        /// <summary>
        /// Try to get a registered service.
        /// </summary>
        public bool TryGet<T>(out T service) where T : class
        {
            Type type = typeof(T);

            if (services.TryGetValue(type, out object obj))
            {
                service = obj as T;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// Check if service is registered.
        /// </summary>
        public bool Has<T>() where T : class
        {
            return services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Get or create a service.
        /// </summary>
        public T GetOrCreate<T>() where T : class, new()
        {
            Type type = typeof(T);

            if (services.TryGetValue(type, out object service))
            {
                return service as T;
            }

            T newService = new T();
            Register(newService);
            return newService;
        }

        /// <summary>
        /// Clear all services.
        /// </summary>
        public void ClearAll()
        {
            services.Clear();
            monoServices.Clear();
            Debug.Log("[ServiceLocator] Cleared all services");
        }

        /// <summary>
        /// Get all registered service types.
        /// </summary>
        public List<Type> GetRegisteredTypes()
        {
            return new List<Type>(services.Keys);
        }
    }

    /// <summary>
    /// Generic singleton base class for MonoBehaviours.
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning($"[Singleton] Instance of {typeof(T)} already destroyed. Returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindFirstObjectByType<T>();

                        if (_instance == null)
                        {
                            var go = new GameObject($"[{typeof(T).Name}]");
                            _instance = go.AddComponent<T>();
                        }
                    }

                    return _instance;
                }
            }
        }

        public static bool HasInstance => _instance != null;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[Singleton] Duplicate instance of {typeof(T).Name} destroyed.");
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            OnAwake();
        }

        protected virtual void OnAwake() { }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }
    }

    /// <summary>
    /// Persistent singleton that survives scene loads.
    /// </summary>
    public abstract class PersistentSingleton<T> : Singleton<T> where T : MonoBehaviour
    {
        protected override void OnAwake()
        {
            DontDestroyOnLoad(gameObject);
            base.OnAwake();
        }
    }

    /// <summary>
    /// Lazy singleton that creates itself when first accessed.
    /// </summary>
    public abstract class LazySingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();

        public static T Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindFirstObjectByType<T>();

                        if (_instance == null)
                        {
                            var go = new GameObject($"[{typeof(T).Name}]");
                            _instance = go.AddComponent<T>();
                            DontDestroyOnLoad(go);
                        }
                    }

                    return _instance;
                }
            }
        }

        public static bool HasInstance => _instance != null;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }

    /// <summary>
    /// ScriptableObject singleton for configuration data.
    /// </summary>
    public abstract class ScriptableSingleton<T> : ScriptableObject where T : ScriptableObject
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<T>(typeof(T).Name);

                    if (_instance == null)
                    {
                        _instance = CreateInstance<T>();
                        Debug.LogWarning($"[ScriptableSingleton] {typeof(T).Name} not found in Resources. Created default instance.");
                    }
                }

                return _instance;
            }
        }

        public static bool HasInstance => _instance != null;
    }

    /// <summary>
    /// Auto-registering singleton that registers with ServiceLocator.
    /// </summary>
    public abstract class RegisteredSingleton<T> : Singleton<T> where T : MonoBehaviour
    {
        protected override void OnAwake()
        {
            base.OnAwake();
            ServiceLocator.Instance.Register(this as T);
        }

        protected override void OnDestroy()
        {
            if (ServiceLocator.Instance != null)
            {
                ServiceLocator.Instance.Unregister<T>();
            }
            base.OnDestroy();
        }
    }

    /// <summary>
    /// Manager registry for finding and caching manager instances.
    /// </summary>
    public static class Managers
    {
        private static Dictionary<Type, MonoBehaviour> cache = new Dictionary<Type, MonoBehaviour>();

        public static T Get<T>() where T : MonoBehaviour
        {
            Type type = typeof(T);

            if (cache.TryGetValue(type, out MonoBehaviour cached) && cached != null)
            {
                return cached as T;
            }

            T found = UnityEngine.Object.FindFirstObjectByType<T>();
            if (found != null)
            {
                cache[type] = found;
            }

            return found;
        }

        public static void Register<T>(T manager) where T : MonoBehaviour
        {
            cache[typeof(T)] = manager;
        }

        public static void Unregister<T>() where T : MonoBehaviour
        {
            cache.Remove(typeof(T));
        }

        public static void ClearCache()
        {
            cache.Clear();
        }
    }
}
