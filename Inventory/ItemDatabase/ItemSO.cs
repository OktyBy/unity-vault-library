using UnityEngine;

namespace UnityVault.Inventory
{
    /// <summary>
    /// ScriptableObject defining an item in the game.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "UnityVault/Items/Item")]
    public class ItemSO : ScriptableObject
    {
        [Header("Basic Info")]
        public string itemId;
        public string itemName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;
        public GameObject prefab;

        [Header("Type & Rarity")]
        public ItemType itemType = ItemType.Consumable;
        public ItemRarity rarity = ItemRarity.Common;

        [Header("Stacking")]
        public bool isStackable = true;
        public int maxStackSize = 99;

        [Header("Value")]
        public int buyPrice = 10;
        public int sellPrice = 5;

        [Header("Usage")]
        public bool isUsable = false;
        public bool consumeOnUse = true;
        public float cooldown = 0f;

        [Header("Equipment (if applicable)")]
        public EquipSlotType equipSlot = EquipSlotType.None;
        public StatModifier[] statModifiers;

        [Header("Audio")]
        public AudioClip useSound;
        public AudioClip equipSound;
        public AudioClip dropSound;

        /// <summary>
        /// Create a new item instance from this definition.
        /// </summary>
        public ItemInstance CreateInstance(int quantity = 1)
        {
            return new ItemInstance
            {
                itemData = this,
                quantity = Mathf.Clamp(quantity, 1, maxStackSize),
                durability = 100f
            };
        }
    }

    /// <summary>
    /// Runtime instance of an item.
    /// </summary>
    [System.Serializable]
    public class ItemInstance
    {
        public ItemSO itemData;
        public int quantity;
        public float durability;

        public string ItemId => itemData?.itemId ?? "";
        public string Name => itemData?.itemName ?? "Unknown";
        public Sprite Icon => itemData?.icon;
        public bool IsStackable => itemData?.isStackable ?? false;
        public int MaxStack => itemData?.maxStackSize ?? 1;
        public ItemType Type => itemData?.itemType ?? ItemType.Misc;

        public bool CanStack(ItemInstance other)
        {
            return IsStackable &&
                   other != null &&
                   other.itemData == itemData &&
                   quantity < MaxStack;
        }

        public int AddToStack(int amount)
        {
            int space = MaxStack - quantity;
            int toAdd = Mathf.Min(amount, space);
            quantity += toAdd;
            return amount - toAdd; // Return overflow
        }

        public ItemInstance Split(int amount)
        {
            amount = Mathf.Min(amount, quantity - 1);
            if (amount <= 0) return null;

            quantity -= amount;
            return new ItemInstance
            {
                itemData = itemData,
                quantity = amount,
                durability = durability
            };
        }

        public ItemInstance Clone()
        {
            return new ItemInstance
            {
                itemData = itemData,
                quantity = quantity,
                durability = durability
            };
        }
    }

    public enum ItemType
    {
        Consumable,
        Weapon,
        Armor,
        Accessory,
        Material,
        QuestItem,
        Key,
        Misc
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythic
    }

    public enum EquipSlotType
    {
        None,
        Head,
        Chest,
        Legs,
        Feet,
        Hands,
        MainHand,
        OffHand,
        TwoHand,
        Ring,
        Necklace,
        Trinket
    }

    [System.Serializable]
    public class StatModifier
    {
        public string statName;
        public ModifierType type;
        public float value;
    }

    public enum ModifierType
    {
        Flat,
        Percentage
    }
}
