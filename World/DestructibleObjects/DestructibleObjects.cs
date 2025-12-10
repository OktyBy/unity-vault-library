using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.World
{
    /// <summary>
    /// Destructible object system for breakable objects.
    /// </summary>
    public class DestructibleObject : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;
        [SerializeField] private bool invincible = false;

        [Header("Destruction")]
        [SerializeField] private DestructionType destructionType = DestructionType.Replace;
        [SerializeField] private GameObject destroyedPrefab;
        [SerializeField] private GameObject[] debrisPrefabs;
        [SerializeField] private int debrisCount = 5;
        [SerializeField] private float debrisForce = 100f;
        [SerializeField] private float debrisLifetime = 5f;

        [Header("Damage Stages")]
        [SerializeField] private bool useDamageStages = true;
        [SerializeField] private DamageStage[] damageStages;

        [Header("Effects")]
        [SerializeField] private ParticleSystem hitEffect;
        [SerializeField] private ParticleSystem destroyEffect;
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip destroySound;

        [Header("Loot")]
        [SerializeField] private GameObject[] lootPrefabs;
        [SerializeField] private float lootDropChance = 1f;
        [SerializeField] private Vector3 lootSpawnOffset;

        [Header("Events")]
        [SerializeField] private UnityEvent<float> onDamaged;
        [SerializeField] private UnityEvent onDestroyed;
        [SerializeField] private UnityEvent<DamageStage> onStageChanged;

        // State
        private int currentStageIndex = -1;
        private bool isDestroyed;
        private MeshRenderer meshRenderer;
        private Collider objectCollider;
        private AudioSource audioSource;

        // Events
        public event Action<float, float> Damaged; // current, max
        public event Action Destroyed;
        public event Action<DamageStage> StageChanged;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthPercent => currentHealth / maxHealth;
        public bool IsDestroyed => isDestroyed;

        private void Awake()
        {
            currentHealth = maxHealth;
            meshRenderer = GetComponent<MeshRenderer>();
            objectCollider = GetComponent<Collider>();
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        /// <summary>
        /// Apply damage to the object.
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (isDestroyed || invincible) return;

            currentHealth = Mathf.Max(0, currentHealth - damage);

            // Effects
            if (hitEffect != null)
            {
                hitEffect.Play();
            }

            if (hitSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(hitSound);
            }

            Damaged?.Invoke(currentHealth, maxHealth);
            onDamaged?.Invoke(currentHealth);

            // Check damage stages
            if (useDamageStages)
            {
                UpdateDamageStage();
            }

            // Check destruction
            if (currentHealth <= 0)
            {
                Destroy();
            }
        }

        /// <summary>
        /// Apply damage at specific point.
        /// </summary>
        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection)
        {
            TakeDamage(damage);

            // Spawn hit effect at point
            if (hitEffect != null)
            {
                hitEffect.transform.position = hitPoint;
                hitEffect.transform.rotation = Quaternion.LookRotation(hitDirection);
            }
        }

        private void UpdateDamageStage()
        {
            if (damageStages == null || damageStages.Length == 0) return;

            float healthPercent = HealthPercent;

            for (int i = damageStages.Length - 1; i >= 0; i--)
            {
                if (healthPercent <= damageStages[i].healthThreshold)
                {
                    if (currentStageIndex != i)
                    {
                        currentStageIndex = i;
                        ApplyDamageStage(damageStages[i]);
                    }
                    return;
                }
            }
        }

        private void ApplyDamageStage(DamageStage stage)
        {
            // Change material
            if (stage.material != null && meshRenderer != null)
            {
                meshRenderer.material = stage.material;
            }

            // Change mesh
            if (stage.mesh != null)
            {
                var meshFilter = GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    meshFilter.mesh = stage.mesh;
                }
            }

            // Play effect
            if (stage.transitionEffect != null)
            {
                Instantiate(stage.transitionEffect, transform.position, Quaternion.identity);
            }

            // Play sound
            if (stage.transitionSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(stage.transitionSound);
            }

            StageChanged?.Invoke(stage);
            onStageChanged?.Invoke(stage);

            Debug.Log($"[Destructible] {gameObject.name} reached damage stage: {stage.stageName}");
        }

        /// <summary>
        /// Destroy the object.
        /// </summary>
        public void Destroy()
        {
            if (isDestroyed) return;
            isDestroyed = true;

            // Effects
            if (destroyEffect != null)
            {
                Instantiate(destroyEffect, transform.position, Quaternion.identity);
            }

            if (destroySound != null)
            {
                AudioSource.PlayClipAtPoint(destroySound, transform.position);
            }

            // Handle destruction type
            switch (destructionType)
            {
                case DestructionType.Replace:
                    ReplaceWithDestroyed();
                    break;
                case DestructionType.SpawnDebris:
                    SpawnDebris();
                    break;
                case DestructionType.DisableRenderer:
                    DisableVisuals();
                    break;
                case DestructionType.Destroy:
                    // Just destroy
                    break;
            }

            // Spawn loot
            if (lootPrefabs != null && lootPrefabs.Length > 0 && UnityEngine.Random.value <= lootDropChance)
            {
                SpawnLoot();
            }

            Destroyed?.Invoke();
            onDestroyed?.Invoke();

            Debug.Log($"[Destructible] {gameObject.name} destroyed");

            if (destructionType == DestructionType.Destroy)
            {
                Destroy(gameObject);
            }
        }

        private void ReplaceWithDestroyed()
        {
            if (destroyedPrefab != null)
            {
                Instantiate(destroyedPrefab, transform.position, transform.rotation);
            }

            Destroy(gameObject);
        }

        private void SpawnDebris()
        {
            if (debrisPrefabs == null || debrisPrefabs.Length == 0)
            {
                Destroy(gameObject);
                return;
            }

            for (int i = 0; i < debrisCount; i++)
            {
                GameObject prefab = debrisPrefabs[UnityEngine.Random.Range(0, debrisPrefabs.Length)];
                GameObject debris = Instantiate(prefab, transform.position, UnityEngine.Random.rotation);

                Rigidbody rb = debris.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 randomDir = UnityEngine.Random.onUnitSphere;
                    rb.AddForce(randomDir * debrisForce, ForceMode.Impulse);
                    rb.AddTorque(UnityEngine.Random.onUnitSphere * debrisForce * 0.5f, ForceMode.Impulse);
                }

                Destroy(debris, debrisLifetime);
            }

            Destroy(gameObject);
        }

        private void DisableVisuals()
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }

            if (objectCollider != null)
            {
                objectCollider.enabled = false;
            }
        }

        private void SpawnLoot()
        {
            foreach (var lootPrefab in lootPrefabs)
            {
                if (lootPrefab == null) continue;

                Vector3 spawnPos = transform.position + lootSpawnOffset;
                spawnPos += UnityEngine.Random.insideUnitSphere * 0.5f;

                Instantiate(lootPrefab, spawnPos, Quaternion.identity);
            }
        }

        /// <summary>
        /// Repair the object.
        /// </summary>
        public void Repair(float amount)
        {
            if (isDestroyed) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

            if (useDamageStages)
            {
                UpdateDamageStage();
            }
        }

        /// <summary>
        /// Fully restore the object.
        /// </summary>
        public void FullRestore()
        {
            currentHealth = maxHealth;
            isDestroyed = false;
            currentStageIndex = -1;

            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
            }

            if (objectCollider != null)
            {
                objectCollider.enabled = true;
            }
        }

        /// <summary>
        /// Set invincibility.
        /// </summary>
        public void SetInvincible(bool value)
        {
            invincible = value;
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Optional: Take damage from physics impact
            if (collision.relativeVelocity.magnitude > 10f)
            {
                float impactDamage = collision.relativeVelocity.magnitude * 2f;
                TakeDamage(impactDamage, collision.contacts[0].point, collision.contacts[0].normal);
            }
        }
    }

    public enum DestructionType
    {
        Destroy,
        Replace,
        SpawnDebris,
        DisableRenderer
    }

    [Serializable]
    public class DamageStage
    {
        public string stageName;
        [Range(0f, 1f)]
        public float healthThreshold = 0.5f;
        public Material material;
        public Mesh mesh;
        public GameObject transitionEffect;
        public AudioClip transitionSound;
    }

    /// <summary>
    /// Manager for tracking destructible objects.
    /// </summary>
    public class DestructibleManager : MonoBehaviour
    {
        public static DestructibleManager Instance { get; private set; }

        private List<DestructibleObject> allDestructibles = new List<DestructibleObject>();
        private int destroyedCount;

        public int TotalCount => allDestructibles.Count;
        public int DestroyedCount => destroyedCount;
        public int RemainingCount => TotalCount - destroyedCount;

        public event Action<DestructibleObject> ObjectDestroyed;
        public event Action AllDestroyed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void Register(DestructibleObject obj)
        {
            if (obj == null || allDestructibles.Contains(obj)) return;

            allDestructibles.Add(obj);
            obj.Destroyed += () => OnObjectDestroyed(obj);
        }

        public void Unregister(DestructibleObject obj)
        {
            allDestructibles.Remove(obj);
        }

        private void OnObjectDestroyed(DestructibleObject obj)
        {
            destroyedCount++;
            ObjectDestroyed?.Invoke(obj);

            if (destroyedCount >= TotalCount)
            {
                AllDestroyed?.Invoke();
            }
        }

        public void DestroyAll()
        {
            foreach (var obj in new List<DestructibleObject>(allDestructibles))
            {
                if (!obj.IsDestroyed)
                {
                    obj.Destroy();
                }
            }
        }

        public void RepairAll()
        {
            foreach (var obj in allDestructibles)
            {
                obj.FullRestore();
            }
            destroyedCount = 0;
        }
    }
}
