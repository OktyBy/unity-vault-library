using UnityEngine;
using UnityEngine.Events;

namespace UnityVault.World
{
    /// <summary>
    /// Base interactable component for objects that can be interacted with.
    /// </summary>
    public class Interactable : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [SerializeField] private string interactionPrompt = "Press E to interact";
        [SerializeField] private float interactionRange = 2f;
        [SerializeField] private bool requireLookAt = true;
        [SerializeField] private float lookAtAngle = 45f;
        [SerializeField] private bool canInteractMultipleTimes = true;
        [SerializeField] private float cooldownTime = 0.5f;

        [Header("Highlight")]
        [SerializeField] private bool highlightOnHover = true;
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private Color outlineColor = Color.yellow;

        [Header("Events")]
        [SerializeField] private UnityEvent onInteract;
        [SerializeField] private UnityEvent onEnterRange;
        [SerializeField] private UnityEvent onExitRange;

        // State
        private bool isInRange;
        private bool hasInteracted;
        private float lastInteractTime;
        private Renderer objectRenderer;
        private Material originalMaterial;

        public string InteractionPrompt => interactionPrompt;
        public float InteractionRange => interactionRange;
        public bool IsInRange => isInRange;
        public bool CanInteract => canInteractMultipleTimes || !hasInteracted;

        public event System.Action<GameObject> Interacted;

        private void Awake()
        {
            objectRenderer = GetComponent<Renderer>();
            if (objectRenderer != null)
            {
                originalMaterial = objectRenderer.material;
            }
        }

        public virtual bool TryInteract(GameObject interactor)
        {
            if (!CanInteract) return false;
            if (Time.time - lastInteractTime < cooldownTime) return false;

            if (requireLookAt && !IsLookingAt(interactor.transform))
            {
                return false;
            }

            Interact(interactor);
            return true;
        }

        protected virtual void Interact(GameObject interactor)
        {
            hasInteracted = true;
            lastInteractTime = Time.time;

            Interacted?.Invoke(interactor);
            onInteract?.Invoke();

            Debug.Log($"[Interactable] {interactor.name} interacted with {gameObject.name}");
        }

        private bool IsLookingAt(Transform interactor)
        {
            Vector3 directionToObject = (transform.position - interactor.position).normalized;
            float angle = Vector3.Angle(interactor.forward, directionToObject);
            return angle <= lookAtAngle;
        }

        public void SetInRange(bool inRange)
        {
            if (isInRange == inRange) return;

            isInRange = inRange;

            if (isInRange)
            {
                OnEnterRange();
            }
            else
            {
                OnExitRange();
            }
        }

        protected virtual void OnEnterRange()
        {
            if (highlightOnHover)
            {
                SetHighlight(true);
            }

            onEnterRange?.Invoke();
        }

        protected virtual void OnExitRange()
        {
            if (highlightOnHover)
            {
                SetHighlight(false);
            }

            onExitRange?.Invoke();
        }

        private void SetHighlight(bool highlighted)
        {
            if (objectRenderer == null) return;

            if (highlighted && highlightMaterial != null)
            {
                objectRenderer.material = highlightMaterial;
            }
            else if (!highlighted && originalMaterial != null)
            {
                objectRenderer.material = originalMaterial;
            }
        }

        public void ResetInteraction()
        {
            hasInteracted = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);

            if (requireLookAt)
            {
                Gizmos.color = Color.blue;
                Vector3 leftBound = Quaternion.Euler(0, -lookAtAngle, 0) * transform.forward * interactionRange;
                Vector3 rightBound = Quaternion.Euler(0, lookAtAngle, 0) * transform.forward * interactionRange;
                Gizmos.DrawLine(transform.position, transform.position + leftBound);
                Gizmos.DrawLine(transform.position, transform.position + rightBound);
            }
        }
    }

    public interface IInteractable
    {
        string InteractionPrompt { get; }
        float InteractionRange { get; }
        bool CanInteract { get; }
        bool TryInteract(GameObject interactor);
    }

    /// <summary>
    /// Handles player interaction with interactables.
    /// </summary>
    public class InteractionHandler : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private float checkInterval = 0.1f;
        [SerializeField] private LayerMask interactableMask;

        [Header("UI")]
        [SerializeField] private UnityEngine.UI.Text promptText;

        private IInteractable currentTarget;
        private float lastCheckTime;

        public IInteractable CurrentTarget => currentTarget;

        public event System.Action<IInteractable> TargetChanged;

        private void Update()
        {
            if (Time.time - lastCheckTime >= checkInterval)
            {
                lastCheckTime = Time.time;
                CheckForInteractables();
            }

            if (currentTarget != null && Input.GetKeyDown(interactKey))
            {
                currentTarget.TryInteract(gameObject);
            }
        }

        private void CheckForInteractables()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, 5f, interactableMask);

            IInteractable closest = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var interactable = hit.GetComponent<IInteractable>();
                if (interactable == null || !interactable.CanInteract) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist <= interactable.InteractionRange && dist < closestDist)
                {
                    closestDist = dist;
                    closest = interactable;
                }
            }

            SetTarget(closest);
        }

        private void SetTarget(IInteractable target)
        {
            if (currentTarget == target) return;

            // Exit old target
            if (currentTarget is Interactable oldInteractable)
            {
                oldInteractable.SetInRange(false);
            }

            currentTarget = target;

            // Enter new target
            if (currentTarget is Interactable newInteractable)
            {
                newInteractable.SetInRange(true);
            }

            // Update UI
            if (promptText != null)
            {
                promptText.text = currentTarget?.InteractionPrompt ?? "";
                promptText.enabled = currentTarget != null;
            }

            TargetChanged?.Invoke(currentTarget);
        }
    }
}
