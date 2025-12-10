using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityVault.Camera
{
    /// <summary>
    /// Cinematic camera system for cutscenes and special sequences.
    /// </summary>
    public class CinematicCamera : MonoBehaviour
    {
        public static CinematicCamera Instance { get; private set; }

        [Header("Camera Reference")]
        [SerializeField] private UnityEngine.Camera cinematicCamera;
        [SerializeField] private UnityEngine.Camera gameCamera;

        [Header("Settings")]
        [SerializeField] private float defaultTransitionTime = 1f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool hideUIOnCinematic = true;
        [SerializeField] private bool pauseGameOnCinematic = false;

        [Header("Letterbox")]
        [SerializeField] private bool useLetterbox = true;
        [SerializeField] private float letterboxSize = 0.1f;
        [SerializeField] private RectTransform topBar;
        [SerializeField] private RectTransform bottomBar;

        [Header("Events")]
        [SerializeField] private UnityEvent onCinematicStart;
        [SerializeField] private UnityEvent onCinematicEnd;
        [SerializeField] private UnityEvent<CinematicShot> onShotStart;

        // State
        private bool isPlaying;
        private CinematicSequence currentSequence;
        private int currentShotIndex;
        private Coroutine sequenceCoroutine;
        private Coroutine shotCoroutine;

        // Events
        public event Action CinematicStarted;
        public event Action CinematicEnded;
        public event Action<CinematicShot> ShotStarted;
        public event Action<CinematicShot> ShotEnded;

        public bool IsPlaying => isPlaying;
        public CinematicSequence CurrentSequence => currentSequence;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (cinematicCamera == null)
            {
                cinematicCamera = GetComponent<UnityEngine.Camera>();
            }

            SetCinematicMode(false);
        }

        /// <summary>
        /// Play a cinematic sequence.
        /// </summary>
        public void PlaySequence(CinematicSequence sequence)
        {
            if (sequence == null || sequence.shots == null || sequence.shots.Count == 0) return;
            if (isPlaying) StopSequence();

            currentSequence = sequence;
            sequenceCoroutine = StartCoroutine(PlaySequenceRoutine());
        }

        private IEnumerator PlaySequenceRoutine()
        {
            isPlaying = true;
            currentShotIndex = 0;

            SetCinematicMode(true);

            CinematicStarted?.Invoke();
            onCinematicStart?.Invoke();

            // Play each shot
            while (currentShotIndex < currentSequence.shots.Count)
            {
                CinematicShot shot = currentSequence.shots[currentShotIndex];
                yield return StartCoroutine(PlayShotRoutine(shot));
                currentShotIndex++;
            }

            SetCinematicMode(false);

            isPlaying = false;
            currentSequence = null;

            CinematicEnded?.Invoke();
            onCinematicEnd?.Invoke();
        }

        private IEnumerator PlayShotRoutine(CinematicShot shot)
        {
            ShotStarted?.Invoke(shot);
            onShotStart?.Invoke(shot);

            // Transition to shot
            if (shot.transitionTime > 0)
            {
                yield return StartCoroutine(TransitionToShot(shot));
            }
            else
            {
                ApplyShotInstant(shot);
            }

            // Hold for duration
            float holdTime = shot.duration;
            float elapsed = 0f;

            while (elapsed < holdTime)
            {
                // Apply camera movement during hold
                if (shot.moveType != CameraMoveType.Static)
                {
                    float t = elapsed / holdTime;
                    ApplyShotMovement(shot, t);
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            ShotEnded?.Invoke(shot);
        }

        private IEnumerator TransitionToShot(CinematicShot shot)
        {
            Vector3 startPos = cinematicCamera.transform.position;
            Quaternion startRot = cinematicCamera.transform.rotation;
            float startFOV = cinematicCamera.fieldOfView;

            Vector3 endPos = shot.startPosition != null ? shot.startPosition.position : startPos;
            Quaternion endRot = shot.startPosition != null ? shot.startPosition.rotation : startRot;
            float endFOV = shot.fieldOfView > 0 ? shot.fieldOfView : startFOV;

            float elapsed = 0f;
            float duration = shot.transitionTime;

            while (elapsed < duration)
            {
                float t = transitionCurve.Evaluate(elapsed / duration);

                cinematicCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
                cinematicCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                cinematicCamera.fieldOfView = Mathf.Lerp(startFOV, endFOV, t);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            cinematicCamera.transform.position = endPos;
            cinematicCamera.transform.rotation = endRot;
            cinematicCamera.fieldOfView = endFOV;
        }

        private void ApplyShotInstant(CinematicShot shot)
        {
            if (shot.startPosition != null)
            {
                cinematicCamera.transform.position = shot.startPosition.position;
                cinematicCamera.transform.rotation = shot.startPosition.rotation;
            }

            if (shot.fieldOfView > 0)
            {
                cinematicCamera.fieldOfView = shot.fieldOfView;
            }
        }

        private void ApplyShotMovement(CinematicShot shot, float t)
        {
            switch (shot.moveType)
            {
                case CameraMoveType.Dolly:
                    if (shot.startPosition != null && shot.endPosition != null)
                    {
                        cinematicCamera.transform.position = Vector3.Lerp(
                            shot.startPosition.position,
                            shot.endPosition.position,
                            shot.movementCurve.Evaluate(t)
                        );
                    }
                    break;

                case CameraMoveType.Pan:
                    if (shot.startPosition != null && shot.endPosition != null)
                    {
                        cinematicCamera.transform.rotation = Quaternion.Slerp(
                            shot.startPosition.rotation,
                            shot.endPosition.rotation,
                            shot.movementCurve.Evaluate(t)
                        );
                    }
                    break;

                case CameraMoveType.Zoom:
                    float startFOV = shot.fieldOfView > 0 ? shot.fieldOfView : 60f;
                    float endFOV = shot.endFieldOfView > 0 ? shot.endFieldOfView : startFOV;
                    cinematicCamera.fieldOfView = Mathf.Lerp(startFOV, endFOV, shot.movementCurve.Evaluate(t));
                    break;

                case CameraMoveType.Follow:
                    if (shot.followTarget != null)
                    {
                        Vector3 targetPos = shot.followTarget.position + shot.followOffset;
                        cinematicCamera.transform.position = Vector3.Lerp(
                            cinematicCamera.transform.position,
                            targetPos,
                            shot.followSpeed * Time.unscaledDeltaTime
                        );

                        if (shot.lookAtTarget)
                        {
                            cinematicCamera.transform.LookAt(shot.followTarget);
                        }
                    }
                    break;

                case CameraMoveType.Orbit:
                    if (shot.orbitTarget != null)
                    {
                        float angle = shot.orbitSpeed * t * 360f;
                        Vector3 offset = Quaternion.Euler(0, angle, 0) * (shot.startPosition != null ?
                            shot.startPosition.position - shot.orbitTarget.position : Vector3.back * 5f);
                        cinematicCamera.transform.position = shot.orbitTarget.position + offset;
                        cinematicCamera.transform.LookAt(shot.orbitTarget);
                    }
                    break;
            }
        }

        /// <summary>
        /// Stop current sequence.
        /// </summary>
        public void StopSequence()
        {
            if (sequenceCoroutine != null)
            {
                StopCoroutine(sequenceCoroutine);
            }

            if (shotCoroutine != null)
            {
                StopCoroutine(shotCoroutine);
            }

            SetCinematicMode(false);
            isPlaying = false;
            currentSequence = null;

            CinematicEnded?.Invoke();
            onCinematicEnd?.Invoke();
        }

        /// <summary>
        /// Skip to next shot.
        /// </summary>
        public void SkipShot()
        {
            if (!isPlaying) return;

            if (shotCoroutine != null)
            {
                StopCoroutine(shotCoroutine);
            }

            // Will continue to next shot in sequence
        }

        /// <summary>
        /// Skip entire sequence.
        /// </summary>
        public void SkipSequence()
        {
            StopSequence();
        }

        private void SetCinematicMode(bool enabled)
        {
            if (cinematicCamera != null)
            {
                cinematicCamera.enabled = enabled;
            }

            if (gameCamera != null)
            {
                gameCamera.enabled = !enabled;
            }

            if (useLetterbox)
            {
                SetLetterbox(enabled);
            }

            if (pauseGameOnCinematic)
            {
                Time.timeScale = enabled ? 0f : 1f;
            }
        }

        private void SetLetterbox(bool enabled)
        {
            if (topBar != null)
            {
                float targetY = enabled ? 0 : Screen.height * letterboxSize;
                topBar.gameObject.SetActive(enabled);
            }

            if (bottomBar != null)
            {
                bottomBar.gameObject.SetActive(enabled);
            }
        }

        /// <summary>
        /// Quick camera look at target.
        /// </summary>
        public void LookAt(Transform target, float duration = 0.5f)
        {
            if (target == null) return;
            StartCoroutine(LookAtRoutine(target, duration));
        }

        private IEnumerator LookAtRoutine(Transform target, float duration)
        {
            Quaternion startRot = cinematicCamera.transform.rotation;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                Vector3 direction = target.position - cinematicCamera.transform.position;
                Quaternion targetRot = Quaternion.LookRotation(direction);
                float t = transitionCurve.Evaluate(elapsed / duration);

                cinematicCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Move camera to position.
        /// </summary>
        public void MoveTo(Vector3 position, float duration = 1f)
        {
            StartCoroutine(MoveToRoutine(position, duration));
        }

        private IEnumerator MoveToRoutine(Vector3 position, float duration)
        {
            Vector3 startPos = cinematicCamera.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = transitionCurve.Evaluate(elapsed / duration);
                cinematicCamera.transform.position = Vector3.Lerp(startPos, position, t);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            cinematicCamera.transform.position = position;
        }
    }

    [Serializable]
    public class CinematicSequence
    {
        public string sequenceName;
        public List<CinematicShot> shots = new List<CinematicShot>();
        public bool loop;
        public bool skippable = true;
    }

    [Serializable]
    public class CinematicShot
    {
        public string shotName;
        public float duration = 3f;
        public float transitionTime = 0.5f;

        [Header("Camera Position")]
        public Transform startPosition;
        public Transform endPosition;
        public float fieldOfView = 60f;
        public float endFieldOfView;

        [Header("Movement")]
        public CameraMoveType moveType = CameraMoveType.Static;
        public AnimationCurve movementCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Follow/Orbit Settings")]
        public Transform followTarget;
        public Vector3 followOffset;
        public float followSpeed = 5f;
        public bool lookAtTarget;
        public Transform orbitTarget;
        public float orbitSpeed = 1f;

        [Header("Effects")]
        public bool enableDOF;
        public float focusDistance = 10f;
    }

    public enum CameraMoveType
    {
        Static,
        Dolly,
        Pan,
        Zoom,
        Follow,
        Orbit,
        Crane
    }
}
