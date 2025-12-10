using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Skills
{
    /// <summary>
    /// Skill tree system for progression and unlocking abilities.
    /// </summary>
    public class SkillTree : MonoBehaviour
    {
        [Header("Skill Tree Data")]
        [SerializeField] private List<SkillTreeNode> nodes = new List<SkillTreeNode>();
        [SerializeField] private int startingSkillPoints = 0;

        [Header("Settings")]
        [SerializeField] private bool allowRespec = true;
        [SerializeField] private int respecCost = 100;
        [SerializeField] private int maxSkillLevel = 5;

        [Header("UI")]
        [SerializeField] private Transform nodesContainer;
        [SerializeField] private Transform connectionsContainer;
        [SerializeField] private GameObject nodeUIPrefab;
        [SerializeField] private GameObject connectionPrefab;

        [Header("Events")]
        [SerializeField] private UnityEvent<SkillTreeNode> onSkillUnlocked;
        [SerializeField] private UnityEvent<SkillTreeNode> onSkillUpgraded;
        [SerializeField] private UnityEvent onSkillPointsChanged;
        [SerializeField] private UnityEvent onTreeReset;

        // State
        private int currentSkillPoints;
        private Dictionary<string, SkillTreeNode> nodeMap = new Dictionary<string, SkillTreeNode>();
        private Dictionary<string, int> skillLevels = new Dictionary<string, int>();

        // Events
        public event Action<SkillTreeNode> SkillUnlocked;
        public event Action<SkillTreeNode, int> SkillUpgraded;
        public event Action<int> SkillPointsChanged;
        public event Action TreeReset;

        public int SkillPoints => currentSkillPoints;

        private void Awake()
        {
            currentSkillPoints = startingSkillPoints;

            // Build node map
            foreach (var node in nodes)
            {
                nodeMap[node.skillId] = node;
                skillLevels[node.skillId] = 0;
            }
        }

        /// <summary>
        /// Try to unlock/upgrade a skill.
        /// </summary>
        public bool TryUnlockSkill(string skillId)
        {
            if (!nodeMap.TryGetValue(skillId, out SkillTreeNode node))
            {
                Debug.LogWarning($"[SkillTree] Skill not found: {skillId}");
                return false;
            }

            return TryUnlockSkill(node);
        }

        /// <summary>
        /// Try to unlock/upgrade a skill.
        /// </summary>
        public bool TryUnlockSkill(SkillTreeNode node)
        {
            if (!CanUnlock(node))
            {
                return false;
            }

            int currentLevel = GetSkillLevel(node.skillId);

            // Check if maxed
            if (currentLevel >= node.maxLevel)
            {
                Debug.Log($"[SkillTree] {node.skillName} is already max level");
                return false;
            }

            // Check cost
            int cost = GetUpgradeCost(node, currentLevel);
            if (currentSkillPoints < cost)
            {
                Debug.Log($"[SkillTree] Not enough skill points for {node.skillName}");
                return false;
            }

            // Spend points
            currentSkillPoints -= cost;
            skillLevels[node.skillId] = currentLevel + 1;

            if (currentLevel == 0)
            {
                SkillUnlocked?.Invoke(node);
                onSkillUnlocked?.Invoke(node);
                Debug.Log($"[SkillTree] Unlocked: {node.skillName}");
            }
            else
            {
                SkillUpgraded?.Invoke(node, currentLevel + 1);
                onSkillUpgraded?.Invoke(node);
                Debug.Log($"[SkillTree] Upgraded {node.skillName} to level {currentLevel + 1}");
            }

            SkillPointsChanged?.Invoke(currentSkillPoints);
            onSkillPointsChanged?.Invoke();

            return true;
        }

        /// <summary>
        /// Check if a skill can be unlocked.
        /// </summary>
        public bool CanUnlock(SkillTreeNode node)
        {
            if (node == null) return false;

            int currentLevel = GetSkillLevel(node.skillId);

            // Already maxed
            if (currentLevel >= node.maxLevel) return false;

            // Check prerequisites
            foreach (var prereqId in node.prerequisites)
            {
                if (!IsSkillUnlocked(prereqId))
                {
                    return false;
                }

                // Check prerequisite level requirement
                var prereqNode = GetNode(prereqId);
                if (prereqNode != null && GetSkillLevel(prereqId) < prereqNode.requiredLevel)
                {
                    return false;
                }
            }

            // Check cost
            int cost = GetUpgradeCost(node, currentLevel);
            if (currentSkillPoints < cost) return false;

            return true;
        }

        /// <summary>
        /// Get upgrade cost for next level.
        /// </summary>
        public int GetUpgradeCost(SkillTreeNode node, int currentLevel)
        {
            return node.baseCost + (node.costPerLevel * currentLevel);
        }

        /// <summary>
        /// Get current skill level.
        /// </summary>
        public int GetSkillLevel(string skillId)
        {
            return skillLevels.TryGetValue(skillId, out int level) ? level : 0;
        }

        /// <summary>
        /// Check if skill is unlocked (level > 0).
        /// </summary>
        public bool IsSkillUnlocked(string skillId)
        {
            return GetSkillLevel(skillId) > 0;
        }

        /// <summary>
        /// Check if skill is maxed.
        /// </summary>
        public bool IsSkillMaxed(string skillId)
        {
            if (!nodeMap.TryGetValue(skillId, out SkillTreeNode node)) return false;
            return GetSkillLevel(skillId) >= node.maxLevel;
        }

        /// <summary>
        /// Add skill points.
        /// </summary>
        public void AddSkillPoints(int amount)
        {
            currentSkillPoints += amount;
            SkillPointsChanged?.Invoke(currentSkillPoints);
            onSkillPointsChanged?.Invoke();
        }

        /// <summary>
        /// Reset skill tree (respec).
        /// </summary>
        public bool ResetTree(bool refundPoints = true)
        {
            if (!allowRespec)
            {
                Debug.Log("[SkillTree] Respec not allowed");
                return false;
            }

            // Calculate total points to refund
            if (refundPoints)
            {
                int totalSpent = 0;
                foreach (var kvp in skillLevels)
                {
                    if (kvp.Value > 0 && nodeMap.TryGetValue(kvp.Key, out SkillTreeNode node))
                    {
                        for (int i = 0; i < kvp.Value; i++)
                        {
                            totalSpent += GetUpgradeCost(node, i);
                        }
                    }
                }
                currentSkillPoints += totalSpent;
            }

            // Reset all skills
            foreach (var skillId in new List<string>(skillLevels.Keys))
            {
                skillLevels[skillId] = 0;
            }

            TreeReset?.Invoke();
            onTreeReset?.Invoke();
            SkillPointsChanged?.Invoke(currentSkillPoints);
            onSkillPointsChanged?.Invoke();

            Debug.Log("[SkillTree] Tree reset");
            return true;
        }

        /// <summary>
        /// Get skill effect value at current level.
        /// </summary>
        public float GetSkillValue(string skillId)
        {
            if (!nodeMap.TryGetValue(skillId, out SkillTreeNode node)) return 0;

            int level = GetSkillLevel(skillId);
            if (level == 0) return 0;

            return node.baseValue + (node.valuePerLevel * (level - 1));
        }

        /// <summary>
        /// Get skill node.
        /// </summary>
        public SkillTreeNode GetNode(string skillId)
        {
            return nodeMap.TryGetValue(skillId, out SkillTreeNode node) ? node : null;
        }

        /// <summary>
        /// Get all unlocked skills.
        /// </summary>
        public List<SkillTreeNode> GetUnlockedSkills()
        {
            List<SkillTreeNode> unlocked = new List<SkillTreeNode>();
            foreach (var kvp in skillLevels)
            {
                if (kvp.Value > 0 && nodeMap.TryGetValue(kvp.Key, out SkillTreeNode node))
                {
                    unlocked.Add(node);
                }
            }
            return unlocked;
        }

        /// <summary>
        /// Get all nodes in tree.
        /// </summary>
        public List<SkillTreeNode> GetAllNodes()
        {
            return new List<SkillTreeNode>(nodes);
        }

        /// <summary>
        /// Get total spent points.
        /// </summary>
        public int GetTotalSpentPoints()
        {
            int total = 0;
            foreach (var kvp in skillLevels)
            {
                if (kvp.Value > 0 && nodeMap.TryGetValue(kvp.Key, out SkillTreeNode node))
                {
                    for (int i = 0; i < kvp.Value; i++)
                    {
                        total += GetUpgradeCost(node, i);
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// Save skill tree state.
        /// </summary>
        public SkillTreeSaveData GetSaveData()
        {
            return new SkillTreeSaveData
            {
                skillPoints = currentSkillPoints,
                skillLevels = new Dictionary<string, int>(skillLevels)
            };
        }

        /// <summary>
        /// Load skill tree state.
        /// </summary>
        public void LoadSaveData(SkillTreeSaveData data)
        {
            if (data == null) return;

            currentSkillPoints = data.skillPoints;

            foreach (var kvp in data.skillLevels)
            {
                if (skillLevels.ContainsKey(kvp.Key))
                {
                    skillLevels[kvp.Key] = kvp.Value;
                }
            }

            SkillPointsChanged?.Invoke(currentSkillPoints);
            onSkillPointsChanged?.Invoke();
        }
    }

    [Serializable]
    public class SkillTreeNode
    {
        public string skillId;
        public string skillName;
        [TextArea]
        public string description;
        public Sprite icon;

        [Header("Cost")]
        public int baseCost = 1;
        public int costPerLevel = 0;

        [Header("Levels")]
        public int maxLevel = 1;
        public int requiredLevel = 1;

        [Header("Value")]
        public float baseValue;
        public float valuePerLevel;

        [Header("Prerequisites")]
        public List<string> prerequisites = new List<string>();

        [Header("Position (for UI)")]
        public Vector2 uiPosition;
        public SkillTreeBranch branch;
    }

    public enum SkillTreeBranch
    {
        Combat,
        Magic,
        Utility,
        Defense,
        Passive
    }

    [Serializable]
    public class SkillTreeSaveData
    {
        public int skillPoints;
        public Dictionary<string, int> skillLevels;
    }
}
