using System.Collections.Generic;
using TappyTale.BossFight.Feedback;
using TappyTale.BossFight.Projectiles;
using TappyTale.BossFight.Telegraphs;
using UnityEngine;

namespace TappyTale.BossFight.Data
{
    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Payload Library", fileName = "BossPayloadLibrary")]
    public sealed class BossPayloadLibrary : ScriptableObject
    {
        [SerializeField] private List<ProjectileDefinition> projectiles = new();
        [SerializeField] private List<ProjectilePatternDefinition> patterns = new();
        [SerializeField] private List<TelegraphDefinition> telegraphs = new();
        [SerializeField] private List<AudioCueDefinition> audioCues = new();
        [SerializeField] private List<VfxDefinition> vfx = new();
        [SerializeField] private List<CameraShakeDefinition> cameraShakes = new();

        public IReadOnlyList<ProjectileDefinition> Projectiles => projectiles;
        public IReadOnlyList<ProjectilePatternDefinition> Patterns => patterns;
        public IReadOnlyList<TelegraphDefinition> Telegraphs => telegraphs;
        public IReadOnlyList<AudioCueDefinition> AudioCues => audioCues;
        public IReadOnlyList<VfxDefinition> Vfx => vfx;
        public IReadOnlyList<CameraShakeDefinition> CameraShakes => cameraShakes;
    }
}