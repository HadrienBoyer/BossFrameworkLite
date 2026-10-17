using System;
using TappyTale.BossFight.Attacks;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Events;
using UnityEngine;

namespace TappyTale.BossFight.Telegraphs
{
    public sealed class BossTelegraphService : IBossService, IBossTickable
    {
        private BossEventHub _events;
        private BossTimelineRunner _runner;
        private BossPayloadResolver _resolver;
        private TelegraphDefinition _activeDefinition;
        private float _totalDuration;
        private float _remaining;
        private string _activeId = string.Empty;

        public int TickOrder => 40;
        public bool IsActive => _remaining > 0f;
        public string ActiveId => _activeId;
        public TelegraphDefinition ActiveDefinition => _activeDefinition;
        public float Remaining => _remaining;
        public float NormalizedProgress => _totalDuration > 0.0001f
            ? Mathf.Clamp01(1f - (_remaining / _totalDuration))
            : 0f;

        public event Action<string, float> Started;
        public event Action<string> Ended;

        public void Initialize(BossContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.Services.TryResolve(out _events);
            context.Services.TryResolve(out _runner);
            context.Services.TryResolve(out _resolver);
            if (_runner != null)
            {
                _runner.EntryTriggered += OnTimelineEntry;
                _runner.Completed += OnAttackCompleted;
                _runner.Cancelled += OnAttackCancelled;
            }
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive)
            {
                return;
            }

            _remaining -= deltaTime;
            if (_remaining <= 0f)
            {
                StopTelegraph();
            }
        }

        public void ForceStop()
        {
            StopTelegraph();
        }

        public void Shutdown()
        {
            if (_runner != null)
            {
                _runner.EntryTriggered -= OnTimelineEntry;
                _runner.Completed -= OnAttackCompleted;
                _runner.Cancelled -= OnAttackCancelled;
            }

            StopTelegraph();
            Started = null;
            Ended = null;
            _resolver = null;
            _runner = null;
            _events = null;
        }

        private void OnTimelineEntry(AttackTimelineEntry entry)
        {
            if (entry.Action != AttackTimelineAction.Telegraph)
            {
                return;
            }

            StopTelegraph();
            _activeId = string.IsNullOrWhiteSpace(entry.PayloadId) ? "default_telegraph" : entry.PayloadId;
            _activeDefinition = null;
            _resolver?.TryGetTelegraph(_activeId, out _activeDefinition);

            float requestedDuration = entry.EffectiveDuration > 0f
                ? entry.EffectiveDuration
                : (_activeDefinition != null ? _activeDefinition.Duration : 0.5f);
            _totalDuration = Mathf.Max(0.01f, requestedDuration);
            _remaining = _totalDuration;
            Started?.Invoke(_activeId, _remaining);
            _events?.Raise(new BossEvent(BossEventType.TelegraphStarted, _activeId, _remaining));
        }

        private void OnAttackCompleted(BossAttackDefinition attack) => StopTelegraph();
        private void OnAttackCancelled(BossAttackDefinition attack) => StopTelegraph();

        private void StopTelegraph()
        {
            if (string.IsNullOrEmpty(_activeId))
            {
                _remaining = 0f;
                _totalDuration = 0f;
                _activeDefinition = null;
                return;
            }

            string endedId = _activeId;
            _activeId = string.Empty;
            _activeDefinition = null;
            _remaining = 0f;
            _totalDuration = 0f;
            Ended?.Invoke(endedId);
            _events?.Raise(new BossEvent(BossEventType.TelegraphEnded, endedId));
        }
    }
}
