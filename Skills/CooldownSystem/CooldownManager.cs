using UnityEngine;
using System;
using System.Collections.Generic;

namespace UnityVault.Skills
{
    /// <summary>
    /// Generic cooldown management system.
    /// </summary>
    public class CooldownManager : MonoBehaviour
    {
        public static CooldownManager Instance { get; private set; }

        private Dictionary<string, Cooldown> cooldowns = new Dictionary<string, Cooldown>();

        public event Action<string> CooldownStarted;
        public event Action<string> CooldownFinished;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            UpdateCooldowns();
        }

        private void UpdateCooldowns()
        {
            var keysToRemove = new List<string>();

            foreach (var kvp in cooldowns)
            {
                var cd = kvp.Value;
                cd.remainingTime -= Time.deltaTime;

                if (cd.remainingTime <= 0)
                {
                    keysToRemove.Add(kvp.Key);
                    CooldownFinished?.Invoke(kvp.Key);
                    cd.onComplete?.Invoke();
                }
            }

            foreach (var key in keysToRemove)
            {
                cooldowns.Remove(key);
            }
        }

        public void StartCooldown(string id, float duration, Action onComplete = null)
        {
            if (cooldowns.ContainsKey(id))
            {
                cooldowns[id].remainingTime = duration;
                cooldowns[id].totalTime = duration;
                cooldowns[id].onComplete = onComplete;
            }
            else
            {
                cooldowns[id] = new Cooldown
                {
                    id = id,
                    remainingTime = duration,
                    totalTime = duration,
                    onComplete = onComplete
                };
            }

            CooldownStarted?.Invoke(id);
        }

        public bool IsOnCooldown(string id)
        {
            return cooldowns.ContainsKey(id) && cooldowns[id].remainingTime > 0;
        }

        public float GetRemainingTime(string id)
        {
            return cooldowns.TryGetValue(id, out var cd) ? cd.remainingTime : 0f;
        }

        public float GetProgress(string id)
        {
            if (!cooldowns.TryGetValue(id, out var cd)) return 1f;
            if (cd.totalTime <= 0) return 1f;
            return 1f - (cd.remainingTime / cd.totalTime);
        }

        public void ResetCooldown(string id)
        {
            if (cooldowns.ContainsKey(id))
            {
                cooldowns.Remove(id);
                CooldownFinished?.Invoke(id);
            }
        }

        public void ResetAllCooldowns()
        {
            var keys = new List<string>(cooldowns.Keys);
            foreach (var key in keys)
            {
                CooldownFinished?.Invoke(key);
            }
            cooldowns.Clear();
        }

        public void ReduceCooldown(string id, float amount)
        {
            if (cooldowns.TryGetValue(id, out var cd))
            {
                cd.remainingTime = Mathf.Max(0, cd.remainingTime - amount);
            }
        }

        public void ReduceAllCooldowns(float amount)
        {
            foreach (var cd in cooldowns.Values)
            {
                cd.remainingTime = Mathf.Max(0, cd.remainingTime - amount);
            }
        }

        public void ReduceAllCooldownsPercent(float percent)
        {
            foreach (var cd in cooldowns.Values)
            {
                cd.remainingTime *= (1f - percent);
            }
        }
    }

    public class Cooldown
    {
        public string id;
        public float remainingTime;
        public float totalTime;
        public Action onComplete;
    }
}
