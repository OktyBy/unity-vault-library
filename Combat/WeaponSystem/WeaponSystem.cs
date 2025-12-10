using UnityEngine;
using UnityEngine.Events;
using System;

namespace UnityVault.Combat
{
    /// <summary>
    /// Weapon equipping, swapping, and management system.
    /// </summary>
    public class WeaponSystem : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Weapon Slots")]
        [SerializeField] private Transform weaponHolder;
        [SerializeField] private int maxWeaponSlots = 2;

        [Header("Current Loadout")]
        [SerializeField] private WeaponSO[] weaponSlots;
        [SerializeField] private int activeSlotIndex = 0;

        [Header("Switching")]
        [SerializeField] private float switchCooldown = 0.5f;

        [Header("Events")]
        [SerializeField] private UnityEvent<WeaponSO> onWeaponEquipped;
        [SerializeField] private UnityEvent<WeaponSO> onWeaponUnequipped;
        [SerializeField] private UnityEvent<int> onWeaponSwitched;

        #endregion

        #region Properties

        public WeaponSO CurrentWeapon => GetWeaponAt(activeSlotIndex);
        public int ActiveSlotIndex => activeSlotIndex;
        public bool HasWeapon => CurrentWeapon != null;
        public bool CanSwitch => Time.time >= lastSwitchTime + switchCooldown;

        #endregion

        #region C# Events

        public event Action<WeaponSO> WeaponEquipped;
        public event Action<WeaponSO> WeaponUnequipped;
        public event Action<int> WeaponSwitched;

        #endregion

        #region Private Fields

        private GameObject currentWeaponInstance;
        private float lastSwitchTime;
        private MeleeCombat meleeCombat;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (weaponSlots == null || weaponSlots.Length != maxWeaponSlots)
            {
                weaponSlots = new WeaponSO[maxWeaponSlots];
            }

            meleeCombat = GetComponent<MeleeCombat>();

            if (weaponHolder == null)
            {
                weaponHolder = transform;
            }
        }

        private void Start()
        {
            // Equip initial weapon if set
            if (CurrentWeapon != null)
            {
                SpawnWeaponModel(CurrentWeapon);
            }
        }

        private void Update()
        {
            HandleInput();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Equip a weapon to a specific slot.
        /// </summary>
        public bool EquipWeapon(WeaponSO weapon, int slotIndex = -1)
        {
            if (weapon == null) return false;

            // Auto-find slot if not specified
            if (slotIndex < 0)
            {
                slotIndex = FindEmptySlot();
                if (slotIndex < 0) slotIndex = activeSlotIndex;
            }

            if (!IsValidSlot(slotIndex)) return false;

            // Unequip existing weapon in slot
            if (weaponSlots[slotIndex] != null)
            {
                UnequipWeapon(slotIndex);
            }

            weaponSlots[slotIndex] = weapon;

            // If equipping to active slot, spawn the model
            if (slotIndex == activeSlotIndex)
            {
                SpawnWeaponModel(weapon);
            }

            WeaponEquipped?.Invoke(weapon);
            onWeaponEquipped?.Invoke(weapon);

            Debug.Log($"[WeaponSystem] Equipped {weapon.weaponName} to slot {slotIndex}");
            return true;
        }

        /// <summary>
        /// Unequip weapon from a slot.
        /// </summary>
        public WeaponSO UnequipWeapon(int slotIndex = -1)
        {
            if (slotIndex < 0) slotIndex = activeSlotIndex;
            if (!IsValidSlot(slotIndex)) return null;

            var weapon = weaponSlots[slotIndex];
            if (weapon == null) return null;

            weaponSlots[slotIndex] = null;

            if (slotIndex == activeSlotIndex)
            {
                DestroyWeaponModel();
            }

            WeaponUnequipped?.Invoke(weapon);
            onWeaponUnequipped?.Invoke(weapon);

            Debug.Log($"[WeaponSystem] Unequipped {weapon.weaponName} from slot {slotIndex}");
            return weapon;
        }

        /// <summary>
        /// Switch to a specific weapon slot.
        /// </summary>
        public bool SwitchToSlot(int slotIndex)
        {
            if (!CanSwitch) return false;
            if (!IsValidSlot(slotIndex)) return false;
            if (slotIndex == activeSlotIndex) return false;

            activeSlotIndex = slotIndex;
            lastSwitchTime = Time.time;

            // Swap weapon model
            DestroyWeaponModel();
            if (CurrentWeapon != null)
            {
                SpawnWeaponModel(CurrentWeapon);
            }

            WeaponSwitched?.Invoke(slotIndex);
            onWeaponSwitched?.Invoke(slotIndex);

            Debug.Log($"[WeaponSystem] Switched to slot {slotIndex}");
            return true;
        }

        /// <summary>
        /// Switch to the next weapon.
        /// </summary>
        public void SwitchToNext()
        {
            int nextSlot = (activeSlotIndex + 1) % maxWeaponSlots;
            SwitchToSlot(nextSlot);
        }

        /// <summary>
        /// Switch to the previous weapon.
        /// </summary>
        public void SwitchToPrevious()
        {
            int prevSlot = activeSlotIndex - 1;
            if (prevSlot < 0) prevSlot = maxWeaponSlots - 1;
            SwitchToSlot(prevSlot);
        }

        /// <summary>
        /// Get weapon at a specific slot.
        /// </summary>
        public WeaponSO GetWeaponAt(int slotIndex)
        {
            return IsValidSlot(slotIndex) ? weaponSlots[slotIndex] : null;
        }

        /// <summary>
        /// Check if a slot is empty.
        /// </summary>
        public bool IsSlotEmpty(int slotIndex)
        {
            return IsValidSlot(slotIndex) && weaponSlots[slotIndex] == null;
        }

        #endregion

        #region Private Methods

        private void HandleInput()
        {
            // Number keys for weapon slots
            for (int i = 0; i < maxWeaponSlots && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SwitchToSlot(i);
                }
            }

            // Scroll wheel
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0f)
            {
                SwitchToNext();
            }
            else if (scroll < 0f)
            {
                SwitchToPrevious();
            }
        }

        private void SpawnWeaponModel(WeaponSO weapon)
        {
            DestroyWeaponModel();

            if (weapon.weaponPrefab != null)
            {
                currentWeaponInstance = Instantiate(weapon.weaponPrefab, weaponHolder);
                currentWeaponInstance.transform.localPosition = weapon.holdOffset;
                currentWeaponInstance.transform.localRotation = Quaternion.Euler(weapon.holdRotation);
            }
        }

        private void DestroyWeaponModel()
        {
            if (currentWeaponInstance != null)
            {
                Destroy(currentWeaponInstance);
                currentWeaponInstance = null;
            }
        }

        private int FindEmptySlot()
        {
            for (int i = 0; i < maxWeaponSlots; i++)
            {
                if (weaponSlots[i] == null) return i;
            }
            return -1;
        }

        private bool IsValidSlot(int index)
        {
            return index >= 0 && index < maxWeaponSlots;
        }

        #endregion
    }

    /// <summary>
    /// ScriptableObject defining a weapon.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "UnityVault/Combat/Weapon")]
    public class WeaponSO : ScriptableObject
    {
        [Header("Basic Info")]
        public string weaponId;
        public string weaponName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("Type")]
        public WeaponType weaponType;
        public WeaponHandedness handedness = WeaponHandedness.OneHanded;

        [Header("Stats")]
        public float baseDamage = 10f;
        public float attackSpeed = 1f;
        public float range = 1.5f;
        public float critChance = 0.1f;
        public float critMultiplier = 2f;

        [Header("Model")]
        public GameObject weaponPrefab;
        public Vector3 holdOffset;
        public Vector3 holdRotation;

        [Header("Attacks")]
        public AttackData[] attacks;

        [Header("Audio")]
        public AudioClip swingSound;
        public AudioClip hitSound;
        public AudioClip equipSound;
    }

    public enum WeaponType
    {
        Sword,
        Axe,
        Mace,
        Spear,
        Dagger,
        Fist,
        Staff,
        Bow,
        Crossbow,
        Gun
    }

    public enum WeaponHandedness
    {
        OneHanded,
        TwoHanded,
        DualWield
    }
}
