using System;
using System.Collections.Generic;

namespace TappyTale.BossFight.Preferences
{
    /// <summary>
    /// Deterministic in-memory store for EditMode tests and sandboxed preference inspection.
    /// </summary>
    public sealed class InMemoryPreferencesStore : IPreferencesStore
    {
        private readonly Dictionary<string, int> _ints = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _floats = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);

        public int SaveCallCount { get; private set; }

        public bool HasKey(string fullKey)
        {
            PlayerPrefsStore.ValidateNamespace(fullKey);
            return _ints.ContainsKey(fullKey) || _floats.ContainsKey(fullKey) || _strings.ContainsKey(fullKey);
        }

        public int GetInt(string fullKey, int defaultValue = 0)
        {
            PlayerPrefsStore.ValidateNamespace(fullKey);
            return _ints.TryGetValue(fullKey, out int val) ? val : defaultValue;
        }

        public void SetInt(string fullKey, int value)
        {
            PlayerPrefsStore.ValidateNamespace(fullKey);
            _floats.Remove(fullKey);
            _strings.Remove(fullKey);
            _ints[fullKey] = value;
        }

        public float GetFloat(string fullKey, float defaultValue = 0f)
        {
            PlayerPrefsStore.ValidateNamespace(fullKey);
            return _floats.TryGetValue(fullKey, out float val) ? val : defaultValue;
        }

        public void SetFloat(string fullKey, float value)
        {
            PlayerPrefsStore.ValidateNamespace(fullKey);
            _ints.Remove(fullKey);
            _strings.Remove(fullKey);
            _floats[fullKey] = value;
        }

        public string GetString(string fullKey, string defaultValue = "")
        {
            PlayerPrefsStore.ValidateNamespace(fullKey);
            return _strings.TryGetValue(fullKey, out string val) ? val : defaultValue;
        }

        public void SetString(string fullKey, string value)
        {
            PlayerPrefsStore.ValidateNamespace(fullKey);
            _ints.Remove(fullKey);
            _floats.Remove(fullKey);
            _strings[fullKey] = value ?? string.Empty;
        }

        public void DeleteKey(string fullKey)
        {
            PlayerPrefsStore.ValidateNamespace(fullKey);
            _ints.Remove(fullKey);
            _floats.Remove(fullKey);
            _strings.Remove(fullKey);
        }

        public void Save()
        {
            SaveCallCount++;
        }

        public void ClearAllRegisteredOnly()
        {
            _ints.Clear();
            _floats.Clear();
            _strings.Clear();
        }
    }
}
