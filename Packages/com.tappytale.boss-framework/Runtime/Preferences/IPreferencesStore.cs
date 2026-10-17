namespace TappyTale.BossFight.Preferences
{
    public enum PreferenceValueType
    {
        Int = 0,
        Float = 1,
        String = 2
    }

    /// <summary>
    /// Abstraction over key-value runtime preference persistence (PlayerPrefs at runtime/editor, InMemoryStore in tests).
    /// Implementations must NEVER call PlayerPrefs.DeleteAll() or touch keys outside the framework namespace.
    /// </summary>
    public interface IPreferencesStore
    {
        bool HasKey(string fullKey);
        int GetInt(string fullKey, int defaultValue = 0);
        void SetInt(string fullKey, int value);
        float GetFloat(string fullKey, float defaultValue = 0f);
        void SetFloat(string fullKey, float value);
        string GetString(string fullKey, string defaultValue = "");
        void SetString(string fullKey, string value);
        void DeleteKey(string fullKey);
        void Save();
    }
}
