using UnityEngine;

namespace TappyTale.BossFight.Attacks
{
    public enum BossAttackKind
    {
        Melee,
        Dash,
        Projectile,
        Laser,
        Summon,
        Custom
    }

    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Attack Definition", fileName = "BossAttack_")]
    public sealed class BossAttackDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "attack";
        [SerializeField] private string displayName = "Attack";
        [SerializeField] private BossAttackKind kind;
        [SerializeField] private AttackTimelineDefinition timeline;

        [Header("Scheduling & Range")]
        [SerializeField, Min(0f)] private float cooldown = 1.5f;
        [SerializeField, Min(0f)] private float minimumRange;
        [SerializeField, Min(0f)] private float maximumRange = 20f;
        [SerializeField, Min(0.01f)] private float selectionWeight = 1f;
        [SerializeField, Min(0)] private int priority;

        [Header("Reaction & Counterplay")]
        [SerializeField] private bool canBeParried;
        [SerializeField] private bool canBePerfectDodged = true;
        [SerializeField] private bool interruptOnParry = true;
        [SerializeField, Min(0f)] private float staggerDurationOnParry = 2.2f;
        [SerializeField, Min(1f)] private float punishDamageMultiplier = 1.5f;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        public BossAttackKind Kind => kind;
        public AttackTimelineDefinition Timeline => timeline;
        public float Cooldown => cooldown;
        public float MinimumRange => minimumRange;
        public float MaximumRange => maximumRange;
        public float SelectionWeight => selectionWeight;
        public int Priority => priority;
        public bool CanBeParried => canBeParried;
        public bool CanBePerfectDodged => canBePerfectDodged;
        public bool InterruptOnParry => interruptOnParry;
        public float StaggerDurationOnParry => staggerDurationOnParry;
        public float PunishDamageMultiplier => punishDamageMultiplier;

        public bool IsInRange(float distance)
        {
            return distance >= minimumRange && distance <= maximumRange;
        }

        private void OnValidate()
        {
            if (maximumRange < minimumRange)
            {
                maximumRange = minimumRange;
            }
        }
    }
}
