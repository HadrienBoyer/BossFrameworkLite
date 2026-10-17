using System.Collections.Generic;
using UnityEngine;

namespace TappyTale.BossFight.Projectiles
{
    public enum ProjectilePatternKind
    {
        Single = 0,
        Burst = 1,
        Spread = 2,
        Radial = 3,
        Spiral = 4,
        Custom = 5
    }

    public readonly struct PatternShotSample
    {
        public PatternShotSample(Vector3 direction, float delaySeconds, float speedMultiplier, int waveIndex, int shotIndex)
        {
            Direction = direction;
            DelaySeconds = delaySeconds;
            SpeedMultiplier = speedMultiplier;
            WaveIndex = waveIndex;
            ShotIndex = shotIndex;
        }

        public Vector3 Direction { get; }
        public float DelaySeconds { get; }
        public float SpeedMultiplier { get; }
        public int WaveIndex { get; }
        public int ShotIndex { get; }
    }

    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Projectile Pattern", fileName = "ProjectilePattern_")]
    public sealed class ProjectilePatternDefinition : ScriptableObject
    {
        [Header("Pattern Identity")]
        [SerializeField] private string id = "pattern";
        [SerializeField] private string displayName = "Projectile Pattern";
        [SerializeField] private ProjectilePatternKind kind = ProjectilePatternKind.Spread;
        [SerializeField] private ProjectileDefinition projectile;

        [Header("Emission Parameters")]
        [SerializeField, Min(1)] private int count = 5;
        [SerializeField, Min(1)] private int waves = 1;
        [SerializeField, Min(0f)] private float interval = 0.08f;
        [SerializeField, Min(0f)] private float waveInterval = 0.25f;
        [SerializeField, Range(0f, 360f)] private float arc = 60f;
        [SerializeField] private float angularOffset;
        [SerializeField] private float spiralStepAngle = 22.5f;
        [SerializeField, Min(0.1f)] private float speedMultiplier = 1f;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        public ProjectilePatternKind Kind => kind;
        public ProjectileDefinition Projectile => projectile;
        public int Count => count;
        public int Waves => waves;
        public float Interval => interval;
        public float WaveInterval => waveInterval;
        public float Arc => arc;
        public float AngularOffset => angularOffset;
        public float SpiralStepAngle => spiralStepAngle;
        public float SpeedMultiplier => speedMultiplier;

        /// <summary>
        /// Deterministically evaluates all projectile directions and launch delays for runtime spawning and editor gizmo preview.
        /// </summary>
        public void EvaluateSamples(Vector3 forward, List<PatternShotSample> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            int safeCount = Mathf.Max(1, count);
            int safeWaves = Mathf.Max(1, waves);
            float baseSpeedMult = Mathf.Max(0.1f, speedMultiplier);

            for (int w = 0; w < safeWaves; w++)
            {
                float waveDelay = w * Mathf.Max(0f, waveInterval);
                float waveAngleShift = w * spiralStepAngle;

                switch (kind)
                {
                    case ProjectilePatternKind.Single:
                    {
                        Vector3 dir = RotateY(forward, angularOffset + waveAngleShift);
                        output.Add(new PatternShotSample(dir, waveDelay, baseSpeedMult, w, 0));
                        break;
                    }

                    case ProjectilePatternKind.Burst:
                    {
                        for (int i = 0; i < safeCount; i++)
                        {
                            float shotDelay = waveDelay + i * Mathf.Max(0.02f, interval);
                            float spreadJitter = safeCount > 1 && arc > 0.01f
                                ? Mathf.Lerp(-arc * 0.5f, arc * 0.5f, i / (float)(safeCount - 1))
                                : 0f;
                            Vector3 dir = RotateY(forward, angularOffset + waveAngleShift + spreadJitter);
                            output.Add(new PatternShotSample(dir, shotDelay, baseSpeedMult, w, i));
                        }
                        break;
                    }

                    case ProjectilePatternKind.Spread:
                    case ProjectilePatternKind.Custom:
                    {
                        for (int i = 0; i < safeCount; i++)
                        {
                            float t = safeCount == 1 ? 0.5f : i / (float)(safeCount - 1);
                            float angle = angularOffset + waveAngleShift + Mathf.Lerp(-arc * 0.5f, arc * 0.5f, t);
                            Vector3 dir = RotateY(forward, angle);
                            output.Add(new PatternShotSample(dir, waveDelay, baseSpeedMult, w, i));
                        }
                        break;
                    }

                    case ProjectilePatternKind.Radial:
                    {
                        float effectiveArc = arc <= 0.01f ? 360f : arc;
                        bool fullCircle = effectiveArc >= 359.5f;
                        for (int i = 0; i < safeCount; i++)
                        {
                            float step = fullCircle
                                ? effectiveArc / safeCount
                                : (safeCount > 1 ? effectiveArc / (safeCount - 1) : 0f);
                            float startAngle = fullCircle ? 0f : -effectiveArc * 0.5f;
                            float angle = angularOffset + waveAngleShift + startAngle + i * step;
                            Vector3 dir = RotateY(forward, angle);
                            output.Add(new PatternShotSample(dir, waveDelay, baseSpeedMult, w, i));
                        }
                        break;
                    }

                    case ProjectilePatternKind.Spiral:
                    {
                        int globalIndexBase = w * safeCount;
                        for (int i = 0; i < safeCount; i++)
                        {
                            int idx = globalIndexBase + i;
                            float shotDelay = waveDelay + i * Mathf.Max(0.02f, interval);
                            float angle = angularOffset + idx * (Mathf.Abs(spiralStepAngle) > 0.01f ? spiralStepAngle : (arc / Mathf.Max(1, safeCount)));
                            Vector3 dir = RotateY(forward, angle);
                            output.Add(new PatternShotSample(dir, shotDelay, baseSpeedMult, w, i));
                        }
                        break;
                    }
                }
            }
        }

        private static Vector3 RotateY(Vector3 forward, float degrees)
        {
            return (Quaternion.Euler(0f, degrees, 0f) * forward).normalized;
        }
    }
}
