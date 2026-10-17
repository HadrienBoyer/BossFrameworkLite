using System;
using TappyTale.BossFight.Attacks;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Events;
using UnityEngine;

namespace TappyTale.BossFight.Combat
{
    public sealed class BossReactionWindowService : IBossService, IBossTickable
    {
        private readonly BossDefinition _bossDefinition;
        private BossContext _context;
        private BossEventHub _events;
        private BossTimelineRunner _runner;
        private BossHealthService _health;
        private float _parryRemaining;
        private float _dodgeRemaining;
        private float _staggerRemaining;

        public BossReactionWindowService(BossDefinition bossDefinition = null)
        {
            _bossDefinition = bossDefinition;
        }

        public int TickOrder => 50;
        public bool IsParryWindowOpen => _context != null && _context.Blackboard.IsParryWindowOpen;
        public bool IsPerfectDodgeWindowOpen => _context != null && _context.Blackboard.IsPerfectDodgeWindowOpen;
        public bool IsStaggered => _context != null && _context.Blackboard.IsStaggered;
        public float ParryRemaining => _parryRemaining;
        public float DodgeRemaining => _dodgeRemaining;
        public float StaggerRemaining => _staggerRemaining;

        public event Action<float> ParryWindowOpened;
        public event Action ParryWindowClosed;
        public event Action<float> PerfectDodgeWindowOpened;
        public event Action PerfectDodgeWindowClosed;
        public event Action PlayerParried;
        public event Action PlayerPerfectDodged;
        public event Action<float> StaggerStarted;
        public event Action StaggerEnded;

        public void Initialize(BossContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            context.Services.TryResolve(out _events);
            context.Services.TryResolve(out _runner);
            context.Services.TryResolve(out _health);
            if (_runner != null)
            {
                _runner.EntryTriggered += OnTimelineEntry;
                _runner.Completed += OnAttackCompleted;
                _runner.Cancelled += OnAttackCancelled;
            }
        }

        public void Tick(float deltaTime)
        {
            TickWindow(ref _parryRemaining, deltaTime, true);
            TickWindow(ref _dodgeRemaining, deltaTime, false);
            TickStagger(deltaTime);
        }

        public bool TryParry()
        {
            if (_context == null || !_context.Blackboard.IsParryWindowOpen)
            {
                return false;
            }

            BossAttackDefinition currentAttack = _runner?.CurrentAttack;
            CloseParryWindow();
            ClosePerfectDodgeWindow();
            _context.Blackboard.ParriesReceived++;

            float staggerDuration = currentAttack != null && currentAttack.StaggerDurationOnParry > 0f
                ? currentAttack.StaggerDurationOnParry
                : (_bossDefinition != null && _bossDefinition.BaseStaggerDuration > 0f ? _bossDefinition.BaseStaggerDuration : 2.0f);
            float punishMultiplier = currentAttack != null && currentAttack.PunishDamageMultiplier > 1f
                ? currentAttack.PunishDamageMultiplier
                : 1.5f;

            if (currentAttack == null || currentAttack.InterruptOnParry)
            {
                _runner?.Cancel();
            }

            TriggerStagger(staggerDuration, punishMultiplier);
            PlayerParried?.Invoke();
            _events?.Raise(new BossEvent(BossEventType.PlayerParried, currentAttack != null ? currentAttack.Id : string.Empty, staggerDuration));
            return true;
        }

        public bool TryPerfectDodge()
        {
            if (_context == null || !_context.Blackboard.IsPerfectDodgeWindowOpen)
            {
                return false;
            }

            BossAttackDefinition currentAttack = _runner?.CurrentAttack;
            ClosePerfectDodgeWindow();
            _context.Blackboard.PerfectDodgesReceived++;

            PlayerPerfectDodged?.Invoke();
            _events?.Raise(new BossEvent(BossEventType.PlayerPerfectDodged, currentAttack != null ? currentAttack.Id : string.Empty));
            return true;
        }

        public void TriggerStagger(float duration, float punishDamageMultiplier = 1.5f)
        {
            if (_context == null || duration <= 0f)
            {
                return;
            }

            CloseAllReactionWindows();
            _runner?.Cancel();

            _staggerRemaining = Mathf.Max(_staggerRemaining, duration);
            _context.Blackboard.IsStaggered = true;
            _context.Blackboard.StaggerRemaining = _staggerRemaining;
            _context.Blackboard.PunishDamageMultiplier = Mathf.Max(1f, punishDamageMultiplier);

            StaggerStarted?.Invoke(_staggerRemaining);
            _events?.Raise(new BossEvent(BossEventType.BossStaggered, value: _staggerRemaining));
        }

        public void ClearStagger()
        {
            _staggerRemaining = 0f;
            if (_context != null)
            {
                bool wasStaggered = _context.Blackboard.IsStaggered;
                _context.Blackboard.IsStaggered = false;
                _context.Blackboard.StaggerRemaining = 0f;
                _context.Blackboard.PunishDamageMultiplier = 1f;
                if (wasStaggered)
                {
                    StaggerEnded?.Invoke();
                    _events?.Raise(new BossEvent(BossEventType.BossStaggerEnded));
                }
            }
        }

        public void CloseAll()
        {
            CloseAllReactionWindows();
            ClearStagger();
        }

        public void Shutdown()
        {
            if (_runner != null)
            {
                _runner.EntryTriggered -= OnTimelineEntry;
                _runner.Completed -= OnAttackCompleted;
                _runner.Cancelled -= OnAttackCancelled;
            }

            CloseAll();
            ParryWindowOpened = null;
            ParryWindowClosed = null;
            PerfectDodgeWindowOpened = null;
            PerfectDodgeWindowClosed = null;
            PlayerParried = null;
            PlayerPerfectDodged = null;
            StaggerStarted = null;
            StaggerEnded = null;
            _health = null;
            _runner = null;
            _events = null;
            _context = null;
        }

        private void OnTimelineEntry(AttackTimelineEntry entry)
        {
            switch (entry.Action)
            {
                case AttackTimelineAction.OpenParryWindow:
                    OpenParryWindow(entry.EffectiveDuration);
                    break;
                case AttackTimelineAction.OpenPerfectDodgeWindow:
                    OpenPerfectDodgeWindow(entry.EffectiveDuration);
                    break;
                case AttackTimelineAction.InvulnerabilityWindow:
                    _health?.GrantTemporaryInvulnerability(entry.EffectiveDuration);
                    break;
            }
        }

        private void OnAttackCompleted(BossAttackDefinition attack) => CloseAllReactionWindows();
        private void OnAttackCancelled(BossAttackDefinition attack) => CloseAllReactionWindows();

        public void OpenParryWindow(float duration)
        {
            _parryRemaining = Math.Max(0.01f, duration);
            _context.Blackboard.IsParryWindowOpen = true;
            ParryWindowOpened?.Invoke(_parryRemaining);
            _events?.Raise(new BossEvent(BossEventType.ParryWindowOpened, value: _parryRemaining));
        }

        public void OpenPerfectDodgeWindow(float duration)
        {
            _dodgeRemaining = Math.Max(0.01f, duration);
            _context.Blackboard.IsPerfectDodgeWindowOpen = true;
            PerfectDodgeWindowOpened?.Invoke(_dodgeRemaining);
            _events?.Raise(new BossEvent(BossEventType.PerfectDodgeWindowOpened, value: _dodgeRemaining));
        }

        private void TickWindow(ref float remaining, float deltaTime, bool parry)
        {
            if (remaining <= 0f)
            {
                return;
            }

            remaining -= deltaTime;
            if (remaining <= 0f)
            {
                if (parry)
                {
                    CloseParryWindow();
                }
                else
                {
                    ClosePerfectDodgeWindow();
                }
            }
        }

        private void TickStagger(float deltaTime)
        {
            if (_staggerRemaining <= 0f)
            {
                return;
            }

            _staggerRemaining = Mathf.Max(0f, _staggerRemaining - deltaTime);
            if (_context != null)
            {
                _context.Blackboard.StaggerRemaining = _staggerRemaining;
            }

            if (_staggerRemaining <= 0f)
            {
                ClearStagger();
            }
        }

        private void CloseParryWindow()
        {
            _parryRemaining = 0f;
            if (_context != null && _context.Blackboard.IsParryWindowOpen)
            {
                _context.Blackboard.IsParryWindowOpen = false;
                ParryWindowClosed?.Invoke();
                _events?.Raise(new BossEvent(BossEventType.ParryWindowClosed));
            }
        }

        private void ClosePerfectDodgeWindow()
        {
            _dodgeRemaining = 0f;
            if (_context != null && _context.Blackboard.IsPerfectDodgeWindowOpen)
            {
                _context.Blackboard.IsPerfectDodgeWindowOpen = false;
                PerfectDodgeWindowClosed?.Invoke();
                _events?.Raise(new BossEvent(BossEventType.PerfectDodgeWindowClosed));
            }
        }

        private void CloseAllReactionWindows()
        {
            CloseParryWindow();
            ClosePerfectDodgeWindow();
        }
    }
}
