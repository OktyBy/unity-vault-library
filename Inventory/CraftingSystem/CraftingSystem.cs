using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityVault.Inventory
{
    /// <summary>
    /// Crafting system with recipes and material requirements.
    /// </summary>
    public class CraftingSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventorySystem inventory;

        [Header("Recipes")]
        [SerializeField] private List<CraftingRecipeSO> knownRecipes = new List<CraftingRecipeSO>();
        [SerializeField] private bool learnAllOnStart = false;

        [Header("Settings")]
        [SerializeField] private float craftingTime = 1f;
        [SerializeField] private bool allowQueueing = true;
        [SerializeField] private int maxQueueSize = 5;

        [Header("Events")]
        [SerializeField] private UnityEvent<CraftingRecipeSO> onCraftStarted;
        [SerializeField] private UnityEvent<CraftingRecipeSO, ItemInstance> onCraftCompleted;
        [SerializeField] private UnityEvent<CraftingRecipeSO> onCraftFailed;
        [SerializeField] private UnityEvent<CraftingRecipeSO> onRecipeLearned;

        // State
        private Queue<CraftingRecipeSO> craftingQueue = new Queue<CraftingRecipeSO>();
        private CraftingRecipeSO currentlyCrafting;
        private float craftingProgress;
        private bool isCrafting;

        public bool IsCrafting => isCrafting;
        public float CraftingProgress => craftingProgress;
        public CraftingRecipeSO CurrentRecipe => currentlyCrafting;
        public int QueueCount => craftingQueue.Count;
        public IReadOnlyList<CraftingRecipeSO> KnownRecipes => knownRecipes;

        public event Action<CraftingRecipeSO> CraftStarted;
        public event Action<CraftingRecipeSO, ItemInstance> CraftCompleted;
        public event Action<CraftingRecipeSO> CraftFailed;
        public event Action<CraftingRecipeSO> RecipeLearned;

        private void Awake()
        {
            if (inventory == null)
            {
                inventory = GetComponent<InventorySystem>();
            }
        }

        private void Start()
        {
            if (learnAllOnStart)
            {
                var allRecipes = Resources.LoadAll<CraftingRecipeSO>("Recipes");
                foreach (var recipe in allRecipes)
                {
                    LearnRecipe(recipe);
                }
            }
        }

        private void Update()
        {
            if (isCrafting)
            {
                UpdateCrafting();
            }
            else if (craftingQueue.Count > 0)
            {
                StartCrafting(craftingQueue.Dequeue());
            }
        }

        public bool CanCraft(CraftingRecipeSO recipe)
        {
            if (recipe == null) return false;
            if (!knownRecipes.Contains(recipe)) return false;

            // Check materials
            foreach (var material in recipe.requiredMaterials)
            {
                if (!inventory.HasItem(material.item.itemId, material.quantity))
                {
                    return false;
                }
            }

            // Check inventory space for result
            if (!inventory.CanAddItem(recipe.result.item, recipe.result.quantity))
            {
                return false;
            }

            return true;
        }

        public bool Craft(CraftingRecipeSO recipe)
        {
            if (!CanCraft(recipe)) return false;

            if (isCrafting)
            {
                if (allowQueueing && craftingQueue.Count < maxQueueSize)
                {
                    craftingQueue.Enqueue(recipe);
                    return true;
                }
                return false;
            }

            return StartCrafting(recipe);
        }

        private bool StartCrafting(CraftingRecipeSO recipe)
        {
            // Consume materials
            foreach (var material in recipe.requiredMaterials)
            {
                inventory.RemoveItem(material.item.itemId, material.quantity);
            }

            currentlyCrafting = recipe;
            craftingProgress = 0f;
            isCrafting = true;

            CraftStarted?.Invoke(recipe);
            onCraftStarted?.Invoke(recipe);

            return true;
        }

        private void UpdateCrafting()
        {
            craftingProgress += Time.deltaTime / (currentlyCrafting.craftTime > 0 ? currentlyCrafting.craftTime : craftingTime);

            if (craftingProgress >= 1f)
            {
                CompleteCrafting();
            }
        }

        private void CompleteCrafting()
        {
            var recipe = currentlyCrafting;

            // Create result item
            var result = recipe.result.item.CreateInstance(recipe.result.quantity);

            // Try to add to inventory
            var addResult = inventory.AddItem(result);

            if (addResult.success)
            {
                CraftCompleted?.Invoke(recipe, result);
                onCraftCompleted?.Invoke(recipe, result);
                Debug.Log($"[Crafting] Crafted {recipe.result.item.itemName} x{recipe.result.quantity}");
            }
            else
            {
                // Refund materials if failed
                foreach (var material in recipe.requiredMaterials)
                {
                    inventory.AddItem(material.item, material.quantity);
                }

                CraftFailed?.Invoke(recipe);
                onCraftFailed?.Invoke(recipe);
                Debug.Log($"[Crafting] Failed to craft {recipe.result.item.itemName}");
            }

            currentlyCrafting = null;
            craftingProgress = 0f;
            isCrafting = false;
        }

        public void CancelCrafting()
        {
            if (!isCrafting) return;

            // Refund materials
            foreach (var material in currentlyCrafting.requiredMaterials)
            {
                inventory.AddItem(material.item, material.quantity);
            }

            currentlyCrafting = null;
            craftingProgress = 0f;
            isCrafting = false;
        }

        public void LearnRecipe(CraftingRecipeSO recipe)
        {
            if (recipe == null || knownRecipes.Contains(recipe)) return;

            knownRecipes.Add(recipe);
            RecipeLearned?.Invoke(recipe);
            onRecipeLearned?.Invoke(recipe);

            Debug.Log($"[Crafting] Learned recipe: {recipe.recipeName}");
        }

        public bool KnowsRecipe(CraftingRecipeSO recipe)
        {
            return knownRecipes.Contains(recipe);
        }

        public List<CraftingRecipeSO> GetCraftableRecipes()
        {
            return knownRecipes.Where(r => CanCraft(r)).ToList();
        }

        public List<CraftingRecipeSO> GetRecipesByCategory(string category)
        {
            return knownRecipes.Where(r => r.category == category).ToList();
        }
    }

    [CreateAssetMenu(fileName = "NewRecipe", menuName = "UnityVault/Crafting/Recipe")]
    public class CraftingRecipeSO : ScriptableObject
    {
        [Header("Basic Info")]
        public string recipeId;
        public string recipeName;
        [TextArea] public string description;
        public string category;
        public Sprite icon;

        [Header("Requirements")]
        public List<CraftingMaterial> requiredMaterials = new List<CraftingMaterial>();
        public int requiredCraftingLevel = 1;
        public string requiredStation;

        [Header("Result")]
        public CraftingResult result;
        public float craftTime = 1f;
        public float successChance = 100f;

        [Header("Experience")]
        public int craftingXP = 10;
    }

    [Serializable]
    public class CraftingMaterial
    {
        public ItemSO item;
        public int quantity = 1;
    }

    [Serializable]
    public class CraftingResult
    {
        public ItemSO item;
        public int quantity = 1;
    }
}
