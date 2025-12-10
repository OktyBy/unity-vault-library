namespace UnityVault.Core
{
    /// <summary>
    /// Interface for objects that can be saved and loaded.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// Unique identifier for this saveable object.
        /// </summary>
        string SaveId { get; }

        /// <summary>
        /// Get the data to save as a JSON string.
        /// </summary>
        string GetSaveData();

        /// <summary>
        /// Load data from a JSON string.
        /// </summary>
        void LoadSaveData(string data);
    }
}
