using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Skills
{
    /// <summary>
    /// Skill/ability system with cooldowns and targeting.
    /// </summary>
    public class SkillSystem : MonoBehaviour
    {
        [Header("Skill Slots")]
        [SerializeField] private int maxSkillSlots = 4;
        [SerializeField] private SkillSO[] equippedSkills;

        [Header("Events")]
        [SerializeField] private UnityEvent<SkillSO> onSkillUsed;
        [SerializeField] private UnityEvent<int, float> onCooldownStarted;
        [SerializeField] private UnityEvent<int> onCooldownFinished;

        // State
        private Dictionary<int, float> cooldowns = new Dictionary<int, float>();
        private bool isCasting;
        private int castingSlot = -1;

        // References
        private UnityVault.Core.ManaSystem manaSystem;
        private Animator animator;

        public bool IsCasting => isCasting;
        public event Action<SkillSO> SkillUsed;
        public event Action<int, float> CooldownStarted;
        public event Action<int> CooldownFinished;

        private void Awake()
        {
            manaSystem = GetComponent<UnityVault.Core.ManaSystem>();
            animator = GetComponent<Animator>();

            if (equippedSkills == null || equippedSkills.Length != maxSkillSlots)
            {
                equippedSkills = new SkillSO[maxSkillSlots];
            }
        }

        private void Update()
        {
            UpdateCooldowns();
        }

        public bool UseSkill(int slotIndex, Transform target = null)
        {
            if (!CanUseSkill(slotIndex)) return false;

            var skill = equippedSkills[slotIndex];

            // Check mana
            if (manaSystem != null && !manaSystem.UseMana(skill.manaCost))
            {
                Debug.Log("[SkillSystem] Not enough mana");
                return false;
            }

            // Start casting
            StartCoroutine(CastSkill(slotIndex, skill, target));
            return true;
        }

        private System.Collections.IEnumerator CastSkill(int slot, SkillSO skill, Transform target)
        {
            isCasting = true;
            castingSlot = slot;

            // Cast time
            if (skill.castTime > 0)
            {
                animator?.SetTrigger(skill.castAnimation);
                yield return new WaitForSeconds(skill.castTime);
            }

            // Execute skill
            ExecuteSkill(skill, target);

            // Start cooldown
            cooldowns[slot] = skill.cooldown;
            CooldownStarted?.Invoke(slot, skill.cooldown);
            onCooldownStarted?.Invoke(slot, skill.cooldown);

            isCasting = false;
            castingSlot = -1;

            SkillUsed?.Invoke(skill);
            onSkillUsed?.Invoke(skill);
        }

        private void ExecuteSkill(SkillSO skill, Transform target)
        {
            switch (skill.targetType)
            {
                case SkillTargetType.Self:
                    ApplyEffects(skill, gameObject);
                    break;

                case SkillTargetType.Target:
                    if (target != null)
                    {
                        ApplyEffects(skill, target.gameObject);
                    }
                    break;

                case SkillTargetType.Area:
                    ApplyAreaEffects(skill, target?.position ?? transform.position + transform.forward * skill.range);
                    break;

                case SkillTargetType.Projectile:
                    SpawnProjectile(skill);
                    break;
            }

            // Spawn VFX
            if (skill.effectPrefab != null)
            {
                Vector3 spawnPos = skill.targetType == SkillTargetType.Self ?
                    transform.position : (target?.position ?? transform.position + transform.forward * skill.range);
                Instantiate(skill.effectPrefab, spawnPos, Quaternion.identity);
            }

            // Play sound
            if (skill.useSound != null)
            {
                AudioSource.PlayClipAtPoint(skill.useSound, transform.position);
            }
        }

        private void ApplyEffects(SkillSO skill, GameObject target)
        {
            foreach (var effect in skill.effects)
            {
                effect.Apply(target, gameObject);
            }
        }

        private void ApplyAreaEffects(SkillSO skill, Vector3 center)
        {
            Collider[] hits = Physics.OverlapSphere(center, skill.areaRadius);

            foreach (var hit in hits)
            {
                if (skill.affectsSelf || hit.gameObject != gameObject)
                {
                    ApplyEffects(skill, hit.gameObject);
                }
            }
        }

        private void SpawnProjectile(SkillSO skill)
        {
            if (skill.projectilePrefab == null) return;

            var proj = Instantiate(skill.projectilePrefab, transform.position + transform.forward, transform.rotation);
            var skillProj = proj.GetComponent<SkillProjectile>();

            if (skillProj != null)
            {
                skillProj.Initialize(skill, gameObject);
            }
        }

        private void UpdateCooldowns()
        {
            var slots = new List<int>(cooldowns.Keys);
            foreach (var slot in slots)
            {
                cooldowns[slot] -= Time.deltaTime;
                if (cooldowns[slot] <= 0)
                {
                    cooldowns.Remove(slot);
                    CooldownFinished?.Invoke(slot);
                    onCooldownFinished?.Invoke(slot);
                }
            }
        }

        public bool CanUseSkill(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= maxSkillSlots) return false;
            if (equippedSkills[slotIndex] == null) return false;
            if (isCasting) return false;
            if (IsOnCooldown(slotIndex)) return false;

            return true;
        }

        public bool IsOnCooldown(int slotIndex)
        {
            return cooldowns.ContainsKey(slotIndex) && cooldowns[slotIndex] > 0;
        }

        public float GetCooldownRemaining(int slotIndex)
        {
            return cooldowns.TryGetValue(slotIndex, out float cd) ? cd : 0f;
        }

        public float GetCooldownPercent(int slotIndex)
        {
            if (!IsOnCooldown(slotIndex)) return 0f;
            var skill = equippedSkills[slotIndex];
            if (skill == null) return 0f;
            return cooldowns[slotIndex] / skill.cooldown;
        }

        public void EquipSkill(SkillSO skill, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= maxSkillSlots) return;
            equippedSkills[slotIndex] = skill;
        }

        public SkillSO GetEquippedSkill(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= maxSkillSlots) return null;
            return equippedSkills[slotIndex];
        }
    }

    [CreateAssetMenu(fileName = "NewSkill", menuName = "UnityVault/Skills/Skill")]
    public class SkillSO : ScriptableObject
    {
        [Header("Basic Info")]
        public string skillId;
        public string skillName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("Type")]
        public SkillTargetType targetType;
        public bool affectsSelf = false;

        [Header("Costs")]
        public float manaCost = 10f;
        public float healthCost = 0f;

        [Header("Timing")]
        public float castTime = 0f;
        public float cooldown = 5f;
        public float duration = 0f;

        [Header("Range & Area")]
        public float range = 10f;
        public float areaRadius = 3f;

        [Header("Effects")]
        public List<SkillEffect> effects = new List<SkillEffect>();

        [Header("Visuals")]
        public GameObject effectPrefab;
        public GameObject projectilePrefab;
        public string castAnimation = "Cast";

        [Header("Audio")]
        public AudioClip useSound;
        public AudioClip hitSound;
    }

    [Serializable]
    public class SkillEffect
    {
        public SkillEffectType type;
        public float value;
        public float duration;

        public void Apply(GameObject target, GameObject caster)
        {
            switch (type)
            {
                case SkillEffectType.Damage:
                    var damageable = target.GetComponent<UnityVault.Core.IDamageable>();
                    damageable?.TakeDamage(value);
                    break;

                case SkillEffectType.Heal:
                    var health = target.GetComponent<UnityVault.Core.HealthSystem>();
                    health?.Heal(value);
                    break;

                case SkillEffectType.Buff:
                case SkillEffectType.Debuff:
                    // Apply status effect
                    break;
            }
        }
    }

    public enum SkillTargetType
    {
        Self,
        Target,
        Area,
        Projectile,
        Cone
    }

    public enum SkillEffectType
    {
        Damage,
        Heal,
        Buff,
        Debuff,
        Knockback,
        Stun
    }

    public class SkillProjectile : MonoBehaviour
    {
        [SerializeField] private float speed = 20f;
        [SerializeField] private float lifetime = 5f;

        private SkillSO skill;
        private GameObject caster;
        private float spawnTime;

        public void Initialize(SkillSO skill, GameObject caster)
        {
            this.skill = skill;
            this.caster = caster;
            spawnTime = Time.time;
        }

        private void Update()
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime);

            if (Time.time - spawnTime >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject == caster) return;

            foreach (var effect in skill.effects)
            {
                effect.Apply(other.gameObject, caster);
            }

            if (skill.hitSound != null)
            {
                AudioSource.PlayClipAtPoint(skill.hitSound, transform.position);
            }

            Destroy(gameObject);
        }
    }
}
