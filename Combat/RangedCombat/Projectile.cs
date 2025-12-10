using UnityEngine;

namespace UnityVault.Combat
{
    /// <summary>
    /// Projectile component for ranged combat.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private ProjectileData data;
        private Vector3 direction;
        private GameObject owner;
        private float spawnTime;

        public void Initialize(ProjectileData data, Vector3 direction, GameObject owner)
        {
            this.data = data;
            this.direction = direction.normalized;
            this.owner = owner;
            spawnTime = Time.time;
        }

        private void Update()
        {
            if (data == null) return;

            // Move projectile
            transform.position += direction * data.speed * Time.deltaTime;

            // Check lifetime
            if (Time.time - spawnTime >= data.lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject == owner) return;

            // Deal damage
            var damageable = other.GetComponent<UnityVault.Core.IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(data.damage);
            }

            // Explosion
            if (data.isExplosive && data.explosionRadius > 0)
            {
                Explode();
            }

            // Hit effect
            if (data.hitEffect != null)
            {
                Instantiate(data.hitEffect, transform.position, Quaternion.identity);
            }

            // Hit sound
            if (data.hitSound != null)
            {
                AudioSource.PlayClipAtPoint(data.hitSound, transform.position);
            }

            Destroy(gameObject);
        }

        private void Explode()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, data.explosionRadius);

            foreach (var hit in hits)
            {
                if (hit.gameObject == owner) continue;

                var damageable = hit.GetComponent<UnityVault.Core.IDamageable>();
                if (damageable != null)
                {
                    float distance = Vector3.Distance(transform.position, hit.transform.position);
                    float falloff = 1f - (distance / data.explosionRadius);
                    float damage = data.damage * falloff;
                    damageable.TakeDamage(damage);
                }

                var rb = hit.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddExplosionForce(data.damage * 50f, transform.position, data.explosionRadius);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (data != null && data.isExplosive)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, data.explosionRadius);
            }
        }
    }
}
