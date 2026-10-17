using TappyTale.BossFight.Combat;
using UnityEngine;

namespace TappyTale.BossFight.Projectiles
{
    [DisallowMultipleComponent]
    public sealed class BossProjectileImpact : MonoBehaviour
    {
        [SerializeField] private BossProjectile projectile;
        [SerializeField] private LayerMask hitMask = ~0;

        private void Awake()
        {
            if (projectile == null) projectile = GetComponent<BossProjectile>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (projectile == null || !projectile.IsActive || other == null)
            {
                return;
            }

            if ((hitMask.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            if (projectile.SourceOwnerTransform != null && other.transform.IsChildOf(projectile.SourceOwnerTransform))
            {
                return;
            }

            if (other.GetComponentInParent<BossProjectile>() != null)
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

                if (receiver.Team != DamageTeam.Neutral && receiver.Team == projectile.Team)
                {
                    return;
                }

                DamageInfo info = new(
                    projectile.Damage,
                    gameObject,
                    projectile.Team,
                    other.ClosestPoint(transform.position),
                    projectile.Velocity.normalized,
                    stagger: projectile.Team == DamageTeam.Player ? 35f : 15f,
                    ignoreInvulnerability: false,
                    attackId: projectile.Definition != null ? projectile.Definition.Id : "projectile");

                receiver.ReceiveDamage(info);
                projectile.Despawn();
                return;
            }

            if (!other.isTrigger)
            {
                projectile.Despawn();
            }
        }
    }
}