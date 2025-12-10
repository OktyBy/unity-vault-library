using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Combat
{
    /// <summary>
    /// Combo system for chaining attacks with timing windows.
    /// </summary>
    public class ComboSystem : MonoBehaviour
    {
        [Header("Combo Settings")]
        [SerializeField] private float comboWindowTime = 0.5f;
        [SerializeField] private float comboCooldown = 0.2f;
        [SerializeField] private int maxComboLength = 5;
        [SerializeField] private bool resetOnMiss = true;

        [Header("Input")]
        [SerializeField] private KeyCode lightAttackKey = KeyCode.Mouse0;
        [SerializeField] private KeyCode heavyAttackKey = KeyCode.Mouse1;

        [Header("Combo Definitions")]
        [SerializeField] private List<ComboDefinition> combos = new List<ComboDefinition>();

        [Header("Events")]
        [SerializeField] private UnityEvent<int> onComboStep;
        [SerializeField] private UnityEvent<ComboDefinition> onComboComplete;
        [SerializeField] private UnityEvent onComboReset;
        [SerializeField] private UnityEvent onComboWindowOpen;
        [SerializeField] private UnityEvent onComboWindowClose;

        // State
        private List<AttackType> currentCombo = new List<AttackType>();
        private float lastAttackTime;
        private float windowCloseTime;
        private bool isInComboWindow;
        private bool isAttacking;
        private int currentComboIndex;

        // Properties
        public int CurrentComboStep => currentComboIndex;
        public bool IsInComboWindow => isInComboWindow;
        public bool IsAttacking => isAttacking;
        public IReadOnlyList<AttackType> CurrentComboSequence => currentCombo;

        // Events
        public event Action<int> ComboStepExecuted;
        public event Action<ComboDefinition> ComboCompleted;
        public event Action ComboReset;
        public event Action ComboWindowOpened;
        public event Action ComboWindowClosed;

        private void Update()
        {
            HandleInput();
            UpdateComboWindow();
        }

        private void HandleInput()
        {
            if (isAttacking) return;

            if (Input.GetKeyDown(lightAttackKey))
            {
                TryAddToCombo(AttackType.Light);
            }
            else if (Input.GetKeyDown(heavyAttackKey))
            {
                TryAddToCombo(AttackType.Heavy);
            }
        }

        private void UpdateComboWindow()
        {
            if (isInComboWindow && Time.time >= windowCloseTime)
            {
                CloseComboWindow();
            }
        }

        public void TryAddToCombo(AttackType attackType)
        {
            float timeSinceLastAttack = Time.time - lastAttackTime;

            // Check if we're within combo window or starting fresh
            if (currentCombo.Count > 0 && timeSinceLastAttack > comboWindowTime)
            {
                if (resetOnMiss)
                {
                    ResetCombo();
                }
            }

            // Check cooldown
            if (timeSinceLastAttack < comboCooldown)
            {
                return;
            }

            // Check max combo length
            if (currentCombo.Count >= maxComboLength)
            {
                ResetCombo();
            }

            // Add attack to combo
            currentCombo.Add(attackType);
            currentComboIndex = currentCombo.Count;
            lastAttackTime = Time.time;

            // Open combo window
            OpenComboWindow();

            // Execute attack
            ExecuteComboStep(currentComboIndex, attackType);

            // Check for combo completion
            CheckComboCompletion();
        }

        private void ExecuteComboStep(int step, AttackType attackType)
        {
            isAttacking = true;

            ComboStepExecuted?.Invoke(step);
            onComboStep?.Invoke(step);

            Debug.Log($"[Combo] Step {step}: {attackType}");

            // Attack animation would go here
            // For now, just reset attacking flag after a delay
            Invoke(nameof(EndAttack), 0.3f);
        }

        private void EndAttack()
        {
            isAttacking = false;
        }

        private void CheckComboCompletion()
        {
            foreach (var combo in combos)
            {
                if (MatchesCombo(combo))
                {
                    CompleteCombo(combo);
                    return;
                }
            }
        }

        private bool MatchesCombo(ComboDefinition combo)
        {
            if (combo.sequence == null || combo.sequence.Length == 0)
                return false;

            if (currentCombo.Count < combo.sequence.Length)
                return false;

            // Check if current combo ends with the defined sequence
            int startIndex = currentCombo.Count - combo.sequence.Length;
            for (int i = 0; i < combo.sequence.Length; i++)
            {
                if (currentCombo[startIndex + i] != combo.sequence[i])
                    return false;
            }

            return true;
        }

        private void CompleteCombo(ComboDefinition combo)
        {
            Debug.Log($"[Combo] Completed: {combo.comboName}!");

            ComboCompleted?.Invoke(combo);
            onComboComplete?.Invoke(combo);

            // Apply combo effects
            if (combo.damageMultiplier > 1f)
            {
                // Apply damage multiplier to next attack
            }

            ResetCombo();
        }

        private void OpenComboWindow()
        {
            isInComboWindow = true;
            windowCloseTime = Time.time + comboWindowTime;

            ComboWindowOpened?.Invoke();
            onComboWindowOpen?.Invoke();
        }

        private void CloseComboWindow()
        {
            isInComboWindow = false;

            ComboWindowClosed?.Invoke();
            onComboWindowClose?.Invoke();

            if (resetOnMiss)
            {
                ResetCombo();
            }
        }

        public void ResetCombo()
        {
            currentCombo.Clear();
            currentComboIndex = 0;
            isInComboWindow = false;

            ComboReset?.Invoke();
            onComboReset?.Invoke();
        }

        public void RegisterCombo(ComboDefinition combo)
        {
            if (!combos.Contains(combo))
            {
                combos.Add(combo);
            }
        }

        public void UnregisterCombo(ComboDefinition combo)
        {
            combos.Remove(combo);
        }
    }

    public enum AttackType
    {
        Light,
        Heavy,
        Special
    }

    [Serializable]
    public class ComboDefinition
    {
        public string comboName;
        public AttackType[] sequence;
        public float damageMultiplier = 1f;
        public string animationTrigger;
        public AudioClip soundEffect;
        [TextArea] public string description;
    }
}
