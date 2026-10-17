using System;
using System.Collections.Generic;
using TappyTale.BossFight.Combat;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Events;
using UnityEngine;

namespace TappyTale.BossFight.Projectiles
{
    public sealed class BossProjectilePool : IBossService, IBossTickable
    {
        private struct ScheduledShot
        {
            public ProjectileDefinition Definition;
            public Vector3 Direction;
            public float RemainingDelay;
            public float SpeedMultiplier;
        }

        private readonly Dictionary<ProjectileDefinition, Queue<BossProjectile>> _available = new();
        private readonly HashSet<BossProjectile> _active = new();
        private readonly List<ScheduledShot> _scheduledShots = new();
        private readonly List<PatternShotSample> _sampleBuffer = new();
        private readonly List<BossProjectile> _despawnScratch = new();
        private BossContext _context;
        private BossEventHub _events;
        private Transform _root;

        public int TickOrder => 120;
        public int ActiveCount => _active.Count;
        public IReadOnlyCollection<BossProjectile> ActiveProjectiles => _active;

        public void Initialize(BossContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            context.Services.TryResolve(out _events);
            GameObject rootObject = new("Boss Projectile Pool");
            _root = rootObject.transform;
        }

        public void Tick(float deltaTime)
        {
            if (_scheduledShots.Count == 0 || _context == null)
            {
                return;
            }

            if (!_context.Blackboard.IsFightActive || _context.Blackboard.CurrentHealth <= 0f || _context.Blackboard.IsStaggered)
            {
                _scheduledShots.Clear();
                return;
            }

            for (int i = _scheduledShots.Count - 1; i >= 0; i--)
            {
                ScheduledShot shot = _scheduledShots[i];
                shot.RemainingDelay -= deltaTime;
                if (shot.RemainingDelay <= 0f)
                {
                    _scheduledShots.RemoveAt(i);
                    Vector3 origin = _context.BossTransform != null
                        ? _context.BossTransform.position + shot.Direction * 0.9f
                        : Vector3.zero;
                    Spawn(shot.Definition, origin, shot.Direction, shot.SpeedMultiplier);
                }
                else
                {
                    _scheduledShots[i] = shot;
                }
            }
        }

        public BossProjectile Spawn(ProjectileDefinition definition, Vector3 position, Vector3 direction, float speedMultiplier = 1f)
        {
            if (definition == null)
            {
                return null;
            }

            if (!_available.TryGetValue(definition, out Queue<BossProjectile> queue))
            {
                queue = new Queue<BossProjectile>();
                _available.Add(definition, queue);
            }

            BossProjectile projectile = null;
            while (queue.Count > 0 && projectile == null)
            {
                projectile = queue.Dequeue();
            }

            if (projectile == null)
            {
                projectile = CreateProjectile(definition);
            }

            if (projectile == null)
            {
                return null;
            }

            _active.Add(projectile);
            projectile.Launch(
                this,
                definition,
                position,
                direction,
                _context?.BossTransform,
                _context?.TargetTransform,
                speedMultiplier,
                DamageTeam.Boss);
            _events?.Raise(new BossEvent(BossEventType.ProjectileSpawned, definition.Id));
            return projectile;
        }

        public int SpawnPattern(ProjectilePatternDefinition pattern, Vector3 origin, Vector3 forward)
        {
            if (pattern == null || pattern.Projectile == null)
            {
                return 0;
            }

            pattern.EvaluateSamples(forward, _sampleBuffer);
            int immediateCount = 0;

            for (int i = 0; i < _sampleBuffer.Count; i++)
            {
                PatternShotSample sample = _sampleBuffer[i];
                if (sample.DelaySeconds <= 0.001f)
                {
                    Vector3 pos = _context?.BossTransform != null
                        ? _context.BossTransform.position + sample.Direction * 0.9f
                        : origin;
                    if (Spawn(pattern.Projectile, pos, sample.Direction, sample.SpeedMultiplier) != null)
                    {
                        immediateCount++;
                    }
                }
                else
                {
                    _scheduledShots.Add(new ScheduledShot
                    {
                        Definition = pattern.Projectile,
                        Direction = sample.Direction,
                        RemainingDelay = sample.DelaySeconds,
                        SpeedMultiplier = sample.SpeedMultiplier
                    });
                }
            }

            return immediateCount;
        }

        public void Despawn(BossProjectile projectile)
        {
            if (projectile == null || !_active.Remove(projectile))
            {
                return;
            }

            ProjectileDefinition definition = projectile.Definition;
            projectile.ResetForPool();
            if (definition != null)
            {
                if (!_available.TryGetValue(definition, out Queue<BossProjectile> queue))
                {
                    queue = new Queue<BossProjectile>();
                    _available.Add(definition, queue);
                }

                queue.Enqueue(projectile);
                _events?.Raise(new BossEvent(BossEventType.ProjectileDespawned, definition.Id));
            }
        }

        public void DespawnAllActive()
        {
            _scheduledShots.Clear();
            if (_active.Count == 0)
            {
                return;
            }

            _despawnScratch.Clear();
            foreach (BossProjectile projectile in _active)
            {
                _despawnScratch.Add(projectile);
            }

            for (int i = 0; i < _despawnScratch.Count; i++)
            {
                Despawn(_despawnScratch[i]);
            }
            _despawnScratch.Clear();
        }

        public void Shutdown()
        {
            _scheduledShots.Clear();
            _sampleBuffer.Clear();
            _despawnScratch.Clear();

            foreach (BossProjectile projectile in _active)
            {
                if (projectile != null)
                {
                    projectile.ResetForPool();
                    DestroySafe(projectile.gameObject);
                }
            }

            foreach (Queue<BossProjectile> queue in _available.Values)
            {
                while (queue.Count > 0)
                {
                    BossProjectile projectile = queue.Dequeue();
                    if (projectile != null)
                    {
                        DestroySafe(projectile.gameObject);
                    }
                }
            }

            _active.Clear();
            _available.Clear();
            if (_root != null)
            {
                DestroySafe(_root.gameObject);
            }

            _events = null;
            _context = null;
            _root = null;
        }

        private BossProjectile CreateProjectile(ProjectileDefinition definition)
        {
            GameObject instance;
            if (definition.Prefab != null)
            {
                instance = UnityEngine.Object.Instantiate(definition.Prefab, _root);
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                instance.name = $"FallbackProjectile_{definition.Id}";
                if (_root != null)
                {
                    instance.transform.SetParent(_root, false);
                }
                SphereCollider col = instance.GetComponent<SphereCollider>();
                if (col != null) col.isTrigger = true;
                Rigidbody rb = instance.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                instance.AddComponent<BossProjectile>();
                instance.AddComponent<BossProjectileImpact>();
            }

            BossProjectile projectile = instance.GetComponent<BossProjectile>();
            if (projectile == null)
            {
                projectile = instance.AddComponent<BossProjectile>();
            }

            if (instance.GetComponent<BossProjectileImpact>() == null)
            {
                instance.AddComponent<BossProjectileImpact>();
            }

            instance.SetActive(false);
            return projectile;
        }

        private static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
}
