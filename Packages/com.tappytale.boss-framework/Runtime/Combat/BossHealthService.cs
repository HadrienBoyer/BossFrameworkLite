using System;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Events;
using UnityEngine;

namespace TappyTale.BossFight.Combat
{
    public sealed class BossHealthService : IBossService, IBossTickable
    {
        private readonly BossDefinition _definition;
        private BossContext _context;
        private BossEventHub _events;
        private BossReactionWindowService _reactionWindows;
        private bool _manualInvulnerable;

        public BossHealthService(BossDefinition definition = null)
        {
            _definition = definition;
        }

        public int TickOrder => -200;

        public bool IsInvulnerable
        {
            get => _manualInvulnerable || (_context != null && _context.Blackboard.InvulnerabilityRemaining > 0f);
            set
            {
                _manualInvulnerable = value;
                if (_context != null)
                {
                    _context.Blackboard.IsInvulnerable = IsInvulnerable;
                    if (!value)
                    {
                        _context.Blackboard.InvulnerabilityRemaining = 0f;
                    }
                }
            }
        }

        public bool IsDead => _context != null && _context.Blackboard.CurrentHealth <= 0f;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        public void Initialize(BossContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            context.Services.TryResolve(out _events);
            context.Services.TryResolve(out _reactionWindows);
        }

        public void Tick(float deltaTime)
        {
            if (_context == null)
            {
                return;
            }

            if (_context.Blackboard.InvulnerabilityRemaining > 0f)
            {
                _context.Blackboard.InvulnerabilityRemaining = Mathf.Max(0f, _context.Blackboard.InvulnerabilityRemaining - deltaTime);
            }

            _context.Blackboard.IsInvulnerable = IsInvulnerable;
        }

        public void GrantTemporaryInvulnerability(float duration)
        {
            if (_context == null || duration <= 0f)
            {
                return;
            }

            _context.Blackboard.InvulnerabilityRemaining = Mathf.Max(_context.Blackboard.InvulnerabilityRemaining, duration);
            _context.Blackboard.IsInvulnerable = true;
        }

        public void ClearInvulnerability()
        {
            _manualInvulnerable = false;
            if (_context != null)
            {
                _context.Blackboard.InvulnerabilityRemaining = 0f;
                _context.Blackboard.IsInvulnerable = false;
            }
        }

        public bool ApplyDamage(BossDamageRequest request)
        {
            if (_context == null || IsDead || request.Amount <= 0f)
            {
                return false;
            }

            if (IsInvulnerable && !request.IgnoreInvulnerability)
            {
                return false;
            }

            BossBlackboard blackboard = _context.Blackboard;
            float multiplier = blackboard.IsStaggered ? Mathf.Max(1f, blackboard.PunishDamageMultiplier) : 1f;
            float effectiveAmount = request.Amount * multiplier;

            float previous = blackboard.CurrentHealth;
            blackboard.CurrentHealth = Mathf.Max(0f, previous - effectiveAmount);
            float applied = previous - blackboard.CurrentHealth;
            blackboard.TotalDamageTaken += applied;

            HealthChanged?.Invoke(blackboard.CurrentHealth, blackboard.MaxHealth);
            _events?.Raise(new BossEvent(BossEventType.Damaged, request.SourceId, applied));

            if (blackboard.CurrentHealth <= 0f)
            {
                ClearInvulnerability();
                Died?.Invoke();
                _events?.Raise(new BossEvent(BossEventType.Died));
                return applied > 0f;
            }

            if (request.Stagger > 0f && !blackboard.IsStaggered)
            {
                blackboard.CurrentStaggerBuildup += request.Stagger;
                float threshold = _definition != null && _definition.StaggerThreshold > 0f ? _definition.StaggerThreshold : 100f;
                if (blackboard.CurrentStaggerBuildup >= threshold)
                {
                    blackboard.CurrentStaggerBuildup = 0f;
                    if (_reactionWindows == null)
                    {
                        _context.Services.TryResolve(out _reactionWindows);
                    }

                    float staggerDuration = _definition != null && _definition.BaseStaggerDuration > 0f
                        ? _definition.BaseStaggerDuration
                        : 2f;
                    _reactionWindows?.TriggerStagger(staggerDuration, 1.5f);
                }
            }

            return applied > 0f;
        }

        public void Heal(float amount)
        {
            if (_context == null || IsDead || amount <= 0f)
            {
                return;
            }

            BossBlackboard blackboard = _context.Blackboard;
            blackboard.CurrentHealth = Mathf.Min(blackboard.MaxHealth, blackboard.CurrentHealth + amount);
            HealthChanged?.Invoke(blackboard.CurrentHealth, blackboard.MaxHealth);
        }

        public void RestoreFullHealth()
        {
            if (_context == null)
            {
                return;
            }

            ClearInvulnerability();
            _context.Blackboard.CurrentHealth = _context.Blackboard.MaxHealth;
            _context.Blackboard.CurrentStaggerBuildup = 0f;
            HealthChanged?.Invoke(_context.Blackboard.CurrentHealth, _context.Blackboard.MaxHealth);
        }

        public void Shutdown()
        {
            ClearInvulnerability();
            HealthChanged = null;
            Died = null;
            _reactionWindows = null;
            _events = null;
            _context = null;
        }
    }
}
