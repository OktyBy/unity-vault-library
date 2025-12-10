using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace UnityVault.UI
{
    /// <summary>
    /// In-game pause menu controller.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        [Header("Settings")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private bool freezeTime = true;

        [Header("Audio")]
        [SerializeField] private AudioClip pauseSound;
        [SerializeField] private AudioClip resumeSound;

        private AudioSource audioSource;
        private bool isPaused;

        public bool IsPaused => isPaused;

        public event System.Action OnPaused;
        public event System.Action OnResumed;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        private void Start()
        {
            SetupButtons();

            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(pauseKey))
            {
                TogglePause();
            }
        }

        private void SetupButtons()
        {
            if (resumeButton != null)
                resumeButton.onClick.AddListener(Resume);

            if (optionsButton != null)
                optionsButton.onClick.AddListener(ShowOptions);

            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(GoToMainMenu);

            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);
        }

        public void TogglePause()
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        public void Pause()
        {
            isPaused = true;

            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
            }

            if (freezeTime)
            {
                Time.timeScale = 0f;
            }

            PlaySound(pauseSound);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            OnPaused?.Invoke();
        }

        public void Resume()
        {
            isPaused = false;

            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }

            if (freezeTime)
            {
                Time.timeScale = 1f;
            }

            PlaySound(resumeSound);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            OnResumed?.Invoke();
        }

        public void ShowOptions()
        {
            // Options panel logic - can be extended
            Debug.Log("[PauseMenu] Show Options");
        }

        public void GoToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void OnDestroy()
        {
            // Ensure time scale is reset
            Time.timeScale = 1f;
        }
    }
}
