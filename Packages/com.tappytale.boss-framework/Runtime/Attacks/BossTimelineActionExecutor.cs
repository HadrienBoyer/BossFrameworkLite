using System;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Events;
using TappyTale.BossFight.Projectiles;
using UnityEngine;

namespace TappyTale.BossFight.Attacks
{
    public sealed class BossTimelineActionExecutor : IBossService
    {
        private BossContext _context;
        private BossTimelineRunner _runner;
        private BossPayloadResolver _resolver;
        private BossProjectilePool _projectiles;
        private BossEventHub _events;

        public event Action<AttackTimelineEntry> MeleeRequested;
        public event Action<AttackTimelineEntry> DashRequested;
        public event Action<AttackTimelineEntry> AudioRequested;
        public event Action<AttackTimelineEntry> VfxRequested;
        public event Action<AttackTimelineEntry> CameraShakeRequested;
        public event Action<AttackTimelineEntry> CustomRequested;

        public void Initialize(BossContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            context.Services.TryResolve(out _runner);
            context.Services.TryResolve(out _resolver);
            context.Services.TryResolve(out _projectiles);
            context.Services.TryResolve(out _events);
            if (_runner != null) _runner.EntryTriggered += Execute;
        }

        public void Shutdown()
        {
            if (_runner != null) _runner.EntryTriggered -= Execute;
            MeleeRequested = null;
            DashRequested = null;
            AudioRequested = null;
            VfxRequested = null;
            CameraShakeRequested = null;
            CustomRequested = null;
            _context = null;
            _runner = null;
            _resolver = null;
            _projectiles = null;
            _events = null;
        }

        private void Execute(AttackTimelineEntry entry)
        {
            switch (entry.Action)
            {
                case AttackTimelineAction.SpawnProjectile:
                    SpawnProjectileOrPattern(entry);
                    break;
                case AttackTimelineAction.SpawnPattern:
                    SpawnPattern(entry);
                    break;
                case AttackTimelineAction.MeleeHit:
                    MeleeRequested?.Invoke(entry);
                    break;
                case AttackTimelineAction.Dash:
                    DashRequested?.Invoke(entry);
                    break;
                case AttackTimelineAction.PlayAudio:
                    if (_resolver == null || _resolver.TryGetAudio(entry.PayloadId, out _) || !string.IsNullOrEmpty(entry.PayloadId))
                    {
                        AudioRequested?.Invoke(entry);
                    }
                    break;
                case AttackTimelineAction.PlayVfx:
                    if (_resolver == null || _resolver.TryGetVfx(entry.PayloadId, out _) || !string.IsNullOrEmpty(entry.PayloadId))
                    {
                        VfxRequested?.Invoke(entry);
                    }
                    break;
                case AttackTimelineAction.CameraShake:
                    CameraShakeRequested?.Invoke(entry);
                    _events?.Raise(new BossEvent(BossEventType.CameraShakeRequested, entry.PayloadId, entry.Value > 0f ? entry.Value : 1f));
                    break;
                case AttackTimelineAction.Custom:
                    CustomRequested?.Invoke(entry);
                    break;
            }
        }

        private void SpawnProjectileOrPattern(AttackTimelineEntry entry)
        {
            if (_resolver == null || _projectiles == null)
            {
                return;
            }

            if (_resolver.TryGetPattern(entry.PayloadId, out ProjectilePatternDefinition patternDef))
            {
                SpawnPatternInternal(patternDef);
                return;
            }

            if (!_resolver.TryGetProjectile(entry.PayloadId, out ProjectileDefinition definition))
            {
                return;
            }

            Vector3 direction = ResolveAimDirection();
            Vector3 spawnPos = ResolveMuzzlePosition(direction);
            _projectiles.Spawn(definition, spawnPos, direction);
            _events?.Raise(new BossEvent(BossEventType.TimelineAction, definition.Id, entry.Value, (int)entry.Action));
        }

        private void SpawnPattern(AttackTimelineEntry entry)
        {
            if (_resolver == null || _projectiles == null)
            {
                return;
            }

            if (_resolver.TryGetPattern(entry.PayloadId, out ProjectilePatternDefinition patternDef))
            {
                SpawnPatternInternal(patternDef);
            }
            else if (_resolver.TryGetProjectile(entry.PayloadId, out ProjectileDefinition projectileDef))
            {
                Vector3 direction = ResolveAimDirection();
                Vector3 spawnPos = ResolveMuzzlePosition(direction);
                _projectiles.Spawn(projectileDef, spawnPos, direction);
            }
        }

        private void SpawnPatternInternal(ProjectilePatternDefinition pattern)
        {
            if (pattern == null || pattern.Projectile == null)
            {
                return;
            }

            Vector3 forward = ResolveAimDirection();
            Vector3 spawnPos = ResolveMuzzlePosition(forward);
            _projectiles.SpawnPattern(pattern, spawnPos, forward);
            _events?.Raise(new BossEvent(BossEventType.PatternSpawned, pattern.Id, pattern.Count));
        }

        private Vector3 ResolveAimDirection()
        {
            Vector3 direction = _context.Blackboard.DirectionToTarget;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = _context.BossTransform.forward;
                direction.y = 0f;
            }

            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        private Vector3 ResolveMuzzlePosition(Vector3 direction)
        {
            return _context.BossTransform.position + direction * 0.9f;
        }
    }
}