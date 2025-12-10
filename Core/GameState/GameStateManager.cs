using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Core
{
    /// <summary>
    /// Manages game flow states: menu, playing, paused, game over, etc.
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        #region Singleton

        private static GameStateManager instance;
        public static GameStateManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<GameStateManager>();
                }
                return instance;
            }
        }

        #endregion

        #region Serialized Fields

        [Header("Initial State")]
        [SerializeField] private GameState initialState = GameState.MainMenu;

        [Header("Time Settings")]
        [SerializeField] private bool pauseTime = true; // Pause Time.timeScale when paused

        [Header("Events")]
        [SerializeField] private UnityEvent<GameState, GameState> onStateChanged;
        [SerializeField] private UnityEvent onGamePaused;
        [SerializeField] private UnityEvent onGameResumed;
        [SerializeField] private UnityEvent onGameStarted;
        [SerializeField] private UnityEvent onGameOver;

        #endregion

        #region Properties

        public GameState CurrentState { get; private set; }
        public GameState PreviousState { get; private set; }
        public bool IsPaused => CurrentState == GameState.Paused;
        public bool IsPlaying => CurrentState == GameState.Playing;
        public bool IsInMenu => CurrentState == GameState.MainMenu;
        public bool IsGameOver => CurrentState == GameState.GameOver;
        public bool IsLoading => CurrentState == GameState.Loading;

        #endregion

        #region C# Events

        public event Action<GameState, GameState> StateChanged; // new, old
        public event Action GamePaused;
        public event Action GameResumed;
        public event Action GameStarted;
        public event Action GameEnded;

        #endregion

        #region Private Fields

        private float storedTimeScale = 1f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            CurrentState = initialState;
        }

        private void Update()
        {
            // Optional: Handle pause input here or let UI handle it
            if (Input.GetKeyDown(KeyCode.Escape) && IsPlaying)
            {
                Pause();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Change to a new state.
        /// </summary>
        public void SetState(GameState newState)
        {
            if (CurrentState == newState) return;

            PreviousState = CurrentState;
            CurrentState = newState;

            HandleStateTransition(PreviousState, newState);

            StateChanged?.Invoke(newState, PreviousState);
            onStateChanged?.Invoke(newState, PreviousState);

            Debug.Log($"[GameState] {PreviousState} -> {newState}");
        }

        /// <summary>
        /// Start the game.
        /// </summary>
        public void StartGame()
        {
            SetState(GameState.Playing);
            GameStarted?.Invoke();
            onGameStarted?.Invoke();
        }

        /// <summary>
        /// Pause the game.
        /// </summary>
        public void Pause()
        {
            if (!IsPlaying) return;

            storedTimeScale = Time.timeScale;

            SetState(GameState.Paused);

            if (pauseTime)
            {
                Time.timeScale = 0f;
            }

            GamePaused?.Invoke();
            onGamePaused?.Invoke();
        }

        /// <summary>
        /// Resume the game.
        /// </summary>
        public void Resume()
        {
            if (!IsPaused) return;

            SetState(GameState.Playing);

            if (pauseTime)
            {
                Time.timeScale = storedTimeScale;
            }

            GameResumed?.Invoke();
            onGameResumed?.Invoke();
        }

        /// <summary>
        /// Toggle pause state.
        /// </summary>
        public void TogglePause()
        {
            if (IsPaused)
                Resume();
            else if (IsPlaying)
                Pause();
        }

        /// <summary>
        /// End the game (game over).
        /// </summary>
        public void EndGame(bool won = false)
        {
            SetState(won ? GameState.Victory : GameState.GameOver);
            GameEnded?.Invoke();
            onGameOver?.Invoke();
        }

        /// <summary>
        /// Return to main menu.
        /// </summary>
        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            SetState(GameState.MainMenu);
        }

        /// <summary>
        /// Set loading state.
        /// </summary>
        public void SetLoading(bool isLoading)
        {
            if (isLoading)
            {
                SetState(GameState.Loading);
            }
            else if (CurrentState == GameState.Loading)
            {
                SetState(PreviousState);
            }
        }

        /// <summary>
        /// Restart the current game.
        /// </summary>
        public void RestartGame()
        {
            Time.timeScale = 1f;
            SetState(GameState.Playing);
            GameStarted?.Invoke();
            onGameStarted?.Invoke();
        }

        /// <summary>
        /// Quit the application.
        /// </summary>
        public void QuitGame()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        #endregion

        #region Private Methods

        private void HandleStateTransition(GameState from, GameState to)
        {
            // Handle cursor visibility
            switch (to)
            {
                case GameState.Playing:
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    break;

                case GameState.Paused:
                case GameState.MainMenu:
                case GameState.GameOver:
                case GameState.Victory:
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
            }
        }

        #endregion
    }

    /// <summary>
    /// Possible game states.
    /// </summary>
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        Loading,
        Cutscene,
        GameOver,
        Victory
    }
}
