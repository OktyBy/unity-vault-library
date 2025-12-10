using UnityEngine;
using UnityEngine.UI;

namespace UnityVault.UI
{
    /// <summary>
    /// Health bar UI component with smooth animations.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image damageFlashImage;
        [SerializeField] private Text healthText;

        [Header("Settings")]
        [SerializeField] private bool showText = true;
        [SerializeField] private bool usePercentage = false;
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private bool flashOnDamage = true;
        [SerializeField] private float flashDuration = 0.2f;

        [Header("Colors")]
        [SerializeField] private Gradient healthGradient;
        [SerializeField] private Color damageFlashColor = new Color(1f, 0f, 0f, 0.5f);

        // State
        private float currentHealth;
        private float maxHealth;
        private float displayedHealth;
        private float flashTimer;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthPercent => maxHealth > 0 ? currentHealth / maxHealth : 0f;

        private void Start()
        {
            if (healthGradient == null || healthGradient.colorKeys.Length == 0)
            {
                healthGradient = new Gradient();
                healthGradient.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(Color.red, 0f),
                        new GradientColorKey(Color.yellow, 0.5f),
                        new GradientColorKey(Color.green, 1f)
                    },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
            }

            if (damageFlashImage != null)
            {
                damageFlashImage.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, 0f);
            }
        }

        private void Update()
        {
            // Smooth health bar
            if (!Mathf.Approximately(displayedHealth, currentHealth))
            {
                displayedHealth = Mathf.MoveTowards(displayedHealth, currentHealth, maxHealth * smoothSpeed * Time.deltaTime);
                UpdateVisuals();
            }

            // Flash fade out
            if (flashTimer > 0)
            {
                flashTimer -= Time.deltaTime;
                if (damageFlashImage != null)
                {
                    float alpha = Mathf.Lerp(0f, damageFlashColor.a, flashTimer / flashDuration);
                    damageFlashImage.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, alpha);
                }
            }
        }

        public void Initialize(float maxHealth, float currentHealth = -1)
        {
            this.maxHealth = maxHealth;
            this.currentHealth = currentHealth < 0 ? maxHealth : currentHealth;
            this.displayedHealth = this.currentHealth;
            UpdateVisuals();
        }

        public void SetHealth(float health)
        {
            float previousHealth = currentHealth;
            currentHealth = Mathf.Clamp(health, 0f, maxHealth);

            if (currentHealth < previousHealth && flashOnDamage)
            {
                TriggerDamageFlash();
            }
        }

        public void SetMaxHealth(float max)
        {
            maxHealth = max;
            currentHealth = Mathf.Min(currentHealth, maxHealth);
            UpdateVisuals();
        }

        public void TakeDamage(float damage)
        {
            SetHealth(currentHealth - damage);
        }

        public void Heal(float amount)
        {
            SetHealth(currentHealth + amount);
        }

        private void UpdateVisuals()
        {
            float percent = maxHealth > 0 ? displayedHealth / maxHealth : 0f;

            if (fillImage != null)
            {
                fillImage.fillAmount = percent;
                fillImage.color = healthGradient.Evaluate(percent);
            }

            if (healthText != null && showText)
            {
                if (usePercentage)
                {
                    healthText.text = $"{Mathf.RoundToInt(percent * 100)}%";
                }
                else
                {
                    healthText.text = $"{Mathf.RoundToInt(displayedHealth)}/{Mathf.RoundToInt(maxHealth)}";
                }
            }
        }

        private void TriggerDamageFlash()
        {
            flashTimer = flashDuration;
            if (damageFlashImage != null)
            {
                damageFlashImage.color = damageFlashColor;
            }
        }
    }
}
