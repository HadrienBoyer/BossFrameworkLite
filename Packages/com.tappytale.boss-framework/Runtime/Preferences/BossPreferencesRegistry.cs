using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TappyTale.BossFight.Preferences
{
    [Serializable]
    public struct PreferenceKeyDefinition
    {
        [SerializeField] private string key;
        [SerializeField] private PreferenceValueType valueType;
        [SerializeField] private string category;
        [SerializeField] private string description;
        [SerializeField] private int defaultInt;
        [SerializeField] private float defaultFloat;
        [SerializeField] private string defaultString;

        public PreferenceKeyDefinition(
            string key,
            PreferenceValueType valueType,
            string category,
            string description,
            int defaultInt = 0,
            float defaultFloat = 0f,
            string defaultString = "")
        {
            this.key = key ?? string.Empty;
            this.valueType = valueType;
            this.category = string.IsNullOrWhiteSpace(category) ? "General" : category;
            this.description = description ?? string.Empty;
            this.defaultInt = defaultInt;
            this.defaultFloat = defaultFloat;
            this.defaultString = defaultString ?? string.Empty;
        }

        public string Key => key;
        public PreferenceValueType ValueType => valueType;
        public string Category => string.IsNullOrWhiteSpace(category) ? "General" : category;
        public string Description => description ?? string.Empty;
        public int DefaultInt => defaultInt;
        public float DefaultFloat => defaultFloat;
        public string DefaultString => defaultString ?? string.Empty;

        public string BuildFullKey(string profile)
        {
            string safeProfile = BossPreferencesService.SanitizeToken(string.IsNullOrWhiteSpace(profile) ? "default" : profile);
            string safeKey = BossPreferencesService.SanitizeToken(key);
            return $"{PlayerPrefsStore.RequiredNamespacePrefix}{safeProfile}.{safeKey}";
        }

        public string FormatDefaultValue()
        {
            return valueType switch
            {
                PreferenceValueType.Int => defaultInt.ToString(CultureInfo.InvariantCulture),
                PreferenceValueType.Float => defaultFloat.ToString("0.###", CultureInfo.InvariantCulture),
                _ => defaultString ?? string.Empty
            };
        }
    }

    [Serializable]
    public struct PreferenceSnapshotEntry
    {
        public string key;
        public PreferenceValueType valueType;
        public bool isSet;
        public int intValue;
        public float floatValue;
        public string stringValue;
    }

    [Serializable]
    public sealed class BossPreferencesSnapshot
    {
        public string formatVersion = "1.0";
        public string profile = "default";
        public string exportedAtUtc = string.Empty;
        public List<PreferenceSnapshotEntry> entries = new();
    }

    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Preferences Registry", fileName = "BossPreferencesRegistry")]
    public sealed class BossPreferencesRegistry : ScriptableObject
    {
        [SerializeField] private string defaultProfile = "default";
        [SerializeField] private List<string> knownProfiles = new() { "default", "duel_lab", "playtest" };
        [SerializeField] private List<PreferenceKeyDefinition> keys = new();

        public string DefaultProfile => string.IsNullOrWhiteSpace(defaultProfile) ? "default" : defaultProfile;
        public IReadOnlyList<string> KnownProfiles => knownProfiles;
        public IReadOnlyList<PreferenceKeyDefinition> Keys => keys;

        public void EnsureBuiltInDefaults()
        {
            if (knownProfiles == null || knownProfiles.Count == 0)
            {
                knownProfiles = new List<string> { "default", "duel_lab", "playtest" };
            }

            IReadOnlyList<PreferenceKeyDefinition> builtIn = BossPreferencesService.GetBuiltInKeys();
            for (int i = 0; i < builtIn.Count; i++)
            {
                PreferenceKeyDefinition candidate = builtIn[i];
                bool exists = false;
                for (int j = 0; j < keys.Count; j++)
                {
                    if (string.Equals(keys[j].Key, candidate.Key, StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    keys.Add(candidate);
                }
            }
        }
    }

    /// <summary>
    /// High-level typed preferences manager operating strictly on registered framework keys.
    /// Supports reading, writing, default restoration, diff checks, profile reset, and JSON snapshot export/import.
    /// </summary>
    public sealed class BossPreferencesService
    {
        public const string KeyFightsStarted = "duel.fights_started";
        public const string KeyVictories = "duel.victories";
        public const string KeyDefeats = "duel.defeats";
        public const string KeyParriesLanded = "duel.parries_landed";
        public const string KeyPerfectDodges = "duel.perfect_dodges";
        public const string KeyBestVictoryTime = "duel.best_victory_time";
        public const string KeyLastBossId = "duel.last_boss_id";
        public const string KeySfxVolume = "settings.sfx_volume";
        public const string KeyCameraShakeScale = "settings.camera_shake_scale";
        public const string KeyShowCombatPrompts = "hud.show_combat_prompts";
        public const string KeyShowTelemetryOverlay = "debug.show_telemetry";

        private static readonly List<PreferenceKeyDefinition> BuiltInKeys = new()
        {
            new PreferenceKeyDefinition(KeyFightsStarted, PreferenceValueType.Int, "Duel Telemetry", "Total number of boss duels started in this profile.", defaultInt: 0),
            new PreferenceKeyDefinition(KeyVictories, PreferenceValueType.Int, "Duel Telemetry", "Total number of boss victories recorded.", defaultInt: 0),
            new PreferenceKeyDefinition(KeyDefeats, PreferenceValueType.Int, "Duel Telemetry", "Total number of player defeats recorded.", defaultInt: 0),
            new PreferenceKeyDefinition(KeyParriesLanded, PreferenceValueType.Int, "Duel Telemetry", "Total number of successful parries triggered during boss reaction windows.", defaultInt: 0),
            new PreferenceKeyDefinition(KeyPerfectDodges, PreferenceValueType.Int, "Duel Telemetry", "Total number of successful perfect dodges triggered.", defaultInt: 0),
            new PreferenceKeyDefinition(KeyBestVictoryTime, PreferenceValueType.Float, "Duel Telemetry", "Fastest boss defeat time in seconds (0 = none recorded).", defaultFloat: 0f),
            new PreferenceKeyDefinition(KeyLastBossId, PreferenceValueType.String, "Duel Telemetry", "Identifier of the last boss fought in this profile.", defaultString: "starter_boss"),
            new PreferenceKeyDefinition(KeySfxVolume, PreferenceValueType.Float, "Audio & Feedback", "Master volume multiplier for combat audio cues (0..1).", defaultFloat: 0.85f),
            new PreferenceKeyDefinition(KeyCameraShakeScale, PreferenceValueType.Float, "Audio & Feedback", "Multiplier for camera shake amplitude (0..2).", defaultFloat: 1.0f),
            new PreferenceKeyDefinition(KeyShowCombatPrompts, PreferenceValueType.Int, "HUD & Readability", "Whether to display Parry / Dodge reaction prompts on HUD (1 = enabled, 0 = disabled).", defaultInt: 1),
            new PreferenceKeyDefinition(KeyShowTelemetryOverlay, PreferenceValueType.Int, "Debug", "Whether to display live state telemetry on the demo HUD (1 = enabled, 0 = disabled).", defaultInt: 1)
        };

        private readonly IPreferencesStore _store;
        private readonly List<PreferenceKeyDefinition> _registeredKeys = new();

        public BossPreferencesService(IPreferencesStore store, BossPreferencesRegistry registryAsset = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            RebuildCatalog(registryAsset);
        }

        public IPreferencesStore Store => _store;
        public IReadOnlyList<PreferenceKeyDefinition> RegisteredKeys => _registeredKeys;

        public static IReadOnlyList<PreferenceKeyDefinition> GetBuiltInKeys() => BuiltInKeys;

        public void RebuildCatalog(BossPreferencesRegistry registryAsset = null)
        {
            _registeredKeys.Clear();
            HashSet<string> added = new(StringComparer.Ordinal);

            if (registryAsset != null && registryAsset.Keys != null)
            {
                for (int i = 0; i < registryAsset.Keys.Count; i++)
                {
                    PreferenceKeyDefinition def = registryAsset.Keys[i];
                    if (!string.IsNullOrWhiteSpace(def.Key) && added.Add(def.Key))
                    {
                        _registeredKeys.Add(def);
                    }
                }
            }

            for (int i = 0; i < BuiltInKeys.Count; i++)
            {
                PreferenceKeyDefinition def = BuiltInKeys[i];
                if (added.Add(def.Key))
                {
                    _registeredKeys.Add(def);
                }
            }
        }

        public bool TryGetDefinition(string key, out PreferenceKeyDefinition definition)
        {
            for (int i = 0; i < _registeredKeys.Count; i++)
            {
                if (string.Equals(_registeredKeys[i].Key, key, StringComparison.Ordinal))
                {
                    definition = _registeredKeys[i];
                    return true;
                }
            }

            definition = default;
            return false;
        }

        public bool HasSavedValue(string profile, PreferenceKeyDefinition definition)
        {
            return _store.HasKey(definition.BuildFullKey(profile));
        }

        public bool IsModifiedFromDefault(string profile, PreferenceKeyDefinition definition)
        {
            string fullKey = definition.BuildFullKey(profile);
            if (!_store.HasKey(fullKey))
            {
                return false;
            }

            return definition.ValueType switch
            {
                PreferenceValueType.Int => _store.GetInt(fullKey, definition.DefaultInt) != definition.DefaultInt,
                PreferenceValueType.Float => !Mathf.Approximately(_store.GetFloat(fullKey, definition.DefaultFloat), definition.DefaultFloat),
                _ => !string.Equals(_store.GetString(fullKey, definition.DefaultString), definition.DefaultString, StringComparison.Ordinal)
            };
        }

        public int GetInt(string profile, string key)
        {
            PreferenceKeyDefinition def = RequireDefinition(key, PreferenceValueType.Int);
            return _store.GetInt(def.BuildFullKey(profile), def.DefaultInt);
        }

        public void SetInt(string profile, string key, int value, bool saveImmediately = true)
        {
            PreferenceKeyDefinition def = RequireDefinition(key, PreferenceValueType.Int);
            _store.SetInt(def.BuildFullKey(profile), value);
            if (saveImmediately) _store.Save();
        }

        public float GetFloat(string profile, string key)
        {
            PreferenceKeyDefinition def = RequireDefinition(key, PreferenceValueType.Float);
            return _store.GetFloat(def.BuildFullKey(profile), def.DefaultFloat);
        }

        public void SetFloat(string profile, string key, float value, bool saveImmediately = true)
        {
            PreferenceKeyDefinition def = RequireDefinition(key, PreferenceValueType.Float);
            _store.SetFloat(def.BuildFullKey(profile), value);
            if (saveImmediately) _store.Save();
        }

        public string GetString(string profile, string key)
        {
            PreferenceKeyDefinition def = RequireDefinition(key, PreferenceValueType.String);
            return _store.GetString(def.BuildFullKey(profile), def.DefaultString);
        }

        public void SetString(string profile, string key, string value, bool saveImmediately = true)
        {
            PreferenceKeyDefinition def = RequireDefinition(key, PreferenceValueType.String);
            _store.SetString(def.BuildFullKey(profile), value ?? string.Empty);
            if (saveImmediately) _store.Save();
        }

        public string FormatCurrentValue(string profile, PreferenceKeyDefinition definition)
        {
            string fullKey = definition.BuildFullKey(profile);
            return definition.ValueType switch
            {
                PreferenceValueType.Int => _store.GetInt(fullKey, definition.DefaultInt).ToString(CultureInfo.InvariantCulture),
                PreferenceValueType.Float => _store.GetFloat(fullKey, definition.DefaultFloat).ToString("0.###", CultureInfo.InvariantCulture),
                _ => _store.GetString(fullKey, definition.DefaultString)
            };
        }

        public void ResetKeyToDefault(string profile, string key, bool deleteKey = false)
        {
            if (!TryGetDefinition(key, out PreferenceKeyDefinition def))
            {
                throw new KeyNotFoundException($"Unregistered preference key '{key}'.");
            }

            string fullKey = def.BuildFullKey(profile);
            if (deleteKey)
            {
                _store.DeleteKey(fullKey);
            }
            else
            {
                switch (def.ValueType)
                {
                    case PreferenceValueType.Int:
                        _store.SetInt(fullKey, def.DefaultInt);
                        break;
                    case PreferenceValueType.Float:
                        _store.SetFloat(fullKey, def.DefaultFloat);
                        break;
                    case PreferenceValueType.String:
                        _store.SetString(fullKey, def.DefaultString);
                        break;
                }
            }

            _store.Save();
        }

        public void DeleteKey(string profile, string key)
        {
            ResetKeyToDefault(profile, key, deleteKey: true);
        }

        public void ResetProfile(string profile, bool deleteKeys = true)
        {
            for (int i = 0; i < _registeredKeys.Count; i++)
            {
                ResetKeyToDefault(profile, _registeredKeys[i].Key, deleteKey: deleteKeys);
            }
            _store.Save();
        }

        public string ExportSnapshotJson(string profile, bool prettyPrint = true)
        {
            BossPreferencesSnapshot snapshot = new()
            {
                formatVersion = "1.0",
                profile = string.IsNullOrWhiteSpace(profile) ? "default" : profile,
                exportedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };

            for (int i = 0; i < _registeredKeys.Count; i++)
            {
                PreferenceKeyDefinition def = _registeredKeys[i];
                string fullKey = def.BuildFullKey(profile);
                bool hasKey = _store.HasKey(fullKey);

                PreferenceSnapshotEntry entry = new()
                {
                    key = def.Key,
                    valueType = def.ValueType,
                    isSet = hasKey,
                    intValue = def.ValueType == PreferenceValueType.Int ? _store.GetInt(fullKey, def.DefaultInt) : 0,
                    floatValue = def.ValueType == PreferenceValueType.Float ? _store.GetFloat(fullKey, def.DefaultFloat) : 0f,
                    stringValue = def.ValueType == PreferenceValueType.String ? _store.GetString(fullKey, def.DefaultString) : string.Empty
                };

                snapshot.entries.Add(entry);
            }

            return JsonUtility.ToJson(snapshot, prettyPrint);
        }

        public int ImportSnapshotJson(string json, string targetProfileOverride = null)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Snapshot JSON cannot be empty.", nameof(json));
            }

            BossPreferencesSnapshot snapshot = JsonUtility.FromJson<BossPreferencesSnapshot>(json);
            if (snapshot == null || snapshot.entries == null)
            {
                throw new InvalidOperationException("Failed to deserialize BossPreferencesSnapshot JSON.");
            }

            string profile = !string.IsNullOrWhiteSpace(targetProfileOverride)
                ? targetProfileOverride
                : (!string.IsNullOrWhiteSpace(snapshot.profile) ? snapshot.profile : "default");

            int appliedCount = 0;
            for (int i = 0; i < snapshot.entries.Count; i++)
            {
                PreferenceSnapshotEntry entry = snapshot.entries[i];
                if (!TryGetDefinition(entry.key, out PreferenceKeyDefinition def))
                {
                    continue;
                }

                string fullKey = def.BuildFullKey(profile);
                if (!entry.isSet)
                {
                    _store.DeleteKey(fullKey);
                    appliedCount++;
                    continue;
                }

                switch (def.ValueType)
                {
                    case PreferenceValueType.Int:
                        _store.SetInt(fullKey, entry.intValue);
                        break;
                    case PreferenceValueType.Float:
                        _store.SetFloat(fullKey, entry.floatValue);
                        break;
                    case PreferenceValueType.String:
                        _store.SetString(fullKey, entry.stringValue ?? string.Empty);
                        break;
                }

                appliedCount++;
            }

            _store.Save();
            return appliedCount;
        }

        public static string SanitizeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return "default";
            }

            char[] chars = token.Trim().ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool valid = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '.' || c == '-';
                if (!valid)
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private PreferenceKeyDefinition RequireDefinition(string key, PreferenceValueType expectedType)
        {
            if (!TryGetDefinition(key, out PreferenceKeyDefinition def))
            {
                throw new KeyNotFoundException($"Preference key '{key}' is not registered in BossPreferencesService.");
            }

            if (def.ValueType != expectedType)
            {
                throw new InvalidOperationException($"Preference key '{key}' has type {def.ValueType}, not {expectedType}.");
            }

            return def;
        }
    }
}
