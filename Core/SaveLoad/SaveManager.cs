using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace UnityVault.Core
{
    /// <summary>
    /// Centralized save/load system supporting JSON and binary formats.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        #region Singleton

        private static SaveManager instance;
        public static SaveManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<SaveManager>();
                    if (instance == null)
                    {
                        var go = new GameObject("SaveManager");
                        instance = go.AddComponent<SaveManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        #endregion

        #region Serialized Fields

        [Header("Settings")]
        [SerializeField] private string saveFolder = "Saves";
        [SerializeField] private string saveExtension = ".sav";
        [SerializeField] private SaveFormat format = SaveFormat.JSON;
        [SerializeField] private bool autoSave = false;
        [SerializeField] private float autoSaveInterval = 300f; // 5 minutes
        [SerializeField] private int maxSaveSlots = 10;

        #endregion

        #region Properties

        public string SavePath => Path.Combine(Application.persistentDataPath, saveFolder);
        public SaveFormat Format => format;
        public bool AutoSaveEnabled => autoSave;

        #endregion

        #region Events

        public event Action<string> OnSaveStarted;
        public event Action<string> OnSaveCompleted;
        public event Action<string> OnLoadStarted;
        public event Action<string> OnLoadCompleted;
        public event Action<string> OnSaveFailed;
        public event Action<string> OnLoadFailed;

        #endregion

        #region Private Fields

        private float lastAutoSaveTime;
        private Dictionary<string, ISaveable> saveables = new Dictionary<string, ISaveable>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureSaveFolderExists();
        }

        private void Update()
        {
            if (autoSave && Time.time >= lastAutoSaveTime + autoSaveInterval)
            {
                AutoSave();
                lastAutoSaveTime = Time.time;
            }
        }

        #endregion

        #region Registration

        /// <summary>
        /// Register a saveable object.
        /// </summary>
        public void Register(string id, ISaveable saveable)
        {
            if (!saveables.ContainsKey(id))
            {
                saveables[id] = saveable;
            }
        }

        /// <summary>
        /// Unregister a saveable object.
        /// </summary>
        public void Unregister(string id)
        {
            saveables.Remove(id);
        }

        #endregion

        #region Save Methods

        /// <summary>
        /// Save game to a slot.
        /// </summary>
        public bool Save(int slot)
        {
            return Save($"save_{slot}");
        }

        /// <summary>
        /// Save game with a name.
        /// </summary>
        public bool Save(string saveName)
        {
            OnSaveStarted?.Invoke(saveName);

            try
            {
                var saveData = new SaveData
                {
                    saveName = saveName,
                    saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    gameVersion = Application.version,
                    data = new Dictionary<string, string>()
                };

                // Collect data from all saveables
                foreach (var kvp in saveables)
                {
                    try
                    {
                        string data = kvp.Value.GetSaveData();
                        saveData.data[kvp.Key] = data;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[SaveManager] Failed to get save data from {kvp.Key}: {e.Message}");
                    }
                }

                // Write to file
                string filePath = GetSavePath(saveName);
                WriteToFile(filePath, saveData);

                OnSaveCompleted?.Invoke(saveName);
                Debug.Log($"[SaveManager] Saved: {saveName}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e.Message}");
                OnSaveFailed?.Invoke(saveName);
                return false;
            }
        }

        /// <summary>
        /// Quick save.
        /// </summary>
        public bool QuickSave()
        {
            return Save("quicksave");
        }

        /// <summary>
        /// Auto save.
        /// </summary>
        public bool AutoSave()
        {
            return Save("autosave");
        }

        #endregion

        #region Load Methods

        /// <summary>
        /// Load game from a slot.
        /// </summary>
        public bool Load(int slot)
        {
            return Load($"save_{slot}");
        }

        /// <summary>
        /// Load game with a name.
        /// </summary>
        public bool Load(string saveName)
        {
            OnLoadStarted?.Invoke(saveName);

            try
            {
                string filePath = GetSavePath(saveName);

                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"[SaveManager] Save not found: {saveName}");
                    OnLoadFailed?.Invoke(saveName);
                    return false;
                }

                var saveData = ReadFromFile(filePath);

                if (saveData == null || saveData.data == null)
                {
                    OnLoadFailed?.Invoke(saveName);
                    return false;
                }

                // Distribute data to saveables
                foreach (var kvp in saveData.data)
                {
                    if (saveables.TryGetValue(kvp.Key, out ISaveable saveable))
                    {
                        try
                        {
                            saveable.LoadSaveData(kvp.Value);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[SaveManager] Failed to load data for {kvp.Key}: {e.Message}");
                        }
                    }
                }

                OnLoadCompleted?.Invoke(saveName);
                Debug.Log($"[SaveManager] Loaded: {saveName}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load failed: {e.Message}");
                OnLoadFailed?.Invoke(saveName);
                return false;
            }
        }

        /// <summary>
        /// Quick load.
        /// </summary>
        public bool QuickLoad()
        {
            return Load("quicksave");
        }

        #endregion

        #region File Management

        /// <summary>
        /// Get all save files.
        /// </summary>
        public SaveFileInfo[] GetAllSaves()
        {
            EnsureSaveFolderExists();

            var files = Directory.GetFiles(SavePath, $"*{saveExtension}");
            var saves = new List<SaveFileInfo>();

            foreach (var file in files)
            {
                try
                {
                    var saveData = ReadFromFile(file);
                    saves.Add(new SaveFileInfo
                    {
                        name = saveData.saveName,
                        filePath = file,
                        saveTime = saveData.saveTime,
                        fileSize = new FileInfo(file).Length
                    });
                }
                catch
                {
                    // Skip corrupted files
                }
            }

            return saves.OrderByDescending(s => s.saveTime).ToArray();
        }

        /// <summary>
        /// Check if a save exists.
        /// </summary>
        public bool SaveExists(string saveName)
        {
            return File.Exists(GetSavePath(saveName));
        }

        /// <summary>
        /// Delete a save.
        /// </summary>
        public bool DeleteSave(string saveName)
        {
            string path = GetSavePath(saveName);

            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }

            return false;
        }

        #endregion

        #region PlayerPrefs Helpers

        /// <summary>
        /// Save a value to PlayerPrefs.
        /// </summary>
        public void SavePref<T>(string key, T value)
        {
            string json = JsonUtility.ToJson(new Wrapper<T> { value = value });
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Load a value from PlayerPrefs.
        /// </summary>
        public T LoadPref<T>(string key, T defaultValue = default)
        {
            if (!PlayerPrefs.HasKey(key)) return defaultValue;

            string json = PlayerPrefs.GetString(key);
            var wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
            return wrapper.value;
        }

        /// <summary>
        /// Check if a PlayerPref exists.
        /// </summary>
        public bool HasPref(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        /// <summary>
        /// Delete a PlayerPref.
        /// </summary>
        public void DeletePref(string key)
        {
            PlayerPrefs.DeleteKey(key);
        }

        #endregion

        #region Private Methods

        private void EnsureSaveFolderExists()
        {
            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
            }
        }

        private string GetSavePath(string saveName)
        {
            return Path.Combine(SavePath, saveName + saveExtension);
        }

        private void WriteToFile(string path, SaveData data)
        {
            string json = JsonUtility.ToJson(data, true);

            if (format == SaveFormat.JSON)
            {
                File.WriteAllText(path, json);
            }
            else
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
                bytes = CompressBytes(bytes);
                File.WriteAllBytes(path, bytes);
            }
        }

        private SaveData ReadFromFile(string path)
        {
            string json;

            if (format == SaveFormat.JSON)
            {
                json = File.ReadAllText(path);
            }
            else
            {
                byte[] bytes = File.ReadAllBytes(path);
                bytes = DecompressBytes(bytes);
                json = System.Text.Encoding.UTF8.GetString(bytes);
            }

            return JsonUtility.FromJson<SaveData>(json);
        }

        private byte[] CompressBytes(byte[] data)
        {
            // Simple XOR "encryption" for binary format
            // In production, use proper compression/encryption
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                result[i] = (byte)(data[i] ^ 0x5A);
            }
            return result;
        }

        private byte[] DecompressBytes(byte[] data)
        {
            return CompressBytes(data); // XOR is symmetric
        }

        #endregion

        #region Data Classes

        [Serializable]
        private class SaveData
        {
            public string saveName;
            public string saveTime;
            public string gameVersion;
            public Dictionary<string, string> data;
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T value;
        }

        public class SaveFileInfo
        {
            public string name;
            public string filePath;
            public string saveTime;
            public long fileSize;
        }

        public enum SaveFormat
        {
            JSON,
            Binary
        }

        #endregion
    }
}
