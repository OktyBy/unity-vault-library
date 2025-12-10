using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.UI
{
    /// <summary>
    /// Settings menu with video, audio, and control options.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject videoPanel;
        [SerializeField] private GameObject audioPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private GameObject gameplayPanel;

        [Header("Video Settings")]
        [SerializeField] private Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Dropdown qualityDropdown;
        [SerializeField] private Slider brightnessSlider;
        [SerializeField] private Toggle vsyncToggle;
        [SerializeField] private Dropdown frameRateDropdown;

        [Header("Audio Settings")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider voiceVolumeSlider;
        [SerializeField] private Toggle muteToggle;

        [Header("Controls Settings")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Toggle invertYToggle;
        [SerializeField] private Toggle invertXToggle;
        [SerializeField] private Slider aimSensitivitySlider;

        [Header("Gameplay Settings")]
        [SerializeField] private Toggle subtitlesToggle;
        [SerializeField] private Dropdown languageDropdown;
        [SerializeField] private Slider fovSlider;
        [SerializeField] private Toggle screenshakeToggle;
        [SerializeField] private Toggle tutorialToggle;

        [Header("Buttons")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button backButton;

        [Header("Events")]
        [SerializeField] private UnityEvent onSettingsApplied;
        [SerializeField] private UnityEvent onSettingsReset;

        // Current settings
        private GameSettings currentSettings;
        private GameSettings pendingSettings;
        private List<Resolution> availableResolutions;

        // Events
        public event Action<GameSettings> SettingsApplied;
        public event Action SettingsReset;

        private const string SETTINGS_KEY = "GameSettings";

        private void Awake()
        {
            LoadSettings();
            SetupUI();
        }

        private void SetupUI()
        {
            // Setup resolution dropdown
            if (resolutionDropdown != null)
            {
                SetupResolutionDropdown();
            }

            // Setup quality dropdown
            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            }

            // Setup frame rate dropdown
            if (frameRateDropdown != null)
            {
                frameRateDropdown.ClearOptions();
                frameRateDropdown.AddOptions(new List<string> { "30", "60", "120", "Unlimited" });
            }

            // Setup language dropdown
            if (languageDropdown != null)
            {
                languageDropdown.ClearOptions();
                languageDropdown.AddOptions(new List<string> { "English", "Spanish", "French", "German", "Japanese", "Chinese" });
            }

            // Connect button events
            if (applyButton != null)
            {
                applyButton.onClick.AddListener(ApplySettings);
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(ResetToDefaults);
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackPressed);
            }

            // Connect slider events
            ConnectSliderEvents();

            // Apply current settings to UI
            ApplySettingsToUI(currentSettings);
        }

        private void SetupResolutionDropdown()
        {
            availableResolutions = new List<Resolution>();
            List<string> options = new List<string>();

            Resolution[] resolutions = Screen.resolutions;
            int currentResIndex = 0;

            for (int i = 0; i < resolutions.Length; i++)
            {
                Resolution res = resolutions[i];

                // Filter out very low resolutions and duplicates
                if (res.width < 800) continue;

                string option = $"{res.width} x {res.height}";

                // Check for duplicate
                if (!options.Contains(option))
                {
                    options.Add(option);
                    availableResolutions.Add(res);

                    if (res.width == Screen.currentResolution.width &&
                        res.height == Screen.currentResolution.height)
                    {
                        currentResIndex = availableResolutions.Count - 1;
                    }
                }
            }

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentResIndex;
        }

        private void ConnectSliderEvents()
        {
            // Volume sliders
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.AddListener(v => {
                    pendingSettings.masterVolume = v;
                    AudioListener.volume = v;
                });
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.onValueChanged.AddListener(v => pendingSettings.musicVolume = v);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.AddListener(v => pendingSettings.sfxVolume = v);
            }

            if (voiceVolumeSlider != null)
            {
                voiceVolumeSlider.onValueChanged.AddListener(v => pendingSettings.voiceVolume = v);
            }

            // Control sliders
            if (sensitivitySlider != null)
            {
                sensitivitySlider.onValueChanged.AddListener(v => pendingSettings.mouseSensitivity = v);
            }

            if (aimSensitivitySlider != null)
            {
                aimSensitivitySlider.onValueChanged.AddListener(v => pendingSettings.aimSensitivity = v);
            }

            // Video sliders
            if (brightnessSlider != null)
            {
                brightnessSlider.onValueChanged.AddListener(v => pendingSettings.brightness = v);
            }

            if (fovSlider != null)
            {
                fovSlider.onValueChanged.AddListener(v => pendingSettings.fieldOfView = v);
            }
        }

        private void ApplySettingsToUI(GameSettings settings)
        {
            pendingSettings = settings.Clone();

            // Video
            if (resolutionDropdown != null)
            {
                int resIndex = availableResolutions.FindIndex(r =>
                    r.width == settings.resolutionWidth && r.height == settings.resolutionHeight);
                if (resIndex >= 0) resolutionDropdown.value = resIndex;
            }

            if (fullscreenToggle != null) fullscreenToggle.isOn = settings.fullscreen;
            if (qualityDropdown != null) qualityDropdown.value = settings.qualityLevel;
            if (brightnessSlider != null) brightnessSlider.value = settings.brightness;
            if (vsyncToggle != null) vsyncToggle.isOn = settings.vsync;

            // Audio
            if (masterVolumeSlider != null) masterVolumeSlider.value = settings.masterVolume;
            if (musicVolumeSlider != null) musicVolumeSlider.value = settings.musicVolume;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = settings.sfxVolume;
            if (voiceVolumeSlider != null) voiceVolumeSlider.value = settings.voiceVolume;
            if (muteToggle != null) muteToggle.isOn = settings.muted;

            // Controls
            if (sensitivitySlider != null) sensitivitySlider.value = settings.mouseSensitivity;
            if (invertYToggle != null) invertYToggle.isOn = settings.invertY;
            if (invertXToggle != null) invertXToggle.isOn = settings.invertX;
            if (aimSensitivitySlider != null) aimSensitivitySlider.value = settings.aimSensitivity;

            // Gameplay
            if (subtitlesToggle != null) subtitlesToggle.isOn = settings.subtitles;
            if (fovSlider != null) fovSlider.value = settings.fieldOfView;
            if (screenshakeToggle != null) screenshakeToggle.isOn = settings.screenShake;
            if (tutorialToggle != null) tutorialToggle.isOn = settings.showTutorials;
        }

        public void ApplySettings()
        {
            // Gather settings from UI
            GatherSettingsFromUI();

            // Apply to game
            ApplyToGame(pendingSettings);

            // Save
            currentSettings = pendingSettings.Clone();
            SaveSettings();

            SettingsApplied?.Invoke(currentSettings);
            onSettingsApplied?.Invoke();

            Debug.Log("[Settings] Applied and saved");
        }

        private void GatherSettingsFromUI()
        {
            // Video
            if (resolutionDropdown != null && resolutionDropdown.value < availableResolutions.Count)
            {
                Resolution res = availableResolutions[resolutionDropdown.value];
                pendingSettings.resolutionWidth = res.width;
                pendingSettings.resolutionHeight = res.height;
            }

            if (fullscreenToggle != null) pendingSettings.fullscreen = fullscreenToggle.isOn;
            if (qualityDropdown != null) pendingSettings.qualityLevel = qualityDropdown.value;
            if (vsyncToggle != null) pendingSettings.vsync = vsyncToggle.isOn;

            // Frame rate
            if (frameRateDropdown != null)
            {
                int[] rates = { 30, 60, 120, -1 };
                pendingSettings.targetFrameRate = rates[Mathf.Min(frameRateDropdown.value, rates.Length - 1)];
            }

            // Audio
            if (muteToggle != null) pendingSettings.muted = muteToggle.isOn;

            // Controls
            if (invertYToggle != null) pendingSettings.invertY = invertYToggle.isOn;
            if (invertXToggle != null) pendingSettings.invertX = invertXToggle.isOn;

            // Gameplay
            if (subtitlesToggle != null) pendingSettings.subtitles = subtitlesToggle.isOn;
            if (screenshakeToggle != null) pendingSettings.screenShake = screenshakeToggle.isOn;
            if (tutorialToggle != null) pendingSettings.showTutorials = tutorialToggle.isOn;
            if (languageDropdown != null) pendingSettings.languageIndex = languageDropdown.value;
        }

        private void ApplyToGame(GameSettings settings)
        {
            // Resolution
            Screen.SetResolution(settings.resolutionWidth, settings.resolutionHeight, settings.fullscreen);

            // Quality
            QualitySettings.SetQualityLevel(settings.qualityLevel);

            // VSync
            QualitySettings.vSyncCount = settings.vsync ? 1 : 0;

            // Frame rate
            Application.targetFrameRate = settings.targetFrameRate;

            // Audio
            AudioListener.volume = settings.muted ? 0 : settings.masterVolume;

            // Field of view
            if (Camera.main != null)
            {
                Camera.main.fieldOfView = settings.fieldOfView;
            }
        }

        public void ResetToDefaults()
        {
            currentSettings = GameSettings.GetDefaults();
            ApplySettingsToUI(currentSettings);
            ApplyToGame(currentSettings);
            SaveSettings();

            SettingsReset?.Invoke();
            onSettingsReset?.Invoke();

            Debug.Log("[Settings] Reset to defaults");
        }

        private void OnBackPressed()
        {
            // Revert pending changes
            ApplySettingsToUI(currentSettings);
            gameObject.SetActive(false);
        }

        private void SaveSettings()
        {
            string json = JsonUtility.ToJson(currentSettings);
            PlayerPrefs.SetString(SETTINGS_KEY, json);
            PlayerPrefs.Save();
        }

        private void LoadSettings()
        {
            if (PlayerPrefs.HasKey(SETTINGS_KEY))
            {
                string json = PlayerPrefs.GetString(SETTINGS_KEY);
                currentSettings = JsonUtility.FromJson<GameSettings>(json);
            }
            else
            {
                currentSettings = GameSettings.GetDefaults();
            }

            pendingSettings = currentSettings.Clone();
            ApplyToGame(currentSettings);
        }

        // Panel switching
        public void ShowVideoPanel()
        {
            SetActivePanel(videoPanel);
        }

        public void ShowAudioPanel()
        {
            SetActivePanel(audioPanel);
        }

        public void ShowControlsPanel()
        {
            SetActivePanel(controlsPanel);
        }

        public void ShowGameplayPanel()
        {
            SetActivePanel(gameplayPanel);
        }

        private void SetActivePanel(GameObject panel)
        {
            if (videoPanel != null) videoPanel.SetActive(panel == videoPanel);
            if (audioPanel != null) audioPanel.SetActive(panel == audioPanel);
            if (controlsPanel != null) controlsPanel.SetActive(panel == controlsPanel);
            if (gameplayPanel != null) gameplayPanel.SetActive(panel == gameplayPanel);
        }

        // Public getters
        public GameSettings GetCurrentSettings() => currentSettings.Clone();
        public float GetMasterVolume() => currentSettings.masterVolume;
        public float GetMusicVolume() => currentSettings.musicVolume;
        public float GetSFXVolume() => currentSettings.sfxVolume;
        public float GetMouseSensitivity() => currentSettings.mouseSensitivity;
        public bool GetInvertY() => currentSettings.invertY;
    }

    [Serializable]
    public class GameSettings
    {
        // Video
        public int resolutionWidth = 1920;
        public int resolutionHeight = 1080;
        public bool fullscreen = true;
        public int qualityLevel = 2;
        public float brightness = 1f;
        public bool vsync = true;
        public int targetFrameRate = 60;

        // Audio
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public float voiceVolume = 1f;
        public bool muted = false;

        // Controls
        public float mouseSensitivity = 1f;
        public float aimSensitivity = 0.8f;
        public bool invertY = false;
        public bool invertX = false;

        // Gameplay
        public bool subtitles = true;
        public int languageIndex = 0;
        public float fieldOfView = 60f;
        public bool screenShake = true;
        public bool showTutorials = true;

        public static GameSettings GetDefaults()
        {
            return new GameSettings
            {
                resolutionWidth = Screen.currentResolution.width,
                resolutionHeight = Screen.currentResolution.height,
                fullscreen = true,
                qualityLevel = QualitySettings.GetQualityLevel(),
                brightness = 1f,
                vsync = true,
                targetFrameRate = 60,
                masterVolume = 1f,
                musicVolume = 0.8f,
                sfxVolume = 1f,
                voiceVolume = 1f,
                muted = false,
                mouseSensitivity = 1f,
                aimSensitivity = 0.8f,
                invertY = false,
                invertX = false,
                subtitles = true,
                languageIndex = 0,
                fieldOfView = 60f,
                screenShake = true,
                showTutorials = true
            };
        }

        public GameSettings Clone()
        {
            return (GameSettings)MemberwiseClone();
        }
    }
}
