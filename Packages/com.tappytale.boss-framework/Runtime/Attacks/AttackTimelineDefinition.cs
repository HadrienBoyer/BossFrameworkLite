using System;
using System.Collections.Generic;
using UnityEngine;

namespace TappyTale.BossFight.Attacks
{
    public enum AttackTimelineAction
    {
        Telegraph = 0,
        OpenParryWindow = 1,
        OpenPerfectDodgeWindow = 2,
        MeleeHit = 3,
        SpawnProjectile = 4,
        Dash = 5,
        PlayAudio = 6,
        PlayVfx = 7,
        Custom = 8,
        SpawnPattern = 9,
        CameraShake = 10,
        InvulnerabilityWindow = 11
    }

    [Serializable]
    public struct AttackTimelineEntry
    {
        [SerializeField, Min(0f)] private float time;
        [SerializeField] private AttackTimelineAction action;
        [SerializeField] private string payloadId;
        [SerializeField] private float value;
        [SerializeField, Min(0f)] private float duration;

        public AttackTimelineEntry(float time, AttackTimelineAction action, string payloadId = "", float value = 0f, float duration = 0f)
        {
            this.time = Mathf.Max(0f, time);
            this.action = action;
            this.payloadId = payloadId ?? string.Empty;
            this.value = value;
            this.duration = Mathf.Max(0f, duration);
        }

        public float Time => time;
        public AttackTimelineAction Action => action;
        public string PayloadId => payloadId ?? string.Empty;
        public float Value => value;
        public float Duration => duration;
        public float EffectiveDuration => duration > 0f ? duration : value;
    }

    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Attack Timeline", fileName = "AttackTimeline_")]
    public sealed class AttackTimelineDefinition : ScriptableObject
    {
        [SerializeField, Min(0.05f)] private float duration = 1f;
        [SerializeField] private List<AttackTimelineEntry> entries = new();

        public float Duration => duration;
        public IReadOnlyList<AttackTimelineEntry> Entries => entries;

        public void SortEntriesByTime()
        {
            entries.Sort(static (a, b) => a.Time.CompareTo(b.Time));
        }

        private void OnValidate()
        {
            if (duration < 0.05f)
            {
                duration = 0.05f;
            }
        }
    }
}
