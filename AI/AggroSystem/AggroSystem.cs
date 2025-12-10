using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityVault.AI
{
    /// <summary>
    /// Aggro/threat system for AI targeting decisions.
    /// </summary>
    public class AggroSystem : MonoBehaviour
    {
        [Header("Aggro Settings")]
        [SerializeField] private float aggroRange = 15f;
        [SerializeField] private float maxAggro = 100f;
        [SerializeField] private float aggroDecayRate = 5f;
        [SerializeField] private float aggroDecayDelay = 3f;

        [Header("Threat Multipliers")]
        [SerializeField] private float damageThreatMultiplier = 1f;
        [SerializeField] private float healingThreatMultiplier = 0.5f;
        [SerializeField] private float proximityThreatMultiplier = 0.1f;
        [SerializeField] private float tauntMultiplier = 2f;

        [Header("Target Switching")]
        [SerializeField] private float switchThreshold = 1.2f; // New target needs 20% more aggro
        [SerializeField] private float switchCooldown = 1f;
        [SerializeField] private bool prioritizeClosest = false;

        [Header("Events")]
        [SerializeField] private UnityEvent<Transform> onTargetChanged;
        [SerializeField] private UnityEvent<Transform, float> onAggroGained;
        [SerializeField] private UnityEvent<Transform> onAggroLost;
        [SerializeField] private UnityEvent onAggroCleared;

        // State
        private Dictionary<Transform, AggroEntry> aggroTable = new Dictionary<Transform, AggroEntry>();
        private Transform currentTarget;
        private float lastSwitchTime;

        // Properties
        public Transform CurrentTarget => currentTarget;
        public bool HasTarget => currentTarget != null;
        public int ThreatCount => aggroTable.Count;
        public float CurrentTargetAggro => currentTarget != null && aggroTable.ContainsKey(currentTarget) ? aggroTable[currentTarget].aggro : 0f;

        // Events
        public event Action<Transform> TargetChanged;
        public event Action<Transform, float> AggroGained;
        public event Action<Transform> AggroLost;
        public event Action AggroCleared;

        private void Update()
        {
            UpdateAggroDecay();
            UpdateTarget();
        }

        private void UpdateAggroDecay()
        {
            List<Transform> toRemove = new List<Transform>();

            foreach (var kvp in aggroTable)
            {
                var entry = kvp.Value;

                // Check if should decay
                if (Time.time - entry.lastAggroTime >= aggroDecayDelay)
                {
                    entry.aggro -= aggroDecayRate * Time.deltaTime;

                    if (entry.aggro <= 0)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
            }

            // Remove entries with no aggro
            foreach (var target in toRemove)
            {
                aggroTable.Remove(target);
                AggroLost?.Invoke(target);
                onAggroLost?.Invoke(target);
                Debug.Log($"[Aggro] Lost aggro on {target.name}");
            }
        }

        private void UpdateTarget()
        {
            if (aggroTable.Count == 0)
            {
                if (currentTarget != null)
                {
                    currentTarget = null;
                    TargetChanged?.Invoke(null);
                    onTargetChanged?.Invoke(null);
                }
                return;
            }

            // Find highest aggro target
            Transform highestTarget = null;
            float highestAggro = 0f;

            foreach (var kvp in aggroTable)
            {
                float effectiveAggro = kvp.Value.aggro;

                // Add proximity bonus
                if (prioritizeClosest)
                {
                    float distance = Vector3.Distance(transform.position, kvp.Key.position);
                    effectiveAggro += (aggroRange - distance) * proximityThreatMultiplier;
                }

                if (effectiveAggro > highestAggro)
                {
                    highestAggro = effectiveAggro;
                    highestTarget = kvp.Key;
                }
            }

            // Check if should switch targets
            if (highestTarget != currentTarget && highestTarget != null)
            {
                if (currentTarget == null || Time.time - lastSwitchTime >= switchCooldown)
                {
                    float currentAggro = currentTarget != null && aggroTable.ContainsKey(currentTarget) ? aggroTable[currentTarget].aggro : 0f;

                    if (highestAggro >= currentAggro * switchThreshold || currentTarget == null)
                    {
                        SwitchTarget(highestTarget);
                    }
                }
            }
        }

        private void SwitchTarget(Transform newTarget)
        {
            Transform oldTarget = currentTarget;
            currentTarget = newTarget;
            lastSwitchTime = Time.time;

            TargetChanged?.Invoke(newTarget);
            onTargetChanged?.Invoke(newTarget);

            Debug.Log($"[Aggro] Target changed: {oldTarget?.name ?? "none"} -> {newTarget?.name ?? "none"}");
        }

        /// <summary>
        /// Add aggro from damage dealt to this AI.
        /// </summary>
        public void AddDamageThreat(Transform source, float damage)
        {
            float threat = damage * damageThreatMultiplier;
            AddAggro(source, threat);
        }

        /// <summary>
        /// Add aggro from healing done to this AI's enemies.
        /// </summary>
        public void AddHealingThreat(Transform healer, float healAmount)
        {
            float threat = healAmount * healingThreatMultiplier;
            AddAggro(healer, threat);
        }

        /// <summary>
        /// Add raw aggro to a target.
        /// </summary>
        public void AddAggro(Transform source, float amount)
        {
            if (source == null || source == transform) return;

            // Check range
            float distance = Vector3.Distance(transform.position, source.position);
            if (distance > aggroRange) return;

            if (!aggroTable.ContainsKey(source))
            {
                aggroTable[source] = new AggroEntry();
            }

            var entry = aggroTable[source];
            entry.aggro = Mathf.Min(entry.aggro + amount, maxAggro);
            entry.lastAggroTime = Time.time;

            AggroGained?.Invoke(source, amount);
            onAggroGained?.Invoke(source, amount);

            Debug.Log($"[Aggro] +{amount:F1} aggro from {source.name} (total: {entry.aggro:F1})");
        }

        /// <summary>
        /// Taunt - forces this AI to target the taunter.
        /// </summary>
        public void Taunt(Transform taunter, float duration = 3f)
        {
            if (taunter == null) return;

            // Add massive aggro
            float tauntAggro = maxAggro * tauntMultiplier;
            AddAggro(taunter, tauntAggro);

            // Force immediate target switch
            SwitchTarget(taunter);

            Debug.Log($"[Aggro] Taunted by {taunter.name}!");
        }

        /// <summary>
        /// Remove all aggro from a source.
        /// </summary>
        public void ClearAggro(Transform source)
        {
            if (aggroTable.Remove(source))
            {
                AggroLost?.Invoke(source);
                onAggroLost?.Invoke(source);

                if (currentTarget == source)
                {
                    currentTarget = null;
                    UpdateTarget();
                }
            }
        }

        /// <summary>
        /// Clear all aggro.
        /// </summary>
        public void ClearAllAggro()
        {
            aggroTable.Clear();
            currentTarget = null;

            AggroCleared?.Invoke();
            onAggroCleared?.Invoke();

            Debug.Log("[Aggro] All aggro cleared");
        }

        /// <summary>
        /// Reduce aggro by percentage.
        /// </summary>
        public void ReduceAggro(Transform source, float percent)
        {
            if (aggroTable.TryGetValue(source, out AggroEntry entry))
            {
                entry.aggro *= (1f - Mathf.Clamp01(percent));
            }
        }

        /// <summary>
        /// Transfer aggro from one source to another.
        /// </summary>
        public void TransferAggro(Transform from, Transform to, float percent)
        {
            if (!aggroTable.TryGetValue(from, out AggroEntry fromEntry)) return;

            float transferAmount = fromEntry.aggro * Mathf.Clamp01(percent);
            fromEntry.aggro -= transferAmount;

            AddAggro(to, transferAmount);
        }

        /// <summary>
        /// Get aggro amount for a specific target.
        /// </summary>
        public float GetAggro(Transform target)
        {
            return aggroTable.TryGetValue(target, out AggroEntry entry) ? entry.aggro : 0f;
        }

        /// <summary>
        /// Get sorted list of all threats.
        /// </summary>
        public List<(Transform target, float aggro)> GetThreatList()
        {
            return aggroTable
                .OrderByDescending(kvp => kvp.Value.aggro)
                .Select(kvp => (kvp.Key, kvp.Value.aggro))
                .ToList();
        }

        /// <summary>
        /// Check if a target is in aggro range.
        /// </summary>
        public bool IsInRange(Transform target)
        {
            return target != null && Vector3.Distance(transform.position, target.position) <= aggroRange;
        }

        private void OnDrawGizmosSelected()
        {
            // Aggro range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, aggroRange);

            // Lines to threats
            foreach (var kvp in aggroTable)
            {
                if (kvp.Key == null) continue;

                float aggroPercent = kvp.Value.aggro / maxAggro;
                Gizmos.color = Color.Lerp(Color.yellow, Color.red, aggroPercent);

                Gizmos.DrawLine(transform.position, kvp.Key.position);

                // Current target
                if (kvp.Key == currentTarget)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(kvp.Key.position, 1f);
                }
            }
        }

        private class AggroEntry
        {
            public float aggro;
            public float lastAggroTime;
        }
    }
}
