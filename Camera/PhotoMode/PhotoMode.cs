using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System;
using System.Collections;

namespace UnityVault.Camera
{
    /// <summary>
    /// Photo mode system for capturing screenshots with camera controls.
    /// </summary>
    public class PhotoMode : MonoBehaviour
    {
        public static PhotoMode Instance { get; private set; }

        [Header("Camera")]
        [SerializeField] private UnityEngine.Camera photoCamera;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotateSpeed = 100f;
        [SerializeField] private float zoomSpeed = 10f;

        [Header("Camera Limits")]
        [SerializeField] private float minFOV = 20f;
        [SerializeField] private float maxFOV = 100f;
        [SerializeField] private float maxDistance = 50f;

        [Header("Effects")]
        [SerializeField] private bool enableDOF = true;
        [SerializeField] private bool enableFilters = true;
        [SerializeField] private PhotoFilter[] filters;

        [Header("UI")]
        [SerializeField] private GameObject photoModeUI;
        [SerializeField] private Slider fovSlider;
        [SerializeField] private Slider dofSlider;
        [SerializeField] private Slider brightnessSlider;
        [SerializeField] private Slider contrastSlider;
        [SerializeField] private Slider saturationSlider;
        [SerializeField] private Toggle hideUIToggle;
        [SerializeField] private Button captureButton;
        [SerializeField] private Button exitButton;

        [Header("Screenshot")]
        [SerializeField] private int screenshotWidth = 1920;
        [SerializeField] private int screenshotHeight = 1080;
        [SerializeField] private int superSampleSize = 2;
        [SerializeField] private string screenshotFolder = "Screenshots";

        [Header("Input")]
        [SerializeField] private KeyCode toggleKey = KeyCode.P;
        [SerializeField] private KeyCode captureKey = KeyCode.Space;
        [SerializeField] private KeyCode hideUIKey = KeyCode.H;

        [Header("Events")]
        [SerializeField] private UnityEvent onPhotoModeEnter;
        [SerializeField] private UnityEvent onPhotoModeExit;
        [SerializeField] private UnityEvent<string> onScreenshotTaken;

        // State
        private bool isActive;
        private bool isUIHidden;
        private Vector3 originalCameraPos;
        private Quaternion originalCameraRot;
        private float originalFOV;
        private float originalTimeScale;
        private int currentFilterIndex = -1;

        // Camera control
        private float pitch;
        private float yaw;

        // Events
        public event Action PhotoModeEntered;
        public event Action PhotoModeExited;
        public event Action<string> ScreenshotTaken;

        public bool IsActive => isActive;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (photoCamera == null)
            {
                photoCamera = UnityEngine.Camera.main;
            }

            SetupUI();
        }

        private void SetupUI()
        {
            if (fovSlider != null)
            {
                fovSlider.minValue = minFOV;
                fovSlider.maxValue = maxFOV;
                fovSlider.onValueChanged.AddListener(OnFOVChanged);
            }

            if (captureButton != null)
            {
                captureButton.onClick.AddListener(CaptureScreenshot);
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(ExitPhotoMode);
            }

            if (hideUIToggle != null)
            {
                hideUIToggle.onValueChanged.AddListener(SetUIHidden);
            }

            if (photoModeUI != null)
            {
                photoModeUI.SetActive(false);
            }
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey))
            {
                if (isActive)
                    ExitPhotoMode();
                else
                    EnterPhotoMode();
            }

            if (!isActive) return;

            HandleInput();

            if (UnityEngine.Input.GetKeyDown(captureKey))
            {
                CaptureScreenshot();
            }

            if (UnityEngine.Input.GetKeyDown(hideUIKey))
            {
                SetUIHidden(!isUIHidden);
            }
        }

        private void HandleInput()
        {
            // Movement
            float horizontal = UnityEngine.Input.GetAxis("Horizontal");
            float vertical = UnityEngine.Input.GetAxis("Vertical");
            float up = 0f;

            if (UnityEngine.Input.GetKey(KeyCode.E)) up = 1f;
            if (UnityEngine.Input.GetKey(KeyCode.Q)) up = -1f;

            Vector3 move = photoCamera.transform.right * horizontal +
                          photoCamera.transform.forward * vertical +
                          Vector3.up * up;

            photoCamera.transform.position += move * moveSpeed * Time.unscaledDeltaTime;

            // Rotation (right mouse button)
            if (UnityEngine.Input.GetMouseButton(1))
            {
                yaw += UnityEngine.Input.GetAxis("Mouse X") * rotateSpeed * Time.unscaledDeltaTime;
                pitch -= UnityEngine.Input.GetAxis("Mouse Y") * rotateSpeed * Time.unscaledDeltaTime;
                pitch = Mathf.Clamp(pitch, -89f, 89f);

                photoCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // Zoom (scroll)
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float newFOV = photoCamera.fieldOfView - scroll * zoomSpeed;
                photoCamera.fieldOfView = Mathf.Clamp(newFOV, minFOV, maxFOV);

                if (fovSlider != null)
                {
                    fovSlider.value = photoCamera.fieldOfView;
                }
            }

            // Roll
            float roll = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.Z)) roll = 1f;
            if (UnityEngine.Input.GetKey(KeyCode.C)) roll = -1f;

            if (roll != 0f)
            {
                photoCamera.transform.Rotate(Vector3.forward, roll * rotateSpeed * 0.5f * Time.unscaledDeltaTime, Space.Self);
            }
        }

        /// <summary>
        /// Enter photo mode.
        /// </summary>
        public void EnterPhotoMode()
        {
            if (isActive) return;

            isActive = true;

            // Save original state
            originalCameraPos = photoCamera.transform.position;
            originalCameraRot = photoCamera.transform.rotation;
            originalFOV = photoCamera.fieldOfView;
            originalTimeScale = Time.timeScale;

            // Initialize rotation
            Vector3 euler = photoCamera.transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
            if (pitch > 180f) pitch -= 360f;

            // Pause game
            Time.timeScale = 0f;

            // Show UI
            if (photoModeUI != null)
            {
                photoModeUI.SetActive(true);
            }

            // Update sliders
            if (fovSlider != null)
            {
                fovSlider.value = photoCamera.fieldOfView;
            }

            // Hide cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            PhotoModeEntered?.Invoke();
            onPhotoModeEnter?.Invoke();

            Debug.Log("[PhotoMode] Entered");
        }

        /// <summary>
        /// Exit photo mode.
        /// </summary>
        public void ExitPhotoMode()
        {
            if (!isActive) return;

            isActive = false;

            // Restore camera
            photoCamera.transform.position = originalCameraPos;
            photoCamera.transform.rotation = originalCameraRot;
            photoCamera.fieldOfView = originalFOV;

            // Resume game
            Time.timeScale = originalTimeScale;

            // Hide UI
            if (photoModeUI != null)
            {
                photoModeUI.SetActive(false);
            }

            // Clear filter
            ClearFilter();

            PhotoModeExited?.Invoke();
            onPhotoModeExit?.Invoke();

            Debug.Log("[PhotoMode] Exited");
        }

        /// <summary>
        /// Capture screenshot.
        /// </summary>
        public void CaptureScreenshot()
        {
            StartCoroutine(CaptureRoutine());
        }

        private IEnumerator CaptureRoutine()
        {
            // Hide UI temporarily
            bool wasUIHidden = isUIHidden;
            if (!wasUIHidden && photoModeUI != null)
            {
                photoModeUI.SetActive(false);
            }

            yield return new WaitForEndOfFrame();

            // Create render texture
            int width = screenshotWidth * superSampleSize;
            int height = screenshotHeight * superSampleSize;

            RenderTexture rt = new RenderTexture(width, height, 24);
            photoCamera.targetTexture = rt;
            photoCamera.Render();

            // Read pixels
            RenderTexture.active = rt;
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();

            // Cleanup
            photoCamera.targetTexture = null;
            RenderTexture.active = null;
            Destroy(rt);

            // Downsample if supersampled
            if (superSampleSize > 1)
            {
                screenshot = ResizeTexture(screenshot, screenshotWidth, screenshotHeight);
            }

            // Save to file
            string filename = SaveScreenshot(screenshot);

            Destroy(screenshot);

            // Restore UI
            if (!wasUIHidden && photoModeUI != null)
            {
                photoModeUI.SetActive(true);
            }

            ScreenshotTaken?.Invoke(filename);
            onScreenshotTaken?.Invoke(filename);

            Debug.Log($"[PhotoMode] Screenshot saved: {filename}");
        }

        private string SaveScreenshot(Texture2D texture)
        {
            byte[] bytes = texture.EncodeToPNG();

            string folder = System.IO.Path.Combine(Application.persistentDataPath, screenshotFolder);
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = System.IO.Path.Combine(folder, $"Screenshot_{timestamp}.png");

            System.IO.File.WriteAllBytes(filename, bytes);

            return filename;
        }

        private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            Graphics.Blit(source, rt);

            RenderTexture.active = rt;
            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(source);

            return result;
        }

        /// <summary>
        /// Apply filter.
        /// </summary>
        public void ApplyFilter(int filterIndex)
        {
            if (filters == null || filterIndex < 0 || filterIndex >= filters.Length) return;

            currentFilterIndex = filterIndex;
            PhotoFilter filter = filters[filterIndex];

            // Apply filter settings (would integrate with post-processing)
            Debug.Log($"[PhotoMode] Applied filter: {filter.filterName}");
        }

        /// <summary>
        /// Clear filter.
        /// </summary>
        public void ClearFilter()
        {
            currentFilterIndex = -1;
        }

        /// <summary>
        /// Cycle to next filter.
        /// </summary>
        public void NextFilter()
        {
            if (filters == null || filters.Length == 0) return;

            currentFilterIndex = (currentFilterIndex + 1) % filters.Length;
            ApplyFilter(currentFilterIndex);
        }

        private void OnFOVChanged(float value)
        {
            if (photoCamera != null)
            {
                photoCamera.fieldOfView = value;
            }
        }

        private void SetUIHidden(bool hidden)
        {
            isUIHidden = hidden;

            if (photoModeUI != null)
            {
                // Keep essential buttons visible
                photoModeUI.SetActive(!hidden);
            }
        }

        /// <summary>
        /// Reset camera position.
        /// </summary>
        public void ResetCamera()
        {
            photoCamera.transform.position = originalCameraPos;
            photoCamera.transform.rotation = originalCameraRot;
            photoCamera.fieldOfView = originalFOV;

            Vector3 euler = originalCameraRot.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
            if (pitch > 180f) pitch -= 360f;
        }

        /// <summary>
        /// Focus on target.
        /// </summary>
        public void FocusOnTarget(Transform target, float distance = 5f)
        {
            if (target == null) return;

            Vector3 direction = (photoCamera.transform.position - target.position).normalized;
            photoCamera.transform.position = target.position + direction * distance;
            photoCamera.transform.LookAt(target);

            Vector3 euler = photoCamera.transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
            if (pitch > 180f) pitch -= 360f;
        }
    }

    [Serializable]
    public class PhotoFilter
    {
        public string filterName;
        public float brightness = 1f;
        public float contrast = 1f;
        public float saturation = 1f;
        public Color tint = Color.white;
        public bool enableVignette;
        public bool enableFilmGrain;
    }
}
