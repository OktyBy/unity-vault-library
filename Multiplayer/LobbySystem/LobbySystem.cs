using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Multiplayer
{
    /// <summary>
    /// Lobby system for multiplayer room management.
    /// Note: This is a framework - integrate with your networking solution (Photon, Mirror, Netcode, etc.)
    /// </summary>
    public class LobbySystem : MonoBehaviour
    {
        public static LobbySystem Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int maxPlayersPerRoom = 8;
        [SerializeField] private int minPlayersToStart = 2;
        [SerializeField] private float readyCheckTimeout = 30f;
        [SerializeField] private bool autoStartWhenAllReady = true;

        [Header("Room Settings")]
        [SerializeField] private string defaultRoomName = "Game Room";
        [SerializeField] private bool isPublic = true;
        [SerializeField] private string password = "";

        [Header("Events")]
        [SerializeField] private UnityEvent onConnectedToLobby;
        [SerializeField] private UnityEvent onDisconnectedFromLobby;
        [SerializeField] private UnityEvent<LobbyRoom> onRoomCreated;
        [SerializeField] private UnityEvent<LobbyRoom> onRoomJoined;
        [SerializeField] private UnityEvent onRoomLeft;
        [SerializeField] private UnityEvent<LobbyPlayer> onPlayerJoined;
        [SerializeField] private UnityEvent<LobbyPlayer> onPlayerLeft;
        [SerializeField] private UnityEvent onGameStarting;

        // State
        private LobbyRoom currentRoom;
        private LobbyPlayer localPlayer;
        private List<LobbyRoom> availableRooms = new List<LobbyRoom>();
        private bool isConnected;
        private bool isInRoom;

        // Events
        public event Action ConnectedToLobby;
        public event Action DisconnectedFromLobby;
        public event Action<LobbyRoom> RoomCreated;
        public event Action<LobbyRoom> RoomJoined;
        public event Action RoomLeft;
        public event Action<LobbyPlayer> PlayerJoined;
        public event Action<LobbyPlayer> PlayerLeft;
        public event Action<LobbyPlayer> PlayerReadyChanged;
        public event Action<List<LobbyRoom>> RoomListUpdated;
        public event Action GameStarting;
        public event Action<string> ErrorOccurred;

        public LobbyRoom CurrentRoom => currentRoom;
        public LobbyPlayer LocalPlayer => localPlayer;
        public bool IsConnected => isConnected;
        public bool IsInRoom => isInRoom;
        public bool IsHost => currentRoom?.hostId == localPlayer?.playerId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        /// <summary>
        /// Connect to lobby server.
        /// </summary>
        public void ConnectToLobby(string playerName)
        {
            // Create local player
            localPlayer = new LobbyPlayer
            {
                playerId = GeneratePlayerId(),
                playerName = playerName,
                isReady = false,
                isHost = false
            };

            // TODO: Implement actual network connection
            // For now, simulate connection
            SimulateConnection();
        }

        private void SimulateConnection()
        {
            isConnected = true;

            ConnectedToLobby?.Invoke();
            onConnectedToLobby?.Invoke();

            Debug.Log("[Lobby] Connected to lobby");
        }

        /// <summary>
        /// Disconnect from lobby.
        /// </summary>
        public void DisconnectFromLobby()
        {
            if (isInRoom)
            {
                LeaveRoom();
            }

            isConnected = false;
            localPlayer = null;

            DisconnectedFromLobby?.Invoke();
            onDisconnectedFromLobby?.Invoke();

            Debug.Log("[Lobby] Disconnected from lobby");
        }

        /// <summary>
        /// Create a new room.
        /// </summary>
        public void CreateRoom(string roomName, int maxPlayers = 0, bool isPublic = true, string password = "")
        {
            if (!isConnected)
            {
                ErrorOccurred?.Invoke("Not connected to lobby");
                return;
            }

            if (isInRoom)
            {
                LeaveRoom();
            }

            currentRoom = new LobbyRoom
            {
                roomId = GenerateRoomId(),
                roomName = string.IsNullOrEmpty(roomName) ? defaultRoomName : roomName,
                hostId = localPlayer.playerId,
                hostName = localPlayer.playerName,
                maxPlayers = maxPlayers > 0 ? maxPlayers : maxPlayersPerRoom,
                isPublic = isPublic,
                password = password,
                players = new List<LobbyPlayer>()
            };

            // Add host to room
            localPlayer.isHost = true;
            currentRoom.players.Add(localPlayer);
            isInRoom = true;

            RoomCreated?.Invoke(currentRoom);
            onRoomCreated?.Invoke(currentRoom);

            Debug.Log($"[Lobby] Room created: {currentRoom.roomName}");
        }

        /// <summary>
        /// Join an existing room.
        /// </summary>
        public void JoinRoom(string roomId, string password = "")
        {
            if (!isConnected)
            {
                ErrorOccurred?.Invoke("Not connected to lobby");
                return;
            }

            if (isInRoom)
            {
                LeaveRoom();
            }

            // Find room
            LobbyRoom room = availableRooms.Find(r => r.roomId == roomId);
            if (room == null)
            {
                ErrorOccurred?.Invoke("Room not found");
                return;
            }

            // Check password
            if (!string.IsNullOrEmpty(room.password) && room.password != password)
            {
                ErrorOccurred?.Invoke("Incorrect password");
                return;
            }

            // Check capacity
            if (room.players.Count >= room.maxPlayers)
            {
                ErrorOccurred?.Invoke("Room is full");
                return;
            }

            currentRoom = room;
            localPlayer.isHost = false;
            currentRoom.players.Add(localPlayer);
            isInRoom = true;

            RoomJoined?.Invoke(currentRoom);
            onRoomJoined?.Invoke(currentRoom);

            // Notify other players
            PlayerJoined?.Invoke(localPlayer);
            onPlayerJoined?.Invoke(localPlayer);

            Debug.Log($"[Lobby] Joined room: {currentRoom.roomName}");
        }

        /// <summary>
        /// Join room by name.
        /// </summary>
        public void JoinRoomByName(string roomName, string password = "")
        {
            LobbyRoom room = availableRooms.Find(r => r.roomName == roomName);
            if (room != null)
            {
                JoinRoom(room.roomId, password);
            }
            else
            {
                ErrorOccurred?.Invoke("Room not found");
            }
        }

        /// <summary>
        /// Leave current room.
        /// </summary>
        public void LeaveRoom()
        {
            if (!isInRoom) return;

            // Remove from room
            currentRoom.players.Remove(localPlayer);

            // If host, assign new host or close room
            if (localPlayer.isHost && currentRoom.players.Count > 0)
            {
                LobbyPlayer newHost = currentRoom.players[0];
                newHost.isHost = true;
                currentRoom.hostId = newHost.playerId;
                currentRoom.hostName = newHost.playerName;
            }

            localPlayer.isHost = false;
            localPlayer.isReady = false;

            PlayerLeft?.Invoke(localPlayer);
            onPlayerLeft?.Invoke(localPlayer);

            currentRoom = null;
            isInRoom = false;

            RoomLeft?.Invoke();
            onRoomLeft?.Invoke();

            Debug.Log("[Lobby] Left room");
        }

        /// <summary>
        /// Set player ready status.
        /// </summary>
        public void SetReady(bool ready)
        {
            if (!isInRoom) return;

            localPlayer.isReady = ready;

            PlayerReadyChanged?.Invoke(localPlayer);

            // Check if all ready
            if (autoStartWhenAllReady && IsHost)
            {
                CheckAllReady();
            }

            Debug.Log($"[Lobby] Ready: {ready}");
        }

        /// <summary>
        /// Toggle ready status.
        /// </summary>
        public void ToggleReady()
        {
            SetReady(!localPlayer.isReady);
        }

        private void CheckAllReady()
        {
            if (currentRoom.players.Count < minPlayersToStart) return;

            bool allReady = true;
            foreach (var player in currentRoom.players)
            {
                if (!player.isReady)
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady)
            {
                StartGame();
            }
        }

        /// <summary>
        /// Start the game (host only).
        /// </summary>
        public void StartGame()
        {
            if (!IsHost)
            {
                ErrorOccurred?.Invoke("Only host can start game");
                return;
            }

            if (currentRoom.players.Count < minPlayersToStart)
            {
                ErrorOccurred?.Invoke($"Need at least {minPlayersToStart} players");
                return;
            }

            currentRoom.gameStarted = true;

            GameStarting?.Invoke();
            onGameStarting?.Invoke();

            Debug.Log("[Lobby] Game starting...");
        }

        /// <summary>
        /// Kick a player (host only).
        /// </summary>
        public void KickPlayer(string playerId)
        {
            if (!IsHost) return;
            if (playerId == localPlayer.playerId) return;

            LobbyPlayer player = currentRoom.players.Find(p => p.playerId == playerId);
            if (player != null)
            {
                currentRoom.players.Remove(player);
                PlayerLeft?.Invoke(player);
                onPlayerLeft?.Invoke(player);

                Debug.Log($"[Lobby] Kicked player: {player.playerName}");
            }
        }

        /// <summary>
        /// Refresh room list.
        /// </summary>
        public void RefreshRoomList()
        {
            // TODO: Request room list from server
            // For now, use local list
            RoomListUpdated?.Invoke(availableRooms);
        }

        /// <summary>
        /// Get available rooms.
        /// </summary>
        public List<LobbyRoom> GetAvailableRooms()
        {
            return new List<LobbyRoom>(availableRooms);
        }

        /// <summary>
        /// Get players in current room.
        /// </summary>
        public List<LobbyPlayer> GetPlayersInRoom()
        {
            if (currentRoom == null) return new List<LobbyPlayer>();
            return new List<LobbyPlayer>(currentRoom.players);
        }

        /// <summary>
        /// Get ready player count.
        /// </summary>
        public int GetReadyCount()
        {
            if (currentRoom == null) return 0;

            int count = 0;
            foreach (var player in currentRoom.players)
            {
                if (player.isReady) count++;
            }
            return count;
        }

        /// <summary>
        /// Check if can start game.
        /// </summary>
        public bool CanStartGame()
        {
            if (!IsHost) return false;
            if (currentRoom == null) return false;
            return currentRoom.players.Count >= minPlayersToStart;
        }

        /// <summary>
        /// Set room settings (host only).
        /// </summary>
        public void SetRoomSettings(int maxPlayers = -1, bool? isPublic = null, string password = null)
        {
            if (!IsHost || currentRoom == null) return;

            if (maxPlayers > 0) currentRoom.maxPlayers = maxPlayers;
            if (isPublic.HasValue) currentRoom.isPublic = isPublic.Value;
            if (password != null) currentRoom.password = password;
        }

        private string GeneratePlayerId()
        {
            return Guid.NewGuid().ToString().Substring(0, 8);
        }

        private string GenerateRoomId()
        {
            return Guid.NewGuid().ToString().Substring(0, 8);
        }

        // Simulate receiving network events (for testing)
        public void SimulatePlayerJoin(string playerName)
        {
            if (currentRoom == null) return;

            LobbyPlayer player = new LobbyPlayer
            {
                playerId = GeneratePlayerId(),
                playerName = playerName,
                isReady = false,
                isHost = false
            };

            currentRoom.players.Add(player);

            PlayerJoined?.Invoke(player);
            onPlayerJoined?.Invoke(player);
        }

        public void SimulatePlayerLeave(string playerId)
        {
            if (currentRoom == null) return;

            LobbyPlayer player = currentRoom.players.Find(p => p.playerId == playerId);
            if (player != null)
            {
                currentRoom.players.Remove(player);
                PlayerLeft?.Invoke(player);
                onPlayerLeft?.Invoke(player);
            }
        }
    }

    [Serializable]
    public class LobbyRoom
    {
        public string roomId;
        public string roomName;
        public string hostId;
        public string hostName;
        public int maxPlayers;
        public bool isPublic;
        public string password;
        public bool gameStarted;
        public string gameMode;
        public string mapName;
        public List<LobbyPlayer> players;

        public int PlayerCount => players?.Count ?? 0;
        public bool IsFull => PlayerCount >= maxPlayers;
        public bool HasPassword => !string.IsNullOrEmpty(password);
    }

    [Serializable]
    public class LobbyPlayer
    {
        public string playerId;
        public string playerName;
        public bool isReady;
        public bool isHost;
        public int team;
        public string characterId;
        public int ping;
    }
}
