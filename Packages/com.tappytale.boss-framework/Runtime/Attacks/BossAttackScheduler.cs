using System;
using System.Collections.Generic;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Phases;
using UnityEngine;

namespace TappyTale.BossFight.Attacks
{
    public sealed class BossAttackScheduler : IBossService, IBossTickable
    {
        private readonly BossDefinition _bossDefinition;
        private readonly List<BossAttackRuntime> _attacks = new();
        private readonly List<BossAttackRuntime> _candidates = new();
        private readonly Dictionary<string, float> _preservedCooldowns = new(StringComparer.Ordinal);
        private BossContext _context;
        private BossPhaseController _phases;
        private BossTimelineRunner _runner;
        private int _roundRobinIndex;

        public BossAttackScheduler(BossDefinition bossDefinition = null)
        {
            _bossDefinition = bossDefinition;
        }

        public int TickOrder => 0;
        public bool Enabled { get; set; } = true;
        public IReadOnlyList<BossAttackRuntime> Attacks => _attacks;

        public void Initialize(BossContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            context.Services.TryResolve(out _phases);
            context.Services.TryResolve(out _runner);
            if (_phases != null)
            {
                _phases.PhaseStarted += OnPhaseStarted;
                if (_phases.CurrentPhase != null)
                {
                    Rebuild(_phases.CurrentPhase.Definition.Attacks, resetCooldowns: true);
                }
            }
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _attacks.Count; i++)
            {
                _attacks[i].Tick(deltaTime);
            }

            if (_context == null || _context.Blackboard.CurrentHealth <= 0f || _context.Blackboard.IsStaggered)
            {
                return;
            }

            if (!Enabled || _runner == null)
            {
                return;
            }

            if (_runner.IsRunning)
            {
                FaceTarget(deltaTime * 0.5f);
                return;
            }

            UpdatePositioningAndFacing(deltaTime);

            if (_attacks.Count == 0)
            {
                return;
            }

            BossAttackRuntime selected = SelectNext();
            if (selected != null && _runner.Play(selected.Definition))
            {
                float cooldownMult = _phases?.CurrentPhase?.Definition != null
                    ? _phases.CurrentPhase.Definition.CooldownMultiplier
                    : 1f;
                selected.CommitCooldown(cooldownMult);
            }
        }

        public void ResetAllCooldowns()
        {
            _preservedCooldowns.Clear();
            for (int i = 0; i < _attacks.Count; i++)
            {
                _attacks[i].ResetCooldown();
            }
        }

        public void Shutdown()
        {
            if (_phases != null)
            {
                _phases.PhaseStarted -= OnPhaseStarted;
            }

            _attacks.Clear();
            _candidates.Clear();
            _preservedCooldowns.Clear();
            _phases = null;
            _runner = null;
            _context = null;
        }

        private void UpdatePositioningAndFacing(float deltaTime)
        {
            if (_context == null || !_context.HasTarget || _context.BossTransform == null)
            {
                return;
            }

            FaceTarget(deltaTime);

            if (_bossDefinition == null || _bossDefinition.MoveSpeed <= 0f)
            {
                return;
            }

            float distance = _context.Blackboard.DistanceToTarget;
            float preferred = Mathf.Max(1.2f, _bossDefinition.PreferredCombatDistance);
            if (distance <= preferred + 0.25f)
            {
                return;
            }

            Vector3 dir = _context.Blackboard.DirectionToTarget;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                return;
            }

            dir.Normalize();
            float phaseMoveMult = _phases?.CurrentPhase?.Definition != null
                ? _phases.CurrentPhase.Definition.MoveSpeedMultiplier
                : 1f;
            float step = Mathf.Min(distance - preferred, _bossDefinition.MoveSpeed * phaseMoveMult * deltaTime);
            if (step > 0f)
            {
                _context.BossTransform.position += dir * step;
            }
        }

        private void FaceTarget(float deltaTime)
        {
            if (_context == null || !_context.HasTarget || _context.BossTransform == null)
            {
                return;
            }

            Vector3 dir = _context.Blackboard.DirectionToTarget;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float rotSpeed = _bossDefinition != null && _bossDefinition.RotationSpeed > 0f
                ? _bossDefinition.RotationSpeed
                : 10f;
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            float blend = 1f - Mathf.Exp(-rotSpeed * deltaTime);
            _context.BossTransform.rotation = Quaternion.Slerp(_context.BossTransform.rotation, targetRot, blend);
        }

        private BossAttackRuntime SelectNext()
        {
            float distance = _context.Blackboard.DistanceToTarget;
            _candidates.Clear();
            int highestPriority = int.MinValue;

            for (int offset = 0; offset < _attacks.Count; offset++)
            {
                int index = (_roundRobinIndex + offset) % _attacks.Count;
                BossAttackRuntime attack = _attacks[index];
                if (!attack.IsReady || !attack.Definition.IsInRange(distance) || attack.Definition.Timeline == null)
                {
                    continue;
                }

                int priority = attack.Definition.Priority;
                if (priority > highestPriority)
                {
                    highestPriority = priority;
                    _candidates.Clear();
                    _candidates.Add(attack);
                }
                else if (priority == highestPriority)
                {
                    _candidates.Add(attack);
                }
            }

            if (_candidates.Count == 0)
            {
                return null;
            }

            BossAttackRuntime chosen = _candidates[0];
            if (_candidates.Count > 1)
            {
                for (int i = 0; i < _candidates.Count; i++)
                {
                    if (_candidates[i].Definition.Id != _context.Blackboard.LastAttackId)
                    {
                        chosen = _candidates[i];
                        break;
                    }
                }
            }

            int chosenIndex = _attacks.IndexOf(chosen);
            if (chosenIndex >= 0)
            {
                _roundRobinIndex = (chosenIndex + 1) % _attacks.Count;
            }

            return chosen;
        }

        private void OnPhaseStarted(BossPhaseRuntime phase)
        {
            Rebuild(phase.Definition.Attacks, phase.Definition.ResetCooldownsOnEnter);
        }

        private void Rebuild(IReadOnlyList<BossAttackDefinition> definitions, bool resetCooldowns)
        {
            _preservedCooldowns.Clear();
            if (!resetCooldowns)
            {
                for (int i = 0; i < _attacks.Count; i++)
                {
                    BossAttackRuntime existing = _attacks[i];
                    if (existing.Definition != null && !string.IsNullOrWhiteSpace(existing.Definition.Id))
                    {
                        _preservedCooldowns[existing.Definition.Id] = existing.CooldownRemaining;
                    }
                }
            }

            _attacks.Clear();
            _roundRobinIndex = 0;
            if (definitions == null)
            {
                return;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                BossAttackDefinition def = definitions[i];
                if (def == null)
                {
                    continue;
                }

                BossAttackRuntime runtime = new(def);
                if (!resetCooldowns && !string.IsNullOrWhiteSpace(def.Id) &&
                    _preservedCooldowns.TryGetValue(def.Id, out float remaining))
                {
                    runtime.SetCooldownRemaining(remaining);
                }

                _attacks.Add(runtime);
            }

            _attacks.Sort(static (left, right) => right.Definition.Priority.CompareTo(left.Definition.Priority));
        }
    }
}
