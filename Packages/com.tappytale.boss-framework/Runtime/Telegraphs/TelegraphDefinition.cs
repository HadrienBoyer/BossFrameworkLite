using UnityEngine;

namespace TappyTale.BossFight.Telegraphs
{
    public enum TelegraphShape
    {
        Circle = 0,
        Ring = 1,
        Cone = 2,
        Line = 3
    }

    public enum TelegraphAnchor
    {
        Boss = 0,
        BossForward = 1,
        Target = 2
    }

    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Telegraph Definition", fileName = "Telegraph_")]
    public sealed class TelegraphDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "telegraph";
        [SerializeField] private string displayName = "Telegraph";

        [Header("Shape & Anchor")]
        [SerializeField] private TelegraphShape shape = TelegraphShape.Circle;
        [SerializeField] private TelegraphAnchor anchor = TelegraphAnchor.BossForward;
        [SerializeField, Min(0.05f)] private float duration = 0.5f;
        [SerializeField, Min(0.1f)] private float radius = 2.5f;
        [SerializeField, Min(0.1f)] private float length = 4f;
        [SerializeField, Range(10f, 360f)] private float angle = 90f;
        [SerializeField] private float forwardOffset = 1.5f;

        [Header("Readability & Colors")]
        [SerializeField] private Color color = new(1f, 0.45f, 0.1f, 0.65f);
        [SerializeField] private Color dangerColor = new(1f, 0.12f, 0.12f, 0.85f);
        [SerializeField] private AnimationCurve intensity = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        public TelegraphShape Shape => shape;
        public TelegraphAnchor Anchor => anchor;
        public float Duration => duration;
        public Color Color => color;
        public Color DangerColor => dangerColor;
        public float Radius => radius;
        public float Length => length;
        public float Angle => angle;
        public float ForwardOffset => forwardOffset;
        public AnimationCurve Intensity => intensity;
    }
}
