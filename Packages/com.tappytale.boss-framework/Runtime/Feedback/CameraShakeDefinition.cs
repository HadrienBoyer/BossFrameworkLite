using System;
using UnityEngine;

namespace TappyTale.BossFight.Feedback
{
    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Camera Shake", fileName = "CameraShake_")]
    public sealed class CameraShakeDefinition : ScriptableObject
    {
        [SerializeField] private string id = "shake";
        [SerializeField] private string displayName = "Camera Shake";
        [SerializeField, Min(0.01f)] private float duration = 0.2f;
        [SerializeField, Min(0f)] private float amplitude = 0.35f;
        [SerializeField, Min(0.1f)] private float frequency = 18f;
        [SerializeField, Range(-15f, 15f)] private float fovKick;
        [SerializeField] private AnimationCurve falloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        public float Duration => duration;
        public float Amplitude => amplitude;
        public float Frequency => frequency;
        public float FovKick => fovKick;
        public AnimationCurve Falloff => falloff;
    }

    public static class CameraShakeBus
    {
        public static event Action<CameraShakeDefinition, float> ShakeRequested;
        public static event Action<float, float, float> RawShakeRequested;

        public static void Request(CameraShakeDefinition definition, float scale = 1f)
        {
            if (definition == null || scale <= 0f) return;
            ShakeRequested?.Invoke(definition, scale);
        }

        public static void RequestRaw(float duration, float amplitude, float frequency = 18f)
        {
            if (duration <= 0f || amplitude <= 0f) return;
            RawShakeRequested?.Invoke(duration, amplitude, frequency);
        }
    }
}
