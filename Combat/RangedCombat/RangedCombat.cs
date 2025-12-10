using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;

namespace UnityVault.Combat
{
    /// <summary>
    /// Ranged combat system for projectile-based weapons.
    /// </summary>
    public class RangedCombat : MonoBehaviour
    {
        [Header("Weapon Settings")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private ProjectileData currentProjectile;
        [SerializeField] private float fireRate = 0.5f;
        [SerializeField] private int magazineSize = 30;
        [SerializeField] private float reloadTime = 2f;

        [Header("Accuracy")]
        [SerializeField] private float baseSpread = 0f;
        [SerializeField] private float movingSpread = 2f;
        [SerializeField] private float maxSpread = 5f;
        [SerializeField] private float spreadRecovery = 5f;

        [Header("Events")]
        [SerializeField] private UnityEvent onFire;
        [SerializeField] private UnityEvent onReloadStart;
        [SerializeField] private UnityEvent onReloadComplete;
        [SerializeField] private UnityEvent onAmmoEmpty;

        // State
        private int currentAmmo;
        private float lastFireTime;
        private float currentSpread;
        private bool isReloading;
        private bool isAiming;

        // Properties
        public int CurrentAmmo => currentAmmo;
        public int MaxAmmo => magazineSize;
        public bool CanFire => !isReloading && currentAmmo > 0 && Time.time >= lastFireTime + fireRate;
        public bool IsReloading => isReloading;
        public bool IsAiming => isAiming;

        // Events
        public event Action Fired;
        public event Action ReloadStarted;
        public event Action ReloadCompleted;
        public event Action AmmoEmpty;

        private void Awake()
        {
            currentAmmo = magazineSize;
        }

        private void Update()
        {
            UpdateSpread();
        }

        public bool Fire()
        {
            if (!CanFire) return false;

            lastFireTime = Time.time;
            currentAmmo--;

            // Calculate spread
            Vector3 spread = CalculateSpread();
            Vector3 direction = firePoint.forward + spread;

            // Spawn projectile
            SpawnProjectile(direction);

            // Increase spread
            currentSpread = Mathf.Min(currentSpread + currentProjectile.spreadPerShot, maxSpread);

            Fired?.Invoke();
            onFire?.Invoke();

            if (currentAmmo <= 0)
            {
                AmmoEmpty?.Invoke();
                onAmmoEmpty?.Invoke();
            }

            return true;
        }

        private void SpawnProjectile(Vector3 direction)
        {
            if (currentProjectile.prefab == null) return;

            var projectile = Instantiate(currentProjectile.prefab, firePoint.position, Quaternion.LookRotation(direction));
            var proj = projectile.GetComponent<Projectile>();

            if (proj != null)
            {
                proj.Initialize(currentProjectile, direction, gameObject);
            }
        }

        private Vector3 CalculateSpread()
        {
            float spread = baseSpread + currentSpread;
            if (isAiming) spread *= 0.3f;

            return new Vector3(
                UnityEngine.Random.Range(-spread, spread),
                UnityEngine.Random.Range(-spread, spread),
                0
            ) * 0.01f;
        }

        private void UpdateSpread()
        {
            if (currentSpread > 0)
            {
                currentSpread = Mathf.MoveTowards(currentSpread, 0, spreadRecovery * Time.deltaTime);
            }
        }

        public void StartReload()
        {
            if (isReloading || currentAmmo >= magazineSize) return;
            StartCoroutine(ReloadRoutine());
        }

        private IEnumerator ReloadRoutine()
        {
            isReloading = true;
            ReloadStarted?.Invoke();
            onReloadStart?.Invoke();

            yield return new WaitForSeconds(reloadTime);

            currentAmmo = magazineSize;
            isReloading = false;

            ReloadCompleted?.Invoke();
            onReloadComplete?.Invoke();
        }

        public void SetAiming(bool aiming)
        {
            isAiming = aiming;
        }

        public void AddAmmo(int amount)
        {
            currentAmmo = Mathf.Min(currentAmmo + amount, magazineSize);
        }
    }

    [System.Serializable]
    public class ProjectileData
    {
        public string name;
        public GameObject prefab;
        public float damage = 10f;
        public float speed = 50f;
        public float lifetime = 5f;
        public float spreadPerShot = 0.5f;
        public bool isExplosive;
        public float explosionRadius;
        public GameObject hitEffect;
        public AudioClip fireSound;
        public AudioClip hitSound;
    }
}
