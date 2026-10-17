using System.Collections.Generic;
using TappyTale.BossFight.Combat;
using TappyTale.BossFight.Projectiles;
using UnityEngine;

namespace TappyTale.BossFight.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerMeleeHitbox : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private Collider hitbox;
        [SerializeField] private LayerMask hitMask = ~0;

        private readonly HashSet<IDamageReceiver> _hitReceivers = new();
        private readonly Collider[] _overlapBuffer = new Collider[16];
        private bool _isActive;

        public bool IsActive => _isActive;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            if (hitbox == null) hitbox = GetComponent<Collider>();
            if (hitbox != null) hitbox.enabled = false;
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            if (controller == null || controller.Blackboard == null || !controller.Blackboard.IsAttackHitboxActive)
            {
                Deactivate();
                return;
            }

            CheckOverlaps();
        }

        private void OnDisable()
        {
            Deactivate();
        }

        public void Activate()
        {
            _isActive = true;
            _hitReceivers.Clear();
            if (hitbox != null)
            {
                hitbox.enabled = true;
            }
            CheckOverlaps();
        }

        public void Deactivate()
        {
            _isActive = false;
            if (hitbox != null)
            {
                hitbox.enabled = false;
            }
            _hitReceivers.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActive || other == null)
            {
                return;
            }

            TryDeliverHit(other);
        }

        private void CheckOverlaps()
        {
            Vector3 center = hitbox != null
                ? hitbox.bounds.center
                : (controller != null ? controller.transform.position + controller.transform.forward * 1.25f + Vector3.up * 1f : transform.position);
            Vector3 halfExtents = hitbox != null
                ? hitbox.bounds.extents
                : new Vector3(0.9f, 0.9f, 1.3f);

            int count = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                _overlapBuffer,
                transform.rotation,
                hitMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                TryDeliverHit(_overlapBuffer[i]);
            }
        }

        private void TryDeliverHit(Collider other)
        {
            if (other == null)
            {
                return;
            }

            if (controller != null && other.transform.IsChildOf(controller.transform))
            {
                return;
            }

            BossProjectile projectile = other.GetComponentInParent<BossProjectile>();
            if (projectile != null && projectile.IsActive && projectile.CanBeParried)
            {
                Vector3 deflectDir = controller != null && controller.Blackboard?.LockedTargetTransform != null
                    ? (controller.Blackboard.LockedTargetTransform.position - projectile.transform.position).normalized
                    : (controller != null ? controller.transform.forward : transform.forward);
                projectile.TryDeflect(controller != null ? controller.transform : transform, deflectDir, DamageTeam.Player);
                return;
            }

            MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not IDamageReceiver receiver)
                {
                    continue;
                }

                if (receiver.Team == DamageTeam.Player || !_hitReceivers.Add(receiver))
                {
                    continue;
                }

                float damage = controller != null && controller.Blackboard != null && controller.Blackboard.ActiveAttackDamage > 0f
                    ? controller.Blackboard.ActiveAttackDamage
                    : 20f;
                float stagger = controller != null && controller.Blackboard != null
                    ? controller.Blackboard.ActiveAttackStagger
                    : 15f;

                Vector3 hitDirection = controller != null
                    ? (other.transform.position - controller.transform.position).normalized
                    : transform.forward;
                hitDirection.y = 0f;

                DamageInfo info = new(
                    damage,
                    controller != null ? controller.gameObject : gameObject,
                    DamageTeam.Player,
                    other.ClosestPoint(transform.position),
                    hitDirection,
                    stagger,
                    ignoreInvulnerability: false,
                    attackId: controller != null && controller.Blackboard != null && controller.Blackboard.IsHeavyAttack
                        ? "player_heavy_attack"
                        : "player_light_attack");

                receiver.ReceiveDamage(info);
                break;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isActive
                ? new Color(0.2f, 0.9f, 1f, 0.55f)
                : new Color(0.2f, 0.7f, 1f, 0.25f);
            Collider col = hitbox != null ? hitbox : GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Matrix4x4 prev = Gizmos.matrix;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = prev;
            }
        }
    }
}
