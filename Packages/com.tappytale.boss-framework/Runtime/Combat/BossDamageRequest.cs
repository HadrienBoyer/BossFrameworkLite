using UnityEngine;

namespace TappyTale.BossFight.Combat
{
    public readonly struct BossDamageRequest
    {
        public BossDamageRequest(
            float amount,
            string sourceId = null,
            bool ignoreInvulnerability = false,
            float stagger = 0f,
            GameObject sourceObject = null)
        {
            Amount = amount;
            SourceId = sourceId ?? (sourceObject != null ? sourceObject.name : string.Empty);
            IgnoreInvulnerability = ignoreInvulnerability;
            Stagger = stagger;
            SourceObject = sourceObject;
        }

        public float Amount { get; }
        public string SourceId { get; }
        public bool IgnoreInvulnerability { get; }
        public float Stagger { get; }
        public GameObject SourceObject { get; }
    }
}
