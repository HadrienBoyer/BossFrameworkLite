using System;
using System.Collections.Generic;
using TappyTale.BossFight.Attacks;
using TappyTale.BossFight.Combat;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Phases;
using TappyTale.BossFight.Projectiles;
using UnityEngine;

namespace TappyTale.BossFight.Core
{
    [DisallowMultipleComponent]
    public sealed class BossDirector : MonoBehaviour, IDamageReceiver, IBossDamageReceiver
    {
        [SerializeField] private BossDefinition definition;
        [SerializeField] private BossPayloadLibrary payloadLibrary;
        [SerializeField] private Transform target;
        [SerializeField, Min(1f)] private float fallbackMaxHealth = 100f;
        [SerializeField] private bool startOnEnable = true;

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private bool _capturedInitialTransform;

        public BossRuntime Runtime { get; private set; }
        public BossBlackboard Blackboard => Runtime?.Context.Blackboard;
        public BossDefinition Definition => definition;
        public BossPayloadLibrary PayloadLibrary => payloadLibrary;
        public Transform Target => target;

        public DamageTeam Team => DamageTeam.Boss;
        public bool IsAlive => Runtime != null && !Runtime.Health.IsDead;

        public event Action RuntimeRebuilt;

        private void Awake()
        {
            CaptureInitialTransform();
            BuildRuntime();
        }

        private void OnEnable()
        {
            if (startOnEnable) StartFight();
        }

        private void Update() => Runtime?.Tick(Time.deltaTime);
        private void OnDisable() => Runtime?.Stop();

        private void OnDestroy()
        {
            Runtime?.Dispose();
            Runtime = null;
            RuntimeRebuilt = null;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (Runtime != null) Runtime.Context.TargetTransform = newTarget;
        }

        public void SetDefinition(BossDefinition newDefinition, bool rebuildRuntime = true)
        {
            definition = newDefinition;
            if (!rebuildRuntime) return;
            bool restart = Runtime != null && Runtime.IsRunning;
            BuildRuntime();
            if (restart) Runtime.Start();
        }

        public void SetPayloadLibrary(BossPayloadLibrary newLibrary, bool rebuildRuntime = true)
        {
            payloadLibrary = newLibrary;
            if (!rebuildRuntime) return;
            bool restart = Runtime != null && Runtime.IsRunning;
            BuildRuntime();
            if (restart) Runtime.Start();
        }

        public bool ApplyDamage(float amount, string sourceId = null, bool ignoreInvulnerability = false, float stagger = 0f)
        {
            return Runtime != null && Runtime.Health.ApplyDamage(new BossDamageRequest(amount, sourceId, ignoreInvulnerability, stagger));
        }

        public bool ReceiveDamage(DamageInfo damage)
        {
            if (damage.SourceTeam == DamageTeam.Boss)
            {
                return false;
            }

            if (damage.Source != null && damage.Source.transform.IsChildOf(transform))
            {
                return false;
            }

            return ApplyDamage(
                damage.Amount,
                !string.IsNullOrEmpty(damage.AttackId) ? damage.AttackId : (damage.Source != null ? damage.Source.name : null),
                damage.IgnoreInvulnerability,
                damage.Stagger);
        }

        public bool ReceiveDamage(float amount, GameObject source)
        {
            return ReceiveDamage(new DamageInfo(amount, source, DamageTeam.Neutral));
        }

        public bool TryParry() => Runtime != null && Runtime.ReactionWindows.TryParry();
        public bool TryPerfectDodge() => Runtime != null && Runtime.ReactionWindows.TryPerfectDodge();

        public void StartFight()
        {
            if (Runtime == null) BuildRuntime();
            Runtime.Start();
        }

        public void StopFight() => Runtime?.Stop();

        public void ResetFight(bool restoreTransform = true)
        {
            if (restoreTransform && _capturedInitialTransform)
            {
                CharacterController cc = GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                transform.SetPositionAndRotation(_initialPosition, _initialRotation);
                if (cc != null) cc.enabled = true;
            }

            if (Runtime == null)
            {
                BuildRuntime();
                Runtime.Start();
                return;
            }

            Runtime.ResetFight();
        }

        private void CaptureInitialTransform()
        {
            if (_capturedInitialTransform) return;
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            _capturedInitialTransform = true;
        }

        private void BuildRuntime()
        {
            Runtime?.Dispose();
            float maxHealth = definition != null ? definition.MaxHealth : fallbackMaxHealth;
            BossBlackboard blackboard = new();
            blackboard.ResetState(maxHealth);

            BossServiceRegistry services = new();
            BossContext context = new(gameObject, transform, target, blackboard, services);
            Runtime = new BossRuntime(context, definition, payloadLibrary);
            RuntimeRebuilt?.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            if (definition == null) return;

            Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, definition.PreferredCombatDistance);

            if (definition.Phases != null)
            {
                for (int p = 0; p < definition.Phases.Count; p++)
                {
                    BossPhaseDefinition phase = definition.Phases[p];
                    if (phase == null || phase.Attacks == null) continue;
                    for (int a = 0; a < phase.Attacks.Count; a++)
                    {
                        BossAttackDefinition atk = phase.Attacks[a];
                        if (atk == null) continue;
                        Gizmos.color = atk.Kind == BossAttackKind.Melee
                            ? new Color(1f, 0.25f, 0.25f, 0.25f)
                            : new Color(0.25f, 0.7f, 1f, 0.2f);
                        Gizmos.DrawWireSphere(transform.position, atk.MaximumRange);
                    }
                }
            }

            if (payloadLibrary != null && payloadLibrary.Patterns != null)
            {
                List<PatternShotSample> samples = new();
                for (int i = 0; i < payloadLibrary.Patterns.Count; i++)
                {
                    ProjectilePatternDefinition pattern = payloadLibrary.Patterns[i];
                    if (pattern == null) continue;
                    pattern.EvaluateSamples(transform.forward, samples);
                    Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.55f);
                    Vector3 origin = transform.position + transform.forward * 0.9f;
                    for (int s = 0; s < samples.Count; s++)
                    {
                        Vector3 end = origin + samples[s].Direction * (4f + samples[s].DelaySeconds * 2f);
                        Gizmos.DrawLine(origin, end);
                        Gizmos.DrawSphere(end, 0.08f);
                    }
                }
            }
        }
    }
}
