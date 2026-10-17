using System.Collections.Generic;
using TappyTale.BossFight.Phases;
using UnityEngine;

namespace TappyTale.BossFight.Data
{
    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Boss Definition", fileName = "Boss_")]
    public sealed class BossDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "boss";
        [SerializeField] private string displayName = "Boss";
        [SerializeField, TextArea(2, 4)] private string description = "Data-driven combat encounter.";

        [Header("Combat & Vitality")]
        [SerializeField, Min(1f)] private float maxHealth = 500f;
        [SerializeField, Min(1f)] private float staggerThreshold = 100f;
        [SerializeField, Min(0.1f)] private float baseStaggerDuration = 2.2f;

        [Header("Positioning & Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
        [SerializeField, Min(0f)] private float rotationSpeed = 10f;
        [SerializeField, Min(0.5f)] private float preferredCombatDistance = 2.6f;

        [Header("Presentation")]
        [SerializeField] private Color themeColor = new(0.85f, 0.22f, 0.18f, 1f);
        [SerializeField] private Vector3 scale = new(1.25f, 1.35f, 1.25f);

        [Header("Phases")]
        [SerializeField] private List<BossPhaseDefinition> phases = new();

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public float MaxHealth => maxHealth;
        public float StaggerThreshold => staggerThreshold;
        public float BaseStaggerDuration => baseStaggerDuration;
        public float MoveSpeed => moveSpeed;
        public float RotationSpeed => rotationSpeed;
        public float PreferredCombatDistance => preferredCombatDistance;
        public Color ThemeColor => themeColor;
        public Vector3 Scale => scale;
        public IReadOnlyList<BossPhaseDefinition> Phases => phases;
    }
}
