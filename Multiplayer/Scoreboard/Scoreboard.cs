using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityVault.Multiplayer
{
    /// <summary>
    /// Multiplayer scoreboard and leaderboard system.
    /// </summary>
    public class Scoreboard : MonoBehaviour
    {
        public static Scoreboard Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject scoreboardPanel;
        [SerializeField] private RectTransform entriesContainer;
        [SerializeField] private GameObject entryPrefab;

        [Header("Header References")]
        [SerializeField] private TextMeshProUGUI gameModeText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI scoreText;

        [Header("Settings")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
        [SerializeField] private bool holdToShow = true;
        [SerializeField] private SortMode sortMode = SortMode.Score;
        [SerializeField] private bool descendingOrder = true;

        [Header("Stat Columns")]
        [SerializeField] private List<StatColumn> columns = new List<StatColumn>();

        [Header("Team Settings")]
        [SerializeField] private bool useTeams = false;
        [SerializeField] private List<TeamInfo> teams = new List<TeamInfo>();

        [Header("Events")]
        [SerializeField] private UnityEvent onScoreboardShown;
        [SerializeField] private UnityEvent onScoreboardHidden;
        [SerializeField] private UnityEvent<PlayerScore> onScoreUpdated;

        // State
        private Dictionary<string, PlayerScore> playerScores = new Dictionary<string, PlayerScore>();
        private List<ScoreboardEntry> entryObjects = new List<ScoreboardEntry>();
        private bool isShowing;
        private string localPlayerId;

        // Events
        public event Action ScoreboardShown;
        public event Action ScoreboardHidden;
        public event Action<PlayerScore> ScoreUpdated;
        public event Action<List<PlayerScore>> LeaderboardChanged;

        public bool IsShowing => isShowing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (scoreboardPanel != null)
            {
                scoreboardPanel.SetActive(false);
            }
        }

        private void Update()
        {
            if (holdToShow)
            {
                if (Input.GetKeyDown(toggleKey))
                {
                    Show();
                }
                else if (Input.GetKeyUp(toggleKey))
                {
                    Hide();
                }
            }
            else
            {
                if (Input.GetKeyDown(toggleKey))
                {
                    Toggle();
                }
            }
        }

        /// <summary>
        /// Set local player ID for highlighting.
        /// </summary>
        public void SetLocalPlayer(string playerId)
        {
            localPlayerId = playerId;
        }

        /// <summary>
        /// Show scoreboard.
        /// </summary>
        public void Show()
        {
            if (isShowing) return;

            isShowing = true;

            if (scoreboardPanel != null)
            {
                scoreboardPanel.SetActive(true);
            }

            RefreshDisplay();

            ScoreboardShown?.Invoke();
            onScoreboardShown?.Invoke();
        }

        /// <summary>
        /// Hide scoreboard.
        /// </summary>
        public void Hide()
        {
            if (!isShowing) return;

            isShowing = false;

            if (scoreboardPanel != null)
            {
                scoreboardPanel.SetActive(false);
            }

            ScoreboardHidden?.Invoke();
            onScoreboardHidden?.Invoke();
        }

        /// <summary>
        /// Toggle scoreboard.
        /// </summary>
        public void Toggle()
        {
            if (isShowing)
                Hide();
            else
                Show();
        }

        /// <summary>
        /// Register a player.
        /// </summary>
        public void RegisterPlayer(string playerId, string playerName, int teamId = 0)
        {
            if (playerScores.ContainsKey(playerId)) return;

            PlayerScore score = new PlayerScore
            {
                playerId = playerId,
                playerName = playerName,
                teamId = teamId,
                stats = new Dictionary<string, int>()
            };

            // Initialize stats
            foreach (var column in columns)
            {
                score.stats[column.statId] = 0;
            }

            playerScores[playerId] = score;

            if (isShowing)
            {
                RefreshDisplay();
            }
        }

        /// <summary>
        /// Unregister a player.
        /// </summary>
        public void UnregisterPlayer(string playerId)
        {
            playerScores.Remove(playerId);

            if (isShowing)
            {
                RefreshDisplay();
            }
        }

        /// <summary>
        /// Update player stat.
        /// </summary>
        public void UpdateStat(string playerId, string statId, int value)
        {
            if (!playerScores.TryGetValue(playerId, out PlayerScore score)) return;

            score.stats[statId] = value;

            ScoreUpdated?.Invoke(score);
            onScoreUpdated?.Invoke(score);

            if (isShowing)
            {
                RefreshDisplay();
            }
        }

        /// <summary>
        /// Add to player stat.
        /// </summary>
        public void AddStat(string playerId, string statId, int amount)
        {
            if (!playerScores.TryGetValue(playerId, out PlayerScore score)) return;

            if (!score.stats.ContainsKey(statId))
            {
                score.stats[statId] = 0;
            }

            score.stats[statId] += amount;

            ScoreUpdated?.Invoke(score);
            onScoreUpdated?.Invoke(score);

            if (isShowing)
            {
                RefreshDisplay();
            }
        }

        /// <summary>
        /// Get player stat value.
        /// </summary>
        public int GetStat(string playerId, string statId)
        {
            if (!playerScores.TryGetValue(playerId, out PlayerScore score)) return 0;
            return score.stats.TryGetValue(statId, out int value) ? value : 0;
        }

        /// <summary>
        /// Get player score.
        /// </summary>
        public PlayerScore GetPlayerScore(string playerId)
        {
            return playerScores.TryGetValue(playerId, out PlayerScore score) ? score : null;
        }

        /// <summary>
        /// Get sorted leaderboard.
        /// </summary>
        public List<PlayerScore> GetLeaderboard()
        {
            List<PlayerScore> sorted = new List<PlayerScore>(playerScores.Values);

            sorted.Sort((a, b) =>
            {
                int comparison = 0;

                switch (sortMode)
                {
                    case SortMode.Score:
                        int scoreA = a.stats.TryGetValue("score", out int sA) ? sA : 0;
                        int scoreB = b.stats.TryGetValue("score", out int sB) ? sB : 0;
                        comparison = scoreA.CompareTo(scoreB);
                        break;

                    case SortMode.Kills:
                        int killsA = a.stats.TryGetValue("kills", out int kA) ? kA : 0;
                        int killsB = b.stats.TryGetValue("kills", out int kB) ? kB : 0;
                        comparison = killsA.CompareTo(killsB);
                        break;

                    case SortMode.KD:
                        float kdA = CalculateKD(a);
                        float kdB = CalculateKD(b);
                        comparison = kdA.CompareTo(kdB);
                        break;

                    case SortMode.Name:
                        comparison = string.Compare(a.playerName, b.playerName);
                        break;
                }

                return descendingOrder ? -comparison : comparison;
            });

            return sorted;
        }

        private float CalculateKD(PlayerScore score)
        {
            int kills = score.stats.TryGetValue("kills", out int k) ? k : 0;
            int deaths = score.stats.TryGetValue("deaths", out int d) ? d : 1;
            return deaths > 0 ? (float)kills / deaths : kills;
        }

        /// <summary>
        /// Get player rank.
        /// </summary>
        public int GetPlayerRank(string playerId)
        {
            List<PlayerScore> leaderboard = GetLeaderboard();

            for (int i = 0; i < leaderboard.Count; i++)
            {
                if (leaderboard[i].playerId == playerId)
                {
                    return i + 1;
                }
            }

            return -1;
        }

        /// <summary>
        /// Get team score.
        /// </summary>
        public int GetTeamScore(int teamId)
        {
            int total = 0;

            foreach (var score in playerScores.Values)
            {
                if (score.teamId == teamId)
                {
                    total += score.stats.TryGetValue("score", out int s) ? s : 0;
                }
            }

            return total;
        }

        /// <summary>
        /// Refresh the scoreboard display.
        /// </summary>
        public void RefreshDisplay()
        {
            // Clear existing entries
            foreach (var entry in entryObjects)
            {
                if (entry != null)
                {
                    Destroy(entry.gameObject);
                }
            }
            entryObjects.Clear();

            if (entriesContainer == null || entryPrefab == null) return;

            // Get sorted scores
            List<PlayerScore> sorted = GetLeaderboard();

            if (useTeams)
            {
                // Group by team
                foreach (var team in teams)
                {
                    var teamPlayers = sorted.Where(p => p.teamId == team.teamId).ToList();
                    foreach (var player in teamPlayers)
                    {
                        CreateEntry(player, team);
                    }
                }
            }
            else
            {
                foreach (var player in sorted)
                {
                    CreateEntry(player, null);
                }
            }

            LeaderboardChanged?.Invoke(sorted);
        }

        private void CreateEntry(PlayerScore player, TeamInfo team)
        {
            GameObject entryObj = Instantiate(entryPrefab, entriesContainer);
            ScoreboardEntry entry = entryObj.GetComponent<ScoreboardEntry>();

            if (entry == null)
            {
                entry = entryObj.AddComponent<ScoreboardEntry>();
            }

            entry.Setup(player, columns, team, player.playerId == localPlayerId);
            entryObjects.Add(entry);
        }

        /// <summary>
        /// Reset all scores.
        /// </summary>
        public void ResetAllScores()
        {
            foreach (var score in playerScores.Values)
            {
                foreach (var key in new List<string>(score.stats.Keys))
                {
                    score.stats[key] = 0;
                }
            }

            if (isShowing)
            {
                RefreshDisplay();
            }
        }

        /// <summary>
        /// Clear all players.
        /// </summary>
        public void ClearAll()
        {
            playerScores.Clear();

            foreach (var entry in entryObjects)
            {
                Destroy(entry.gameObject);
            }
            entryObjects.Clear();
        }

        /// <summary>
        /// Set game mode text.
        /// </summary>
        public void SetGameMode(string mode)
        {
            if (gameModeText != null)
            {
                gameModeText.text = mode;
            }
        }

        /// <summary>
        /// Set time text.
        /// </summary>
        public void SetTime(string time)
        {
            if (timeText != null)
            {
                timeText.text = time;
            }
        }
    }

    /// <summary>
    /// Individual scoreboard entry UI.
    /// </summary>
    public class ScoreboardEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI[] statTexts;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color localPlayerColor = new Color(1f, 1f, 0.5f, 0.3f);
        [SerializeField] private Color defaultColor = new Color(1f, 1f, 1f, 0.1f);

        public void Setup(PlayerScore player, List<StatColumn> columns, TeamInfo team, bool isLocalPlayer)
        {
            if (nameText != null)
            {
                nameText.text = player.playerName;

                if (team != null)
                {
                    nameText.color = team.teamColor;
                }
            }

            // Set stat values
            if (statTexts != null)
            {
                for (int i = 0; i < statTexts.Length && i < columns.Count; i++)
                {
                    string statId = columns[i].statId;
                    int value = player.stats.TryGetValue(statId, out int v) ? v : 0;
                    statTexts[i].text = value.ToString();
                }
            }

            // Highlight local player
            if (backgroundImage != null)
            {
                backgroundImage.color = isLocalPlayer ? localPlayerColor : defaultColor;
            }
        }
    }

    [Serializable]
    public class PlayerScore
    {
        public string playerId;
        public string playerName;
        public int teamId;
        public Dictionary<string, int> stats;
    }

    [Serializable]
    public class StatColumn
    {
        public string statId;
        public string displayName;
        public int width = 60;
    }

    [Serializable]
    public class TeamInfo
    {
        public int teamId;
        public string teamName;
        public Color teamColor = Color.white;
    }

    public enum SortMode
    {
        Score,
        Kills,
        KD,
        Name
    }
}
