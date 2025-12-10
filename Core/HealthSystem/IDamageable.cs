namespace UnityVault.Core
{
    /// <summary>
    /// Interface for any object that can receive damage.
    /// Implement this to make objects compatible with damage systems.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Apply damage to the object.
        /// </summary>
        /// <param name="damage">Amount of damage to apply</param>
        /// <returns>Actual damage dealt after calculations</returns>
        float TakeDamage(float damage);

        /// <summary>
        /// Whether the object is still alive.
        /// </summary>
        bool IsAlive { get; }
    }
}
