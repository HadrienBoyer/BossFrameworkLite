using System;
using TappyTale.BossFight.Attacks;
using TappyTale.BossFight.Combat;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Events;

namespace TappyTale.BossFight.Phases
{
    public sealed class BossPhaseController : IBossService, IBossTickable
    {
        private readonly BossDefinition _definition;
        private BossContext _context;
        private BossEventHub _events;
        private BossHealthService _health;
        private BossTimelineRunner _runner;
        private float _lastObservedNormalizedHealth = 1f;

        public BossPhaseController(BossDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public int TickOrder => -100;
        public BossPhaseRuntime CurrentPhase { get; private set; }

        public event Action<BossPhaseRuntime> PhaseStarted;
        public event Action<BossPhaseRuntime> PhaseEnded;

        public void Initialize(BossContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            context.Services.TryResolve(out _events);
            context.Services.TryResolve(out _health);
            context.Services.TryResolve(out _runner);
        }

        public void Start()
        {
            _lastObservedNormalizedHealth = _context != null ? _context.Blackboard.NormalizedHealth : 1f;
            if (_definition.Phases.Count > 0)
            {
                int initialIndex = ResolvePhaseIndex(_lastObservedNormalizedHealth);
                EnterPhase(initialIndex, isInitialPhase: true);
            }
        }

        public void Tick(float deltaTime)
        {
            if (_context == null || CurrentPhase == null || _context.Blackboard.CurrentHealth <= 0f)
            {
                return;
            }

            CurrentPhase.Tick(deltaTime);
            float normalizedHealth = _context.Blackboard.NormalizedHealth;
            bool healthRestored = normalizedHealth > _lastObservedNormalizedHealth + 0.01f;
            _lastObservedNormalizedHealth = normalizedHealth;

            int desiredIndex = ResolvePhaseIndex(normalizedHealth);
            if (desiredIndex != CurrentPhase.Index && (desiredIndex > CurrentPhase.Index || healthRestored))
            {
                EnterPhase(desiredIndex, isInitialPhase: false);
            }
        }

        public void Stop()
        {
            ExitCurrentPhase();
        }

        public void Shutdown()
        {
            Stop();
            PhaseStarted = null;
            PhaseEnded = null;
            _runner = null;
            _health = null;
            _events = null;
            _context = null;
        }

        public int ResolvePhaseIndex(float normalizedHealth)
        {
            int bestIndex = 0;
            float tightestThreshold = float.MaxValue;

            for (int i = 0; i < _definition.Phases.Count; i++)
            {
                BossPhaseDefinition phase = _definition.Phases[i];
                if (phase == null)
                {
                    continue;
                }

                if (normalizedHealth <= phase.EnterAtNormalizedHealth + 0.0001f)
                {
                    if (phase.EnterAtNormalizedHealth <= tightestThreshold)
                    {
                        tightestThreshold = phase.EnterAtNormalizedHealth;
                        bestIndex = i;
                    }
                }
            }

            return bestIndex;
        }

        private void EnterPhase(int index, bool isInitialPhase)
        {
            if (index < 0 || index >= _definition.Phases.Count || _definition.Phases[index] == null)
            {
                return;
            }

            if (!isInitialPhase)
            {
                _runner?.Cancel();
            }

            ExitCurrentPhase();
            BossPhaseDefinition phaseDef = _definition.Phases[index];
            CurrentPhase = new BossPhaseRuntime(phaseDef, index);
            CurrentPhase.Enter();
            _context.Blackboard.CurrentPhaseIndex = index;

            if (!isInitialPhase && phaseDef.InvulnerabilityOnEnter > 0f)
            {
                _health?.GrantTemporaryInvulnerability(phaseDef.InvulnerabilityOnEnter);
            }

            PhaseStarted?.Invoke(CurrentPhase);
            _events?.Raise(new BossEvent(BossEventType.PhaseStarted, phaseDef.Id, phaseDef.EnterAtNormalizedHealth, index));

            if (!isInitialPhase && !string.IsNullOrWhiteSpace(phaseDef.EntryCameraShakeId))
            {
                _events?.Raise(new BossEvent(BossEventType.CameraShakeRequested, phaseDef.EntryCameraShakeId, 1f, index));
            }
        }

        private void ExitCurrentPhase()
        {
            if (CurrentPhase == null)
            {
                return;
            }

            BossPhaseRuntime previous = CurrentPhase;
            previous.Exit();
            PhaseEnded?.Invoke(previous);
            _events?.Raise(new BossEvent(BossEventType.PhaseEnded, previous.Definition.Id, index: previous.Index));
            CurrentPhase = null;
        }
    }
}
