using UnityEngine;
using System.Collections.Generic;

namespace UnityVault.Core
{
    /// <summary>
    /// ScriptableObject-based game event for decoupled communication.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGameEvent", menuName = "UnityVault/Events/Game Event")]
    public class GameEvent : ScriptableObject
    {
        private List<GameEventListener> listeners = new List<GameEventListener>();

        public void Raise()
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                listeners[i].OnEventRaised();
            }
        }

        public void RegisterListener(GameEventListener listener)
        {
            if (!listeners.Contains(listener))
            {
                listeners.Add(listener);
            }
        }

        public void UnregisterListener(GameEventListener listener)
        {
            listeners.Remove(listener);
        }
    }

    /// <summary>
    /// Generic game event with parameter.
    /// </summary>
    public class GameEvent<T> : ScriptableObject
    {
        private List<GameEventListener<T>> listeners = new List<GameEventListener<T>>();

        public void Raise(T value)
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                listeners[i].OnEventRaised(value);
            }
        }

        public void RegisterListener(GameEventListener<T> listener)
        {
            if (!listeners.Contains(listener))
            {
                listeners.Add(listener);
            }
        }

        public void UnregisterListener(GameEventListener<T> listener)
        {
            listeners.Remove(listener);
        }
    }

    [CreateAssetMenu(fileName = "NewIntEvent", menuName = "UnityVault/Events/Int Event")]
    public class IntGameEvent : GameEvent<int> { }

    [CreateAssetMenu(fileName = "NewFloatEvent", menuName = "UnityVault/Events/Float Event")]
    public class FloatGameEvent : GameEvent<float> { }

    [CreateAssetMenu(fileName = "NewStringEvent", menuName = "UnityVault/Events/String Event")]
    public class StringGameEvent : GameEvent<string> { }

    [CreateAssetMenu(fileName = "NewVector3Event", menuName = "UnityVault/Events/Vector3 Event")]
    public class Vector3GameEvent : GameEvent<Vector3> { }
}
