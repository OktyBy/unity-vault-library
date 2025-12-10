using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.World
{
    /// <summary>
    /// Trap system for spikes, projectiles, and environmental hazards.
    /// </summary>
    public class TrapSystem : MonoBehaviour
    {
        [Header("Trap Type")]
        [SerializeField] private TrapType trapType = TrapType.Damage;
        [SerializeField] private bool isActive = true;

        [Header("Damage")]
        [SerializeField] private float damage = 20f;
        [SerializeField] private float damageInterval = 0.5f;
        [SerializeField] private bool instantKill = false;

        [Header("Trigger")]
        [SerializeField] private TriggerType triggerType = TriggerType.OnEnter;
        [SerializeField] private float triggerDelay = 0f;
        [SerializeField] private float resetDelay = 2f;
        [SerializeField] private bool oneTimeUse = false;
        [SerializeField] private string targetTag = "Player";

        [Header("Activation")]
        [SerializeField] private float activeDuration = 0f; // 0 = continuous
        [SerializeField] private float cycleTime = 3f; // For cycling traps
        [SerializeField] private float activePhase = 1f; // Active portion of cycle

        [Header("Movement (For Moving Traps)")]
        [SerializeField] private bool moves = false;
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool pingPong = false;

        [Header("Projectile (For Projectile Traps)")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private float fireRate = 1f;

        [Header("Visuals")]
        [SerializeField] private GameObject activeVisual;
        [SerializeField] private GameObject inactiveVisual;
        [SerializeField] private ParticleSystem activationEffect;
        [SerializeField] private Animator animator;
        [SerializeField] private string activateTrigger = "Activate";

        [Header("Audio")]
        [SerializeField] private AudioClip activateSound;
        [SerializeField] private AudioClip deactivateSound;
        [SerializeField] private AudioClip damageSound;

        [Header("Events")]
        [SerializeField] private UnityEvent onActivated;
        [SerializeField] private UnityEvent onDeactivated;
        [SerializeField] private UnityEvent<GameObject> onTargetHit;

        // State
        private bool isTriggered;
        private bool isUsed;
        private float cycleTimer;
        private float damageTimer;
        private float fireTimer;
        private int currentWaypoint;
        private int waypointDirection = 1;
        private AudioSource audioSource;

        // Events
        public event Action Activated;
        public event Action Deactivated;
        public event Action<GameObject> TargetHit;

        public bool IsActive => isActive && !isUsed;
        public bool IsTriggered => isTriggered;

        public enum TrapType
        {
            Damage,
            Projectile,
            Push,
            Slow,
            Stun,
            Teleport
        }

        public enum TriggerType
        {
            OnEnter,
            OnStay,
            Timed,
            Manual,
            Pressure
        }

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
            if (!isActive || isUsed) return;

            // Cycling traps
            if (triggerType == TriggerType.Timed)
            {
                UpdateCycle();
            }

            // Moving traps
            if (moves && waypoints != null && waypoints.Length > 0)
            {
                UpdateMovement();
            }

            // Projectile traps
            if (trapType == TrapType.Projectile && isTriggered)
            {
                UpdateProjectile();
            }

            UpdateVisuals();
        }

        private void UpdateCycle()
        {
            cycleTimer += Time.deltaTime;

            if (cycleTimer >= cycleTime)
            {
                cycleTimer = 0;
            }

            bool shouldBeActive = cycleTimer < activePhase;

            if (shouldBeActive && !isTriggered)
            {
                Activate();
            }
            else if (!shouldBeActive && isTriggered)
            {
                Deactivate();
            }
        }

        private void UpdateMovement()
        {
            if (waypoints.Length == 0) return;

            Transform target = waypoints[currentWaypoint];
            transform.position = Vector3.MoveTowards(transform.position, target.position, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, target.position) < 0.1f)
            {
                if (pingPong)
                {
                    if (currentWaypoint == 0) waypointDirection = 1;
                    else if (currentWaypoint == waypoints.Length - 1) waypointDirection = -1;
                    currentWaypoint += waypointDirection;
                }
                else if (loop)
                {
                    currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
                }
                else if (currentWaypoint < waypoints.Length - 1)
                {
                    currentWaypoint++;
                }
            }
        }

        private void UpdateProjectile()
        {
            fireTimer += Time.deltaTime;

            if (fireTimer >= 1f / fireRate)
            {
                fireTimer = 0;
                FireProjectile();
            }
        }

        private void UpdateVisuals()
        {
            if (activeVisual != null)
            {
                activeVisual.SetActive(isTriggered);
            }

            if (inactiveVisual != null)
            {
                inactiveVisual.SetActive(!isTriggered);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsActive) return;
            if (!other.CompareTag(targetTag)) return;

            if (triggerType == TriggerType.OnEnter || triggerType == TriggerType.Pressure)
            {
                StartCoroutine(TriggerWithDelay(other.gameObject));
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsActive || !isTriggered) return;
            if (!other.CompareTag(targetTag)) return;

            if (triggerType == TriggerType.OnStay || triggerType == TriggerType.OnEnter)
            {
                ApplyTrapEffect(other.gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(targetTag)) return;

            if (triggerType == TriggerType.Pressure)
            {
                Deactivate();
            }
        }

        private System.Collections.IEnumerator TriggerWithDelay(GameObject target)
        {
            if (triggerDelay > 0)
            {
                yield return new WaitForSeconds(triggerDelay);
            }

            Activate();
            ApplyTrapEffect(target);

            if (activeDuration > 0)
            {
                yield return new WaitForSeconds(activeDuration);
                Deactivate();
            }

            if (oneTimeUse)
            {
                isUsed = true;
            }
            else if (resetDelay > 0)
            {
                yield return new WaitForSeconds(resetDelay);
                if (!isUsed)
                {
                    // Ready for next trigger
                }
            }
        }

        private void Activate()
        {
            if (isTriggered) return;

            isTriggered = true;

            // Animation
            if (animator != null)
            {
                animator.SetTrigger(activateTrigger);
            }

            // Effect
            if (activationEffect != null)
            {
                activationEffect.Play();
            }

            // Sound
            if (activateSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(activateSound);
            }

            Activated?.Invoke();
            onActivated?.Invoke();

            Debug.Log($"[Trap] Activated: {gameObject.name}");
        }

        private void Deactivate()
        {
            if (!isTriggered) return;

            isTriggered = false;

            // Sound
            if (deactivateSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(deactivateSound);
            }

            Deactivated?.Invoke();
            onDeactivated?.Invoke();
        }

        private void ApplyTrapEffect(GameObject target)
        {
            // Damage timer for continuous damage
            damageTimer += Time.deltaTime;
            if (damageTimer < damageInterval) return;
            damageTimer = 0;

            switch (trapType)
            {
                case TrapType.Damage:
                    ApplyDamage(target);
                    break;
                case TrapType.Push:
                    ApplyPush(target);
                    break;
                case TrapType.Slow:
                    ApplySlow(target);
                    break;
                case TrapType.Stun:
                    ApplyStun(target);
                    break;
                case TrapType.Teleport:
                    ApplyTeleport(target);
                    break;
            }

            // Sound
            if (damageSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(damageSound);
            }

            TargetHit?.Invoke(target);
            onTargetHit?.Invoke(target);
        }

        private void ApplyDamage(GameObject target)
        {
            var health = target.GetComponent<Core.HealthSystem>();
            if (health != null)
            {
                if (instantKill)
                {
                    health.TakeDamage(health.MaxHealth);
                }
                else
                {
                    health.TakeDamage(damage);
                }
            }
        }

        private void ApplyPush(GameObject target)
        {
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 pushDir = (target.transform.position - transform.position).normalized;
                rb.AddForce(pushDir * damage, ForceMode.Impulse);
            }
        }

        private void ApplySlow(GameObject target)
        {
            // Would integrate with status effect system
            Debug.Log($"[Trap] Slowed {target.name}");
        }

        private void ApplyStun(GameObject target)
        {
            // Would integrate with status effect system
            Debug.Log($"[Trap] Stunned {target.name}");
        }

        private void ApplyTeleport(GameObject target)
        {
            if (waypoints != null && waypoints.Length > 0)
            {
                target.transform.position = waypoints[0].position;
            }
        }

        private void FireProjectile()
        {
            if (projectilePrefab == null || firePoint == null) return;

            GameObject proj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

            Rigidbody rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = firePoint.forward * projectileSpeed;
            }

            // Setup projectile damage
            var projDamage = proj.GetComponent<TrapProjectile>();
            if (projDamage != null)
            {
                projDamage.Setup(damage, targetTag);
            }
        }

        // Public methods
        public void TriggerManually()
        {
            if (triggerType == TriggerType.Manual)
            {
                StartCoroutine(TriggerWithDelay(null));
            }
        }

        public void SetActive(bool active)
        {
            isActive = active;
            if (!active)
            {
                Deactivate();
            }
        }

        public void Reset()
        {
            isUsed = false;
            isTriggered = false;
            cycleTimer = 0;
            damageTimer = 0;
            currentWaypoint = 0;
            waypointDirection = 1;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = isTriggered ? Color.red : (IsActive ? Color.yellow : Color.gray);
            Gizmos.DrawWireCube(transform.position, Vector3.one);

            // Waypoints
            if (moves && waypoints != null)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < waypoints.Length; i++)
                {
                    if (waypoints[i] == null) continue;
                    Gizmos.DrawWireSphere(waypoints[i].position, 0.3f);
                    if (i > 0 && waypoints[i - 1] != null)
                    {
                        Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
                    }
                }
            }

            // Fire direction
            if (trapType == TrapType.Projectile && firePoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(firePoint.position, firePoint.forward * 3f);
            }
        }
    }

    /// <summary>
    /// Component for trap projectiles.
    /// </summary>
    public class TrapProjectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 5f;
        private float damage;
        private string targetTag;

        public void Setup(float dmg, string tag)
        {
            damage = dmg;
            targetTag = tag;
            Destroy(gameObject, lifetime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(targetTag))
            {
                var health = other.GetComponent<Core.HealthSystem>();
                if (health != null)
                {
                    health.TakeDamage(damage);
                }
                Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            Destroy(gameObject);
        }
    }
}
