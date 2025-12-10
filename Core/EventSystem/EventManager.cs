using UnityEngine;
using System;
using System.Collections.Generic;

namespace UnityVault.Core
{
    /// <summary>
    /// Code-based event manager for runtime events.
    /// </summary>
    public class EventManager : MonoBehaviour
    {
        public static EventManager Instance { get; private set; }

        private Dictionary<string, Action> events = new Dictionary<string, Action>();
        private Dictionary<string, Delegate> genericEvents = new Dictionary<string, Delegate>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #region Parameterless Events

        public void Subscribe(string eventName, Action callback)
        {
            if (!events.ContainsKey(eventName))
            {
                events[eventName] = null;
            }
            events[eventName] += callback;
        }

        public void Unsubscribe(string eventName, Action callback)
        {
            if (events.ContainsKey(eventName))
            {
                events[eventName] -= callback;
            }
        }

        public void Trigger(string eventName)
        {
            if (events.TryGetValue(eventName, out var action))
            {
                action?.Invoke();
            }
        }

        #endregion

        #region Generic Events

        public void Subscribe<T>(string eventName, Action<T> callback)
        {
            if (!genericEvents.ContainsKey(eventName))
            {
                genericEvents[eventName] = null;
            }
            genericEvents[eventName] = Delegate.Combine(genericEvents[eventName], callback);
        }

        public void Unsubscribe<T>(string eventName, Action<T> callback)
        {
            if (genericEvents.ContainsKey(eventName))
            {
                genericEvents[eventName] = Delegate.Remove(genericEvents[eventName], callback);
            }
        }

        public void Trigger<T>(string eventName, T arg)
        {
            if (genericEvents.TryGetValue(eventName, out var del))
            {
                (del as Action<T>)?.Invoke(arg);
            }
        }

        #endregion

        #region Utility

        public void Clear(string eventName)
        {
            events.Remove(eventName);
            genericEvents.Remove(eventName);
        }

        public void ClearAll()
        {
            events.Clear();
            genericEvents.Clear();
        }

        #endregion
    }

    /// <summary>
    /// Common event names as constants.
    /// </summary>
    public static class GameEvents
    {
        public const string PLAYER_DIED = "PlayerDied";
        public const string PLAYER_SPAWNED = "PlayerSpawned";
        public const string ENEMY_KILLED = "EnemyKilled";
        public const string LEVEL_COMPLETED = "LevelCompleted";
        public const string GAME_PAUSED = "GamePaused";
        public const string GAME_RESUMED = "GameResumed";
        public const string ITEM_COLLECTED = "ItemCollected";
        public const string CHECKPOINT_REACHED = "CheckpointReached";
        public const string SCORE_CHANGED = "ScoreChanged";
    }
}
