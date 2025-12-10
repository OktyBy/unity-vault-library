namespace UnityVault.Core
{
    /// <summary>
    /// Interface for any consumable resource (mana, stamina, energy, etc.)
    /// </summary>
    public interface IResource
    {
        float CurrentValue { get; }
        float MaxValue { get; }
        float Percentage { get; }

        bool TryConsume(float amount);
        void Restore(float amount);
    }
}
