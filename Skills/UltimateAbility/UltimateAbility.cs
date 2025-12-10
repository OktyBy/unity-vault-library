using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;

namespace UnityVault.Skills
{
    /// <summary>
    /// Ultimate/Special ability system with charge mechanics.
    /// </summary>
    public class UltimateAbility : MonoBehaviour
    {
        [Header("Ability Settings")]
        [SerializeField] private string abilityName = "Ultimate";
        [SerializeField] private string abilityDescription;
        [SerializeField] private Sprite abilityIcon;

        [Header("Charge Settings")]
        [SerializeField] private float maxCharge = 100f;
        [SerializeField] private float chargePerKill = 20f;
        [SerializeField] private float chargePerDamageDealt = 0.5f;
        [SerializeField] private float chargePerDamageTaken = 1f;
        [SerializeField] private float passiveChargeRate = 0f; // Per second
        [SerializeField] private bool loseChargeOnDeath = true;
        [SerializeField] private float chargeRetainedOnDeath = 0f; // Percentage

        [Header("Duration")]
        [SerializeField] private float duration = 10f;
        [SerializeField] private bool hasDuration = true;

        [Header("Cooldown")]
        [SerializeField] private float cooldownAfterUse = 0f;
        [SerializeField] private bool resetChargeOnUse = true;

        [Header("Effects")]
        [SerializeField] private GameObject activationEffect;
        [SerializeField] private GameObject activeEffect;
        [SerializeField] private GameObject deactivationEffect;
        [SerializeField] private AudioClip activationSound;
        [SerializeField] private AudioClip activeLoopSound;
        [SerializeField] private AudioClip deactivationSound;

        [Header("Stat Modifiers")]
        [SerializeField] private float damageMultiplier = 2f;
        [SerializeField] private float speedMultiplier = 1.5f;
        [SerializeField] private float defenseMultiplier = 1.5f;
        [SerializeField] private bool invincible = false;
        [SerializeField] private bool unlimitedResource = false;

        [Header("Events")]
        [SerializeField] private UnityEvent onUltimateReady;
        [SerializeField] private UnityEvent onUltimateActivated;
        [SerializeField] private UnityEvent onUltimateDeactivated;
        [SerializeField] private UnityEvent<float> onChargeChanged;

        // State
        private float currentCharge;
        private bool isActive;
        private bool isOnCooldown;
        private float cooldownTimer;
        private float durationTimer;
        private Coroutine activeCoroutine;
        private AudioSource audioSource;
        private GameObject activeEffectInstance;

        // Events
        public event Action UltimateReady;
        public event Action UltimateActivated;
        public event Action UltimateDeactivated;
        public event Action<float> ChargeChanged; // 0-1 percentage

        public string AbilityName => abilityName;
        public Sprite AbilityIcon => abilityIcon;
        public float CurrentCharge => currentCharge;
        public float MaxCharge => maxCharge;
        public float ChargePercent => currentCharge / maxCharge;
        public bool IsReady => currentCharge >= maxCharge && !isOnCooldown && !isActive;
        public bool IsActive => isActive;
        public float RemainingDuration => durationTimer;
        public float RemainingCooldown => cooldownTimer;

        // Stat modifiers (used by other systems)
        public float DamageMultiplier => isActive ? damageMultiplier : 1f;
        public float SpeedMultiplier => isActive ? speedMultiplier : 1f;
        public float DefenseMultiplier => isActive ? defenseMultiplier : 1f;
        public bool IsInvincible => isActive && invincible;
        public bool HasUnlimitedResource => isActive && unlimitedResource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        private void Update()
        {
            // Passive charge
            if (passiveChargeRate > 0 && !isActive && !isOnCooldown)
            {
                AddCharge(passiveChargeRate * Time.deltaTime);
            }

            // Cooldown
            if (isOnCooldown)
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0)
                {
                    isOnCooldown = false;
                }
            }
        }

        /// <summary>
        /// Add charge to ultimate.
        /// </summary>
        public void AddCharge(float amount)
        {
            if (isActive) return;

            float oldCharge = currentCharge;
            currentCharge = Mathf.Min(maxCharge, currentCharge + amount);

            if (currentCharge != oldCharge)
            {
                ChargeChanged?.Invoke(ChargePercent);
                onChargeChanged?.Invoke(ChargePercent);

                // Check if just became ready
                if (oldCharge < maxCharge && currentCharge >= maxCharge)
                {
                    UltimateReady?.Invoke();
                    onUltimateReady?.Invoke();
                    Debug.Log($"[Ultimate] {abilityName} ready!");
                }
            }
        }

        /// <summary>
        /// Set charge directly.
        /// </summary>
        public void SetCharge(float amount)
        {
            currentCharge = Mathf.Clamp(amount, 0, maxCharge);
            ChargeChanged?.Invoke(ChargePercent);
            onChargeChanged?.Invoke(ChargePercent);
        }

        /// <summary>
        /// Add charge from dealing damage.
        /// </summary>
        public void OnDamageDealt(float damage)
        {
            AddCharge(damage * chargePerDamageDealt);
        }

        /// <summary>
        /// Add charge from taking damage.
        /// </summary>
        public void OnDamageTaken(float damage)
        {
            AddCharge(damage * chargePerDamageTaken);
        }

        /// <summary>
        /// Add charge from kill.
        /// </summary>
        public void OnKill()
        {
            AddCharge(chargePerKill);
        }

        /// <summary>
        /// Handle player death.
        /// </summary>
        public void OnDeath()
        {
            if (isActive)
            {
                Deactivate();
            }

            if (loseChargeOnDeath)
            {
                currentCharge = maxCharge * chargeRetainedOnDeath;
                ChargeChanged?.Invoke(ChargePercent);
                onChargeChanged?.Invoke(ChargePercent);
            }
        }

        /// <summary>
        /// Activate ultimate ability.
        /// </summary>
        public bool Activate()
        {
            if (!IsReady)
            {
                Debug.Log($"[Ultimate] {abilityName} not ready");
                return false;
            }

            isActive = true;

            // Reset charge if configured
            if (resetChargeOnUse)
            {
                currentCharge = 0;
                ChargeChanged?.Invoke(0);
                onChargeChanged?.Invoke(0);
            }

            // Spawn effects
            if (activationEffect != null)
            {
                Instantiate(activationEffect, transform.position, Quaternion.identity);
            }

            if (activeEffect != null)
            {
                activeEffectInstance = Instantiate(activeEffect, transform);
            }

            // Play sounds
            if (activationSound != null)
            {
                audioSource.PlayOneShot(activationSound);
            }

            if (activeLoopSound != null)
            {
                audioSource.clip = activeLoopSound;
                audioSource.loop = true;
                audioSource.Play();
            }

            UltimateActivated?.Invoke();
            onUltimateActivated?.Invoke();

            Debug.Log($"[Ultimate] {abilityName} activated!");

            // Start duration timer
            if (hasDuration)
            {
                activeCoroutine = StartCoroutine(DurationCoroutine());
            }

            return true;
        }

        private IEnumerator DurationCoroutine()
        {
            durationTimer = duration;

            while (durationTimer > 0)
            {
                durationTimer -= Time.deltaTime;
                yield return null;
            }

            Deactivate();
        }

        /// <summary>
        /// Deactivate ultimate ability.
        /// </summary>
        public void Deactivate()
        {
            if (!isActive) return;

            isActive = false;

            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
                activeCoroutine = null;
            }

            // Clean up effects
            if (activeEffectInstance != null)
            {
                Destroy(activeEffectInstance);
            }

            if (deactivationEffect != null)
            {
                Instantiate(deactivationEffect, transform.position, Quaternion.identity);
            }

            // Stop loop sound
            if (activeLoopSound != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
            }

            if (deactivationSound != null)
            {
                audioSource.PlayOneShot(deactivationSound);
            }

            // Start cooldown
            if (cooldownAfterUse > 0)
            {
                isOnCooldown = true;
                cooldownTimer = cooldownAfterUse;
            }

            UltimateDeactivated?.Invoke();
            onUltimateDeactivated?.Invoke();

            Debug.Log($"[Ultimate] {abilityName} deactivated");
        }

        /// <summary>
        /// Toggle ultimate (activate if ready, deactivate if active).
        /// </summary>
        public void Toggle()
        {
            if (isActive)
            {
                Deactivate();
            }
            else
            {
                Activate();
            }
        }

        /// <summary>
        /// Force set charge to max (debug/cheat).
        /// </summary>
        public void FillCharge()
        {
            SetCharge(maxCharge);
        }

        /// <summary>
        /// Reset ultimate state.
        /// </summary>
        public void Reset()
        {
            if (isActive)
            {
                Deactivate();
            }

            currentCharge = 0;
            isOnCooldown = false;
            cooldownTimer = 0;
            durationTimer = 0;

            ChargeChanged?.Invoke(0);
            onChargeChanged?.Invoke(0);
        }

        /// <summary>
        /// Get formatted description with current values.
        /// </summary>
        public string GetDescription()
        {
            return abilityDescription
                .Replace("{duration}", duration.ToString("F1"))
                .Replace("{damage}", (damageMultiplier * 100).ToString("F0"))
                .Replace("{speed}", (speedMultiplier * 100).ToString("F0"))
                .Replace("{defense}", (defenseMultiplier * 100).ToString("F0"));
        }
    }

    /// <summary>
    /// Ultimate ability UI display.
    /// </summary>
    public class UltimateAbilityUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UltimateAbility ultimate;
        [SerializeField] private UnityEngine.UI.Image chargeBar;
        [SerializeField] private UnityEngine.UI.Image iconImage;
        [SerializeField] private TMPro.TextMeshProUGUI chargeText;
        [SerializeField] private TMPro.TextMeshProUGUI durationText;
        [SerializeField] private GameObject readyIndicator;
        [SerializeField] private GameObject activeIndicator;
        [SerializeField] private UnityEngine.UI.Button activateButton;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.blue;
        [SerializeField] private Color readyColor = Color.yellow;
        [SerializeField] private Color activeColor = Color.red;

        private void Start()
        {
            if (ultimate == null)
            {
                ultimate = GetComponentInParent<UltimateAbility>();
            }

            if (ultimate != null)
            {
                ultimate.ChargeChanged += OnChargeChanged;
                ultimate.UltimateActivated += OnActivated;
                ultimate.UltimateDeactivated += OnDeactivated;

                if (iconImage != null && ultimate.AbilityIcon != null)
                {
                    iconImage.sprite = ultimate.AbilityIcon;
                }

                if (activateButton != null)
                {
                    activateButton.onClick.AddListener(() => ultimate.Activate());
                }

                UpdateUI();
            }
        }

        private void OnDestroy()
        {
            if (ultimate != null)
            {
                ultimate.ChargeChanged -= OnChargeChanged;
                ultimate.UltimateActivated -= OnActivated;
                ultimate.UltimateDeactivated -= OnDeactivated;
            }
        }

        private void Update()
        {
            if (ultimate != null && ultimate.IsActive)
            {
                UpdateDurationText();
            }
        }

        private void OnChargeChanged(float percent)
        {
            UpdateUI();
        }

        private void OnActivated()
        {
            UpdateUI();
        }

        private void OnDeactivated()
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (ultimate == null) return;

            // Update charge bar
            if (chargeBar != null)
            {
                chargeBar.fillAmount = ultimate.ChargePercent;

                if (ultimate.IsActive)
                    chargeBar.color = activeColor;
                else if (ultimate.IsReady)
                    chargeBar.color = readyColor;
                else
                    chargeBar.color = normalColor;
            }

            // Update charge text
            if (chargeText != null)
            {
                chargeText.text = $"{Mathf.RoundToInt(ultimate.ChargePercent * 100)}%";
            }

            // Update indicators
            if (readyIndicator != null)
            {
                readyIndicator.SetActive(ultimate.IsReady);
            }

            if (activeIndicator != null)
            {
                activeIndicator.SetActive(ultimate.IsActive);
            }

            // Update button
            if (activateButton != null)
            {
                activateButton.interactable = ultimate.IsReady;
            }
        }

        private void UpdateDurationText()
        {
            if (durationText != null)
            {
                durationText.text = ultimate.RemainingDuration.ToString("F1") + "s";
            }
        }
    }
}
