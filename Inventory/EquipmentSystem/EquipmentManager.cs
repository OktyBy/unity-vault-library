using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Inventory
{
    /// <summary>
    /// Equipment system with slots and stat modifiers.
    /// </summary>
    public class EquipmentManager : MonoBehaviour
    {
        [Header("Equipment Slots")]
        [SerializeField] private List<EquipmentSlotConfig> slotConfigs = new List<EquipmentSlotConfig>();

        [Header("Events")]
        [SerializeField] private UnityEvent<ItemInstance, EquipmentSlot> onItemEquipped;
        [SerializeField] private UnityEvent<ItemInstance, EquipmentSlot> onItemUnequipped;

        private Dictionary<EquipmentSlot, ItemInstance> equipment = new Dictionary<EquipmentSlot, ItemInstance>();
        private Dictionary<EquipmentSlot, GameObject> visualInstances = new Dictionary<EquipmentSlot, GameObject>();

        // Reference to stat system for applying modifiers
        private UnityVault.Core.StatSystem statSystem;

        public event Action<ItemInstance, EquipmentSlot> ItemEquipped;
        public event Action<ItemInstance, EquipmentSlot> ItemUnequipped;
        public event Action EquipmentChanged;

        private void Awake()
        {
            statSystem = GetComponent<UnityVault.Core.StatSystem>();

            // Initialize all slots as empty
            foreach (var config in slotConfigs)
            {
                equipment[config.slot] = null;
            }
        }

        public bool CanEquip(ItemInstance item)
        {
            if (item == null || item.itemData == null) return false;
            if (item.itemData.itemType != ItemType.Equipment) return false;

            var equipData = item.itemData as EquipmentItemSO;
            if (equipData == null) return false;

            return HasSlot(equipData.equipSlot);
        }

        public bool Equip(ItemInstance item)
        {
            if (!CanEquip(item)) return false;

            var equipData = item.itemData as EquipmentItemSO;
            var slot = equipData.equipSlot;

            // Unequip existing item in slot
            if (equipment[slot] != null)
            {
                Unequip(slot);
            }

            equipment[slot] = item;

            // Apply stat modifiers
            ApplyStatModifiers(equipData);

            // Spawn visual
            SpawnVisual(equipData, slot);

            ItemEquipped?.Invoke(item, slot);
            onItemEquipped?.Invoke(item, slot);
            EquipmentChanged?.Invoke();

            Debug.Log($"[Equipment] Equipped {item.itemData.itemName} to {slot}");
            return true;
        }

        public ItemInstance Unequip(EquipmentSlot slot)
        {
            if (!equipment.ContainsKey(slot) || equipment[slot] == null)
            {
                return null;
            }

            var item = equipment[slot];
            var equipData = item.itemData as EquipmentItemSO;

            // Remove stat modifiers
            if (equipData != null)
            {
                RemoveStatModifiers(equipData);
            }

            // Remove visual
            RemoveVisual(slot);

            equipment[slot] = null;

            ItemUnequipped?.Invoke(item, slot);
            onItemUnequipped?.Invoke(item, slot);
            EquipmentChanged?.Invoke();

            Debug.Log($"[Equipment] Unequipped {item.itemData.itemName} from {slot}");
            return item;
        }

        public ItemInstance GetEquipped(EquipmentSlot slot)
        {
            return equipment.TryGetValue(slot, out var item) ? item : null;
        }

        public bool IsSlotEmpty(EquipmentSlot slot)
        {
            return !equipment.ContainsKey(slot) || equipment[slot] == null;
        }

        public bool HasSlot(EquipmentSlot slot)
        {
            return slotConfigs.Exists(c => c.slot == slot);
        }

        public List<ItemInstance> GetAllEquipped()
        {
            var items = new List<ItemInstance>();
            foreach (var kvp in equipment)
            {
                if (kvp.Value != null)
                {
                    items.Add(kvp.Value);
                }
            }
            return items;
        }

        private void ApplyStatModifiers(EquipmentItemSO equipData)
        {
            if (statSystem == null || equipData.statModifiers == null) return;

            foreach (var mod in equipData.statModifiers)
            {
                var modifier = new UnityVault.Core.StatModifier(mod.value, mod.modType, 0, this);
                statSystem.AddModifier(mod.statType, modifier);
            }
        }

        private void RemoveStatModifiers(EquipmentItemSO equipData)
        {
            statSystem?.RemoveModifiersBySource(this);
        }

        private void SpawnVisual(EquipmentItemSO equipData, EquipmentSlot slot)
        {
            if (equipData.visualPrefab == null) return;

            var config = slotConfigs.Find(c => c.slot == slot);
            if (config?.attachPoint == null) return;

            RemoveVisual(slot);

            var visual = Instantiate(equipData.visualPrefab, config.attachPoint);
            visual.transform.localPosition = equipData.equipOffset;
            visual.transform.localRotation = Quaternion.Euler(equipData.equipRotation);

            visualInstances[slot] = visual;
        }

        private void RemoveVisual(EquipmentSlot slot)
        {
            if (visualInstances.TryGetValue(slot, out var visual) && visual != null)
            {
                Destroy(visual);
            }
            visualInstances.Remove(slot);
        }

        public void UnequipAll()
        {
            var slots = new List<EquipmentSlot>(equipment.Keys);
            foreach (var slot in slots)
            {
                if (equipment[slot] != null)
                {
                    Unequip(slot);
                }
            }
        }
    }

    [Serializable]
    public class EquipmentSlotConfig
    {
        public EquipmentSlot slot;
        public Transform attachPoint;
        public Sprite slotIcon;
    }

    public enum EquipmentSlot
    {
        Head,
        Chest,
        Legs,
        Feet,
        Hands,
        MainHand,
        OffHand,
        Necklace,
        Ring1,
        Ring2,
        Back,
        Belt
    }

    [CreateAssetMenu(fileName = "NewEquipment", menuName = "UnityVault/Items/Equipment")]
    public class EquipmentItemSO : ItemSO
    {
        [Header("Equipment Settings")]
        public EquipmentSlot equipSlot;
        public GameObject visualPrefab;
        public Vector3 equipOffset;
        public Vector3 equipRotation;

        [Header("Stats")]
        public List<EquipmentStatModifier> statModifiers = new List<EquipmentStatModifier>();

        [Header("Requirements")]
        public int requiredLevel = 1;
        public List<StatRequirement> statRequirements = new List<StatRequirement>();
    }

    [Serializable]
    public class EquipmentStatModifier
    {
        public UnityVault.Core.StatType statType;
        public float value;
        public UnityVault.Core.ModifierType modType = UnityVault.Core.ModifierType.Flat;
    }

    [Serializable]
    public class StatRequirement
    {
        public UnityVault.Core.StatType statType;
        public float requiredValue;
    }
}
