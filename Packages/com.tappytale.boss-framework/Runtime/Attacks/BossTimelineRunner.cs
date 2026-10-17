using System;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Events;

namespace TappyTale.BossFight.Attacks
{
    public sealed class BossTimelineRunner : IBossService, IBossTickable
    {
        private BossContext _context;
        private BossEventHub _events;
        private AttackTimelineDefinition _timeline;
        private BossAttackDefinition _attack;
        private float _elapsed;
        private int _nextEntryIndex;

        public int TickOrder => 100;
        public bool IsRunning => _timeline != null;
        public BossAttackDefinition CurrentAttack => _attack;
        public float Elapsed => _elapsed;
        public float NormalizedProgress => _timeline != null && _timeline.Duration > 0f
            ? Math.Clamp(_elapsed / _timeline.Duration, 0f, 1f)
            : 0f;

        public event Action<BossAttackDefinition> Started;
        public event Action<BossAttackDefinition> Completed;
        public event Action<BossAttackDefinition> Cancelled;
        public event Action<AttackTimelineEntry> EntryTriggered;

        public void Initialize(BossContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            context.Services.TryResolve(out _events);
        }

        public bool Play(BossAttackDefinition attack)
        {
            if (attack == null || attack.Timeline == null || IsRunning)
            {
                return false;
            }

            _attack = attack;
            _timeline = attack.Timeline;
            _elapsed = 0f;
            _nextEntryIndex = 0;
            _context.Blackboard.LastAttackId = _context.Blackboard.CurrentAttackId;
            _context.Blackboard.CurrentAttackId = attack.Id;
            Started?.Invoke(attack);
            _events?.Raise(new BossEvent(BossEventType.AttackStarted, attack.Id));
            TriggerDueEntries();
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }

            _elapsed += deltaTime;
            TriggerDueEntries();
            if (_timeline != null && _elapsed >= _timeline.Duration)
            {
                Finish(wasCancelled: false);
            }
        }

        public void Cancel()
        {
            if (!IsRunning)
            {
                return;
            }

            Finish(wasCancelled: true);
        }

        public void Shutdown()
        {
            Cancel();
            Started = null;
            Completed = null;
            Cancelled = null;
            EntryTriggered = null;
            _events = null;
            _context = null;
        }

        private void TriggerDueEntries()
        {
            while (_timeline != null && _nextEntryIndex < _timeline.Entries.Count)
            {
                AttackTimelineEntry entry = _timeline.Entries[_nextEntryIndex];
                if (entry.Time > _elapsed)
                {
                    break;
                }

                _nextEntryIndex++;
                EntryTriggered?.Invoke(entry);
                _events?.Raise(new BossEvent(BossEventType.TimelineAction, entry.PayloadId, entry.Value, (int)entry.Action));
            }
        }

        private void Finish(bool wasCancelled)
        {
            BossAttackDefinition finishedAttack = _attack;
            _timeline = null;
            _attack = null;
            _elapsed = 0f;
            _nextEntryIndex = 0;
            if (_context != null)
            {
                _context.Blackboard.CurrentAttackId = string.Empty;
            }

            if (finishedAttack != null)
            {
                if (wasCancelled)
                {
                    Cancelled?.Invoke(finishedAttack);
                }
                Completed?.Invoke(finishedAttack);
                _events?.Raise(new BossEvent(BossEventType.AttackEnded, finishedAttack.Id, wasCancelled ? 1f : 0f));
            }
        }
    }
}
