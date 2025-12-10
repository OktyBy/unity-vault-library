using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace UnityVault.Inventory
{
    /// <summary>
    /// Loot drop and generation system.
    /// </summary>
    public class LootDropper : MonoBehaviour
    {
        [Header("Loot Table")]
        [SerializeField] private LootTable lootTable;
        [SerializeField] private bool dropOnDeath = true;

        [Header("Drop Settings")]
        [SerializeField] private Transform dropPoint;
        [SerializeField] private float dropRadius = 1f;
        [SerializeField] private float dropForce = 3f;
        [SerializeField] private float dropUpwardForce = 5f;

        [Header("Prefab")]
        [SerializeField] private GameObject lootItemPrefab;

        private void Start()
        {
            if (dropOnDeath)
            {
                var health = GetComponent<UnityVault.Core.HealthSystem>();
                if (health != null)
                {
                    health.Died += OnDeath;
                }
            }
        }

        private void OnDeath()
        {
            DropLoot();
        }

        public void DropLoot()
        {
            if (lootTable == null) return;

            var items = lootTable.GenerateLoot();

            foreach (var item in items)
            {
                SpawnLootItem(item);
            }
        }

        public void DropItem(ItemSO item, int quantity = 1)
        {
            var instance = item.CreateInstance(quantity);
            SpawnLootItem(instance);
        }

        private void SpawnLootItem(ItemInstance item)
        {
            if (lootItemPrefab == null || item == null) return;

            Vector3 spawnPos = dropPoint != null ? dropPoint.position : transform.position;
            spawnPos += Random.insideUnitSphere * dropRadius;
            spawnPos.y = transform.position.y;

            var lootObj = Instantiate(lootItemPrefab, spawnPos, Quaternion.identity);
            var worldItem = lootObj.GetComponent<WorldItem>();

            if (worldItem != null)
            {
                worldItem.Initialize(item);
            }

            // Apply drop force
            var rb = lootObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 force = Random.insideUnitSphere * dropForce;
                force.y = dropUpwardForce;
                rb.AddForce(force, ForceMode.Impulse);
            }
        }
    }

    [CreateAssetMenu(fileName = "NewLootTable", menuName = "UnityVault/Loot/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [Header("Entries")]
        public List<LootEntry> entries = new List<LootEntry>();

        [Header("Settings")]
        public int minDrops = 1;
        public int maxDrops = 3;
        public bool guaranteeOne = true;

        public List<ItemInstance> GenerateLoot()
        {
            var results = new List<ItemInstance>();

            if (entries.Count == 0) return results;

            // Guaranteed drops
            if (guaranteeOne && entries.Count > 0)
            {
                var guaranteed = RollForItem();
                if (guaranteed != null)
                {
                    results.Add(guaranteed);
                }
            }

            // Random drops
            int dropCount = Random.Range(minDrops, maxDrops + 1);
            if (guaranteeOne) dropCount--;

            for (int i = 0; i < dropCount; i++)
            {
                var item = RollForItem();
                if (item != null)
                {
                    results.Add(item);
                }
            }

            return results;
        }

        private ItemInstance RollForItem()
        {
            float totalWeight = entries.Sum(e => e.weight);
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var entry in entries)
            {
                cumulative += entry.weight;
                if (roll <= cumulative)
                {
                    // Check drop chance
                    if (Random.Range(0f, 100f) <= entry.dropChance)
                    {
                        int quantity = Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                        return entry.item.CreateInstance(quantity);
                    }
                    return null;
                }
            }

            return null;
        }
    }

    [System.Serializable]
    public class LootEntry
    {
        public ItemSO item;
        [Range(0f, 100f)] public float dropChance = 100f;
        public float weight = 1f;
        public int minQuantity = 1;
        public int maxQuantity = 1;
        public ItemRarity minRarity = ItemRarity.Common;
    }

    /// <summary>
    /// World item that can be picked up.
    /// </summary>
    public class WorldItem : MonoBehaviour
    {
        [Header("Item Data")]
        [SerializeField] private ItemInstance itemInstance;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobHeight = 0.2f;
        [SerializeField] private float rotateSpeed = 45f;

        [Header("Pickup")]
        [SerializeField] private bool autoPickup = false;
        [SerializeField] private float pickupRadius = 1f;
        [SerializeField] private float pickupDelay = 0.5f;
        [SerializeField] private string playerTag = "Player";

        private float spawnTime;
        private Vector3 startPosition;

        public ItemInstance Item => itemInstance;

        private void Start()
        {
            spawnTime = Time.time;
            startPosition = transform.position;
        }

        private void Update()
        {
            // Bob animation
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = startPosition + Vector3.up * bob;

            // Rotate
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
        }

        public void Initialize(ItemInstance item)
        {
            itemInstance = item;

            // Update visuals
            if (item?.itemData != null)
            {
                if (spriteRenderer != null && item.itemData.icon != null)
                {
                    spriteRenderer.sprite = item.itemData.icon;
                }

                if (item.itemData.worldModel != null && meshRenderer != null)
                {
                    // Could instantiate 3D model here
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!autoPickup) return;
            if (Time.time - spawnTime < pickupDelay) return;
            if (!other.CompareTag(playerTag)) return;

            TryPickup(other.gameObject);
        }

        public bool TryPickup(GameObject picker)
        {
            var inventory = picker.GetComponent<InventorySystem>();
            if (inventory == null) return false;

            var result = inventory.AddItem(itemInstance);
            if (result.success)
            {
                Destroy(gameObject);
                return true;
            }

            return false;
        }
    }
}
