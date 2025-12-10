using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System;
using System.Collections.Generic;

namespace UnityVault.UI
{
    /// <summary>
    /// Tutorial system with step-by-step guidance and highlighting.
    /// </summary>
    public class TutorialSystem : MonoBehaviour
    {
        public static TutorialSystem Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image tutorialImage;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button prevButton;
        [SerializeField] private TextMeshProUGUI stepCounterText;

        [Header("Highlight")]
        [SerializeField] private RectTransform highlightMask;
        [SerializeField] private Image overlayImage;
        [SerializeField] private float highlightPadding = 10f;
        [SerializeField] private Color overlayColor = new Color(0, 0, 0, 0.7f);

        [Header("Arrow/Pointer")]
        [SerializeField] private RectTransform pointerArrow;
        [SerializeField] private float arrowOffset = 50f;

        [Header("Animation")]
        [SerializeField] private float fadeSpeed = 5f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseAmount = 0.1f;

        [Header("Settings")]
        [SerializeField] private bool pauseGameDuringTutorial = true;
        [SerializeField] private bool requireActionToProgress = false;
        [SerializeField] private string completedTutorialsKey = "CompletedTutorials";

        [Header("Events")]
        [SerializeField] private UnityEvent onTutorialStarted;
        [SerializeField] private UnityEvent<int> onStepChanged;
        [SerializeField] private UnityEvent onTutorialCompleted;
        [SerializeField] private UnityEvent onTutorialSkipped;

        // State
        private Tutorial currentTutorial;
        private int currentStepIndex;
        private bool isActive;
        private HashSet<string> completedTutorials = new HashSet<string>();
        private float originalTimeScale;
        private CanvasGroup canvasGroup;

        // Events
        public event Action<Tutorial> TutorialStarted;
        public event Action<int> StepChanged;
        public event Action<Tutorial> TutorialCompleted;
        public event Action<Tutorial> TutorialSkipped;

        public bool IsActive => isActive;
        public int CurrentStep => currentStepIndex;
        public Tutorial CurrentTutorial => currentTutorial;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadCompletedTutorials();
            SetupUI();
            Hide();
        }

        private void SetupUI()
        {
            if (nextButton != null)
            {
                nextButton.onClick.AddListener(NextStep);
            }

            if (prevButton != null)
            {
                prevButton.onClick.AddListener(PreviousStep);
            }

            if (skipButton != null)
            {
                skipButton.onClick.AddListener(SkipTutorial);
            }

            canvasGroup = tutorialPanel?.GetComponent<CanvasGroup>();
            if (canvasGroup == null && tutorialPanel != null)
            {
                canvasGroup = tutorialPanel.AddComponent<CanvasGroup>();
            }
        }

        private void Update()
        {
            if (!isActive) return;

            UpdateHighlightAnimation();
            CheckStepCompletion();
        }

        private void UpdateHighlightAnimation()
        {
            if (highlightMask == null) return;

            // Pulse animation
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            highlightMask.localScale = Vector3.one * pulse;
        }

        private void CheckStepCompletion()
        {
            if (!requireActionToProgress) return;
            if (currentTutorial == null || currentStepIndex >= currentTutorial.steps.Count) return;

            var step = currentTutorial.steps[currentStepIndex];
            if (step.completionCheck != null && step.completionCheck())
            {
                NextStep();
            }
        }

        /// <summary>
        /// Start a tutorial sequence.
        /// </summary>
        public static void StartTutorial(Tutorial tutorial, bool forceReplay = false)
        {
            if (Instance == null) return;

            // Check if already completed
            if (!forceReplay && Instance.IsTutorialCompleted(tutorial.id))
            {
                Debug.Log($"[Tutorial] {tutorial.id} already completed, skipping");
                return;
            }

            Instance.BeginTutorial(tutorial);
        }

        private void BeginTutorial(Tutorial tutorial)
        {
            currentTutorial = tutorial;
            currentStepIndex = 0;
            isActive = true;

            // Pause game
            if (pauseGameDuringTutorial)
            {
                originalTimeScale = Time.timeScale;
                Time.timeScale = 0;
            }

            // Show UI
            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(true);
            }

            ShowStep(0);

            TutorialStarted?.Invoke(tutorial);
            onTutorialStarted?.Invoke();

            Debug.Log($"[Tutorial] Started: {tutorial.id}");
        }

        private void ShowStep(int stepIndex)
        {
            if (currentTutorial == null || stepIndex < 0 || stepIndex >= currentTutorial.steps.Count)
            {
                return;
            }

            currentStepIndex = stepIndex;
            var step = currentTutorial.steps[stepIndex];

            // Update UI
            if (titleText != null)
            {
                titleText.text = step.title;
            }

            if (descriptionText != null)
            {
                descriptionText.text = step.description;
            }

            if (tutorialImage != null)
            {
                tutorialImage.sprite = step.image;
                tutorialImage.gameObject.SetActive(step.image != null);
            }

            // Step counter
            if (stepCounterText != null)
            {
                stepCounterText.text = $"{stepIndex + 1} / {currentTutorial.steps.Count}";
            }

            // Button visibility
            if (prevButton != null)
            {
                prevButton.gameObject.SetActive(stepIndex > 0);
            }

            if (nextButton != null)
            {
                var buttonText = nextButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = stepIndex == currentTutorial.steps.Count - 1 ? "Finish" : "Next";
                }
            }

            // Highlight target
            UpdateHighlight(step);

            // Invoke step action
            step.onStepShown?.Invoke();

            StepChanged?.Invoke(stepIndex);
            onStepChanged?.Invoke(stepIndex);
        }

        private void UpdateHighlight(TutorialStep step)
        {
            if (step.highlightTarget != null)
            {
                // Position highlight over target
                RectTransform targetRect = step.highlightTarget as RectTransform;

                if (targetRect != null && highlightMask != null)
                {
                    highlightMask.gameObject.SetActive(true);
                    highlightMask.position = targetRect.position;
                    highlightMask.sizeDelta = targetRect.sizeDelta + Vector2.one * highlightPadding * 2;
                }

                // Position arrow
                if (pointerArrow != null)
                {
                    pointerArrow.gameObject.SetActive(true);
                    Vector3 arrowPos = targetRect.position;
                    arrowPos.y -= targetRect.sizeDelta.y / 2 + arrowOffset;
                    pointerArrow.position = arrowPos;
                }

                // Enable overlay
                if (overlayImage != null)
                {
                    overlayImage.gameObject.SetActive(true);
                    overlayImage.color = overlayColor;
                }
            }
            else
            {
                // No highlight target
                if (highlightMask != null)
                {
                    highlightMask.gameObject.SetActive(false);
                }

                if (pointerArrow != null)
                {
                    pointerArrow.gameObject.SetActive(false);
                }

                if (overlayImage != null)
                {
                    overlayImage.gameObject.SetActive(false);
                }
            }
        }

        public void NextStep()
        {
            if (currentStepIndex < currentTutorial.steps.Count - 1)
            {
                ShowStep(currentStepIndex + 1);
            }
            else
            {
                CompleteTutorial();
            }
        }

        public void PreviousStep()
        {
            if (currentStepIndex > 0)
            {
                ShowStep(currentStepIndex - 1);
            }
        }

        public void SkipTutorial()
        {
            TutorialSkipped?.Invoke(currentTutorial);
            onTutorialSkipped?.Invoke();
            EndTutorial(false);
        }

        private void CompleteTutorial()
        {
            if (currentTutorial != null)
            {
                MarkTutorialCompleted(currentTutorial.id);

                TutorialCompleted?.Invoke(currentTutorial);
                onTutorialCompleted?.Invoke();

                Debug.Log($"[Tutorial] Completed: {currentTutorial.id}");
            }

            EndTutorial(true);
        }

        private void EndTutorial(bool completed)
        {
            isActive = false;

            // Restore time
            if (pauseGameDuringTutorial)
            {
                Time.timeScale = originalTimeScale;
            }

            Hide();
            currentTutorial = null;
        }

        private void Hide()
        {
            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(false);
            }

            if (highlightMask != null)
            {
                highlightMask.gameObject.SetActive(false);
            }

            if (pointerArrow != null)
            {
                pointerArrow.gameObject.SetActive(false);
            }

            if (overlayImage != null)
            {
                overlayImage.gameObject.SetActive(false);
            }
        }

        private void MarkTutorialCompleted(string tutorialId)
        {
            completedTutorials.Add(tutorialId);
            SaveCompletedTutorials();
        }

        public bool IsTutorialCompleted(string tutorialId)
        {
            return completedTutorials.Contains(tutorialId);
        }

        private void LoadCompletedTutorials()
        {
            string data = PlayerPrefs.GetString(completedTutorialsKey, "");
            if (!string.IsNullOrEmpty(data))
            {
                string[] ids = data.Split(',');
                foreach (string id in ids)
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        completedTutorials.Add(id);
                    }
                }
            }
        }

        private void SaveCompletedTutorials()
        {
            string data = string.Join(",", completedTutorials);
            PlayerPrefs.SetString(completedTutorialsKey, data);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Reset all tutorial progress.
        /// </summary>
        public static void ResetAllProgress()
        {
            if (Instance == null) return;

            Instance.completedTutorials.Clear();
            PlayerPrefs.DeleteKey(Instance.completedTutorialsKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Reset specific tutorial progress.
        /// </summary>
        public static void ResetTutorial(string tutorialId)
        {
            if (Instance == null) return;

            Instance.completedTutorials.Remove(tutorialId);
            Instance.SaveCompletedTutorials();
        }
    }

    [Serializable]
    public class Tutorial
    {
        public string id;
        public string tutorialName;
        public List<TutorialStep> steps = new List<TutorialStep>();
    }

    [Serializable]
    public class TutorialStep
    {
        public string title;
        [TextArea(3, 5)]
        public string description;
        public Sprite image;
        public Transform highlightTarget;
        public Action onStepShown;
        public Func<bool> completionCheck;
    }
}
