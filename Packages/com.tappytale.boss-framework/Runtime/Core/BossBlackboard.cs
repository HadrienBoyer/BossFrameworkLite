using System;
using System.Collections.Generic;
using UnityEngine;

namespace TappyTale.BossFight.Core
{
    public sealed class BossBlackboard
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

        public float CurrentHealth { get; set; }
        public float MaxHealth { get; set; }
        public int CurrentPhaseIndex { get; set; }
        public float DistanceToTarget { get; set; }
        public Vector3 DirectionToTarget { get; set; }
        public bool IsFightActive { get; set; }
        public bool IsParryWindowOpen { get; set; }
        public bool IsPerfectDodgeWindowOpen { get; set; }
        public bool IsStaggered { get; set; }
        public float StaggerRemaining { get; set; }
        public float CurrentStaggerBuildup { get; set; }
        public float PunishDamageMultiplier { get; set; } = 1f;
        public bool IsInvulnerable { get; set; }
        public float InvulnerabilityRemaining { get; set; }
        public float FightElapsedTime { get; set; }
        public float TotalDamageTaken { get; set; }
        public int ParriesReceived { get; set; }
        public int PerfectDodgesReceived { get; set; }
        public string CurrentAttackId { get; set; } = string.Empty;
        public string LastAttackId { get; set; } = string.Empty;

        public float NormalizedHealth => MaxHealth <= 0f ? 0f : Mathf.Clamp01(CurrentHealth / MaxHealth);

        public void ResetState(float maxHealth)
        {
            MaxHealth = Mathf.Max(1f, maxHealth);
            CurrentHealth = MaxHealth;
            CurrentPhaseIndex = 0;
            IsParryWindowOpen = false;
            IsPerfectDodgeWindowOpen = false;
            IsStaggered = false;
            StaggerRemaining = 0f;
            CurrentStaggerBuildup = 0f;
            PunishDamageMultiplier = 1f;
            IsInvulnerable = false;
            InvulnerabilityRemaining = 0f;
            FightElapsedTime = 0f;
            TotalDamageTaken = 0f;
            ParriesReceived = 0;
            PerfectDodgesReceived = 0;
            CurrentAttackId = string.Empty;
            LastAttackId = string.Empty;
            _values.Clear();
        }

        public void Set<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A blackboard key cannot be null or empty.", nameof(key));
            }

            _values[key] = value;
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (_values.TryGetValue(key, out object rawValue) && rawValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool Remove(string key) => _values.Remove(key);
        public void ClearCustomValues() => _values.Clear();
    }
}
