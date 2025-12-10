using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace UnityVault.World
{
    /// <summary>
    /// Area trigger for events and effects.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TriggerZone : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private TriggerType triggerType = TriggerType.OnEnter;
        [SerializeField] private LayerMask targetLayers = -1;
        [SerializeField] private string requiredTag = "";
        [SerializeField] private bool triggerOnce = false;
        [SerializeField] private float cooldownTime = 0f;

        [Header("Events")]
        [SerializeField] private UnityEvent<GameObject> onTriggerEnter;
        [SerializeField] private UnityEvent<GameObject> onTriggerStay;
        [SerializeField] private UnityEvent<GameObject> onTriggerExit;
        [SerializeField] private UnityEvent onAllExited;

        // State
        private bool hasTriggered;
        private float lastTriggerTime;
        private HashSet<GameObject> objectsInZone = new HashSet<GameObject>();

        public int ObjectCount => objectsInZone.Count;
        public bool HasTriggered => hasTriggered;
        public IReadOnlyCollection<GameObject> ObjectsInZone => objectsInZone;

        public event System.Action<GameObject> EnteredZone;
        public event System.Action<GameObject> ExitedZone;
        public event System.Action<GameObject> StayingInZone;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsValidTarget(other.gameObject)) return;
            if (triggerOnce && hasTriggered) return;
            if (cooldownTime > 0 && Time.time - lastTriggerTime < cooldownTime) return;

            objectsInZone.Add(other.gameObject);

            if (triggerType == TriggerType.OnEnter || triggerType == TriggerType.Both)
            {
                Trigger(other.gameObject, TriggerEventType.Enter);
            }

            EnteredZone?.Invoke(other.gameObject);
            onTriggerEnter?.Invoke(other.gameObject);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsValidTarget(other.gameObject)) return;
            if (!objectsInZone.Contains(other.gameObject)) return;

            if (triggerType == TriggerType.OnStay)
            {
                StayingInZone?.Invoke(other.gameObject);
                onTriggerStay?.Invoke(other.gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!objectsInZone.Contains(other.gameObject)) return;

            objectsInZone.Remove(other.gameObject);

            if (triggerType == TriggerType.OnExit || triggerType == TriggerType.Both)
            {
                Trigger(other.gameObject, TriggerEventType.Exit);
            }

            ExitedZone?.Invoke(other.gameObject);
            onTriggerExit?.Invoke(other.gameObject);

            if (objectsInZone.Count == 0)
            {
                onAllExited?.Invoke();
            }
        }

        private bool IsValidTarget(GameObject obj)
        {
            // Check layer
            if ((targetLayers.value & (1 << obj.layer)) == 0)
            {
                return false;
            }

            // Check tag
            if (!string.IsNullOrEmpty(requiredTag) && !obj.CompareTag(requiredTag))
            {
                return false;
            }

            return true;
        }

        private void Trigger(GameObject target, TriggerEventType eventType)
        {
            hasTriggered = true;
            lastTriggerTime = Time.time;

            Debug.Log($"[TriggerZone] {eventType}: {target.name}");
        }

        public void ResetTrigger()
        {
            hasTriggered = false;
        }

        public bool IsObjectInZone(GameObject obj)
        {
            return objectsInZone.Contains(obj);
        }

        private void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);

            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
        }
    }

    public enum TriggerType
    {
        OnEnter,
        OnExit,
        OnStay,
        Both
    }

    public enum TriggerEventType
    {
        Enter,
        Exit,
        Stay
    }
}
