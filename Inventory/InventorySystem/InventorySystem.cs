using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityVault.Inventory
{
    /// <summary>
    /// Grid-based inventory system with stacking, sorting, and filtering.
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Inventory Settings")]
        [SerializeField] private string inventoryId = "player_inventory";
        [SerializeField] private int slotCount = 20;
        [SerializeField] private bool allowDuplicates = true;

        [Header("Weight System (Optional)")]
        [SerializeField] private bool useWeight = false;
        [SerializeField] private float maxWeight = 100f;

        [Header("Events")]
        [SerializeField] private UnityEvent<ItemInstance, int> onItemAdded; // item, slot
        [SerializeField] private UnityEvent<ItemInstance, int> onItemRemoved; // item, slot
        [SerializeField] private UnityEvent onInventoryChanged;

        #endregion

        #region Properties

        public string InventoryId => inventoryId;
        public int SlotCount => slotCount;
        public int UsedSlots => slots.Count(s => s != null);
        public int FreeSlots => slotCount - UsedSlots;
        public bool IsFull => UsedSlots >= slotCount;
        public float CurrentWeight => CalculateWeight();
        public float MaxWeight => maxWeight;
        public IReadOnlyList<ItemInstance> Items => slots;

        #endregion

        #region C# Events

        public event Action<ItemInstance, int> ItemAdded;
        public event Action<ItemInstance, int> ItemRemoved;
        public event Action<int, int> ItemMoved; // from, to
        public event Action InventoryChanged;

        #endregion

        #region Private Fields

        private ItemInstance[] slots;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            slots = new ItemInstance[slotCount];
        }

        #endregion

        #region Add Items

        /// <summary>
        /// Add an item to the inventory.
        /// </summary>
        public AddResult AddItem(ItemSO itemData, int quantity = 1)
        {
            if (itemData == null) return new AddResult { success = false };

            var instance = itemData.CreateInstance(quantity);
            return AddItem(instance);
        }

        /// <summary>
        /// Add an item instance to the inventory.
        /// </summary>
        public AddResult AddItem(ItemInstance item)
        {
            if (item == null || item.itemData == null)
                return new AddResult { success = false };

            var result = new AddResult { originalQuantity = item.quantity };

            // Try to stack with existing items
            if (item.IsStackable)
            {
                for (int i = 0; i < slotCount && item.quantity > 0; i++)
                {
                    if (slots[i] != null && slots[i].CanStack(item))
                    {
                        int overflow = slots[i].AddToStack(item.quantity);
                        result.addedQuantity += item.quantity - overflow;
                        item.quantity = overflow;
                    }
                }
            }

            // Add to empty slots
            while (item.quantity > 0)
            {
                int emptySlot = FindEmptySlot();
                if (emptySlot < 0) break;

                int toAdd = Mathf.Min(item.quantity, item.MaxStack);
                var newInstance = new ItemInstance
                {
                    itemData = item.itemData,
                    quantity = toAdd,
                    durability = item.durability
                };

                slots[emptySlot] = newInstance;
                item.quantity -= toAdd;
                result.addedQuantity += toAdd;
                result.slotsUsed.Add(emptySlot);

                ItemAdded?.Invoke(newInstance, emptySlot);
                onItemAdded?.Invoke(newInstance, emptySlot);
            }

            result.success = result.addedQuantity > 0;
            result.overflow = item.quantity;

            if (result.success)
            {
                OnInventoryChanged();
            }

            return result;
        }

        #endregion

        #region Remove Items

        /// <summary>
        /// Remove item from a specific slot.
        /// </summary>
        public ItemInstance RemoveFromSlot(int slotIndex, int quantity = -1)
        {
            if (!IsValidSlot(slotIndex) || slots[slotIndex] == null)
                return null;

            var item = slots[slotIndex];

            if (quantity < 0 || quantity >= item.quantity)
            {
                slots[slotIndex] = null;
                ItemRemoved?.Invoke(item, slotIndex);
                onItemRemoved?.Invoke(item, slotIndex);
                OnInventoryChanged();
                return item;
            }
            else
            {
                var removed = item.Split(quantity);
                if (removed != null)
                {
                    ItemRemoved?.Invoke(removed, slotIndex);
                    onItemRemoved?.Invoke(removed, slotIndex);
                    OnInventoryChanged();
                }
                return removed;
            }
        }

        /// <summary>
        /// Remove item by ID.
        /// </summary>
        public bool RemoveItem(string itemId, int quantity = 1)
        {
            int remaining = quantity;

            for (int i = 0; i < slotCount && remaining > 0; i++)
            {
                if (slots[i]?.ItemId == itemId)
                {
                    int toRemove = Mathf.Min(remaining, slots[i].quantity);
                    var removed = RemoveFromSlot(i, toRemove);
                    if (removed != null)
                    {
                        remaining -= removed.quantity;
                    }
                }
            }

            return remaining <= 0;
        }

        /// <summary>
        /// Remove item by data reference.
        /// </summary>
        public bool RemoveItem(ItemSO itemData, int quantity = 1)
        {
            return RemoveItem(itemData.itemId, quantity);
        }

        /// <summary>
        /// Clear the entire inventory.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (slots[i] != null)
                {
                    var item = slots[i];
                    slots[i] = null;
                    ItemRemoved?.Invoke(item, i);
                    onItemRemoved?.Invoke(item, i);
                }
            }
            OnInventoryChanged();
        }

        #endregion

        #region Query Items

        /// <summary>
        /// Get item at slot.
        /// </summary>
        public ItemInstance GetSlot(int slotIndex)
        {
            return IsValidSlot(slotIndex) ? slots[slotIndex] : null;
        }

        /// <summary>
        /// Find item by ID.
        /// </summary>
        public ItemInstance FindItem(string itemId)
        {
            return slots.FirstOrDefault(s => s?.ItemId == itemId);
        }

        /// <summary>
        /// Get total count of an item.
        /// </summary>
        public int GetItemCount(string itemId)
        {
            return slots.Where(s => s?.ItemId == itemId).Sum(s => s.quantity);
        }

        /// <summary>
        /// Check if inventory contains item.
        /// </summary>
        public bool HasItem(string itemId, int quantity = 1)
        {
            return GetItemCount(itemId) >= quantity;
        }

        /// <summary>
        /// Get all items of a type.
        /// </summary>
        public List<ItemInstance> GetItemsOfType(ItemType type)
        {
            return slots.Where(s => s?.Type == type).ToList();
        }

        /// <summary>
        /// Check if there's room for an item.
        /// </summary>
        public bool CanAddItem(ItemSO itemData, int quantity = 1)
        {
            if (itemData == null) return false;

            int remaining = quantity;

            // Check stackable slots
            if (itemData.isStackable)
            {
                foreach (var slot in slots)
                {
                    if (slot?.itemData == itemData)
                    {
                        remaining -= slot.MaxStack - slot.quantity;
                        if (remaining <= 0) return true;
                    }
                }
            }

            // Check empty slots needed
            int emptyNeeded = Mathf.CeilToInt((float)remaining / itemData.maxStackSize);
            return FreeSlots >= emptyNeeded;
        }

        #endregion

        #region Slot Operations

        /// <summary>
        /// Move item between slots.
        /// </summary>
        public bool MoveItem(int fromSlot, int toSlot)
        {
            if (!IsValidSlot(fromSlot) || !IsValidSlot(toSlot))
                return false;

            if (slots[fromSlot] == null)
                return false;

            var fromItem = slots[fromSlot];
            var toItem = slots[toSlot];

            // Empty slot - just move
            if (toItem == null)
            {
                slots[toSlot] = fromItem;
                slots[fromSlot] = null;
            }
            // Same item type - try to stack
            else if (fromItem.CanStack(toItem))
            {
                int overflow = toItem.AddToStack(fromItem.quantity);
                if (overflow > 0)
                {
                    fromItem.quantity = overflow;
                }
                else
                {
                    slots[fromSlot] = null;
                }
            }
            // Different items - swap
            else
            {
                slots[fromSlot] = toItem;
                slots[toSlot] = fromItem;
            }

            ItemMoved?.Invoke(fromSlot, toSlot);
            OnInventoryChanged();
            return true;
        }

        /// <summary>
        /// Sort inventory by type, then rarity.
        /// </summary>
        public void Sort()
        {
            var items = slots.Where(s => s != null).OrderBy(s => s.Type).ThenByDescending(s => s.itemData.rarity).ToList();

            for (int i = 0; i < slotCount; i++)
            {
                slots[i] = i < items.Count ? items[i] : null;
            }

            OnInventoryChanged();
        }

        #endregion

        #region Private Methods

        private int FindEmptySlot()
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (slots[i] == null) return i;
            }
            return -1;
        }

        private bool IsValidSlot(int index)
        {
            return index >= 0 && index < slotCount;
        }

        private float CalculateWeight()
        {
            // Weight system implementation would go here
            return 0f;
        }

        private void OnInventoryChanged()
        {
            InventoryChanged?.Invoke();
            onInventoryChanged?.Invoke();
        }

        #endregion

        #region Result Classes

        public class AddResult
        {
            public bool success;
            public int originalQuantity;
            public int addedQuantity;
            public int overflow;
            public List<int> slotsUsed = new List<int>();
        }

        #endregion
    }
}
