#if UNITY_EDITOR
using System.Collections.Generic;

namespace TappyTale.EditorTools.GameStudio
{
    internal static class GameStudioKnownTools
    {
        public static List<GameStudioToolDefinition> Create()
        {
            return new List<GameStudioToolDefinition>
            {
                new()
                {
                    Id = "bark-generator",
                    DisplayName = "Bark Generator",
                    Group = GameStudioGroups.BarkGenerator,
                    Description = "Voice profiles, bark tables, preview and DeepVoice generation queue.",
                    PreferredWindowTypeName = "TappyTale.Integrations.Audio.BarkGenerator.Editor.BarkGeneratorWindow",
                    PreferredMenuPath = "Tools/Tappy Tale/Integrations/Audio/Bark Generator",
                    FolderHints = new[] { "Assets/TappyTale/Integrations/Audio/BarkGenerator" },
                    TypeHints = new[] { "BarkGeneratorWindow", "BarkGeneratorSettings" },
                    MenuHints = new[] { "Integrations/Audio/Bark Generator" },
                    IsKnownTool = true
                },
                new()
                {
                    Id = "traffic",
                    DisplayName = "Traffic & Crowd",
                    Group = GameStudioGroups.World,
                    Description = "Ambient pedestrian/car generation, lanes, anchors, pools and GC2 hooks.",
                    PreferredWindowTypeName = "TrafficWorldGeneratorWindow",
                    PreferredMenuPath = "Tools/Tappy Tale/GC2/Traffic & Crowd/World Generator",
                    FolderHints = new[]
                    {
                        "Assets/TappyTale/GC2/Traffic"
                    },
                    TypeHints = new[]
                    {
                        "TrafficWorld",
                        "TrafficSettings",
                        "TrafficSpawnCatalog",
                        "TrafficLane",
                        "TrafficWorldGeneratorWindow"
                    },
                    MenuHints = new[]
                    {
                        "Traffic & Crowd",
                        "World Generator"
                    },
                    IsKnownTool = true
                },
                new()
                {
                    Id = "crowd-builder",
                    DisplayName = "DED Crowd Builder",
                    Group = GameStudioGroups.World,
                    Description = "Formation presets, crowd generation, Scene handles and Zombie Ou Pouki arena tools.",
                    PreferredWindowTypeName = "ArenaBlockoutWindow",
                    PreferredMenuPath = "Tools/Tappy Tale/Dead End Drive/Zombie Ou Pouki/Arena Blockout",
                    FolderHints = new[]
                    {
                        "Assets/TappyTale/DED/CrowdBuilder"
                    },
                    TypeHints = new[]
                    {
                        "CrowdGenerator",
                        "CrowdShowController",
                        "CrowdFormationPreset",
                        "CrowdReactionPreset",
                        "ArenaBlockoutWindow"
                    },
                    MenuHints = new[]
                    {
                        "Crowd spawner",
                        "Arena Blockout"
                    },
                    IsKnownTool = true
                },
                new()
                {
                    Id = "zombies",
                    DisplayName = "GC2 Zombie Factory",
                    Group = GameStudioGroups.Characters,
                    Description = "Zombie entity generation, Advanced Zombie AI bridge, GC2 actions and health/HUD setup.",
                    PreferredWindowTypeName = "ZombieEntityFactoryWindow",
                    PreferredMenuPath = "Tools/Tappy Tale/GC2/Zombies/Zombie Entity Factory",
                    FolderHints = new[]
                    {
                        "Assets/TappyTale/GC2/Profiles/Zombies",
                        "Assets/TappyTale/GC2/Prefabs/Characters"
                    },
                    TypeHints = new[]
                    {
                        "ZombieEntityFactory",
                        "ZombieEntityFactoryWindow",
                        "ZombieGC2Bridge",
                        "ZombieStateActionsRelay"
                    },
                    MenuHints = new[]
                    {
                        "GC2/Zombies",
                        "Zombie Entity Factory"
                    },
                    IsKnownTool = true
                },
                new()
                {
                    Id = "radio",
                    DisplayName = "Radio Voice",
                    Group = GameStudioGroups.Audio,
                    Description = "Realtime radio/telephone/intercom DSP presets and RadioVoiceFilter inspectors.",
                    FolderHints = new[]
                    {
                        "Assets/TappyTale/Audio"
                    },
                    TypeHints = new[]
                    {
                        "RadioVoiceFilter",
                        "RadioFilterPreset",
                        "RadioFilterDSP",
                        "RadioVoiceFilterEditor"
                    },
                    MenuHints = new[]
                    {
                        "Audio",
                        "Radio Voice Filter"
                    },
                    IsKnownTool = true
                },
                new()
                {
                    Id = "performance",
                    DisplayName = "Performance Optimizer",
                    Group = GameStudioGroups.Performance,
                    Description = "DED runtime optimization, cached shadow budgets, light configuration and dynamic casters.",
                    PreferredWindowTypeName = "PerformanceOptimizerWindow",
                    PreferredMenuPath = "Tools/Tappy Tale/Performance Optimizer",
                    FolderHints = new[]
                    {
                        "Assets/TappyTale/Performance"
                    },
                    TypeHints = new[]
                    {
                        "CachedShadowManager",
                        "CachedShadowLight",
                        "DynamicShadowCaster",
                        "PerformanceProfile"
                    },
                    MenuHints = new[]
                    {
                        "Performance Optimizer"
                    },
                    IsKnownTool = true
                },
                new()
                {
                    Id = "child-patterns",
                    DisplayName = "Child Pattern Placement",
                    Group = GameStudioGroups.Design,
                    Description = "Non-destructive child layout patterns, surface projection, randomization and Scene preview.",
                    PreferredWindowTypeName = "ChildPatternPlacementWindow",
                    PreferredMenuPath = "Tools/Tappy Tale/Studio Tools/Child Pattern Placement",
                    FolderHints = System.Array.Empty<string>(),
                    TypeHints = new[]
                    {
                        "ChildPatternPlacementWindow"
                    },
                    MenuHints = new[]
                    {
                        "Studio Tools/Child Pattern Placement"
                    },
                    IsKnownTool = true
                },
                new()
                {
                    Id = "boss-framework",
                    DisplayName = "Boss Framework",
                    Group = GameStudioGroups.Gameplay,
                    Description = "Boss encounter editor built around states, reusable actions, conditions, phases and services.",
                    PreferredWindowTypeName = "BossEditorWindow",
                    PreferredMenuPath = "Tools/Tappy Tale/Boss Framework/Boss Editor",
                    FolderHints = new[]
                    {
                        "Assets/TappyTale/BossFramework"
                    },
                    TypeHints = new[]
                    {
                        "BossEncounterRunner",
                        "BossDefinition",
                        "BossEditor"
                    },
                    MenuHints = new[]
                    {
                        "Boss Framework"
                    },
                    IsKnownTool = true
                },
                new()
                {
                    Id = "arpg-controller",
                    DisplayName = "ARPG Controller",
                    Group = GameStudioGroups.Gameplay,
                    Description = "ARPG controller tooling and generated Input System actions.",
                    PreferredMenuPath = "Tools/Tappy Tale/Arpg Controller/Create Input Actions",
                    FolderHints = new[]
                    {
                        "Assets/TappyTale/ArpgController"
                    },
                    TypeHints = new[]
                    {
                        "ArpgControllerInputActionsInstaller"
                    },
                    MenuHints = new[]
                    {
                        "Arpg Controller"
                    },
                    IsKnownTool = true
                },
                new()
                {
                    Id = "ttfpc",
                    DisplayName = "TTFPC",
                    Group = GameStudioGroups.Gameplay,
                    Description = "TappyTale FPS controller/sample installer and generated validation tooling.",
                    PreferredMenuPath = "Tools/Tappy Tale/TTFPC/Install Complete Sample",
                    FolderHints = new[]
                    {
                        "Assets/TappyTale/TTFPC"
                    },
                    TypeHints = new[]
                    {
                        "TTFPCSampleInstaller",
                        "TTFPCProjectValidator"
                    },
                    MenuHints = new[]
                    {
                        "TTFPC"
                    },
                    IsKnownTool = true
                },
                new()
                {
                    Id = "framework",
                    DisplayName = "Game Framework",
                    Group = GameStudioGroups.Modules,
                    Description = "ScriptableObject-oriented core, scene management, streaming, entities, items, maps and settings.",
                    FolderHints = new[]
                    {
                        "Assets/TappyTale/Framework"
                    },
                    TypeHints = new[]
                    {
                        "ScriptableSceneController",
                        "MapStreamer",
                        "GameEvent"
                    },
                    MenuHints = new[]
                    {
                        "TappyTale"
                    },
                    IsKnownTool = true
                }
            };
        }
    }
}
#endif
