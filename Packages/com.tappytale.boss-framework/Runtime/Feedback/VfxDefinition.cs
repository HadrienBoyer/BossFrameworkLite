using UnityEngine;

namespace TappyTale.BossFight.Feedback
{
    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/VFX Definition", fileName = "VFX_")]
    public sealed class VfxDefinition : ScriptableObject
    {
        [SerializeField] private string id = "vfx";
        [SerializeField] private string displayName = "VFX";
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0.05f)] private float lifetime = 0.6f;
        [SerializeField] private Vector3 localOffset = new(0f, 1f, 0f);
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private bool followSource;
        [SerializeField] private Color fallbackColor = new(1f, 0.65f, 0.2f, 1f);
        [SerializeField, Min(0.1f)] private float fallbackScale = 1.2f;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        public GameObject Prefab => prefab;
        public float Lifetime => lifetime;
        public Vector3 LocalOffset => localOffset;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        public bool FollowSource => followSource;
        public Color FallbackColor => fallbackColor;
        public float FallbackScale => fallbackScale;
    }
}
