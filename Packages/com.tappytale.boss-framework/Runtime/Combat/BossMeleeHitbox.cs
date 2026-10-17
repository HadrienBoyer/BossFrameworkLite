using System.Collections.Generic;
using TappyTale.BossFight.Attacks;
using TappyTale.BossFight.Core;
using UnityEngine;

namespace TappyTale.BossFight.Combat
{
    [DisallowMultipleComponent]
    public sealed class BossMeleeHitbox : MonoBehaviour
    {
        [SerializeField] private BossDirector director;
        [SerializeField] private Collider hitbox;
        [SerializeField, Min(0f)] private float defaultDamage = 10f;
        [SerializeField, Min(0.02f)] private float activeDuration = 0.16f;
        [SerializeField] private LayerMask hitMask = ~0;

        private readonly HashSet<IDamageReceiver> _hitReceivers = new();
        private readonly Collider[] _overlapBuffer = new Collider[16];
        private BossRuntime _boundRuntime;
        private BossTimelineActionExecutor _executor;
        private BossTimelineRunner _runner;
        private float _activeDamage;
        private float _remaining;

        public bool IsHitboxActive => _remaining > 0f;

        private void Awake()
        {
            if (director == null) director = GetComponentInParent<BossDirector>();
            if (hitbox == null) hitbox = GetComponent<Collider>();
            if (hitbox != null) hitbox.enabled = false;
        }

        private void OnEnable()
        {
            if (director != null)
            {
                director.RuntimeRebuilt += OnRuntimeRebuilt;
            }
            TryBind();
        }

        private void Update()
        {
            if (_boundRuntime != director?.Runtime)
            {
                TryBind();
            }

            if (_remaining <= 0f)
            {
                return;
            }

            if (director != null && director.Runtime != null &&
                (!director.Runtime.IsRunning || director.Runtime.Health.IsDead || director.Runtime.ReactionWindows.IsStaggered))
            {
                Deactivate();
                return;
            }

            CheckOverlaps();
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                Deactivate();
            }
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.RuntimeRebuilt -= OnRuntimeRebuilt;
            }
            Unbind();
            Deactivate();
        }

        private void OnRuntimeRebuilt()
        {
            TryBind();
        }

        private void TryBind()
        {
            Unbind();
            _boundRuntime = director != null ? director.Runtime : null;
            if (_boundRuntime == null)
            {
                return;
            }

            _executor = _boundRuntime.ActionExecutor;
            _runner = _boundRuntime.Timeline;
            if (_executor != null)
            {
                _executor.MeleeRequested += Activate;
            }

            if (_runner != null)
            {
                _runner.Cancelled += OnTimelineCancelled;
                _runner.Completed += OnTimelineCancelled;
            }

            _boundRuntime.Stopped += Deactivate;
        }

        private void Unbind()
        {
            if (_executor != null)
            {
                _executor.MeleeRequested -= Activate;
                _executor = null;
            }

            if (_runner != null)
            {
                _runner.Cancelled -= OnTimelineCancelled;
                _runner.Completed -= OnTimelineCancelled;
                _runner = null;
            }

            if (_boundRuntime != null)
            {
                _boundRuntime.Stopped -= Deactivate;
                _boundRuntime = null;
            }
        }

        private void OnTimelineCancelled(BossAttackDefinition _) => Deactivate();

        private void Activate(AttackTimelineEntry entry)
        {
            _activeDamage = entry.Value > 0f ? entry.Value : defaultDamage;
            _remaining = entry.Duration > 0f ? entry.Duration : activeDuration;
            _hitReceivers.Clear();
            if (hitbox != null)
            {
                hitbox.enabled = true;
            }
            CheckOverlaps();
        }

        public void Deactivate()
        {
            _remaining = 0f;
            if (hitbox != null)
            {
                hitbox.enabled = false;
            }
            _hitReceivers.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_remaining <= 0f || other == null)
            {
                return;
            }

            TryDeliverDamage(other);
        }

        private void CheckOverlaps()
        {
            if (hitbox == null)
            {
                return;
            }

            Bounds bounds = hitbox.bounds;
            int count = Physics.OverlapBoxNonAlloc(
                bounds.center,
                bounds.extents,
                _overlapBuffer,
                transform.rotation,
                hitMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                TryDeliverDamage(_overlapBuffer[i]);
            }
        }

        private void TryDeliverDamage(Collider other)
        {
            if (other == null)
            {
                return;
            }

            if (director != null && other.transform.IsChildOf(director.transform))
            {
                return;
            }

            if ((hitMask.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not IDamageReceiver receiver)
                {
                    continue;
                }

                if (receiver.Team == DamageTeam.Boss || !_hitReceivers.Add(receiver))
                {
                    continue;
                }

                Vector3 hitDirection = director != null
                    ? (other.transform.position - director.transform.position).normalized
                    : transform.forward;
                hitDirection.y = 0f;

                DamageInfo info = new(
                    _activeDamage,
                    director != null ? director.gameObject : gameObject,
                    DamageTeam.Boss,
                    other.ClosestPoint(transform.position),
                    hitDirection,
                    stagger: 20f,
                    ignoreInvulnerability: false,
                    attackId: director?.Blackboard?.CurrentAttackId ?? "boss_melee");

                receiver.ReceiveDamage(info);
                break;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Collider col = hitbox != null ? hitbox : GetComponent<Collider>();
            if (col == null)
            {
                return;
            }

            Gizmos.color = _remaining > 0f
                ? new Color(1f, 0.2f, 0.2f, 0.55f)
                : new Color(1f, 0.55f, 0.1f, 0.25f);
            Matrix4x4 prev = Gizmos.matrix;
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else
            {
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }
            Gizmos.matrix = prev;
        }
    }
}