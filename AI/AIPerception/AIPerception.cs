using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.AI
{
    /// <summary>
    /// AI perception system with sight, hearing, and awareness mechanics.
    /// </summary>
    public class AIPerception : MonoBehaviour
    {
        [Header("Vision")]
        [SerializeField] private float sightRange = 20f;
        [SerializeField] private float sightAngle = 120f;
        [SerializeField] private float peripheralRange = 5f;
        [SerializeField] private LayerMask targetLayer;
        [SerializeField] private LayerMask obstacleLayer;
        [SerializeField] private Transform eyePoint;

        [Header("Hearing")]
        [SerializeField] private float hearingRange = 15f;
        [SerializeField] private float hearingThreshold = 0.1f;

        [Header("Awareness")]
        [SerializeField] private float awarenessDecayRate = 10f;
        [SerializeField] private float awarenessGainRate = 50f;
        [SerializeField] private float detectionThreshold = 100f;
        [SerializeField] private float suspicionThreshold = 30f;

        [Header("Memory")]
        [SerializeField] private float memoryDuration = 10f;
        [SerializeField] private float searchDuration = 5f;

        [Header("Update")]
        [SerializeField] private float perceptionUpdateRate = 0.2f;

        [Header("Events")]
        [SerializeField] private UnityEvent<Transform> onTargetDetected;
        [SerializeField] private UnityEvent<Transform> onTargetLost;
        [SerializeField] private UnityEvent<Vector3> onSoundHeard;
        [SerializeField] private UnityEvent<Transform> onSuspicionRaised;
        [SerializeField] private UnityEvent onSearchStarted;
        [SerializeField] private UnityEvent onSearchEnded;

        // State
        private Dictionary<Transform, PerceivedTarget> perceivedTargets = new Dictionary<Transform, PerceivedTarget>();
        private List<SoundEvent> heardSounds = new List<SoundEvent>();
        private Transform currentTarget;
        private Vector3 lastKnownPosition;
        private PerceptionState currentState = PerceptionState.Idle;
        private float searchTimer;
        private float lastPerceptionUpdate;

        // Properties
        public Transform CurrentTarget => currentTarget;
        public bool HasTarget => currentTarget != null;
        public Vector3 LastKnownPosition => lastKnownPosition;
        public PerceptionState State => currentState;
        public bool IsSearching => currentState == PerceptionState.Searching;
        public bool IsAlert => currentState == PerceptionState.Alert;

        // Events
        public event Action<Transform> TargetDetected;
        public event Action<Transform> TargetLost;
        public event Action<Vector3, float> SoundHeard;
        public event Action<Transform> SuspicionRaised;
        public event Action SearchStarted;
        public event Action SearchEnded;

        public enum PerceptionState
        {
            Idle,
            Suspicious,
            Alert,
            Searching
        }

        private void Awake()
        {
            if (eyePoint == null)
            {
                eyePoint = transform;
            }
        }

        private void Update()
        {
            if (Time.time - lastPerceptionUpdate >= perceptionUpdateRate)
            {
                lastPerceptionUpdate = Time.time;
                UpdatePerception();
            }

            UpdateAwareness();
            UpdateState();
            CleanupMemory();
        }

        private void UpdatePerception()
        {
            // Vision check
            Collider[] colliders = Physics.OverlapSphere(eyePoint.position, sightRange, targetLayer);

            foreach (var col in colliders)
            {
                Transform target = col.transform;
                if (target == transform) continue;

                float distance = Vector3.Distance(eyePoint.position, target.position);
                bool canSee = CanSeeTarget(target, distance);

                if (canSee)
                {
                    OnTargetSeen(target, distance);
                }
            }
        }

        private bool CanSeeTarget(Transform target, float distance)
        {
            // Peripheral vision (close range, any angle)
            if (distance <= peripheralRange)
            {
                return HasLineOfSight(target);
            }

            // Normal vision (cone check)
            Vector3 dirToTarget = (target.position - eyePoint.position).normalized;
            float angle = Vector3.Angle(eyePoint.forward, dirToTarget);

            if (angle <= sightAngle * 0.5f)
            {
                return HasLineOfSight(target);
            }

            return false;
        }

        private bool HasLineOfSight(Transform target)
        {
            Vector3 direction = target.position - eyePoint.position;
            float distance = direction.magnitude;

            // Add offset to target center
            Vector3 targetPoint = target.position + Vector3.up * 1f;
            direction = targetPoint - eyePoint.position;

            return !Physics.Raycast(eyePoint.position, direction.normalized, distance, obstacleLayer);
        }

        private void OnTargetSeen(Transform target, float distance)
        {
            if (!perceivedTargets.ContainsKey(target))
            {
                perceivedTargets[target] = new PerceivedTarget();
            }

            var perceived = perceivedTargets[target];
            perceived.lastSeenTime = Time.time;
            perceived.lastSeenPosition = target.position;
            perceived.isCurrentlyVisible = true;

            // Calculate visibility factor (closer = faster detection)
            float visibilityFactor = 1f - (distance / sightRange);
            perceived.awareness += awarenessGainRate * visibilityFactor * perceptionUpdateRate;

            // Check for detection
            if (perceived.awareness >= detectionThreshold && !perceived.isDetected)
            {
                perceived.isDetected = true;
                OnTargetFullyDetected(target);
            }
            else if (perceived.awareness >= suspicionThreshold && !perceived.isSuspicious)
            {
                perceived.isSuspicious = true;
                SuspicionRaised?.Invoke(target);
                onSuspicionRaised?.Invoke(target);
            }
        }

        private void OnTargetFullyDetected(Transform target)
        {
            currentTarget = target;
            lastKnownPosition = target.position;
            currentState = PerceptionState.Alert;

            TargetDetected?.Invoke(target);
            onTargetDetected?.Invoke(target);

            Debug.Log($"[Perception] Target detected: {target.name}");
        }

        private void UpdateAwareness()
        {
            List<Transform> toRemove = new List<Transform>();

            foreach (var kvp in perceivedTargets)
            {
                var perceived = kvp.Value;

                // Decay awareness if not visible
                if (!perceived.isCurrentlyVisible)
                {
                    perceived.awareness -= awarenessDecayRate * Time.deltaTime;

                    if (perceived.awareness <= 0)
                    {
                        if (perceived.isDetected)
                        {
                            OnTargetLostFromView(kvp.Key);
                        }
                        toRemove.Add(kvp.Key);
                    }
                }

                perceived.isCurrentlyVisible = false;
            }

            foreach (var target in toRemove)
            {
                perceivedTargets.Remove(target);
            }
        }

        private void OnTargetLostFromView(Transform target)
        {
            if (currentTarget == target)
            {
                lastKnownPosition = perceivedTargets[target].lastSeenPosition;
                currentState = PerceptionState.Searching;
                searchTimer = searchDuration;

                SearchStarted?.Invoke();
                onSearchStarted?.Invoke();
            }

            TargetLost?.Invoke(target);
            onTargetLost?.Invoke(target);

            Debug.Log($"[Perception] Lost sight of: {target.name}");
        }

        private void UpdateState()
        {
            switch (currentState)
            {
                case PerceptionState.Searching:
                    searchTimer -= Time.deltaTime;
                    if (searchTimer <= 0)
                    {
                        EndSearch();
                    }
                    break;

                case PerceptionState.Alert:
                    if (currentTarget == null || !perceivedTargets.ContainsKey(currentTarget))
                    {
                        currentState = PerceptionState.Searching;
                        searchTimer = searchDuration;
                        SearchStarted?.Invoke();
                        onSearchStarted?.Invoke();
                    }
                    else
                    {
                        lastKnownPosition = currentTarget.position;
                    }
                    break;
            }
        }

        private void EndSearch()
        {
            currentState = PerceptionState.Idle;
            currentTarget = null;

            SearchEnded?.Invoke();
            onSearchEnded?.Invoke();

            Debug.Log("[Perception] Search ended");
        }

        private void CleanupMemory()
        {
            // Remove old sounds
            heardSounds.RemoveAll(s => Time.time - s.time > memoryDuration);
        }

        /// <summary>
        /// Report a sound event to this AI.
        /// </summary>
        public void HearSound(Vector3 position, float volume, Transform source = null)
        {
            float distance = Vector3.Distance(transform.position, position);
            float effectiveVolume = volume * (1f - distance / hearingRange);

            if (effectiveVolume < hearingThreshold) return;

            var soundEvent = new SoundEvent
            {
                position = position,
                volume = effectiveVolume,
                time = Time.time,
                source = source
            };

            heardSounds.Add(soundEvent);

            // React to sound
            if (currentState == PerceptionState.Idle)
            {
                currentState = PerceptionState.Suspicious;
                lastKnownPosition = position;
            }
            else if (currentState == PerceptionState.Searching)
            {
                // Update search location
                lastKnownPosition = position;
                searchTimer = searchDuration;
            }

            SoundHeard?.Invoke(position, effectiveVolume);
            onSoundHeard?.Invoke(position);

            Debug.Log($"[Perception] Heard sound at {position} (volume: {effectiveVolume:F2})");
        }

        /// <summary>
        /// Force awareness of a target.
        /// </summary>
        public void AlertToTarget(Transform target)
        {
            if (!perceivedTargets.ContainsKey(target))
            {
                perceivedTargets[target] = new PerceivedTarget();
            }

            var perceived = perceivedTargets[target];
            perceived.awareness = detectionThreshold;
            perceived.isDetected = true;
            perceived.lastSeenPosition = target.position;
            perceived.lastSeenTime = Time.time;

            OnTargetFullyDetected(target);
        }

        /// <summary>
        /// Get all perceived targets.
        /// </summary>
        public List<Transform> GetPerceivedTargets()
        {
            return new List<Transform>(perceivedTargets.Keys);
        }

        /// <summary>
        /// Get awareness level for a target (0-100).
        /// </summary>
        public float GetAwarenessLevel(Transform target)
        {
            return perceivedTargets.TryGetValue(target, out var perceived) ? perceived.awareness : 0f;
        }

        /// <summary>
        /// Check if can currently see a specific target.
        /// </summary>
        public bool CanCurrentlySee(Transform target)
        {
            if (target == null) return false;

            float distance = Vector3.Distance(eyePoint.position, target.position);
            if (distance > sightRange) return false;

            return CanSeeTarget(target, distance);
        }

        public void ForgetTarget(Transform target)
        {
            perceivedTargets.Remove(target);
            if (currentTarget == target)
            {
                currentTarget = null;
                currentState = PerceptionState.Idle;
            }
        }

        public void ForgetAll()
        {
            perceivedTargets.Clear();
            heardSounds.Clear();
            currentTarget = null;
            currentState = PerceptionState.Idle;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 eyePos = eyePoint != null ? eyePoint.position : transform.position;
            Vector3 forward = eyePoint != null ? eyePoint.forward : transform.forward;

            // Sight range
            Gizmos.color = new Color(1, 1, 0, 0.2f);
            Gizmos.DrawWireSphere(eyePos, sightRange);

            // Peripheral range
            Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
            Gizmos.DrawWireSphere(eyePos, peripheralRange);

            // Hearing range
            Gizmos.color = new Color(0, 1, 1, 0.1f);
            Gizmos.DrawWireSphere(eyePos, hearingRange);

            // Vision cone
            Gizmos.color = Color.yellow;
            Vector3 leftDir = Quaternion.Euler(0, -sightAngle * 0.5f, 0) * forward;
            Vector3 rightDir = Quaternion.Euler(0, sightAngle * 0.5f, 0) * forward;
            Gizmos.DrawRay(eyePos, leftDir * sightRange);
            Gizmos.DrawRay(eyePos, rightDir * sightRange);

            // Last known position
            if (lastKnownPosition != Vector3.zero)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(lastKnownPosition, 0.5f);
                Gizmos.DrawLine(eyePos, lastKnownPosition);
            }

            // Current target
            if (currentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(eyePos, currentTarget.position);
            }
        }

        private class PerceivedTarget
        {
            public float awareness;
            public float lastSeenTime;
            public Vector3 lastSeenPosition;
            public bool isCurrentlyVisible;
            public bool isSuspicious;
            public bool isDetected;
        }

        private struct SoundEvent
        {
            public Vector3 position;
            public float volume;
            public float time;
            public Transform source;
        }
    }
}
