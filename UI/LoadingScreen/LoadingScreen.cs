using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;

namespace UnityVault.UI
{
    /// <summary>
    /// Loading screen with progress bar and tips.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private Slider progressBar;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image loadingIcon;

        [Header("Animation")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeSpeed = 2f;
        [SerializeField] private bool rotateLoadingIcon = true;
        [SerializeField] private float iconRotationSpeed = 180f;

        [Header("Progress")]
        [SerializeField] private bool smoothProgress = true;
        [SerializeField] private float progressSmoothSpeed = 2f;
        [SerializeField] private float minimumLoadTime = 0.5f;

        [Header("Tips")]
        [SerializeField] private string[] loadingTips;
        [SerializeField] private float tipChangeInterval = 3f;
        [SerializeField] private bool randomizeTips = true;

        [Header("Backgrounds")]
        [SerializeField] private Sprite[] backgrounds;
        [SerializeField] private bool randomizeBackground = true;

        // State
        private float currentProgress;
        private float targetProgress;
        private float loadStartTime;
        private bool isLoading;
        private int currentTipIndex;
        private float tipTimer;
        private AsyncOperation currentLoadOperation;

        // Events
        public event Action LoadingStarted;
        public event Action<float> ProgressUpdated;
        public event Action LoadingCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (canvasGroup == null && loadingPanel != null)
            {
                canvasGroup = loadingPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = loadingPanel.AddComponent<CanvasGroup>();
                }
            }

            Hide();
        }

        private void Update()
        {
            if (!isLoading) return;

            UpdateProgress();
            UpdateTips();
            UpdateLoadingIcon();
        }

        private void UpdateProgress()
        {
            if (smoothProgress)
            {
                currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, progressSmoothSpeed * Time.deltaTime);
            }
            else
            {
                currentProgress = targetProgress;
            }

            if (progressBar != null)
            {
                progressBar.value = currentProgress;
            }

            if (progressText != null)
            {
                progressText.text = $"{Mathf.RoundToInt(currentProgress * 100)}%";
            }

            ProgressUpdated?.Invoke(currentProgress);
        }

        private void UpdateTips()
        {
            if (loadingTips == null || loadingTips.Length == 0) return;

            tipTimer += Time.deltaTime;

            if (tipTimer >= tipChangeInterval)
            {
                tipTimer = 0;
                ShowNextTip();
            }
        }

        private void ShowNextTip()
        {
            if (randomizeTips)
            {
                currentTipIndex = UnityEngine.Random.Range(0, loadingTips.Length);
            }
            else
            {
                currentTipIndex = (currentTipIndex + 1) % loadingTips.Length;
            }

            if (tipText != null)
            {
                tipText.text = loadingTips[currentTipIndex];
            }
        }

        private void UpdateLoadingIcon()
        {
            if (loadingIcon != null && rotateLoadingIcon)
            {
                loadingIcon.transform.Rotate(0, 0, -iconRotationSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Load a scene with loading screen.
        /// </summary>
        public static void LoadScene(string sceneName)
        {
            if (Instance == null) return;
            Instance.StartCoroutine(Instance.LoadSceneAsync(sceneName));
        }

        /// <summary>
        /// Load a scene by index with loading screen.
        /// </summary>
        public static void LoadScene(int sceneIndex)
        {
            if (Instance == null) return;
            Instance.StartCoroutine(Instance.LoadSceneAsync(sceneIndex));
        }

        private IEnumerator LoadSceneAsync(string sceneName)
        {
            yield return StartLoading();

            currentLoadOperation = SceneManager.LoadSceneAsync(sceneName);
            currentLoadOperation.allowSceneActivation = false;

            yield return UpdateLoadProgress();
            yield return FinishLoading();
        }

        private IEnumerator LoadSceneAsync(int sceneIndex)
        {
            yield return StartLoading();

            currentLoadOperation = SceneManager.LoadSceneAsync(sceneIndex);
            currentLoadOperation.allowSceneActivation = false;

            yield return UpdateLoadProgress();
            yield return FinishLoading();
        }

        private IEnumerator StartLoading()
        {
            isLoading = true;
            loadStartTime = Time.time;
            currentProgress = 0;
            targetProgress = 0;
            tipTimer = tipChangeInterval; // Show tip immediately

            // Setup background
            if (backgrounds != null && backgrounds.Length > 0 && backgroundImage != null)
            {
                int bgIndex = randomizeBackground ? UnityEngine.Random.Range(0, backgrounds.Length) : 0;
                backgroundImage.sprite = backgrounds[bgIndex];
            }

            // Show panel
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
            }

            // Fade in
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0;
                while (canvasGroup.alpha < 1)
                {
                    canvasGroup.alpha += fadeSpeed * Time.deltaTime;
                    yield return null;
                }
            }

            SetStatus("Loading...");
            LoadingStarted?.Invoke();
        }

        private IEnumerator UpdateLoadProgress()
        {
            while (currentLoadOperation != null && currentLoadOperation.progress < 0.9f)
            {
                // AsyncOperation.progress goes from 0 to 0.9 during load
                targetProgress = Mathf.Clamp01(currentLoadOperation.progress / 0.9f);
                yield return null;
            }

            targetProgress = 0.9f;

            // Wait for minimum load time
            float elapsed = Time.time - loadStartTime;
            if (elapsed < minimumLoadTime)
            {
                yield return new WaitForSeconds(minimumLoadTime - elapsed);
            }

            // Complete progress
            targetProgress = 1f;

            // Wait for smooth progress to catch up
            while (currentProgress < 0.99f)
            {
                yield return null;
            }
        }

        private IEnumerator FinishLoading()
        {
            SetStatus("Complete!");

            // Short delay before scene activation
            yield return new WaitForSeconds(0.3f);

            // Fade out
            if (canvasGroup != null)
            {
                while (canvasGroup.alpha > 0)
                {
                    canvasGroup.alpha -= fadeSpeed * Time.deltaTime;
                    yield return null;
                }
            }

            // Activate scene
            if (currentLoadOperation != null)
            {
                currentLoadOperation.allowSceneActivation = true;
            }

            LoadingCompleted?.Invoke();
            Hide();
        }

        /// <summary>
        /// Show loading screen for custom operations.
        /// </summary>
        public static void Show(string status = "Loading...")
        {
            if (Instance == null) return;

            Instance.isLoading = true;
            Instance.currentProgress = 0;
            Instance.targetProgress = 0;
            Instance.tipTimer = Instance.tipChangeInterval;

            if (Instance.loadingPanel != null)
            {
                Instance.loadingPanel.SetActive(true);
            }

            if (Instance.canvasGroup != null)
            {
                Instance.canvasGroup.alpha = 1;
            }

            Instance.SetStatus(status);
            Instance.LoadingStarted?.Invoke();
        }

        /// <summary>
        /// Update progress manually.
        /// </summary>
        public static void SetProgress(float progress)
        {
            if (Instance == null) return;
            Instance.targetProgress = Mathf.Clamp01(progress);
        }

        /// <summary>
        /// Update status text.
        /// </summary>
        public static void SetStatusText(string status)
        {
            if (Instance == null) return;
            Instance.SetStatus(status);
        }

        private void SetStatus(string status)
        {
            if (statusText != null)
            {
                statusText.text = status;
            }
        }

        /// <summary>
        /// Hide loading screen.
        /// </summary>
        public static void Hide()
        {
            if (Instance == null) return;

            Instance.isLoading = false;
            Instance.currentLoadOperation = null;

            if (Instance.loadingPanel != null)
            {
                Instance.loadingPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Complete loading and hide.
        /// </summary>
        public static void Complete()
        {
            if (Instance == null) return;
            Instance.StartCoroutine(Instance.CompleteManualLoad());
        }

        private IEnumerator CompleteManualLoad()
        {
            targetProgress = 1f;

            while (currentProgress < 0.99f)
            {
                yield return null;
            }

            SetStatus("Complete!");
            yield return new WaitForSeconds(0.3f);

            if (canvasGroup != null)
            {
                while (canvasGroup.alpha > 0)
                {
                    canvasGroup.alpha -= fadeSpeed * Time.deltaTime;
                    yield return null;
                }
            }

            LoadingCompleted?.Invoke();
            Hide();
        }

        /// <summary>
        /// Add loading tips at runtime.
        /// </summary>
        public static void AddTip(string tip)
        {
            if (Instance == null) return;

            var tips = new string[Instance.loadingTips.Length + 1];
            Instance.loadingTips.CopyTo(tips, 0);
            tips[tips.Length - 1] = tip;
            Instance.loadingTips = tips;
        }

        /// <summary>
        /// Set all loading tips.
        /// </summary>
        public static void SetTips(string[] tips)
        {
            if (Instance == null) return;
            Instance.loadingTips = tips;
        }
    }
}
