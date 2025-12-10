using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Inventory
{
    /// <summary>
    /// Shop system for buying and selling items.
    /// </summary>
    public class ShopSystem : MonoBehaviour
    {
        [Header("Shop Settings")]
        [SerializeField] private string shopId;
        [SerializeField] private string shopName;
        [SerializeField] private ShopInventory shopInventory;

        [Header("Economy")]
        [SerializeField] private float buyMultiplier = 1f;
        [SerializeField] private float sellMultiplier = 0.5f;
        [SerializeField] private CurrencyType acceptedCurrency = CurrencyType.Gold;

        [Header("Events")]
        [SerializeField] private UnityEvent onShopOpened;
        [SerializeField] private UnityEvent onShopClosed;
        [SerializeField] private UnityEvent<ItemSO, int> onItemPurchased;
        [SerializeField] private UnityEvent<ItemSO, int> onItemSold;

        private InventorySystem customerInventory;
        private bool isOpen;

        public string ShopName => shopName;
        public bool IsOpen => isOpen;
        public IReadOnlyList<ShopItem> Items => shopInventory?.items;

        public event Action ShopOpened;
        public event Action ShopClosed;
        public event Action<ItemSO, int, int> ItemPurchased; // item, quantity, cost
        public event Action<ItemSO, int, int> ItemSold; // item, quantity, earnings

        public void OpenShop(InventorySystem customer)
        {
            if (customer == null || shopInventory == null) return;

            customerInventory = customer;
            isOpen = true;

            ShopOpened?.Invoke();
            onShopOpened?.Invoke();

            Debug.Log($"[Shop] {shopName} opened");
        }

        public void CloseShop()
        {
            isOpen = false;
            customerInventory = null;

            ShopClosed?.Invoke();
            onShopClosed?.Invoke();

            Debug.Log($"[Shop] {shopName} closed");
        }

        public int GetBuyPrice(ItemSO item)
        {
            if (item == null) return 0;
            return Mathf.RoundToInt(item.baseValue * buyMultiplier);
        }

        public int GetSellPrice(ItemSO item)
        {
            if (item == null) return 0;
            return Mathf.RoundToInt(item.baseValue * sellMultiplier);
        }

        public bool CanBuy(ItemSO item, int quantity = 1)
        {
            if (!isOpen || customerInventory == null) return false;
            if (item == null || quantity <= 0) return false;

            // Check if shop has the item
            var shopItem = shopInventory.items.Find(i => i.item == item);
            if (shopItem == null) return false;
            if (!shopItem.unlimited && shopItem.stock < quantity) return false;

            // Check customer has enough money
            int totalCost = GetBuyPrice(item) * quantity;
            if (!CurrencyManager.Instance.HasCurrency(acceptedCurrency, totalCost)) return false;

            // Check inventory space
            if (!customerInventory.CanAddItem(item, quantity)) return false;

            return true;
        }

        public bool Buy(ItemSO item, int quantity = 1)
        {
            if (!CanBuy(item, quantity)) return false;

            int totalCost = GetBuyPrice(item) * quantity;

            // Take money
            CurrencyManager.Instance.RemoveCurrency(acceptedCurrency, totalCost);

            // Give item
            customerInventory.AddItem(item, quantity);

            // Update stock
            var shopItem = shopInventory.items.Find(i => i.item == item);
            if (!shopItem.unlimited)
            {
                shopItem.stock -= quantity;
            }

            ItemPurchased?.Invoke(item, quantity, totalCost);
            onItemPurchased?.Invoke(item, quantity);

            Debug.Log($"[Shop] Bought {item.itemName} x{quantity} for {totalCost}");
            return true;
        }

        public bool CanSell(ItemSO item, int quantity = 1)
        {
            if (!isOpen || customerInventory == null) return false;
            if (item == null || quantity <= 0) return false;

            // Check if shop buys this item
            if (!shopInventory.buyItems) return false;

            // Check customer has the item
            if (!customerInventory.HasItem(item.itemId, quantity)) return false;

            return true;
        }

        public bool Sell(ItemSO item, int quantity = 1)
        {
            if (!CanSell(item, quantity)) return false;

            int earnings = GetSellPrice(item) * quantity;

            // Take item
            customerInventory.RemoveItem(item, quantity);

            // Give money
            CurrencyManager.Instance.AddCurrency(acceptedCurrency, earnings);

            ItemSold?.Invoke(item, quantity, earnings);
            onItemSold?.Invoke(item, quantity);

            Debug.Log($"[Shop] Sold {item.itemName} x{quantity} for {earnings}");
            return true;
        }

        public void RestockAll()
        {
            foreach (var item in shopInventory.items)
            {
                item.stock = item.maxStock;
            }
        }
    }

    [CreateAssetMenu(fileName = "NewShopInventory", menuName = "UnityVault/Shop/Shop Inventory")]
    public class ShopInventory : ScriptableObject
    {
        public List<ShopItem> items = new List<ShopItem>();
        public bool buyItems = true;
        public List<ItemType> acceptedItemTypes = new List<ItemType>();
    }

    [Serializable]
    public class ShopItem
    {
        public ItemSO item;
        public int stock = 10;
        public int maxStock = 10;
        public bool unlimited = false;
        public float priceModifier = 1f;
    }

    public enum CurrencyType
    {
        Gold,
        Gems,
        Tokens,
        Credits
    }

    /// <summary>
    /// Currency manager for the economy system.
    /// </summary>
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        [SerializeField] private List<CurrencyData> currencies = new List<CurrencyData>();

        private Dictionary<CurrencyType, int> currencyAmounts = new Dictionary<CurrencyType, int>();

        public event Action<CurrencyType, int> CurrencyChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (var currency in currencies)
            {
                currencyAmounts[currency.type] = currency.startAmount;
            }
        }

        public int GetCurrency(CurrencyType type)
        {
            return currencyAmounts.TryGetValue(type, out int amount) ? amount : 0;
        }

        public bool HasCurrency(CurrencyType type, int amount)
        {
            return GetCurrency(type) >= amount;
        }

        public void AddCurrency(CurrencyType type, int amount)
        {
            if (!currencyAmounts.ContainsKey(type))
            {
                currencyAmounts[type] = 0;
            }

            currencyAmounts[type] += amount;
            CurrencyChanged?.Invoke(type, currencyAmounts[type]);
        }

        public bool RemoveCurrency(CurrencyType type, int amount)
        {
            if (!HasCurrency(type, amount)) return false;

            currencyAmounts[type] -= amount;
            CurrencyChanged?.Invoke(type, currencyAmounts[type]);
            return true;
        }

        public void SetCurrency(CurrencyType type, int amount)
        {
            currencyAmounts[type] = Mathf.Max(0, amount);
            CurrencyChanged?.Invoke(type, currencyAmounts[type]);
        }
    }

    [Serializable]
    public class CurrencyData
    {
        public CurrencyType type;
        public string displayName;
        public Sprite icon;
        public int startAmount;
    }
}
