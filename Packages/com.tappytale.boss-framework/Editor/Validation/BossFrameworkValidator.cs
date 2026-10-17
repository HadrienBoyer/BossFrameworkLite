using System;
using System.Collections.Generic;
using System.IO;
using TappyTale.BossFight.Attacks;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Editor.Demo;
using TappyTale.BossFight.Editor.Install;
using TappyTale.BossFight.Feedback;
using TappyTale.BossFight.Phases;
using TappyTale.BossFight.Player;
using TappyTale.BossFight.Preferences;
using TappyTale.BossFight.Projectiles;
using TappyTale.BossFight.Telegraphs;
using UnityEditor;
using UnityEngine;

namespace TappyTale.BossFight.Editor.Validation
{
    public static class BossFrameworkValidator
    {
        [MenuItem("Tools/Tappy Tale/Boss Fight/Validate Project", priority = 30)]
        public static void RunValidateFromMenu()
        {
            BossValidationReport report = ValidateProject();
            Debug.Log($"[Boss Fight Framework] Validation complete — Errors: {report.ErrorCount}, Warnings: {report.WarningCount}, Info: {report.InfoCount}.");
        }

        public static BossValidationReport ValidateProject()
        {
            BossValidationReport report = new();
            List<BossPayloadLibrary> libraries = new(LoadAssets<BossPayloadLibrary>());
            BossPayloadLibrary primaryLibrary = libraries.Count > 0 ? libraries[0] : null;

            ValidateCoreInstallation(report, primaryLibrary);
            ValidateBosses(report);
            ValidatePhases(report);
            ValidateAttacks(report);
            ValidateTimelines(report, primaryLibrary);
            ValidateProjectilesAndPatterns(report);
            ValidatePayloadLibraries(report, libraries);
            ValidatePlayers(report);
            ValidatePreferencesRegistries(report);
            ValidateDemos(report);

            if (report.Issues.Count == 0)
            {
                report.Add(
                    BossValidationSeverity.Info,
                    "All Boss Fight Framework assets, timelines, payloads, player definitions, preferences, and demo profiles passed validation.",
                    BossFrameworkInstaller.RootFolder,
                    "System");
            }

            return report;
        }

        public static int FixAllAutoFixable(BossValidationReport report)
        {
            if (report == null) return 0;
            int fixedCount = 0;
            foreach (BossValidationIssue issue in report.Issues)
            {
                if (issue.CanAutoFix)
                {
                    issue.FixAction.Invoke();
                    fixedCount++;
                }
            }
            if (fixedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return fixedCount;
        }

        private static void ValidateCoreInstallation(BossValidationReport report, BossPayloadLibrary primaryLibrary)
        {
            if (!AssetDatabase.IsValidFolder(BossFrameworkInstaller.DataFolder))
            {
                report.Add(
                    BossValidationSeverity.Warning,
                    "Starter Data folder is missing. Run 'Install or Repair' to generate starter assets.",
                    BossFrameworkInstaller.RootFolder,
                    "Installation",
                    () => BossFrameworkInstaller.InstallOrRepair(false),
                    "Install Starter Assets");
            }

            if (primaryLibrary == null)
            {
                report.Add(
                    BossValidationSeverity.Warning,
                    "No BossPayloadLibrary asset found in project. Timeline payload IDs cannot be resolved without a library.",
                    BossFrameworkInstaller.DataFolder,
                    "Payloads",
                    () => BossFrameworkInstaller.InstallOrRepair(false),
                    "Create Payload Library");
            }
        }

        private static void ValidateBosses(BossValidationReport report)
        {
            HashSet<string> globalBossIds = new(StringComparer.Ordinal);
            foreach (BossDefinition boss in LoadAssets<BossDefinition>())
            {
                string path = AssetDatabase.GetAssetPath(boss);
                if (string.IsNullOrWhiteSpace(boss.Id))
                {
                    report.Add(
                        BossValidationSeverity.Error,
                        "Boss id is empty.",
                        path,
                        "Boss",
                        () => BossFrameworkInstaller.Configure(boss, "id", boss.name.ToLowerInvariant()),
                        "Set ID from Asset Name");
                }
                else if (!globalBossIds.Add(boss.Id))
                {
                    report.Add(BossValidationSeverity.Warning, $"Duplicate Boss ID '{boss.Id}' across assets.", path, "Boss");
                }

                if (boss.MaxHealth <= 0f)
                {
                    report.Add(
                        BossValidationSeverity.Error,
                        "Boss max health must be greater than zero.",
                        path,
                        "Boss",
                        () => BossFrameworkInstaller.Configure(boss, "maxHealth", 450f),
                        "Set MaxHealth = 450");
                }

                if (boss.Phases == null || boss.Phases.Count == 0)
                {
                    report.Add(BossValidationSeverity.Warning, $"Boss '{boss.DisplayName}' has no phases assigned.", path, "Boss");
                    continue;
                }

                HashSet<string> phaseIds = new(StringComparer.Ordinal);
                for (int index = 0; index < boss.Phases.Count; index++)
                {
                    BossPhaseDefinition phase = boss.Phases[index];
                    if (phase == null)
                    {
                        report.Add(BossValidationSeverity.Error, $"Boss '{boss.DisplayName}' phase slot {index} is null.", path, "Boss");
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(phase.Id) && !phaseIds.Add(phase.Id))
                    {
                        report.Add(BossValidationSeverity.Error, $"Duplicate phase id '{phase.Id}' in Boss '{boss.DisplayName}'.", path, "Boss");
                    }
                }
            }
        }

        private static void ValidatePhases(BossValidationReport report)
        {
            foreach (BossPhaseDefinition phase in LoadAssets<BossPhaseDefinition>())
            {
                string path = AssetDatabase.GetAssetPath(phase);
                if (string.IsNullOrWhiteSpace(phase.Id))
                {
                    report.Add(
                        BossValidationSeverity.Error,
                        "Phase id is empty.",
                        path,
                        "Phase",
                        () => BossFrameworkInstaller.Configure(phase, "id", phase.name.ToLowerInvariant()),
                        "Set ID from Asset Name");
                }

                if (phase.EnterAtNormalizedHealth < 0f || phase.EnterAtNormalizedHealth > 1f)
                {
                    report.Add(
                        BossValidationSeverity.Error,
                        "Phase threshold must be between 0 and 1.",
                        path,
                        "Phase",
                        () => BossFrameworkInstaller.Configure(phase, "enterAtNormalizedHealth", Mathf.Clamp01(phase.EnterAtNormalizedHealth)),
                        "Clamp to [0, 1]");
                }

                if (phase.Attacks == null || phase.Attacks.Count == 0)
                {
                    report.Add(BossValidationSeverity.Warning, $"Phase '{phase.Id}' has no attacks.", path, "Phase");
                    continue;
                }

                for (int i = 0; i < phase.Attacks.Count; i++)
                {
                    if (phase.Attacks[i] == null)
                    {
                        report.Add(BossValidationSeverity.Error, $"Phase '{phase.Id}' has a null attack slot at index {i}.", path, "Phase");
                    }
                }
            }
        }

        private static void ValidateAttacks(BossValidationReport report)
        {
            foreach (BossAttackDefinition attack in LoadAssets<BossAttackDefinition>())
            {
                string path = AssetDatabase.GetAssetPath(attack);
                if (string.IsNullOrWhiteSpace(attack.Id))
                {
                    report.Add(
                        BossValidationSeverity.Error,
                        "Attack id is empty.",
                        path,
                        "Attack",
                        () => BossFrameworkInstaller.Configure(attack, "id", attack.name.ToLowerInvariant()),
                        "Set ID from Asset Name");
                }

                if (attack.Cooldown < 0f)
                {
                    report.Add(
                        BossValidationSeverity.Error,
                        "Attack cooldown cannot be negative.",
                        path,
                        "Attack",
                        () => BossFrameworkInstaller.Configure(attack, "cooldown", 1.5f),
                        "Set Cooldown = 1.5s");
                }

                if (attack.MaximumRange < attack.MinimumRange)
                {
                    report.Add(
                        BossValidationSeverity.Error,
                        "Attack maximum range is lower than minimum range.",
                        path,
                        "Attack",
                        () => BossFrameworkInstaller.Configure(attack, "maximumRange", attack.MinimumRange + 3f),
                        "Fix Max Range");
                }

                if (attack.Timeline == null)
                {
                    report.Add(BossValidationSeverity.Warning, $"Attack '{attack.Id}' has no AttackTimelineDefinition assigned.", path, "Attack");
                }
            }
        }

        private static void ValidateTimelines(BossValidationReport report, BossPayloadLibrary library)
        {
            BossPayloadResolver resolver = library != null ? new BossPayloadResolver(library) : null;

            foreach (AttackTimelineDefinition timeline in LoadAssets<AttackTimelineDefinition>())
            {
                string path = AssetDatabase.GetAssetPath(timeline);
                if (timeline.Duration <= 0f)
                {
                    report.Add(
                        BossValidationSeverity.Error,
                        $"Timeline '{timeline.name}' duration must be > 0.",
                        path,
                        "Timeline",
                        () => BossFrameworkInstaller.Configure(timeline, "duration", 1f),
                        "Set Duration = 1.0s");
                }

                if (timeline.Entries == null || timeline.Entries.Count == 0)
                {
                    report.Add(BossValidationSeverity.Warning, $"Timeline '{timeline.name}' has no timeline entries.", path, "Timeline");
                    continue;
                }

                for (int i = 0; i < timeline.Entries.Count; i++)
                {
                    AttackTimelineEntry entry = timeline.Entries[i];
                    if (entry.Time > timeline.Duration)
                    {
                        float requiredDuration = entry.Time + 0.1f;
                        report.Add(
                            BossValidationSeverity.Warning,
                            $"Timeline '{timeline.name}' entry #{i} ({entry.Action}) triggers at {entry.Time:0.00}s which exceeds duration ({timeline.Duration:0.00}s).",
                            path,
                            "Timeline",
                            () => BossFrameworkInstaller.Configure(timeline, "duration", requiredDuration),
                            "Extend Duration");
                    }

                    if (resolver == null || string.IsNullOrWhiteSpace(entry.PayloadId))
                    {
                        continue;
                    }

                    switch (entry.Action)
                    {
                        case AttackTimelineAction.Telegraph:
                            if (!resolver.TryGetTelegraph(entry.PayloadId, out _))
                                report.Add(BossValidationSeverity.Warning, $"Timeline '{timeline.name}' references missing Telegraph ID '{entry.PayloadId}'.", path, "Timeline");
                            break;
                        case AttackTimelineAction.SpawnProjectile:
                            if (!resolver.TryGetProjectile(entry.PayloadId, out _))
                                report.Add(BossValidationSeverity.Warning, $"Timeline '{timeline.name}' references missing Projectile ID '{entry.PayloadId}'.", path, "Timeline");
                            break;
                        case AttackTimelineAction.SpawnPattern:
                            if (!resolver.TryGetPattern(entry.PayloadId, out _))
                                report.Add(BossValidationSeverity.Warning, $"Timeline '{timeline.name}' references missing Pattern ID '{entry.PayloadId}'.", path, "Timeline");
                            break;
                        case AttackTimelineAction.PlayAudio:
                            if (!resolver.TryGetAudio(entry.PayloadId, out _))
                                report.Add(BossValidationSeverity.Warning, $"Timeline '{timeline.name}' references missing AudioCue ID '{entry.PayloadId}'.", path, "Timeline");
                            break;
                        case AttackTimelineAction.PlayVfx:
                            if (!resolver.TryGetVfx(entry.PayloadId, out _))
                                report.Add(BossValidationSeverity.Warning, $"Timeline '{timeline.name}' references missing VFX ID '{entry.PayloadId}'.", path, "Timeline");
                            break;
                        case AttackTimelineAction.CameraShake:
                            if (!resolver.TryGetCameraShake(entry.PayloadId, out _))
                                report.Add(BossValidationSeverity.Warning, $"Timeline '{timeline.name}' references missing CameraShake ID '{entry.PayloadId}'.", path, "Timeline");
                            break;
                    }
                }
            }
        }

        private static void ValidateProjectilesAndPatterns(BossValidationReport report)
        {
            foreach (ProjectileDefinition proj in LoadAssets<ProjectileDefinition>())
            {
                string path = AssetDatabase.GetAssetPath(proj);
                if (string.IsNullOrWhiteSpace(proj.Id))
                {
                    report.Add(
                        BossValidationSeverity.Error,
                        "ProjectileDefinition ID is empty.",
                        path,
                        "Projectile",
                        () => BossFrameworkInstaller.Configure(proj, "id", proj.name.ToLowerInvariant()),
                        "Set ID from Asset Name");
                }
                if (proj.Prefab == null)
                {
                    report.Add(
                        BossValidationSeverity.Info,
                        $"Projectile '{proj.Id}' has no custom prefab assigned; Runtime primitive sphere fallback will be used.",
                        path,
                        "Projectile");
                }
            }

            foreach (ProjectilePatternDefinition pattern in LoadAssets<ProjectilePatternDefinition>())
            {
                string path = AssetDatabase.GetAssetPath(pattern);
                if (string.IsNullOrWhiteSpace(pattern.Id))
                {
                    report.Add(
                        BossValidationSeverity.Error,
                        "ProjectilePatternDefinition ID is empty.",
                        path,
                        "Pattern",
                        () => BossFrameworkInstaller.Configure(pattern, "id", pattern.name.ToLowerInvariant()),
                        "Set ID from Asset Name");
                }

                if (pattern.Projectile == null)
                {
                    report.Add(
                        BossValidationSeverity.Warning,
                        $"Pattern '{pattern.Id}' has no ProjectileDefinition assigned.",
                        path,
                        "Pattern");
                }
            }
        }

        private static void ValidatePayloadLibraries(BossValidationReport report, List<BossPayloadLibrary> libraries)
        {
            foreach (BossPayloadLibrary lib in libraries)
            {
                string path = AssetDatabase.GetAssetPath(lib);
                CheckPayloadDuplicates(report, path, "Projectile", lib.Projectiles, p => p != null ? p.Id : null);
                CheckPayloadDuplicates(report, path, "Pattern", lib.Patterns, p => p != null ? p.Id : null);
                CheckPayloadDuplicates(report, path, "Telegraph", lib.Telegraphs, t => t != null ? t.Id : null);
                CheckPayloadDuplicates(report, path, "AudioCue", lib.AudioCues, a => a != null ? a.Id : null);
                CheckPayloadDuplicates(report, path, "VFX", lib.Vfx, v => v != null ? v.Id : null);
                CheckPayloadDuplicates(report, path, "CameraShake", lib.CameraShakes, c => c != null ? c.Id : null);
            }
        }

        private static void CheckPayloadDuplicates<T>(
            BossValidationReport report,
            string libraryPath,
            string label,
            IReadOnlyList<T> items,
            Func<T, string> idSelector)
        {
            if (items == null) return;
            HashSet<string> ids = new(StringComparer.Ordinal);
            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                if (item == null)
                {
                    report.Add(BossValidationSeverity.Warning, $"PayloadLibrary contains a null {label} entry at index {i}.", libraryPath, "Payloads");
                    continue;
                }
                string id = idSelector(item);
                if (!string.IsNullOrWhiteSpace(id) && !ids.Add(id))
                {
                    report.Add(BossValidationSeverity.Warning, $"PayloadLibrary contains duplicate {label} ID '{id}'.", libraryPath, "Payloads");
                }
            }
        }

        private static void ValidatePlayers(BossValidationReport report)
        {
            foreach (PlayerDefinition player in LoadAssets<PlayerDefinition>())
            {
                string path = AssetDatabase.GetAssetPath(player);
                if (player.MaxHealth <= 0f)
                {
                    report.Add(
                        BossValidationSeverity.Error,
                        $"PlayerDefinition '{player.name}' maxHealth must be > 0.",
                        path,
                        "Player",
                        () => BossFrameworkInstaller.Configure(player, "maxHealth", 100f),
                        "Set MaxHealth = 100");
                }

                if (player.MoveSpeed < player.WalkSpeed)
                {
                    report.Add(
                        BossValidationSeverity.Warning,
                        $"PlayerDefinition '{player.name}' MoveSpeed ({player.MoveSpeed}) is lower than WalkSpeed ({player.WalkSpeed}).",
                        path,
                        "Player",
                        () => BossFrameworkInstaller.Configure(player, "moveSpeed", player.WalkSpeed + 2.5f),
                        "Fix MoveSpeed");
                }
            }
        }

        private static void ValidatePreferencesRegistries(BossValidationReport report)
        {
            foreach (BossPreferencesRegistry reg in LoadAssets<BossPreferencesRegistry>())
            {
                string path = AssetDatabase.GetAssetPath(reg);
                if (reg.Keys == null || reg.Keys.Count == 0)
                {
                    report.Add(
                        BossValidationSeverity.Warning,
                        $"PreferencesRegistry '{reg.name}' has no registered keys.",
                        path,
                        "Preferences",
                        () =>
                        {
                            reg.EnsureBuiltInDefaults();
                            EditorUtility.SetDirty(reg);
                        },
                        "Populate Default Schema");
                }
            }
        }

        private static void ValidateDemos(BossValidationReport report)
        {
            foreach (BossDemoProfile profile in LoadAssets<BossDemoProfile>())
            {
                string path = AssetDatabase.GetAssetPath(profile);
                if (string.IsNullOrWhiteSpace(profile.ScenePath))
                {
                    report.Add(BossValidationSeverity.Warning, $"DemoProfile '{profile.DisplayName}' has no ScenePath configured.", path, "Demos");
                }
                else if (!File.Exists(profile.ScenePath))
                {
                    BossDemoProfile capturedProfile = profile;
                    report.Add(
                        BossValidationSeverity.Info,
                        $"Demo scene '{profile.ScenePath}' for '{profile.DisplayName}' has not been generated on disk yet.",
                        path,
                        "Demos",
                        () => GenerateSceneForDemoProfile(capturedProfile),
                        "Generate Demo Scene");
                }
            }
        }

        public static void GenerateSceneForDemoProfile(BossDemoProfile profile)
        {
            if (profile == null) return;
            if (profile.Id == "player_core_demo")
            {
                PlayerCoreDemoBuilder.CreateDemo();
            }
            else if (profile.Id == "boss_fight_demo")
            {
                BossDemoSceneBuilder.CreateDemoScene();
            }
            else
            {
                BossDuelLabBuilder.BuildSceneForProfile(profile, profile.ScenePath);
            }
        }

        public static IEnumerable<T> LoadAssets<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    yield return asset;
                }
            }
        }
    }
}
