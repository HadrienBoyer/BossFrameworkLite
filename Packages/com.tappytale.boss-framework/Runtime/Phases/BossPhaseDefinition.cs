using System.Collections.Generic;
using TappyTale.BossFight.Attacks;
using UnityEngine;

namespace TappyTale.BossFight.Phases
{
    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Phase Definition", fileName = "BossPhase_")]
    public sealed class BossPhaseDefinition : ScriptableObject
    {
        [Header("Phase Identity & Threshold")]
        [SerializeField] private string id = "phase";
        [SerializeField] private string displayName = "Phase";
        [SerializeField, Range(0f, 1f)] private float enterAtNormalizedHealth = 1f;

        [Header("Transition Rules")]
        [SerializeField] private bool resetCooldownsOnEnter = true;
        [SerializeField, Min(0f)] private float invulnerabilityOnEnter = 0.35f;
        [SerializeField, Min(0.1f)] private float moveSpeedMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float cooldownMultiplier = 1f;

        [Header("Transition Payloads")]
        [SerializeField] private string entryAudioCueId = string.Empty;
        [SerializeField] private string entryVfxId = string.Empty;
        [SerializeField] private string entryCameraShakeId = string.Empty;

        [Header("Allowed Attacks")]
        [SerializeField] private List<BossAttackDefinition> attacks = new();

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        public float EnterAtNormalizedHealth => enterAtNormalizedHealth;
        public bool ResetCooldownsOnEnter => resetCooldownsOnEnter;
        public float InvulnerabilityOnEnter => invulnerabilityOnEnter;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;
        public float CooldownMultiplier => cooldownMultiplier;
        public string EntryAudioCueId => entryAudioCueId;
        public string EntryVfxId => entryVfxId;
        public string EntryCameraShakeId => entryCameraShakeId;
        public IReadOnlyList<BossAttackDefinition> Attacks => attacks;
    }
}
