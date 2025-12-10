using UnityEngine;

namespace UnityVault.Camera
{
    /// <summary>
    /// Smooth camera follow system with offset and damping.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private bool findPlayerOnStart = true;
        [SerializeField] private string playerTag = "Player";

        [Header("Position")]
        [SerializeField] private Vector3 offset = new Vector3(0, 5, -10);
        [SerializeField] private bool useLocalOffset = false;

        [Header("Smoothing")]
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private bool useSmoothDamp = false;
        [SerializeField] private float smoothTime = 0.3f;

        [Header("Look At")]
        [SerializeField] private bool lookAtTarget = true;
        [SerializeField] private Vector3 lookAtOffset = Vector3.up;
        [SerializeField] private float lookSmoothSpeed = 10f;

        [Header("Boundaries")]
        [SerializeField] private bool useBoundaries = false;
        [SerializeField] private Vector3 minBounds;
        [SerializeField] private Vector3 maxBounds;

        [Header("Deadzone")]
        [SerializeField] private bool useDeadzone = false;
        [SerializeField] private Vector2 deadzoneSize = new Vector2(2f, 2f);

        // State
        private Vector3 velocity;
        private Vector3 lastTargetPosition;
        private bool isInitialized;

        private void Start()
        {
            if (findPlayerOnStart && target == null)
            {
                var player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null)
                {
                    target = player.transform;
                }
            }

            if (target != null)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
            lastTargetPosition = target.position;
            transform.position = GetDesiredPosition();
            isInitialized = true;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (!isInitialized) Initialize();

            Vector3 targetPos = target.position;

            // Apply deadzone
            if (useDeadzone)
            {
                Vector3 delta = targetPos - lastTargetPosition;

                if (Mathf.Abs(delta.x) < deadzoneSize.x)
                {
                    targetPos.x = lastTargetPosition.x;
                }
                if (Mathf.Abs(delta.z) < deadzoneSize.y)
                {
                    targetPos.z = lastTargetPosition.z;
                }
            }

            Vector3 desiredPosition = GetDesiredPosition();

            // Apply smoothing
            Vector3 smoothedPosition;
            if (useSmoothDamp)
            {
                smoothedPosition = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
            }
            else
            {
                smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            }

            // Apply boundaries
            if (useBoundaries)
            {
                smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, minBounds.x, maxBounds.x);
                smoothedPosition.y = Mathf.Clamp(smoothedPosition.y, minBounds.y, maxBounds.y);
                smoothedPosition.z = Mathf.Clamp(smoothedPosition.z, minBounds.z, maxBounds.z);
            }

            transform.position = smoothedPosition;

            // Look at target
            if (lookAtTarget)
            {
                Vector3 lookAtPos = target.position + lookAtOffset;
                Quaternion targetRotation = Quaternion.LookRotation(lookAtPos - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lookSmoothSpeed * Time.deltaTime);
            }

            lastTargetPosition = target.position;
        }

        private Vector3 GetDesiredPosition()
        {
            if (useLocalOffset)
            {
                return target.position + target.TransformDirection(offset);
            }
            return target.position + offset;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                Initialize();
            }
        }

        public void SetOffset(Vector3 newOffset)
        {
            offset = newOffset;
        }

        public void SnapToTarget()
        {
            if (target != null)
            {
                transform.position = GetDesiredPosition();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (useBoundaries)
            {
                Gizmos.color = Color.yellow;
                Vector3 center = (minBounds + maxBounds) / 2f;
                Vector3 size = maxBounds - minBounds;
                Gizmos.DrawWireCube(center, size);
            }

            if (target != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, target.position);
                Gizmos.DrawWireSphere(GetDesiredPosition(), 0.3f);
            }
        }
    }
}
