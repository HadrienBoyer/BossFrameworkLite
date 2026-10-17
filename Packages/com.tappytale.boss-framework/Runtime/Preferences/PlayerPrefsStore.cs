using System;
using UnityEngine;

namespace TappyTale.BossFight.Preferences
{
    /// <summary>
    /// UnityEngine.PlayerPrefs adapter enforcing the 'tappytale.bossframework.' namespace prefix.
    /// Never calls PlayerPrefs.DeleteAll().
    /// </summary>
    public sealed class PlayerPrefsStore : IPreferencesStore
    {
        public const string RequiredNamespacePrefix = "tappytale.bossframework.";

        public bool HasKey(string fullKey)
        {
            ValidateNamespace(fullKey);
            return PlayerPrefs.HasKey(fullKey);
        }

        public int GetInt(string fullKey, int defaultValue = 0)
        {
            ValidateNamespace(fullKey);
            return PlayerPrefs.GetInt(fullKey, defaultValue);
        }

        public void SetInt(string fullKey, int value)
        {
            ValidateNamespace(fullKey);
            PlayerPrefs.SetInt(fullKey, value);
        }

        public float GetFloat(string fullKey, float defaultValue = 0f)
        {
            ValidateNamespace(fullKey);
            return PlayerPrefs.GetFloat(fullKey, defaultValue);
        }

        public void SetFloat(string fullKey, float value)
        {
            ValidateNamespace(fullKey);
            PlayerPrefs.SetFloat(fullKey, value);
        }

        public string GetString(string fullKey, string defaultValue = "")
        {
            ValidateNamespace(fullKey);
            return PlayerPrefs.GetString(fullKey, defaultValue);
        }

        public void SetString(string fullKey, string value)
        {
            ValidateNamespace(fullKey);
            PlayerPrefs.SetString(fullKey, value ?? string.Empty);
        }

        public void DeleteKey(string fullKey)
        {
            ValidateNamespace(fullKey);
            PlayerPrefs.DeleteKey(fullKey);
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }

        public static void ValidateNamespace(string fullKey)
        {
            if (string.IsNullOrWhiteSpace(fullKey) ||
                !fullKey.StartsWith(RequiredNamespacePrefix, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Key '{fullKey}' is outside the allowed '{RequiredNamespacePrefix}' namespace.",
                    nameof(fullKey));
            }
        }
    }
}
