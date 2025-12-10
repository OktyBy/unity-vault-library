using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace UnityVault.World
{
    /// <summary>
    /// Checkpoint and respawn system.
    /// </summary>
    public class CheckpointManager : MonoBehaviour
    {
        public static CheckpointManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private Transform defaultSpawnPoint;
        [SerializeField] private float respawnDelay = 2f;
        [SerializeField] private bool autoRespawn = true;

        [Header("Events")]
        [SerializeField] private UnityEvent<Checkpoint> onCheckpointReached;
        [SerializeField] private UnityEvent onRespawn;

        private Checkpoint currentCheckpoint;
        private List<Checkpoint> activatedCheckpoints = new List<Checkpoint>();

        public Checkpoint CurrentCheckpoint => currentCheckpoint;
        public Vector3 RespawnPosition => currentCheckpoint != null ?
            currentCheckpoint.SpawnPosition : (defaultSpawnPoint != null ?
            defaultSpawnPoint.position : Vector3.zero);
        public Quaternion RespawnRotation => currentCheckpoint != null ?
            currentCheckpoint.SpawnRotation : (defaultSpawnPoint != null ?
            defaultSpawnPoint.rotation : Quaternion.identity);

        public event System.Action<Checkpoint> CheckpointReached;
        public event System.Action PlayerRespawned;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void SetCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint == null) return;
            if (checkpoint == currentCheckpoint) return;

            // Deactivate previous
            currentCheckpoint?.SetActive(false);

            currentCheckpoint = checkpoint;
            currentCheckpoint.SetActive(true);

            if (!activatedCheckpoints.Contains(checkpoint))
            {
                activatedCheckpoints.Add(checkpoint);
            }

            CheckpointReached?.Invoke(checkpoint);
            onCheckpointReached?.Invoke(checkpoint);

            Debug.Log($"[Checkpoint] Reached: {checkpoint.name}");
        }

        public void RespawnPlayer(GameObject player)
        {
            if (player == null) return;

            if (autoRespawn)
            {
                StartCoroutine(RespawnRoutine(player));
            }
            else
            {
                DoRespawn(player);
            }
        }

        private System.Collections.IEnumerator RespawnRoutine(GameObject player)
        {
            // Disable player temporarily
            player.SetActive(false);

            yield return new WaitForSeconds(respawnDelay);

            DoRespawn(player);
            player.SetActive(true);
        }

        private void DoRespawn(GameObject player)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            player.transform.position = RespawnPosition;
            player.transform.rotation = RespawnRotation;

            if (controller != null)
            {
                controller.enabled = true;
            }

            // Reset velocity if has rigidbody
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Heal player if has health
            var health = player.GetComponent<UnityVault.Core.HealthSystem>();
            health?.Revive();

            PlayerRespawned?.Invoke();
            onRespawn?.Invoke();

            Debug.Log("[Checkpoint] Player respawned");
        }

        public void ResetAllCheckpoints()
        {
            foreach (var checkpoint in activatedCheckpoints)
            {
                checkpoint.Reset();
            }
            activatedCheckpoints.Clear();
            currentCheckpoint = null;
        }
    }

    /// <summary>
    /// Individual checkpoint trigger.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        [Header("Spawn Point")]
        [SerializeField] private Transform spawnPoint;

        [Header("Visual")]
        [SerializeField] private GameObject inactiveVisual;
        [SerializeField] private GameObject activeVisual;

        [Header("Audio")]
        [SerializeField] private AudioClip activationSound;

        [Header("Events")]
        [SerializeField] private UnityEvent onActivated;

        private bool isActivated;

        public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;
        public Quaternion SpawnRotation => spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        public bool IsActivated => isActivated;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;

            UpdateVisuals();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (isActivated) return;

            Activate();
        }

        private void Activate()
        {
            isActivated = true;
            CheckpointManager.Instance?.SetCheckpoint(this);

            if (activationSound != null)
            {
                AudioSource.PlayClipAtPoint(activationSound, transform.position);
            }

            onActivated?.Invoke();
            UpdateVisuals();
        }

        public void SetActive(bool active)
        {
            isActivated = active;
            UpdateVisuals();
        }

        public void Reset()
        {
            isActivated = false;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (inactiveVisual != null) inactiveVisual.SetActive(!isActivated);
            if (activeVisual != null) activeVisual.SetActive(isActivated);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = isActivated ? Color.green : Color.yellow;

            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.DrawWireCube(transform.position + box.center, box.size);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, 1f);
            }

            // Draw spawn point
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(spawnPos, 0.5f);
            Gizmos.DrawLine(spawnPos, spawnPos + (spawnPoint != null ? spawnPoint.forward : transform.forward));
        }
    }
}
