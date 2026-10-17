using TappyTale.BossFight.Core;
using UnityEngine;

namespace TappyTale.BossFight.Combat
{
    [DisallowMultipleComponent]
    public sealed class BossDamageReceiver : MonoBehaviour, IBossDamageReceiver, IDamageReceiver
    {
        [SerializeField] private BossDirector director;

        public BossDirector Director => director != null ? director : (director = GetComponentInParent<BossDirector>());
        public DamageTeam Team => DamageTeam.Boss;
        public bool IsAlive => Director != null && Director.Runtime != null && !Director.Runtime.Health.IsDead;

        private void Reset() => director = GetComponentInParent<BossDirector>();

        private void Awake()
        {
            if (director == null)
            {
                director = GetComponentInParent<BossDirector>();
            }
        }

        public bool ReceiveDamage(DamageInfo damage)
        {
            if (damage.SourceTeam == DamageTeam.Boss)
            {
                return false;
            }

            if (director == null)
            {
                director = GetComponentInParent<BossDirector>();
            }

            if (director == null)
            {
                return false;
            }

            if (damage.Source != null && damage.Source.transform.IsChildOf(director.transform))
            {
                return false;
            }

            return director.ApplyDamage(
                damage.Amount,
                !string.IsNullOrEmpty(damage.AttackId) ? damage.AttackId : (damage.Source != null ? damage.Source.name : null),
                damage.IgnoreInvulnerability,
                damage.Stagger);
        }

        public bool ReceiveDamage(float amount, GameObject source)
        {
            return ReceiveDamage(new DamageInfo(amount, source, DamageTeam.Neutral));
        }
    }
}