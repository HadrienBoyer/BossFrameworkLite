using UnityEngine;

namespace TappyTale.BossFight.Projectiles
{
    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Projectile Definition", fileName = "Projectile_")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        [Header("Identity & Visuals")]
        [SerializeField] private string id = "projectile";
        [SerializeField] private string displayName = "Projectile";
        [SerializeField] private GameObject prefab;
        [SerializeField] private Color color = new(1f, 0.35f, 0.15f, 1f);

        [Header("Kinematics & Combat")]
        [SerializeField, Min(0.1f)] private float speed = 12f;
        [SerializeField, Min(0.1f)] private float lifetime = 5f;
        [SerializeField, Min(0f)] private float damage = 12f;
        [SerializeField, Min(0.05f)] private float radius = 0.25f;
        [SerializeField, Range(0f, 10f)] private float homingStrength;

        [Header("Counterplay")]
        [SerializeField] private bool canBeParried = true;
        [SerializeField, Min(1f)] private float deflectedSpeedMultiplier = 1.4f;
        [SerializeField, Min(1f)] private float deflectedDamageMultiplier = 2.0f;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        public GameObject Prefab => prefab;
        public Color Color => color;
        public float Speed => speed;
        public float Lifetime => lifetime;
        public float Damage => damage;
        public float Radius => radius;
        public float HomingStrength => homingStrength;
        public bool CanBeParried => canBeParried;
        public float DeflectedSpeedMultiplier => deflectedSpeedMultiplier;
        public float DeflectedDamageMultiplier => deflectedDamageMultiplier;
    }
}
