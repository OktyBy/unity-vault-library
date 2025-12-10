using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityVault.Multiplayer
{
    /// <summary>
    /// Matchmaking system for finding and joining games.
    /// Note: This is a framework - integrate with your backend matchmaking service.
    /// </summary>
    public class MatchMaking : MonoBehaviour
    {
        public static MatchMaking Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float searchTimeout = 60f;
        [SerializeField] private float searchUpdateInterval = 5f;
        [SerializeField] private int minPlayersToStart = 2;
        [SerializeField] private int maxPlayersPerMatch = 10;

        [Header("Skill Based")]
        [SerializeField] private bool useSkillBasedMatching = true;
        [SerializeField] private int skillRangeStart = 100;
        [SerializeField] private int skillRangeExpansion = 50;
        [SerializeField] private float skillExpansionInterval = 10f;

        [Header("Game Modes")]
        [SerializeField] private List<GameMode> gameModes = new List<GameMode>();

        [Header("Events")]
        [SerializeField] private UnityEvent onSearchStarted;
        [SerializeField] private UnityEvent onSearchCancelled;
        [SerializeField] private UnityEvent<MatchResult> onMatchFound;
        [SerializeField] private UnityEvent<string> onSearchFailed;

        // State
        private bool isSearching;
        private float searchStartTime;
        private int currentSkillRange;
        private MatchmakingTicket currentTicket;
        private GameMode selectedGameMode;
        private Coroutine searchCoroutine;

        // Player info
        private string playerId;
        private string playerName;
        private int playerSkill = 1000;
        private int playerLevel = 1;

        // Events
        public event Action SearchStarted;
        public event Action SearchCancelled;
        public event Action<MatchResult> MatchFound;
        public event Action<string> SearchFailed;
        public event Action<int, int> PlayersFoundUpdated; // current, needed
        public event Action<float> SearchTimeUpdated;

        public bool IsSearching => isSearching;
        public float SearchTime => isSearching ? Time.time - searchStartTime : 0f;
        public GameMode SelectedGameMode => selectedGameMode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (gameModes.Count > 0)
            {
                selectedGameMode = gameModes[0];
            }
        }

        /// <summary>
        /// Set player info for matchmaking.
        /// </summary>
        public void SetPlayerInfo(string id, string name, int skill, int level)
        {
            playerId = id;
            playerName = name;
            playerSkill = skill;
            playerLevel = level;
        }

        /// <summary>
        /// Start searching for a match.
        /// </summary>
        public void StartSearch()
        {
            if (isSearching) return;
            if (selectedGameMode == null)
            {
                SearchFailed?.Invoke("No game mode selected");
                return;
            }

            isSearching = true;
            searchStartTime = Time.time;
            currentSkillRange = skillRangeStart;

            // Create matchmaking ticket
            currentTicket = new MatchmakingTicket
            {
                ticketId = Guid.NewGuid().ToString(),
                playerId = playerId,
                playerName = playerName,
                playerSkill = playerSkill,
                playerLevel = playerLevel,
                gameModeId = selectedGameMode.modeId,
                timestamp = DateTime.Now
            };

            searchCoroutine = StartCoroutine(SearchRoutine());

            SearchStarted?.Invoke();
            onSearchStarted?.Invoke();

            Debug.Log($"[Matchmaking] Started search for {selectedGameMode.modeName}");
        }

        /// <summary>
        /// Cancel current search.
        /// </summary>
        public void CancelSearch()
        {
            if (!isSearching) return;

            if (searchCoroutine != null)
            {
                StopCoroutine(searchCoroutine);
                searchCoroutine = null;
            }

            isSearching = false;
            currentTicket = null;

            SearchCancelled?.Invoke();
            onSearchCancelled?.Invoke();

            Debug.Log("[Matchmaking] Search cancelled");
        }

        private IEnumerator SearchRoutine()
        {
            float lastExpansionTime = 0f;

            while (isSearching)
            {
                // Check timeout
                float elapsed = Time.time - searchStartTime;
                if (elapsed >= searchTimeout)
                {
                    isSearching = false;
                    SearchFailed?.Invoke("Search timed out");
                    onSearchFailed?.Invoke("Search timed out");
                    yield break;
                }

                // Expand skill range over time
                if (useSkillBasedMatching && elapsed - lastExpansionTime >= skillExpansionInterval)
                {
                    currentSkillRange += skillRangeExpansion;
                    lastExpansionTime = elapsed;
                    Debug.Log($"[Matchmaking] Expanded skill range to {currentSkillRange}");
                }

                // Update time
                SearchTimeUpdated?.Invoke(elapsed);

                // TODO: Query matchmaking server
                // SimulateMatchQuery();

                yield return new WaitForSeconds(searchUpdateInterval);
            }
        }

        /// <summary>
        /// Simulate finding a match (for testing).
        /// </summary>
        public void SimulateMatchFound()
        {
            if (!isSearching) return;

            MatchResult result = new MatchResult
            {
                matchId = Guid.NewGuid().ToString(),
                serverAddress = "127.0.0.1",
                serverPort = 7777,
                gameMode = selectedGameMode,
                players = new List<MatchPlayer>
                {
                    new MatchPlayer { playerId = playerId, playerName = playerName, skill = playerSkill }
                }
            };

            // Add simulated players
            for (int i = 1; i < minPlayersToStart; i++)
            {
                result.players.Add(new MatchPlayer
                {
                    playerId = $"bot_{i}",
                    playerName = $"Player {i + 1}",
                    skill = playerSkill + UnityEngine.Random.Range(-100, 100)
                });
            }

            OnMatchFound(result);
        }

        private void OnMatchFound(MatchResult result)
        {
            if (searchCoroutine != null)
            {
                StopCoroutine(searchCoroutine);
                searchCoroutine = null;
            }

            isSearching = false;
            currentTicket = null;

            MatchFound?.Invoke(result);
            onMatchFound?.Invoke(result);

            Debug.Log($"[Matchmaking] Match found: {result.matchId}");
        }

        /// <summary>
        /// Select game mode.
        /// </summary>
        public void SelectGameMode(string modeId)
        {
            GameMode mode = gameModes.Find(m => m.modeId == modeId);
            if (mode != null)
            {
                selectedGameMode = mode;
                Debug.Log($"[Matchmaking] Selected mode: {mode.modeName}");
            }
        }

        /// <summary>
        /// Select game mode by index.
        /// </summary>
        public void SelectGameMode(int index)
        {
            if (index >= 0 && index < gameModes.Count)
            {
                selectedGameMode = gameModes[index];
            }
        }

        /// <summary>
        /// Get available game modes.
        /// </summary>
        public List<GameMode> GetGameModes()
        {
            return new List<GameMode>(gameModes);
        }

        /// <summary>
        /// Get estimated wait time.
        /// </summary>
        public float GetEstimatedWaitTime()
        {
            // TODO: Get from server based on queue population
            return 30f; // Placeholder
        }

        /// <summary>
        /// Get players in queue.
        /// </summary>
        public int GetPlayersInQueue()
        {
            // TODO: Get from server
            return 0;
        }

        /// <summary>
        /// Join specific match by ID.
        /// </summary>
        public void JoinMatch(string matchId)
        {
            // TODO: Request to join specific match
            Debug.Log($"[Matchmaking] Requesting to join match: {matchId}");
        }

        /// <summary>
        /// Create private match.
        /// </summary>
        public MatchResult CreatePrivateMatch(GameMode mode = null)
        {
            MatchResult result = new MatchResult
            {
                matchId = Guid.NewGuid().ToString(),
                isPrivate = true,
                gameMode = mode ?? selectedGameMode,
                players = new List<MatchPlayer>
                {
                    new MatchPlayer { playerId = playerId, playerName = playerName, skill = playerSkill, isHost = true }
                }
            };

            Debug.Log($"[Matchmaking] Created private match: {result.matchId}");
            return result;
        }

        /// <summary>
        /// Join private match with code.
        /// </summary>
        public void JoinPrivateMatch(string matchCode)
        {
            // TODO: Validate and join private match
            Debug.Log($"[Matchmaking] Joining private match: {matchCode}");
        }

        /// <summary>
        /// Set party members for group matchmaking.
        /// </summary>
        public void SetParty(List<string> partyMemberIds)
        {
            if (currentTicket != null)
            {
                currentTicket.partyMembers = partyMemberIds;
            }
        }

        /// <summary>
        /// Update player skill rating.
        /// </summary>
        public void UpdateSkill(int newSkill)
        {
            playerSkill = newSkill;
        }

        /// <summary>
        /// Report match result for skill calculation.
        /// </summary>
        public void ReportMatchResult(string matchId, bool won, int score)
        {
            // TODO: Send to backend for skill recalculation
            Debug.Log($"[Matchmaking] Reported result for {matchId}: Won={won}, Score={score}");
        }
    }

    [Serializable]
    public class GameMode
    {
        public string modeId;
        public string modeName;
        [TextArea]
        public string description;
        public Sprite icon;
        public int minPlayers = 2;
        public int maxPlayers = 10;
        public bool isRanked;
        public bool isTeamBased;
        public int teamsCount = 2;
        public string mapPool;
    }

    [Serializable]
    public class MatchmakingTicket
    {
        public string ticketId;
        public string playerId;
        public string playerName;
        public int playerSkill;
        public int playerLevel;
        public string gameModeId;
        public List<string> partyMembers;
        public DateTime timestamp;
    }

    [Serializable]
    public class MatchResult
    {
        public string matchId;
        public string serverAddress;
        public int serverPort;
        public string matchCode;
        public bool isPrivate;
        public GameMode gameMode;
        public List<MatchPlayer> players;
        public string mapName;
    }

    [Serializable]
    public class MatchPlayer
    {
        public string playerId;
        public string playerName;
        public int skill;
        public int team;
        public bool isHost;
    }
}
