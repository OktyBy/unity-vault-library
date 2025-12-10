using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Combat
{
    /// <summary>
    /// Block and parry system for defensive combat mechanics.
    /// </summary>
    public class BlockParry : MonoBehaviour
    {
        [Header("Block Settings")]
        [SerializeField] private float blockDamageReduction = 0.7f;
        [SerializeField] private float blockStaminaCost = 5f;
        [SerializeField] private float blockStaminaDrain = 2f;
        [SerializeField] private bool canMoveWhileBlocking = false;

        [Header("Parry Settings")]
        [SerializeField] private float parryWindow = 0.2f;
        [SerializeField] private float parryStaminaCost = 10f;
        [SerializeField] private float parryCooldown = 0.5f;
        [SerializeField] private float parryStunDuration = 1.5f;

        [Header("Perfect Block")]
        [SerializeField] private bool enablePerfectBlock = true;
        [SerializeField] private float perfectBlockWindow = 0.1f;
        [SerializeField] private float perfectBlockDamageReduction = 1f;

        [Header("Stamina Reference")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaRegenRate = 10f;
        [SerializeField] private float staminaRegenDelay = 1f;

        [Header("Input")]
        [SerializeField] private KeyCode blockKey = KeyCode.Mouse1;

        [Header("Events")]
        [SerializeField] private UnityEvent onBlockStart;
        [SerializeField] private UnityEvent onBlockEnd;
        [SerializeField] private UnityEvent<float> onBlockDamage;
        [SerializeField] private UnityEvent onParrySuccess;
        [SerializeField] private UnityEvent onParryFail;
        [SerializeField] private UnityEvent onPerfectBlock;
        [SerializeField] private UnityEvent onStaminaDepleted;

        // State
        private bool isBlocking;
        private bool isParrying;
        private float parryStartTime;
        private float lastParryTime;
        private float currentStamina;
        private float lastStaminaUseTime;

        // Properties
        public bool IsBlocking => isBlocking;
        public bool IsParrying => isParrying && Time.time - parryStartTime <= parryWindow;
        public float CurrentStamina => currentStamina;
        public float StaminaPercent => currentStamina / maxStamina;
        public bool CanBlock => currentStamina >= blockStaminaCost;
        public bool CanParry => Time.time - lastParryTime >= parryCooldown && currentStamina >= parryStaminaCost;

        // Events
        public event Action BlockStarted;
        public event Action BlockEnded;
        public event Action<float, float> DamageBlocked; // original, reduced
        public event Action<GameObject> ParrySucceeded;
        public event Action ParryFailed;
        public event Action PerfectBlockTriggered;
        public event Action StaminaDepleted;
        public event Action<float> StaminaChanged;

        private void Awake()
        {
            currentStamina = maxStamina;
        }

        private void Update()
        {
            HandleInput();
            UpdateStamina();
            UpdateParryWindow();
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(blockKey))
            {
                StartBlock();
            }
            else if (Input.GetKeyUp(blockKey))
            {
                EndBlock();
            }
        }

        private void UpdateStamina()
        {
            // Drain stamina while blocking
            if (isBlocking)
            {
                UseStamina(blockStaminaDrain * Time.deltaTime);
            }
            // Regenerate stamina when not blocking
            else if (Time.time - lastStaminaUseTime >= staminaRegenDelay)
            {
                currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
                StaminaChanged?.Invoke(currentStamina);
            }
        }

        private void UpdateParryWindow()
        {
            if (isParrying && Time.time - parryStartTime > parryWindow)
            {
                isParrying = false;
            }
        }

        public void StartBlock()
        {
            if (!CanBlock) return;

            isBlocking = true;
            isParrying = true;
            parryStartTime = Time.time;

            UseStamina(blockStaminaCost);

            BlockStarted?.Invoke();
            onBlockStart?.Invoke();

            Debug.Log("[Block] Started blocking");
        }

        public void EndBlock()
        {
            if (!isBlocking) return;

            isBlocking = false;
            isParrying = false;

            BlockEnded?.Invoke();
            onBlockEnd?.Invoke();

            Debug.Log("[Block] Stopped blocking");
        }

        /// <summary>
        /// Process incoming damage while blocking/parrying.
        /// Returns the actual damage to apply.
        /// </summary>
        public float ProcessIncomingDamage(float damage, GameObject attacker = null)
        {
            // Check for parry first
            if (IsParrying)
            {
                return ProcessParry(damage, attacker);
            }

            // Check for block
            if (isBlocking)
            {
                return ProcessBlock(damage);
            }

            // No defense - full damage
            return damage;
        }

        private float ProcessParry(float damage, GameObject attacker)
        {
            lastParryTime = Time.time;
            UseStamina(parryStaminaCost);

            // Check for perfect block timing
            if (enablePerfectBlock && Time.time - parryStartTime <= perfectBlockWindow)
            {
                PerfectBlockTriggered?.Invoke();
                onPerfectBlock?.Invoke();

                // Stun attacker
                if (attacker != null)
                {
                    StunAttacker(attacker);
                }

                Debug.Log("[Block] PERFECT PARRY!");
                return damage * (1f - perfectBlockDamageReduction);
            }

            // Regular parry
            ParrySucceeded?.Invoke(attacker);
            onParrySuccess?.Invoke();

            // Stun attacker
            if (attacker != null)
            {
                StunAttacker(attacker);
            }

            Debug.Log("[Block] Parry successful!");
            return 0f;
        }

        private float ProcessBlock(float damage)
        {
            float reducedDamage = damage * (1f - blockDamageReduction);

            DamageBlocked?.Invoke(damage, reducedDamage);
            onBlockDamage?.Invoke(reducedDamage);

            // Use stamina based on damage blocked
            UseStamina(damage * 0.1f);

            Debug.Log($"[Block] Blocked {damage - reducedDamage:F1} damage");
            return reducedDamage;
        }

        private void StunAttacker(GameObject attacker)
        {
            // Try to find and stun the attacker
            var attackerBlockParry = attacker.GetComponent<BlockParry>();
            if (attackerBlockParry != null)
            {
                attackerBlockParry.ApplyStun(parryStunDuration);
            }

            // Could also send a message or event
            attacker.SendMessage("OnStunned", parryStunDuration, SendMessageOptions.DontRequireReceiver);
        }

        public void ApplyStun(float duration)
        {
            // Disable blocking during stun
            if (isBlocking)
            {
                EndBlock();
            }

            Debug.Log($"[Block] Stunned for {duration}s");
            // The actual stun effect would be handled by character controller
        }

        private void UseStamina(float amount)
        {
            currentStamina = Mathf.Max(0, currentStamina - amount);
            lastStaminaUseTime = Time.time;
            StaminaChanged?.Invoke(currentStamina);

            if (currentStamina <= 0)
            {
                OnStaminaDepleted();
            }
        }

        private void OnStaminaDepleted()
        {
            EndBlock();
            StaminaDepleted?.Invoke();
            onStaminaDepleted?.Invoke();
            Debug.Log("[Block] Stamina depleted!");
        }

        public void SetStamina(float amount)
        {
            currentStamina = Mathf.Clamp(amount, 0, maxStamina);
            StaminaChanged?.Invoke(currentStamina);
        }

        public void AddStamina(float amount)
        {
            SetStamina(currentStamina + amount);
        }
    }
}
