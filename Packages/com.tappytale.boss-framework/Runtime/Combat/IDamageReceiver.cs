using UnityEngine;

namespace TappyTale.BossFight.Combat
{
    public enum DamageTeam
    {
        Neutral = 0,
        Player = 1,
        Boss = 2,
        Environment = 3
    }

    public readonly struct DamageInfo
    {
        public DamageInfo(
            float amount,
            GameObject source = null,
            DamageTeam sourceTeam = DamageTeam.Neutral,
            Vector3 hitPoint = default,
            Vector3 hitDirection = default,
            float stagger = 0f,
            bool ignoreInvulnerability = false,
            string attackId = null)
        {
            Amount = amount;
            Source = source;
            SourceTeam = sourceTeam;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
            Stagger = stagger;
            IgnoreInvulnerability = ignoreInvulnerability;
            AttackId = attackId ?? (source != null ? source.name : string.Empty);
        }

        public float Amount { get; }
        public GameObject Source { get; }
        public DamageTeam SourceTeam { get; }
        public Vector3 HitPoint { get; }
        public Vector3 HitDirection { get; }
        public float Stagger { get; }
        public bool IgnoreInvulnerability { get; }
        public string AttackId { get; }
    }

    /// <summary>
    /// Neutral combat damage contract decoupling Boss and Player assemblies/modules.
    /// </summary>
    public interface IDamageReceiver
    {
        DamageTeam Team { get; }
        bool IsAlive { get; }
        bool ReceiveDamage(DamageInfo damage);
        bool ReceiveDamage(float amount, GameObject source);
    }
}
