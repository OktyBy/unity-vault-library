using UnityEngine;
using UnityEngine.Events;

namespace UnityVault.Core
{
    /// <summary>
    /// Listens for a GameEvent and invokes UnityEvents in response.
    /// </summary>
    public class GameEventListener : MonoBehaviour
    {
        [SerializeField] private GameEvent gameEvent;
        [SerializeField] private UnityEvent response;

        private void OnEnable()
        {
            if (gameEvent != null)
            {
                gameEvent.RegisterListener(this);
            }
        }

        private void OnDisable()
        {
            if (gameEvent != null)
            {
                gameEvent.UnregisterListener(this);
            }
        }

        public void OnEventRaised()
        {
            response?.Invoke();
        }
    }

    /// <summary>
    /// Generic event listener with parameter.
    /// </summary>
    public class GameEventListener<T> : MonoBehaviour
    {
        public void OnEventRaised(T value) { }
    }

    /// <summary>
    /// Int event listener.
    /// </summary>
    public class IntGameEventListener : MonoBehaviour
    {
        [SerializeField] private IntGameEvent gameEvent;
        [SerializeField] private UnityEvent<int> response;

        private void OnEnable()
        {
            if (gameEvent != null)
            {
                gameEvent.RegisterListener(this);
            }
        }

        private void OnDisable()
        {
            if (gameEvent != null)
            {
                gameEvent.UnregisterListener(this);
            }
        }

        public void OnEventRaised(int value)
        {
            response?.Invoke(value);
        }
    }

    /// <summary>
    /// Float event listener.
    /// </summary>
    public class FloatGameEventListener : MonoBehaviour
    {
        [SerializeField] private FloatGameEvent gameEvent;
        [SerializeField] private UnityEvent<float> response;

        private void OnEnable()
        {
            if (gameEvent != null)
            {
                gameEvent.RegisterListener(this);
            }
        }

        private void OnDisable()
        {
            if (gameEvent != null)
            {
                gameEvent.UnregisterListener(this);
            }
        }

        public void OnEventRaised(float value)
        {
            response?.Invoke(value);
        }
    }
}
