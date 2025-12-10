using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System;

namespace UnityVault.World
{
    /// <summary>
    /// Portal and teleport system for scene transitions and instant travel.
    /// </summary>
    public class PortalTeleport : MonoBehaviour
    {
        [Header("Teleport Settings")]
        [SerializeField] private TeleportType teleportType = TeleportType.ToPoint;
        [SerializeField] private Transform destinationPoint;
        [SerializeField] private PortalTeleport linkedPortal;
        [SerializeField] private string destinationScene;
        [SerializeField] private string destinationSpawnId;

        [Header("Activation")]
        [SerializeField] private ActivationType activationType = ActivationType.OnTrigger;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private float cooldown = 1f;

        [Header("Effects")]
        [SerializeField] private bool fadeScreen = true;
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private ParticleSystem portalEffect;
        [SerializeField] private AudioClip teleportSound;

        [Header("Requirements")]
        [SerializeField] private bool requiresKey = false;
        [SerializeField] private string requiredKeyId;
        [SerializeField] private bool oneTimeUse = false;
        [SerializeField] private bool bidirectional = true;

        [Header("Spawn Settings")]
        [SerializeField] private Vector3 spawnOffset = Vector3.zero;
        [SerializeField] private bool preserveVelocity = false;
        [SerializeField] private bool faceExitDirection = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onTeleportStart;
        [SerializeField] private UnityEvent onTeleportComplete;
        [SerializeField] private UnityEvent onTeleportFailed;

        // State
        private bool canTeleport = true;
        private bool playerInRange;
        private Transform playerTransform;
        private float lastTeleportTime;
        private bool isUsed;

        // Events
        public event Action TeleportStarted;
        public event Action TeleportCompleted;
        public event Action TeleportFailed;

        public string SpawnId { get; set; }
        public bool IsActive => canTeleport && !isUsed;

        public enum TeleportType
        {
            ToPoint,
            ToLinkedPortal,
            ToScene
        }

        public enum ActivationType
        {
            OnTrigger,
            OnInteract,
            Manual
        }

        private void Start()
        {
            if (portalEffect != null)
            {
                portalEffect.Play();
            }
        }

        private void Update()
        {
            if (activationType == ActivationType.OnInteract && playerInRange)
            {
                if (Input.GetKeyDown(interactKey))
                {
                    TryTeleport(playerTransform);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;

            playerInRange = true;
            playerTransform = other.transform;

            if (activationType == ActivationType.OnTrigger)
            {
                TryTeleport(other.transform);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;

            playerInRange = false;
            playerTransform = null;
        }

        /// <summary>
        /// Attempt to teleport a transform.
        /// </summary>
        public bool TryTeleport(Transform target)
        {
            if (!CanTeleport(target))
            {
                TeleportFailed?.Invoke();
                onTeleportFailed?.Invoke();
                return false;
            }

            StartCoroutine(TeleportCoroutine(target));
            return true;
        }

        private bool CanTeleport(Transform target)
        {
            if (!canTeleport) return false;
            if (oneTimeUse && isUsed) return false;
            if (Time.time - lastTeleportTime < cooldown) return false;

            if (requiresKey)
            {
                // Would check inventory system
                // if (!InventorySystem.Instance.HasItem(requiredKeyId)) return false;
            }

            // Validate destination
            switch (teleportType)
            {
                case TeleportType.ToPoint:
                    if (destinationPoint == null) return false;
                    break;
                case TeleportType.ToLinkedPortal:
                    if (linkedPortal == null) return false;
                    break;
                case TeleportType.ToScene:
                    if (string.IsNullOrEmpty(destinationScene)) return false;
                    break;
            }

            return true;
        }

        private System.Collections.IEnumerator TeleportCoroutine(Transform target)
        {
            lastTeleportTime = Time.time;
            canTeleport = false;

            TeleportStarted?.Invoke();
            onTeleportStart?.Invoke();

            // Play sound
            if (teleportSound != null)
            {
                AudioSource.PlayClipAtPoint(teleportSound, transform.position);
            }

            // Fade out
            if (fadeScreen)
            {
                // Would integrate with screen fade system
                yield return new WaitForSeconds(fadeDuration);
            }

            // Store velocity
            Vector3 velocity = Vector3.zero;
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (preserveVelocity && rb != null)
            {
                velocity = rb.velocity;
            }

            // Perform teleport
            switch (teleportType)
            {
                case TeleportType.ToPoint:
                    TeleportToPoint(target);
                    break;
                case TeleportType.ToLinkedPortal:
                    TeleportToLinkedPortal(target);
                    break;
                case TeleportType.ToScene:
                    yield return TeleportToScene(target);
                    yield break; // Scene loading handles the rest
            }

            // Restore velocity
            if (preserveVelocity && rb != null)
            {
                rb.velocity = velocity;
            }

            // Fade in
            if (fadeScreen)
            {
                yield return new WaitForSeconds(fadeDuration);
            }

            if (oneTimeUse)
            {
                isUsed = true;
                if (portalEffect != null)
                {
                    portalEffect.Stop();
                }
            }
            else
            {
                canTeleport = true;
            }

            TeleportCompleted?.Invoke();
            onTeleportComplete?.Invoke();

            Debug.Log($"[Portal] Teleported to destination");
        }

        private void TeleportToPoint(Transform target)
        {
            Vector3 destPos = destinationPoint.position + spawnOffset;
            Quaternion destRot = faceExitDirection ? destinationPoint.rotation : target.rotation;

            MoveTarget(target, destPos, destRot);
        }

        private void TeleportToLinkedPortal(Transform target)
        {
            if (linkedPortal == null) return;

            Vector3 destPos = linkedPortal.GetSpawnPosition();
            Quaternion destRot = faceExitDirection ? linkedPortal.GetSpawnRotation() : target.rotation;

            // Disable linked portal briefly to prevent bounce-back
            linkedPortal.SetCooldown(cooldown);

            MoveTarget(target, destPos, destRot);
        }

        private System.Collections.IEnumerator TeleportToScene(Transform target)
        {
            // Store spawn info for destination scene
            PlayerPrefs.SetString("PortalSpawnId", destinationSpawnId);
            PlayerPrefs.Save();

            // Load scene
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(destinationScene);

            while (!loadOp.isDone)
            {
                yield return null;
            }
        }

        private void MoveTarget(Transform target, Vector3 position, Quaternion rotation)
        {
            CharacterController cc = target.GetComponent<CharacterController>();

            if (cc != null)
            {
                cc.enabled = false;
                target.position = position;
                target.rotation = rotation;
                cc.enabled = true;
            }
            else
            {
                target.position = position;
                target.rotation = rotation;
            }
        }

        /// <summary>
        /// Get spawn position for incoming teleports.
        /// </summary>
        public Vector3 GetSpawnPosition()
        {
            return transform.position + transform.TransformDirection(spawnOffset);
        }

        /// <summary>
        /// Get spawn rotation for incoming teleports.
        /// </summary>
        public Quaternion GetSpawnRotation()
        {
            return transform.rotation;
        }

        /// <summary>
        /// Set temporary cooldown.
        /// </summary>
        public void SetCooldown(float duration)
        {
            lastTeleportTime = Time.time;
            StartCoroutine(ResetCooldown(duration));
        }

        private System.Collections.IEnumerator ResetCooldown(float duration)
        {
            canTeleport = false;
            yield return new WaitForSeconds(duration);
            canTeleport = true;
        }

        /// <summary>
        /// Enable/disable portal.
        /// </summary>
        public void SetActive(bool active)
        {
            canTeleport = active;

            if (portalEffect != null)
            {
                if (active)
                    portalEffect.Play();
                else
                    portalEffect.Stop();
            }
        }

        /// <summary>
        /// Link to another portal.
        /// </summary>
        public void LinkPortal(PortalTeleport other)
        {
            linkedPortal = other;
            teleportType = TeleportType.ToLinkedPortal;

            if (bidirectional && other != null)
            {
                other.linkedPortal = this;
                other.teleportType = TeleportType.ToLinkedPortal;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsActive ? Color.cyan : Color.gray;
            Gizmos.DrawWireSphere(transform.position, 1f);

            // Draw destination
            if (teleportType == TeleportType.ToPoint && destinationPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(destinationPoint.position, 0.5f);
                Gizmos.DrawLine(transform.position, destinationPoint.position);
            }
            else if (teleportType == TeleportType.ToLinkedPortal && linkedPortal != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, linkedPortal.transform.position);
            }

            // Spawn offset
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(GetSpawnPosition(), 0.3f);
        }
    }
}
