using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace UnityVault.Monetization
{
    /// <summary>
    /// In-App Purchase manager framework.
    /// Note: Integrate with Unity IAP, Google Play Billing, or Apple StoreKit for production use.
    /// </summary>
    public class IAPManager : MonoBehaviour
    {
        public static IAPManager Instance { get; private set; }

        [Header("Products")]
        [SerializeField] private List<IAPProduct> products = new List<IAPProduct>();

        [Header("Settings")]
        [SerializeField] private bool testMode = true;
        [SerializeField] private bool restorePurchasesOnStart = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onStoreInitialized;
        [SerializeField] private UnityEvent<IAPProduct> onPurchaseSuccess;
        [SerializeField] private UnityEvent<string> onPurchaseFailed;
        [SerializeField] private UnityEvent onRestoreComplete;

        // State
        private bool isInitialized;
        private Dictionary<string, IAPProduct> productMap = new Dictionary<string, IAPProduct>();
        private HashSet<string> ownedProducts = new HashSet<string>();
        private bool isProcessingPurchase;

        // Events
        public event Action StoreInitialized;
        public event Action<IAPProduct> PurchaseStarted;
        public event Action<IAPProduct> PurchaseSuccess;
        public event Action<string, string> PurchaseFailed; // productId, error
        public event Action<IAPProduct> PurchaseRestored;
        public event Action RestoreComplete;
        public event Action<string> InitializationFailed;

        public bool IsInitialized => isInitialized;
        public bool IsProcessing => isProcessingPurchase;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeProducts();
        }

        private void Start()
        {
            InitializeStore();
        }

        private void InitializeProducts()
        {
            foreach (var product in products)
            {
                productMap[product.productId] = product;
            }
        }

        /// <summary>
        /// Initialize the IAP store.
        /// </summary>
        public void InitializeStore()
        {
            if (isInitialized) return;

            // TODO: Initialize Unity IAP or native store SDK
            // For now, simulate initialization
            if (testMode)
            {
                SimulateInitialization();
            }
            else
            {
                // Real store initialization would go here
                // UnityPurchasing.Initialize(this, builder);
            }
        }

        private void SimulateInitialization()
        {
            isInitialized = true;
            LoadOwnedProducts();

            if (restorePurchasesOnStart)
            {
                RestorePurchases();
            }

            StoreInitialized?.Invoke();
            onStoreInitialized?.Invoke();

            Debug.Log("[IAP] Store initialized (test mode)");
        }

        /// <summary>
        /// Purchase a product.
        /// </summary>
        public void Purchase(string productId)
        {
            if (!isInitialized)
            {
                PurchaseFailed?.Invoke(productId, "Store not initialized");
                return;
            }

            if (isProcessingPurchase)
            {
                PurchaseFailed?.Invoke(productId, "Purchase already in progress");
                return;
            }

            if (!productMap.TryGetValue(productId, out IAPProduct product))
            {
                PurchaseFailed?.Invoke(productId, "Product not found");
                return;
            }

            // Check if already owned (for non-consumables)
            if (product.productType == IAPProductType.NonConsumable && IsOwned(productId))
            {
                PurchaseFailed?.Invoke(productId, "Product already owned");
                return;
            }

            isProcessingPurchase = true;
            PurchaseStarted?.Invoke(product);

            if (testMode)
            {
                // Simulate purchase
                SimulatePurchase(product);
            }
            else
            {
                // Real purchase would go here
                // storeController.InitiatePurchase(productId);
            }
        }

        private void SimulatePurchase(IAPProduct product)
        {
            // Simulate network delay
            StartCoroutine(SimulatePurchaseCoroutine(product));
        }

        private System.Collections.IEnumerator SimulatePurchaseCoroutine(IAPProduct product)
        {
            yield return new WaitForSeconds(1f);

            // Simulate success (90% chance in test mode)
            if (UnityEngine.Random.value < 0.9f)
            {
                OnPurchaseSuccess(product);
            }
            else
            {
                OnPurchaseFailed(product.productId, "Simulated purchase failure");
            }
        }

        private void OnPurchaseSuccess(IAPProduct product)
        {
            isProcessingPurchase = false;

            // Track ownership for non-consumables
            if (product.productType == IAPProductType.NonConsumable)
            {
                ownedProducts.Add(product.productId);
                SaveOwnedProducts();
            }

            // Grant rewards
            GrantProductRewards(product);

            PurchaseSuccess?.Invoke(product);
            onPurchaseSuccess?.Invoke(product);

            Debug.Log($"[IAP] Purchase success: {product.productId}");
        }

        private void OnPurchaseFailed(string productId, string error)
        {
            isProcessingPurchase = false;

            PurchaseFailed?.Invoke(productId, error);
            onPurchaseFailed?.Invoke(error);

            Debug.LogWarning($"[IAP] Purchase failed: {productId} - {error}");
        }

        private void GrantProductRewards(IAPProduct product)
        {
            if (product.rewards == null) return;

            foreach (var reward in product.rewards)
            {
                GrantReward(reward);
            }
        }

        private void GrantReward(IAPReward reward)
        {
            switch (reward.rewardType)
            {
                case IAPRewardType.Currency:
                    // TODO: Add currency through your currency system
                    Debug.Log($"[IAP] Granted {reward.amount} {reward.rewardId}");
                    break;

                case IAPRewardType.Item:
                    // TODO: Add item through your inventory system
                    Debug.Log($"[IAP] Granted item: {reward.rewardId}");
                    break;

                case IAPRewardType.Premium:
                    // TODO: Add premium currency
                    Debug.Log($"[IAP] Granted {reward.amount} premium currency");
                    break;

                case IAPRewardType.Subscription:
                    // TODO: Enable subscription features
                    Debug.Log($"[IAP] Activated subscription: {reward.rewardId}");
                    break;

                case IAPRewardType.RemoveAds:
                    // TODO: Disable ads
                    PlayerPrefs.SetInt("AdsRemoved", 1);
                    Debug.Log("[IAP] Ads removed");
                    break;
            }
        }

        /// <summary>
        /// Restore purchases (for non-consumables).
        /// </summary>
        public void RestorePurchases()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[IAP] Cannot restore - store not initialized");
                return;
            }

            if (testMode)
            {
                // Simulate restore
                foreach (var productId in ownedProducts)
                {
                    if (productMap.TryGetValue(productId, out IAPProduct product))
                    {
                        PurchaseRestored?.Invoke(product);
                    }
                }

                RestoreComplete?.Invoke();
                onRestoreComplete?.Invoke();

                Debug.Log("[IAP] Restore complete (test mode)");
            }
            else
            {
                // Real restore would go here
                // storeExtensionProvider.GetExtension<IAppleExtensions>().RestoreTransactions(OnRestored);
            }
        }

        /// <summary>
        /// Check if a product is owned.
        /// </summary>
        public bool IsOwned(string productId)
        {
            return ownedProducts.Contains(productId);
        }

        /// <summary>
        /// Get product by ID.
        /// </summary>
        public IAPProduct GetProduct(string productId)
        {
            return productMap.TryGetValue(productId, out IAPProduct product) ? product : null;
        }

        /// <summary>
        /// Get all products.
        /// </summary>
        public List<IAPProduct> GetAllProducts()
        {
            return new List<IAPProduct>(products);
        }

        /// <summary>
        /// Get products by type.
        /// </summary>
        public List<IAPProduct> GetProductsByType(IAPProductType type)
        {
            List<IAPProduct> result = new List<IAPProduct>();

            foreach (var product in products)
            {
                if (product.productType == type)
                {
                    result.Add(product);
                }
            }

            return result;
        }

        /// <summary>
        /// Get localized price string.
        /// </summary>
        public string GetLocalizedPrice(string productId)
        {
            if (!productMap.TryGetValue(productId, out IAPProduct product))
            {
                return "N/A";
            }

            // TODO: Get real localized price from store
            // return storeController.products.WithID(productId).metadata.localizedPriceString;

            return product.priceString;
        }

        /// <summary>
        /// Check if ads are removed.
        /// </summary>
        public bool AreAdsRemoved()
        {
            return PlayerPrefs.GetInt("AdsRemoved", 0) == 1;
        }

        private void SaveOwnedProducts()
        {
            string json = JsonUtility.ToJson(new OwnedProductsData
            {
                productIds = new List<string>(ownedProducts)
            });

            PlayerPrefs.SetString("OwnedIAP", json);
            PlayerPrefs.Save();
        }

        private void LoadOwnedProducts()
        {
            string json = PlayerPrefs.GetString("OwnedIAP", "");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                OwnedProductsData data = JsonUtility.FromJson<OwnedProductsData>(json);
                ownedProducts = new HashSet<string>(data.productIds);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IAP] Failed to load owned products: {e.Message}");
            }
        }

        /// <summary>
        /// Reset all purchases (test mode only).
        /// </summary>
        public void ResetPurchases()
        {
            if (!testMode)
            {
                Debug.LogWarning("[IAP] Cannot reset purchases in production mode");
                return;
            }

            ownedProducts.Clear();
            PlayerPrefs.DeleteKey("OwnedIAP");
            PlayerPrefs.DeleteKey("AdsRemoved");

            Debug.Log("[IAP] Purchases reset");
        }

        /// <summary>
        /// Validate receipt (for production).
        /// </summary>
        public void ValidateReceipt(string receipt, Action<bool> callback)
        {
            // TODO: Implement server-side receipt validation
            // This is critical for production to prevent fraud

            if (testMode)
            {
                callback?.Invoke(true);
            }
            else
            {
                // Send receipt to your server for validation
                // Your server should validate with Apple/Google servers
            }
        }
    }

    [Serializable]
    public class IAPProduct
    {
        public string productId;
        public string productName;
        [TextArea]
        public string description;
        public Sprite icon;
        public IAPProductType productType;
        public string priceString = "$0.99";
        public float priceValue = 0.99f;

        [Header("Rewards")]
        public List<IAPReward> rewards = new List<IAPReward>();

        [Header("Display")]
        public bool isFeatured;
        public bool isBestValue;
        public string bannerText;
    }

    [Serializable]
    public class IAPReward
    {
        public IAPRewardType rewardType;
        public string rewardId;
        public int amount;
    }

    public enum IAPProductType
    {
        Consumable,      // Can be purchased multiple times (gems, coins)
        NonConsumable,   // One-time purchase (remove ads, unlock feature)
        Subscription     // Recurring payment
    }

    public enum IAPRewardType
    {
        Currency,
        Item,
        Premium,
        Subscription,
        RemoveAds
    }

    [Serializable]
    public class OwnedProductsData
    {
        public List<string> productIds = new List<string>();
    }
}
