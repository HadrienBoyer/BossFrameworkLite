using System.Collections.Generic;
using TappyTale.BossFight.Attacks;
using TappyTale.BossFight.Feedback;
using TappyTale.BossFight.Phases;
using TappyTale.BossFight.Player;
using TappyTale.BossFight.Projectiles;
using TappyTale.BossFight.Telegraphs;
using UnityEngine;

namespace TappyTale.BossFight.Data
{
    public enum BossDemoKind
    {
        BossDuelLab = 0,
        BossFightDemo = 1,
        PlayerCoreDemo = 2,
        Custom = 3
    }

    /// <summary>
    /// Explicit manifest describing a playable demo scene and every ScriptableObject definition it consumes.
    /// Allows the UI Toolkit central editor to switch demo context, inspect dependencies, repair, or duplicate demos safely.
    /// </summary>
    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Demo Profile", fileName = "DemoProfile_")]
    public sealed class BossDemoProfile : ScriptableObject
    {
        [Header("Demo Identity")]
        [SerializeField] private string id = "boss_duel_lab";
        [SerializeField] private string displayName = "Boss Duel Lab";
        [SerializeField] private BossDemoKind demoKind = BossDemoKind.BossDuelLab;
        [SerializeField, TextArea(2, 4)] private string description = "Complete arena duel with Player vs Boss, telegraphs, parry/perfect dodge reactions, HUD, and restart.";
        [SerializeField] private string scenePath = "Assets/TappyTale/BossFightFramework/Demo/BossDuelLab.unity";
        [SerializeField] private string preferencesProfile = "duel_lab";

        [Header("Arena & Presentation Settings")]
        [SerializeField, Min(8f)] private float arenaRadius = 16f;
        [SerializeField] private Vector3 playerSpawnPosition = new(0f, 1.05f, -6.5f);
        [SerializeField] private Vector3 bossSpawnPosition = new(0f, 1.35f, 4.5f);
        [SerializeField] private Color arenaFloorColor = new(0.18f, 0.2f, 0.24f, 1f);
        [SerializeField] private Color arenaBorderColor = new(0.95f, 0.48f, 0.15f, 1f);

        [Header("Primary Definitions")]
        [SerializeField] private BossDatabase database;
        [SerializeField] private BossDefinition bossDefinition;
        [SerializeField] private BossPayloadLibrary payloadLibrary;
        [SerializeField] private PlayerDefinition playerDefinition;

        [Header("Linked Encounter Assets")]
        [SerializeField] private List<BossPhaseDefinition> phases = new();
        [SerializeField] private List<BossAttackDefinition> attacks = new();
        [SerializeField] private List<AttackTimelineDefinition> timelines = new();
        [SerializeField] private List<ProjectileDefinition> projectiles = new();
        [SerializeField] private List<ProjectilePatternDefinition> patterns = new();
        [SerializeField] private List<TelegraphDefinition> telegraphs = new();
        [SerializeField] private List<AudioCueDefinition> audioCues = new();
        [SerializeField] private List<VfxDefinition> vfx = new();
        [SerializeField] private List<CameraShakeDefinition> cameraShakes = new();

        public string Id => id;
        public string DisplayName => displayName;
        public BossDemoKind DemoKind => demoKind;
        public string Description => description;
        public string ScenePath => scenePath;
        public string PreferencesProfile => preferencesProfile;
        public float ArenaRadius => arenaRadius;
        public Vector3 PlayerSpawnPosition => playerSpawnPosition;
        public Vector3 BossSpawnPosition => bossSpawnPosition;
        public Color ArenaFloorColor => arenaFloorColor;
        public Color ArenaBorderColor => arenaBorderColor;

        public BossDatabase Database => database;
        public BossDefinition BossDefinition => bossDefinition;
        public BossPayloadLibrary PayloadLibrary => payloadLibrary;
        public PlayerDefinition PlayerDefinition => playerDefinition;

        public IReadOnlyList<BossPhaseDefinition> Phases => phases;
        public IReadOnlyList<BossAttackDefinition> Attacks => attacks;
        public IReadOnlyList<AttackTimelineDefinition> Timelines => timelines;
        public IReadOnlyList<ProjectileDefinition> Projectiles => projectiles;
        public IReadOnlyList<ProjectilePatternDefinition> Patterns => patterns;
        public IReadOnlyList<TelegraphDefinition> Telegraphs => telegraphs;
        public IReadOnlyList<AudioCueDefinition> AudioCues => audioCues;
        public IReadOnlyList<VfxDefinition> Vfx => vfx;
        public IReadOnlyList<CameraShakeDefinition> CameraShakes => cameraShakes;

        public List<ScriptableObject> CollectAllReferencedDefinitions()
        {
            List<ScriptableObject> list = new();
            AddUnique(list, database);
            AddUnique(list, bossDefinition);
            AddUnique(list, payloadLibrary);
            AddUnique(list, playerDefinition);
            AddRangeUnique(list, phases);
            AddRangeUnique(list, attacks);
            AddRangeUnique(list, timelines);
            AddRangeUnique(list, projectiles);
            AddRangeUnique(list, patterns);
            AddRangeUnique(list, telegraphs);
            AddRangeUnique(list, audioCues);
            AddRangeUnique(list, vfx);
            AddRangeUnique(list, cameraShakes);
            return list;
        }

        private static void AddUnique(List<ScriptableObject> target, ScriptableObject item)
        {
            if (item != null && !target.Contains(item))
            {
                target.Add(item);
            }
        }

        private static void AddRangeUnique<T>(List<ScriptableObject> target, IReadOnlyList<T> items) where T : ScriptableObject
        {
            if (items == null) return;
            for (int i = 0; i < items.Count; i++)
            {
                AddUnique(target, items[i]);
            }
        }
    }
}
