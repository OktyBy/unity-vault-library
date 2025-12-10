using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace UnityVault.UI
{
    /// <summary>
    /// Main menu controller with common menu functionality.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private GameObject loadingPanel;

        [Header("Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button backButton;

        [Header("Settings")]
        [SerializeField] private string gameSceneName = "Game";
        [SerializeField] private bool showContinueIfSaveExists = true;
        [SerializeField] private float loadingFadeTime = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField] private AudioClip buttonHoverSound;

        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        private void Start()
        {
            SetupButtons();
            ShowMainPanel();

            // Check for existing save
            if (continueButton != null && showContinueIfSaveExists)
            {
                continueButton.gameObject.SetActive(PlayerPrefs.HasKey("SaveExists"));
            }
        }

        private void SetupButtons()
        {
            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayClicked);
                AddButtonSounds(playButton);
            }

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinueClicked);
                AddButtonSounds(continueButton);
            }

            if (optionsButton != null)
            {
                optionsButton.onClick.AddListener(OnOptionsClicked);
                AddButtonSounds(optionsButton);
            }

            if (creditsButton != null)
            {
                creditsButton.onClick.AddListener(OnCreditsClicked);
                AddButtonSounds(creditsButton);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuitClicked);
                AddButtonSounds(quitButton);
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackClicked);
                AddButtonSounds(backButton);
            }
        }

        private void AddButtonSounds(Button button)
        {
            var trigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            // Click sound is handled by onClick
            // Add hover sound
            var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
            };
            pointerEnter.callback.AddListener((data) => PlaySound(buttonHoverSound));
            trigger.triggers.Add(pointerEnter);
        }

        public void OnPlayClicked()
        {
            PlaySound(buttonClickSound);
            StartCoroutine(LoadScene(gameSceneName, false));
        }

        public void OnContinueClicked()
        {
            PlaySound(buttonClickSound);
            StartCoroutine(LoadScene(gameSceneName, true));
        }

        public void OnOptionsClicked()
        {
            PlaySound(buttonClickSound);
            ShowOptionsPanel();
        }

        public void OnCreditsClicked()
        {
            PlaySound(buttonClickSound);
            ShowCreditsPanel();
        }

        public void OnBackClicked()
        {
            PlaySound(buttonClickSound);
            ShowMainPanel();
        }

        public void OnQuitClicked()
        {
            PlaySound(buttonClickSound);
            QuitGame();
        }

        private void ShowMainPanel()
        {
            SetPanelActive(mainPanel, true);
            SetPanelActive(optionsPanel, false);
            SetPanelActive(creditsPanel, false);
            SetPanelActive(loadingPanel, false);
        }

        private void ShowOptionsPanel()
        {
            SetPanelActive(mainPanel, false);
            SetPanelActive(optionsPanel, true);
            SetPanelActive(creditsPanel, false);
        }

        private void ShowCreditsPanel()
        {
            SetPanelActive(mainPanel, false);
            SetPanelActive(optionsPanel, false);
            SetPanelActive(creditsPanel, true);
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        private System.Collections.IEnumerator LoadScene(string sceneName, bool loadSave)
        {
            SetPanelActive(loadingPanel, true);

            // Start async load
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.allowSceneActivation = false;

            while (asyncLoad.progress < 0.9f)
            {
                yield return null;
            }

            // Store load save flag for game to check
            PlayerPrefs.SetInt("LoadSave", loadSave ? 1 : 0);

            asyncLoad.allowSceneActivation = true;
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
