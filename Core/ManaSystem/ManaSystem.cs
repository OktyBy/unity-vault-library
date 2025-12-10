using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Core
{
    /// <summary>
    /// Resource management system for mana, stamina, energy, or any consumable resource.
    /// Supports regeneration, consumption, and temporary modifiers.
    /// </summary>
    public class ManaSystem : MonoBehaviour, IResource
    {
        #region Serialized Fields

        [Header("Resource Settings")]
        [SerializeField] private string resourceName = "Mana";
        [SerializeField] private float maxMana = 100f;
        [SerializeField] private float currentMana;
        [SerializeField] private bool startAtMax = true;

        [Header("Regeneration")]
        [SerializeField] private bool enableRegen = true;
        [SerializeField] private float regenRate = 5f; // Per second
        [SerializeField] private float regenDelay = 1f; // After consumption

        [Header("Events")]
        [SerializeField] private UnityEvent<float, float> onManaChanged; // current, max
        [SerializeField] private UnityEvent<float> onManaConsumed;
        [SerializeField] private UnityEvent<float> onManaRestored;
        [SerializeField] private UnityEvent onManaEmpty;
        [SerializeField] private UnityEvent onManaFull;

        #endregion

        #region Properties

        public string ResourceName => resourceName;

        public float MaxMana
        {
            get => maxMana;
            set
            {
                maxMana = Mathf.Max(0f, value);
                currentMana = Mathf.Min(currentMana, maxMana);
                OnManaChanged();
            }
        }

        public float CurrentMana
        {
            get => currentMana;
            private set
            {
                float oldValue = currentMana;
                currentMana = Mathf.Clamp(value, 0f, maxMana);

                if (!Mathf.Approximately(oldValue, currentMana))
                {
                    OnManaChanged();

                    if (currentMana <= 0f && oldValue > 0f)
                    {
                        ManaEmpty?.Invoke();
                        onManaEmpty?.Invoke();
                    }
                    else if (Mathf.Approximately(currentMana, maxMana) && oldValue < maxMana)
                    {
                        ManaFull?.Invoke();
                        onManaFull?.Invoke();
                    }
                }
            }
        }

        public float ManaPercentage => maxMana > 0 ? currentMana / maxMana : 0f;
        public bool HasMana => currentMana > 0f;
        public bool IsEmpty => currentMana <= 0f;
        public bool IsFull => Mathf.Approximately(currentMana, maxMana);

        #endregion

        #region C# Events

        public event Action<float, float> ManaChanged; // current, max
        public event Action<float> ManaConsumed;
        public event Action<float> ManaRestored;
        public event Action ManaEmpty;
        public event Action ManaFull;

        #endregion

        #region Private Fields

        private float lastConsumeTime;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (startAtMax)
            {
                currentMana = maxMana;
            }
        }

        private void Update()
        {
            if (enableRegen && !IsFull)
            {
                if (Time.time >= lastConsumeTime + regenDelay)
                {
                    Restore(regenRate * Time.deltaTime, ignoreEvents: true);
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Try to consume mana. Returns true if successful.
        /// </summary>
        public bool TryConsume(float amount)
        {
            if (amount <= 0f) return true;
            if (currentMana < amount) return false;

            Consume(amount);
            return true;
        }

        /// <summary>
        /// Consume mana (can go negative if forced).
        /// </summary>
        public void Consume(float amount)
        {
            if (amount <= 0f) return;

            CurrentMana -= amount;
            lastConsumeTime = Time.time;

            ManaConsumed?.Invoke(amount);
            onManaConsumed?.Invoke(amount);
        }

        /// <summary>
        /// Restore mana.
        /// </summary>
        public float Restore(float amount, bool ignoreEvents = false)
        {
            if (amount <= 0f) return 0f;

            float previousMana = currentMana;
            CurrentMana += amount;
            float actualRestore = currentMana - previousMana;

            if (!ignoreEvents && actualRestore > 0f)
            {
                ManaRestored?.Invoke(actualRestore);
                onManaRestored?.Invoke(actualRestore);
            }

            return actualRestore;
        }

        /// <summary>
        /// Set mana to a specific value.
        /// </summary>
        public void SetMana(float value)
        {
            CurrentMana = value;
        }

        /// <summary>
        /// Restore mana to maximum.
        /// </summary>
        public void RestoreToMax()
        {
            float restoreAmount = maxMana - currentMana;
            CurrentMana = maxMana;

            if (restoreAmount > 0f)
            {
                ManaRestored?.Invoke(restoreAmount);
                onManaRestored?.Invoke(restoreAmount);
            }
        }

        /// <summary>
        /// Drain all mana.
        /// </summary>
        public void DrainAll()
        {
            float drainAmount = currentMana;
            CurrentMana = 0f;

            if (drainAmount > 0f)
            {
                ManaConsumed?.Invoke(drainAmount);
                onManaConsumed?.Invoke(drainAmount);
            }
        }

        /// <summary>
        /// Check if there's enough mana for an action.
        /// </summary>
        public bool HasEnough(float amount) => currentMana >= amount;

        /// <summary>
        /// Modify max mana and optionally scale current.
        /// </summary>
        public void ModifyMaxMana(float newMax, bool scaleCurrent = true)
        {
            float ratio = ManaPercentage;
            maxMana = Mathf.Max(0f, newMax);

            if (scaleCurrent)
            {
                currentMana = maxMana * ratio;
            }
            else
            {
                currentMana = Mathf.Min(currentMana, maxMana);
            }

            OnManaChanged();
        }

        #endregion

        #region IResource Implementation

        float IResource.CurrentValue => currentMana;
        float IResource.MaxValue => maxMana;
        float IResource.Percentage => ManaPercentage;
        bool IResource.TryConsume(float amount) => TryConsume(amount);
        void IResource.Restore(float amount) => Restore(amount);

        #endregion

        #region Private Methods

        private void OnManaChanged()
        {
            ManaChanged?.Invoke(currentMana, maxMana);
            onManaChanged?.Invoke(currentMana, maxMana);
        }

        #endregion

        #region Editor

        private void OnValidate()
        {
            maxMana = Mathf.Max(0f, maxMana);
            currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
            regenRate = Mathf.Max(0f, regenRate);
            regenDelay = Mathf.Max(0f, regenDelay);
        }

        #endregion
    }
}
