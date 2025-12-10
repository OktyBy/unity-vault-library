using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Multiplayer
{
    /// <summary>
    /// Network player component for multiplayer synchronization.
    /// Note: This is a framework - integrate with your networking solution (Photon, Mirror, Netcode, etc.)
    /// </summary>
    public class NetworkPlayer : MonoBehaviour
    {
        [Header("Player Info")]
        [SerializeField] private string playerId;
        [SerializeField] private string playerName;
        [SerializeField] private int playerIndex;
        [SerializeField] private bool isLocalPlayer;
        [SerializeField] private bool isHost;

        [Header("Sync Settings")]
        [SerializeField] private float syncRate = 15f; // Updates per second
        [SerializeField] private float positionThreshold = 0.01f;
        [SerializeField] private float rotationThreshold = 1f;
        [SerializeField] private bool syncPosition = true;
        [SerializeField] private bool syncRotation = true;
        [SerializeField] private bool syncScale = false;

        [Header("Interpolation")]
        [SerializeField] private bool useInterpolation = true;
        [SerializeField] private float interpolationSpeed = 10f;
        [SerializeField] private float teleportThreshold = 5f;

        [Header("Components")]
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Rigidbody rigidBody;

        [Header("Events")]
        [SerializeField] private UnityEvent onSpawned;
        [SerializeField] private UnityEvent onDespawned;
        [SerializeField] private UnityEvent<string> onNameChanged;

        // Network state
        private Vector3 networkPosition;
        private Quaternion networkRotation;
        private Vector3 networkScale;
        private Vector3 networkVelocity;

        // Interpolation
        private Vector3 lastSentPosition;
        private Quaternion lastSentRotation;
        private float lastSyncTime;

        // Animation sync
        private Dictionary<string, float> animatorFloats = new Dictionary<string, float>();
        private Dictionary<string, bool> animatorBools = new Dictionary<string, bool>();
        private Dictionary<string, int> animatorInts = new Dictionary<string, int>();

        // Events
        public event Action Spawned;
        public event Action Despawned;
        public event Action<NetworkPlayerState> StateReceived;
        public event Action<string, object> RPCReceived;

        public string PlayerId => playerId;
        public string PlayerName => playerName;
        public int PlayerIndex => playerIndex;
        public bool IsLocalPlayer => isLocalPlayer;
        public bool IsHost => isHost;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (rigidBody == null)
                rigidBody = GetComponent<Rigidbody>();

            networkPosition = transform.position;
            networkRotation = transform.rotation;
            networkScale = transform.localScale;
        }

        private void Start()
        {
            if (isLocalPlayer)
            {
                // Enable local player components
                EnableLocalComponents();
            }
            else
            {
                // Disable components that should only run locally
                DisableRemoteComponents();
            }

            Spawned?.Invoke();
            onSpawned?.Invoke();
        }

        private void Update()
        {
            if (isLocalPlayer)
            {
                // Send updates at sync rate
                if (Time.time - lastSyncTime >= 1f / syncRate)
                {
                    SendState();
                    lastSyncTime = Time.time;
                }
            }
            else
            {
                // Interpolate to network state
                if (useInterpolation)
                {
                    InterpolateState();
                }
            }
        }

        private void EnableLocalComponents()
        {
            // Enable input, camera, etc.
            var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null) playerInput.enabled = true;
        }

        private void DisableRemoteComponents()
        {
            // Disable components that shouldn't run on remote players
            if (characterController != null)
                characterController.enabled = false;

            var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null) playerInput.enabled = false;

            // Disable audio listener
            var audioListener = GetComponent<AudioListener>();
            if (audioListener != null) audioListener.enabled = false;
        }

        /// <summary>
        /// Initialize player.
        /// </summary>
        public void Initialize(string id, string name, int index, bool isLocal, bool isHostPlayer)
        {
            playerId = id;
            playerName = name;
            playerIndex = index;
            isLocalPlayer = isLocal;
            isHost = isHostPlayer;

            gameObject.name = $"Player_{name}";
        }

        /// <summary>
        /// Send current state to network.
        /// </summary>
        public void SendState()
        {
            if (!isLocalPlayer) return;

            bool positionChanged = Vector3.Distance(transform.position, lastSentPosition) > positionThreshold;
            bool rotationChanged = Quaternion.Angle(transform.rotation, lastSentRotation) > rotationThreshold;

            if (!positionChanged && !rotationChanged) return;

            NetworkPlayerState state = new NetworkPlayerState
            {
                playerId = playerId,
                timestamp = Time.time,
                position = transform.position,
                rotation = transform.rotation,
                scale = transform.localScale,
                velocity = rigidBody != null ? rigidBody.velocity : Vector3.zero
            };

            // Collect animator state
            if (animator != null)
            {
                state.animatorFloats = new Dictionary<string, float>(animatorFloats);
                state.animatorBools = new Dictionary<string, bool>(animatorBools);
                state.animatorInts = new Dictionary<string, int>(animatorInts);
            }

            // TODO: Send state over network
            // NetworkManager.Instance.SendPlayerState(state);

            lastSentPosition = transform.position;
            lastSentRotation = transform.rotation;
        }

        /// <summary>
        /// Receive state from network.
        /// </summary>
        public void ReceiveState(NetworkPlayerState state)
        {
            if (isLocalPlayer) return;

            // Check for teleport
            float distance = Vector3.Distance(transform.position, state.position);
            if (distance > teleportThreshold)
            {
                // Teleport immediately
                transform.position = state.position;
                transform.rotation = state.rotation;
            }
            else
            {
                // Set network target for interpolation
                networkPosition = state.position;
                networkRotation = state.rotation;
                networkScale = state.scale;
                networkVelocity = state.velocity;
            }

            // Apply animator state
            if (animator != null && state.animatorFloats != null)
            {
                foreach (var kvp in state.animatorFloats)
                {
                    animator.SetFloat(kvp.Key, kvp.Value);
                }
                foreach (var kvp in state.animatorBools)
                {
                    animator.SetBool(kvp.Key, kvp.Value);
                }
                foreach (var kvp in state.animatorInts)
                {
                    animator.SetInteger(kvp.Key, kvp.Value);
                }
            }

            StateReceived?.Invoke(state);
        }

        private void InterpolateState()
        {
            if (syncPosition)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    networkPosition,
                    Time.deltaTime * interpolationSpeed
                );
            }

            if (syncRotation)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    networkRotation,
                    Time.deltaTime * interpolationSpeed
                );
            }

            if (syncScale)
            {
                transform.localScale = Vector3.Lerp(
                    transform.localScale,
                    networkScale,
                    Time.deltaTime * interpolationSpeed
                );
            }
        }

        /// <summary>
        /// Set animator float (synced).
        /// </summary>
        public void SetAnimatorFloat(string name, float value)
        {
            if (animator != null)
            {
                animator.SetFloat(name, value);
            }
            animatorFloats[name] = value;
        }

        /// <summary>
        /// Set animator bool (synced).
        /// </summary>
        public void SetAnimatorBool(string name, bool value)
        {
            if (animator != null)
            {
                animator.SetBool(name, value);
            }
            animatorBools[name] = value;
        }

        /// <summary>
        /// Set animator int (synced).
        /// </summary>
        public void SetAnimatorInt(string name, int value)
        {
            if (animator != null)
            {
                animator.SetInteger(name, value);
            }
            animatorInts[name] = value;
        }

        /// <summary>
        /// Set animator trigger (RPC).
        /// </summary>
        public void SetAnimatorTrigger(string name)
        {
            if (animator != null)
            {
                animator.SetTrigger(name);
            }

            // Send as RPC
            SendRPC("SetTrigger", name);
        }

        /// <summary>
        /// Send RPC to all clients.
        /// </summary>
        public void SendRPC(string methodName, params object[] args)
        {
            // TODO: Implement actual RPC through network
            // NetworkManager.Instance.SendRPC(playerId, methodName, args);
        }

        /// <summary>
        /// Receive RPC from network.
        /// </summary>
        public void ReceiveRPC(string methodName, object[] args)
        {
            switch (methodName)
            {
                case "SetTrigger":
                    if (animator != null && args.Length > 0)
                    {
                        animator.SetTrigger(args[0].ToString());
                    }
                    break;
            }

            RPCReceived?.Invoke(methodName, args);
        }

        /// <summary>
        /// Set player name.
        /// </summary>
        public void SetPlayerName(string name)
        {
            playerName = name;
            gameObject.name = $"Player_{name}";

            onNameChanged?.Invoke(name);
        }

        /// <summary>
        /// Teleport player.
        /// </summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.position = position;
            transform.rotation = rotation;
            networkPosition = position;
            networkRotation = rotation;

            if (characterController != null)
            {
                characterController.enabled = true;
            }
        }

        /// <summary>
        /// Called when player is despawned.
        /// </summary>
        public void OnDespawn()
        {
            Despawned?.Invoke();
            onDespawned?.Invoke();
        }

        private void OnDestroy()
        {
            OnDespawn();
        }
    }

    [Serializable]
    public class NetworkPlayerState
    {
        public string playerId;
        public float timestamp;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Vector3 velocity;
        public Dictionary<string, float> animatorFloats;
        public Dictionary<string, bool> animatorBools;
        public Dictionary<string, int> animatorInts;
    }

    /// <summary>
    /// Network player spawner.
    /// </summary>
    public class NetworkPlayerSpawner : MonoBehaviour
    {
        public static NetworkPlayerSpawner Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private GameObject localPlayerPrefab;
        [SerializeField] private GameObject remotePlayerPrefab;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        private Dictionary<string, NetworkPlayer> players = new Dictionary<string, NetworkPlayer>();

        public event Action<NetworkPlayer> PlayerSpawned;
        public event Action<NetworkPlayer> PlayerDespawned;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public NetworkPlayer SpawnPlayer(string playerId, string playerName, int playerIndex, bool isLocal, bool isHost)
        {
            if (players.ContainsKey(playerId))
            {
                Debug.LogWarning($"[NetworkPlayer] Player already exists: {playerId}");
                return players[playerId];
            }

            GameObject prefab = isLocal ? localPlayerPrefab : remotePlayerPrefab;
            Vector3 spawnPos = GetSpawnPosition(playerIndex);

            GameObject playerObj = Instantiate(prefab, spawnPos, Quaternion.identity);
            NetworkPlayer player = playerObj.GetComponent<NetworkPlayer>();

            if (player == null)
            {
                player = playerObj.AddComponent<NetworkPlayer>();
            }

            player.Initialize(playerId, playerName, playerIndex, isLocal, isHost);
            players[playerId] = player;

            PlayerSpawned?.Invoke(player);

            Debug.Log($"[NetworkPlayer] Spawned: {playerName} (Local: {isLocal})");

            return player;
        }

        public void DespawnPlayer(string playerId)
        {
            if (players.TryGetValue(playerId, out NetworkPlayer player))
            {
                players.Remove(playerId);
                PlayerDespawned?.Invoke(player);
                Destroy(player.gameObject);

                Debug.Log($"[NetworkPlayer] Despawned: {player.PlayerName}");
            }
        }

        private Vector3 GetSpawnPosition(int index)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return Vector3.zero;
            }

            int spawnIndex = index % spawnPoints.Length;
            return spawnPoints[spawnIndex].position;
        }

        public NetworkPlayer GetPlayer(string playerId)
        {
            return players.TryGetValue(playerId, out NetworkPlayer player) ? player : null;
        }

        public List<NetworkPlayer> GetAllPlayers()
        {
            return new List<NetworkPlayer>(players.Values);
        }

        public void DespawnAll()
        {
            foreach (var player in new List<NetworkPlayer>(players.Values))
            {
                DespawnPlayer(player.PlayerId);
            }
        }
    }
}
