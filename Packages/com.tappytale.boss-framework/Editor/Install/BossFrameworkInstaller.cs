using System;
using System.Collections.Generic;
using System.IO;
using TappyTale.BossFight.Attacks;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Feedback;
using TappyTale.BossFight.Phases;
using TappyTale.BossFight.Player;
using TappyTale.BossFight.Preferences;
using TappyTale.BossFight.Projectiles;
using TappyTale.BossFight.Telegraphs;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TappyTale.BossFight.Editor.Install
{
    public static class BossFrameworkInstaller
    {
        public const string RootFolder = "Assets/TappyTale/BossFightFramework";
        public const string DataFolder = RootFolder + "/Data";
        public const string DemoFolder = RootFolder + "/Demo";
        public const string PrefabFolder = RootFolder + "/Prefabs";
        public const string MaterialsFolder = RootFolder + "/Materials";

        public const string DuelLabScenePath = DemoFolder + "/BossDuelLab.unity";
        public const string BossFightDemoScenePath = DemoFolder + "/BossFightDemo.unity";
        public const string PlayerCoreDemoScenePath = DemoFolder + "/PlayerCoreDemo.unity";

        [MenuItem("Tools/Tappy Tale/Boss Fight/Install or Repair", priority = 10)]
        public static void InstallOrRepair()
        {
            InstallOrRepair(overwriteExisting: false);
        }

        [MenuItem("Tools/Tappy Tale/Boss Fight/Reset Starter Assets to Defaults", priority = 11)]
        public static void ResetStarterAssetsToDefaults()
        {
            InstallOrRepair(overwriteExisting: true);
        }

        public static void InstallOrRepair(bool overwriteExisting)
        {
            EnsureFolderHierarchy(DataFolder);
            EnsureFolderHierarchy(DemoFolder);
            EnsureFolderHierarchy(PrefabFolder);
            EnsureFolderHierarchy(MaterialsFolder);

            CreateDefaultMaterials(overwriteExisting);
            CreateStarterAssets(overwriteExisting);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Boss Fight Framework] Install or repair completed (overwriteExisting={overwriteExisting}).");
        }

        public static Material GetOrCreateMaterial(string fileName, Color baseColor, float smoothness = 0.35f, float metallic = 0.05f, bool overwrite = false)
        {
            EnsureFolderHierarchy(MaterialsFolder);
            string path = $"{MaterialsFolder}/{fileName}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && !overwrite)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                            ?? Shader.Find("Standard");
            Material mat = existing != null ? existing : new Material(shader);
            mat.shader = shader;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", baseColor);
            }
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", baseColor);
            }
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }
            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", metallic);
            }

            if (existing == null)
            {
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        private static void CreateDefaultMaterials(bool overwrite)
        {
            GetOrCreateMaterial("Mat_ArenaFloor", new Color(0.14f, 0.16f, 0.2f, 1f), 0.25f, 0.05f, overwrite);
            GetOrCreateMaterial("Mat_ArenaWall", new Color(0.2f, 0.23f, 0.29f, 1f), 0.3f, 0.1f, overwrite);
            GetOrCreateMaterial("Mat_BossBody", new Color(0.82f, 0.22f, 0.24f, 1f), 0.5f, 0.15f, overwrite);
            GetOrCreateMaterial("Mat_PlayerBody", new Color(0.22f, 0.64f, 0.92f, 1f), 0.55f, 0.1f, overwrite);
            GetOrCreateMaterial("Mat_Projectile", new Color(1f, 0.58f, 0.14f, 1f), 0.7f, 0.1f, overwrite);
        }

        private static void CreateStarterAssets(bool overwrite)
        {
            // 1. Telegraphs
            TelegraphDefinition telegraphDefault = GetOrCreateAsset<TelegraphDefinition>(DataFolder + "/Telegraph_Default.asset", out bool createdTelegraphDefault);
            if (createdTelegraphDefault || overwrite)
            {
                Configure(telegraphDefault, "id", "default_telegraph");
                Configure(telegraphDefault, "shape", TelegraphShape.Circle);
                Configure(telegraphDefault, "duration", 0.5f);
                Configure(telegraphDefault, "radius", 2.4f);
                Configure(telegraphDefault, "warningColor", new Color(1f, 0.6f, 0.12f, 0.45f));
                Configure(telegraphDefault, "dangerColor", new Color(1f, 0.16f, 0.16f, 0.78f));
            }

            TelegraphDefinition telegraphNova = GetOrCreateAsset<TelegraphDefinition>(DataFolder + "/Telegraph_RadialNova.asset", out bool createdTelegraphNova);
            if (createdTelegraphNova || overwrite)
            {
                Configure(telegraphNova, "id", "nova_telegraph");
                Configure(telegraphNova, "shape", TelegraphShape.Ring);
                Configure(telegraphNova, "duration", 0.65f);
                Configure(telegraphNova, "radius", 4.5f);
                Configure(telegraphNova, "warningColor", new Color(0.95f, 0.45f, 0.15f, 0.45f));
                Configure(telegraphNova, "dangerColor", new Color(1f, 0.15f, 0.35f, 0.8f));
            }

            // 2. Feedback assets
            AudioCueDefinition audioSlash = GetOrCreateAsset<AudioCueDefinition>(DataFolder + "/Audio_BossSlash.asset", out bool createdAudioSlash);
            if (createdAudioSlash || overwrite)
            {
                Configure(audioSlash, "id", "boss_slash");
                Configure(audioSlash, "volume", 0.8f);
                Configure(audioSlash, "pitch", 0.95f);
                Configure(audioSlash, "proceduralFrequencyHz", 240f);
                Configure(audioSlash, "proceduralDuration", 0.14f);
            }

            AudioCueDefinition audioNova = GetOrCreateAsset<AudioCueDefinition>(DataFolder + "/Audio_BossNova.asset", out bool createdAudioNova);
            if (createdAudioNova || overwrite)
            {
                Configure(audioNova, "id", "boss_nova");
                Configure(audioNova, "volume", 0.9f);
                Configure(audioNova, "pitch", 0.85f);
                Configure(audioNova, "proceduralFrequencyHz", 160f);
                Configure(audioNova, "proceduralDuration", 0.24f);
            }

            VfxDefinition vfxBurst = GetOrCreateAsset<VfxDefinition>(DataFolder + "/Vfx_ImpactBurst.asset", out bool createdVfxBurst);
            if (createdVfxBurst || overwrite)
            {
                Configure(vfxBurst, "id", "impact_burst");
                Configure(vfxBurst, "lifetime", 0.75f);
                Configure(vfxBurst, "scale", 1.25f);
                Configure(vfxBurst, "fallbackColor", new Color(1f, 0.52f, 0.18f, 1f));
            }

            CameraShakeDefinition shakeLight = GetOrCreateAsset<CameraShakeDefinition>(DataFolder + "/Shake_LightImpact.asset", out bool createdShakeLight);
            if (createdShakeLight || overwrite)
            {
                Configure(shakeLight, "id", "shake_light");
                Configure(shakeLight, "amplitude", 0.3f);
                Configure(shakeLight, "frequency", 20f);
                Configure(shakeLight, "duration", 0.18f);
            }

            CameraShakeDefinition shakeHeavy = GetOrCreateAsset<CameraShakeDefinition>(DataFolder + "/Shake_HeavySlam.asset", out bool createdShakeHeavy);
            if (createdShakeHeavy || overwrite)
            {
                Configure(shakeHeavy, "id", "shake_heavy");
                Configure(shakeHeavy, "amplitude", 0.6f);
                Configure(shakeHeavy, "frequency", 24f);
                Configure(shakeHeavy, "duration", 0.32f);
            }

            // 3. Projectile & Patterns
            GameObject projectilePrefab = CreateProjectilePrefab(overwrite);
            ProjectileDefinition projectileDefinition = GetOrCreateAsset<ProjectileDefinition>(DataFolder + "/Projectile_Default.asset", out bool createdProjectile);
            if (createdProjectile || overwrite)
            {
                Configure(projectileDefinition, "id", "default_projectile");
                Configure(projectileDefinition, "prefab", projectilePrefab);
                Configure(projectileDefinition, "speed", 12f);
                Configure(projectileDefinition, "lifetime", 5f);
                Configure(projectileDefinition, "damage", 12f);
                Configure(projectileDefinition, "canBeParried", true);
                Configure(projectileDefinition, "deflectedSpeedMultiplier", 1.55f);
                Configure(projectileDefinition, "deflectedDamageMultiplier", 2.2f);
            }
            else if (projectileDefinition.Prefab == null)
            {
                Configure(projectileDefinition, "prefab", projectilePrefab);
            }

            ProjectilePatternDefinition patternRadial = GetOrCreateAsset<ProjectilePatternDefinition>(DataFolder + "/Pattern_RadialBurst.asset", out bool createdPatternRadial);
            if (createdPatternRadial || overwrite)
            {
                Configure(patternRadial, "id", "radial_burst");
                Configure(patternRadial, "displayName", "Radial Burst");
                Configure(patternRadial, "kind", ProjectilePatternKind.Radial);
                Configure(patternRadial, "projectile", projectileDefinition);
                Configure(patternRadial, "count", 8);
                Configure(patternRadial, "waves", 1);
                Configure(patternRadial, "arc", 360f);
            }
            else if (patternRadial.Projectile == null)
            {
                Configure(patternRadial, "projectile", projectileDefinition);
            }

            ProjectilePatternDefinition patternArc = GetOrCreateAsset<ProjectilePatternDefinition>(DataFolder + "/Pattern_SpreadArc.asset", out bool createdPatternArc);
            if (createdPatternArc || overwrite)
            {
                Configure(patternArc, "id", "spread_arc");
                Configure(patternArc, "displayName", "Spread Arc");
                Configure(patternArc, "kind", ProjectilePatternKind.Spread);
                Configure(patternArc, "projectile", projectileDefinition);
                Configure(patternArc, "count", 5);
                Configure(patternArc, "waves", 2);
                Configure(patternArc, "waveInterval", 0.22f);
                Configure(patternArc, "arc", 55f);
            }
            else if (patternArc.Projectile == null)
            {
                Configure(patternArc, "projectile", projectileDefinition);
            }

            // 4. Timelines
            AttackTimelineDefinition meleeTimeline = GetOrCreateAsset<AttackTimelineDefinition>(DataFolder + "/Timeline_Melee.asset", out bool createdMeleeTimeline);
            if (createdMeleeTimeline || overwrite || meleeTimeline.Entries == null || meleeTimeline.Entries.Count == 0)
            {
                ConfigureTimeline(meleeTimeline, 1.25f,
                    (0f, AttackTimelineAction.Telegraph, telegraphDefault.Id, 0.5f),
                    (0.18f, AttackTimelineAction.OpenParryWindow, string.Empty, 0.42f),
                    (0.22f, AttackTimelineAction.Dash, string.Empty, 4.2f),
                    (0.55f, AttackTimelineAction.PlayAudio, audioSlash.Id, 0f),
                    (0.58f, AttackTimelineAction.MeleeHit, string.Empty, 18f),
                    (0.6f, AttackTimelineAction.CameraShake, shakeLight.Id, 0.25f));
            }

            AttackTimelineDefinition projectileTimeline = GetOrCreateAsset<AttackTimelineDefinition>(DataFolder + "/Timeline_Projectile.asset", out bool createdProjectileTimeline);
            if (createdProjectileTimeline || overwrite || projectileTimeline.Entries == null || projectileTimeline.Entries.Count == 0)
            {
                ConfigureTimeline(projectileTimeline, 1.4f,
                    (0f, AttackTimelineAction.Telegraph, telegraphDefault.Id, 0.55f),
                    (0.3f, AttackTimelineAction.OpenPerfectDodgeWindow, string.Empty, 0.38f),
                    (0.32f, AttackTimelineAction.OpenParryWindow, string.Empty, 0.36f),
                    (0.65f, AttackTimelineAction.SpawnProjectile, projectileDefinition.Id, 0f));
            }

            AttackTimelineDefinition novaTimeline = GetOrCreateAsset<AttackTimelineDefinition>(DataFolder + "/Timeline_RadialNova.asset", out bool createdNovaTimeline);
            if (createdNovaTimeline || overwrite || novaTimeline.Entries == null || novaTimeline.Entries.Count == 0)
            {
                ConfigureTimeline(novaTimeline, 1.75f,
                    (0f, AttackTimelineAction.Telegraph, telegraphNova.Id, 0.68f),
                    (0.35f, AttackTimelineAction.OpenPerfectDodgeWindow, string.Empty, 0.4f),
                    (0.7f, AttackTimelineAction.PlayAudio, audioNova.Id, 0f),
                    (0.72f, AttackTimelineAction.PlayVfx, vfxBurst.Id, 0f),
                    (0.72f, AttackTimelineAction.SpawnPattern, patternRadial.Id, 0f),
                    (0.74f, AttackTimelineAction.CameraShake, shakeHeavy.Id, 0.55f));
            }

            // 5. Attacks
            BossAttackDefinition melee = GetOrCreateAsset<BossAttackDefinition>(DataFolder + "/Attack_Melee.asset", out bool createdMelee);
            if (createdMelee || overwrite)
            {
                Configure(melee, "id", "melee");
                Configure(melee, "displayName", "Vanguard Cleave");
                Configure(melee, "kind", BossAttackKind.Melee);
                Configure(melee, "timeline", meleeTimeline);
                Configure(melee, "cooldown", 1.8f);
                Configure(melee, "minimumRange", 0f);
                Configure(melee, "maximumRange", 5.5f);
                Configure(melee, "selectionWeight", 1.3f);
                Configure(melee, "priority", 2);
                Configure(melee, "canBeParried", true);
                Configure(melee, "interruptOnParry", true);
                Configure(melee, "staggerDurationOnParry", 1.6f);
            }
            else if (melee.Timeline == null)
            {
                Configure(melee, "timeline", meleeTimeline);
            }

            BossAttackDefinition projectile = GetOrCreateAsset<BossAttackDefinition>(DataFolder + "/Attack_Projectile.asset", out bool createdProjAttack);
            if (createdProjAttack || overwrite)
            {
                Configure(projectile, "id", "projectile");
                Configure(projectile, "displayName", "Solar Lance");
                Configure(projectile, "kind", BossAttackKind.Projectile);
                Configure(projectile, "timeline", projectileTimeline);
                Configure(projectile, "cooldown", 2.4f);
                Configure(projectile, "minimumRange", 2.5f);
                Configure(projectile, "maximumRange", 22f);
                Configure(projectile, "selectionWeight", 1f);
                Configure(projectile, "priority", 1);
                Configure(projectile, "canBeParried", true);
                Configure(projectile, "interruptOnParry", false);
            }
            else if (projectile.Timeline == null)
            {
                Configure(projectile, "timeline", projectileTimeline);
            }

            BossAttackDefinition radialNova = GetOrCreateAsset<BossAttackDefinition>(DataFolder + "/Attack_RadialNova.asset", out bool createdNovaAttack);
            if (createdNovaAttack || overwrite)
            {
                Configure(radialNova, "id", "radial_nova");
                Configure(radialNova, "displayName", "Crimson Ring Nova");
                Configure(radialNova, "kind", BossAttackKind.Custom);
                Configure(radialNova, "timeline", novaTimeline);
                Configure(radialNova, "cooldown", 4.2f);
                Configure(radialNova, "minimumRange", 0f);
                Configure(radialNova, "maximumRange", 18f);
                Configure(radialNova, "selectionWeight", 1.1f);
                Configure(radialNova, "priority", 3);
                Configure(radialNova, "canBeParried", false);
            }
            else if (radialNova.Timeline == null)
            {
                Configure(radialNova, "timeline", novaTimeline);
            }

            // 6. Phases
            BossPhaseDefinition phaseOne = GetOrCreateAsset<BossPhaseDefinition>(DataFolder + "/Phase_One.asset", out bool createdPhaseOne);
            if (createdPhaseOne || overwrite || phaseOne.Attacks == null || phaseOne.Attacks.Count == 0)
            {
                Configure(phaseOne, "id", "phase_one");
                Configure(phaseOne, "displayName", "Phase I - Measured Duel");
                Configure(phaseOne, "enterAtNormalizedHealth", 1f);
                Configure(phaseOne, "resetCooldownsOnEnter", true);
                Configure(phaseOne, "moveSpeedMultiplier", 1f);
                Configure(phaseOne, "cooldownMultiplier", 1f);
                SetObjectArray(phaseOne, "attacks", melee, projectile);
            }

            BossPhaseDefinition phaseTwo = GetOrCreateAsset<BossPhaseDefinition>(DataFolder + "/Phase_Two.asset", out bool createdPhaseTwo);
            if (createdPhaseTwo || overwrite || phaseTwo.Attacks == null || phaseTwo.Attacks.Count == 0)
            {
                Configure(phaseTwo, "id", "phase_two");
                Configure(phaseTwo, "displayName", "Phase II - Overdrive");
                Configure(phaseTwo, "enterAtNormalizedHealth", 0.55f);
                Configure(phaseTwo, "resetCooldownsOnEnter", true);
                Configure(phaseTwo, "invulnerabilityOnEnter", 0.65f);
                Configure(phaseTwo, "moveSpeedMultiplier", 1.18f);
                Configure(phaseTwo, "cooldownMultiplier", 0.82f);
                Configure(phaseTwo, "entryAudioCueId", audioNova.Id);
                Configure(phaseTwo, "entryVfxId", vfxBurst.Id);
                SetObjectArray(phaseTwo, "attacks", melee, projectile, radialNova);
            }

            // 7. Boss Definition, Database, Payload Library
            BossDefinition boss = GetOrCreateAsset<BossDefinition>(DataFolder + "/Boss_Starter.asset", out bool createdBoss);
            if (createdBoss || overwrite || boss.Phases == null || boss.Phases.Count == 0)
            {
                Configure(boss, "id", "starter_boss");
                Configure(boss, "displayName", "Sovereign Warden");
                Configure(boss, "maxHealth", 450f);
                Configure(boss, "decisionInterval", 0.35f);
                Configure(boss, "engageDistance", 18f);
                Configure(boss, "preferredCombatDistance", 3.6f);
                Configure(boss, "moveSpeed", 4.5f);
                Configure(boss, "rotationSpeedDegrees", 420f);
                Configure(boss, "defaultStaggerDuration", 1.6f);
                SetObjectArray(boss, "phases", phaseOne, phaseTwo);
            }

            BossDatabase database = GetOrCreateAsset<BossDatabase>(DataFolder + "/BossDatabase.asset", out bool createdDb);
            if (createdDb || overwrite || database.Bosses == null || database.Bosses.Count == 0)
            {
                SetObjectArray(database, "bosses", boss);
            }

            BossPayloadLibrary payloads = GetOrCreateAsset<BossPayloadLibrary>(DataFolder + "/BossPayloadLibrary.asset", out bool createdPayloads);
            if (createdPayloads || overwrite)
            {
                SetObjectArray(payloads, "projectiles", projectileDefinition);
                SetObjectArray(payloads, "patterns", patternRadial, patternArc);
                SetObjectArray(payloads, "telegraphs", telegraphDefault, telegraphNova);
                SetObjectArray(payloads, "audioCues", audioSlash, audioNova);
                SetObjectArray(payloads, "vfx", vfxBurst);
                SetObjectArray(payloads, "cameraShakes", shakeLight, shakeHeavy);
            }
            else
            {
                EnsureInObjectArray(payloads, "projectiles", projectileDefinition);
                EnsureInObjectArray(payloads, "patterns", patternRadial, patternArc);
                EnsureInObjectArray(payloads, "telegraphs", telegraphDefault, telegraphNova);
                EnsureInObjectArray(payloads, "audioCues", audioSlash, audioNova);
                EnsureInObjectArray(payloads, "vfx", vfxBurst);
                EnsureInObjectArray(payloads, "cameraShakes", shakeLight, shakeHeavy);
            }

            // 8. Player Definition
            PlayerDefinition playerDef = GetOrCreateAsset<PlayerDefinition>(DataFolder + "/Player_Starter.asset", out bool createdPlayer);
            if (createdPlayer || overwrite)
            {
                Configure(playerDef, "id", "starter_player");
                Configure(playerDef, "displayName", "Duelist Challenger");
            }

            // 9. Preferences Registry
            BossPreferencesRegistry prefsRegistry = GetOrCreateAsset<BossPreferencesRegistry>(DataFolder + "/PreferencesRegistry_Default.asset", out bool createdPrefs);
            if (createdPrefs || overwrite)
            {
                prefsRegistry.EnsureBuiltInDefaults();
                EditorUtility.SetDirty(prefsRegistry);
            }

            // 10. Demo Profiles
            ConfigureDemoProfile(
                DataFolder + "/DemoProfile_BossDuelLab.asset",
                "boss_duel_lab",
                "Boss Duel Lab (Reference)",
                BossDemoKind.BossDuelLab,
                "Flagship duel arena testing the complete Threat -> Signal -> Player Response (Parry / Perfect Dodge) -> Punish -> Victory/Defeat loop.",
                DuelLabScenePath,
                boss,
                database,
                payloads,
                playerDef,
                overwrite,
                new[] { phaseOne, phaseTwo },
                new[] { melee, projectile, radialNova },
                new[] { meleeTimeline, projectileTimeline, novaTimeline },
                new[] { projectileDefinition },
                new[] { patternRadial, patternArc },
                new[] { telegraphDefault, telegraphNova },
                new[] { audioSlash, audioNova },
                new[] { vfxBurst },
                new[] { shakeLight, shakeHeavy });

            ConfigureDemoProfile(
                DataFolder + "/DemoProfile_BossFightDemo.asset",
                "boss_fight_demo",
                "Boss Fight Demo",
                BossDemoKind.BossFightDemo,
                "Focused Boss AI & Timeline runner sandbox scene.",
                BossFightDemoScenePath,
                boss,
                database,
                payloads,
                playerDef,
                overwrite,
                new[] { phaseOne, phaseTwo },
                new[] { melee, projectile, radialNova },
                new[] { meleeTimeline, projectileTimeline, novaTimeline },
                new[] { projectileDefinition },
                new[] { patternRadial },
                new[] { telegraphDefault, telegraphNova },
                null,
                null,
                null);

            ConfigureDemoProfile(
                DataFolder + "/DemoProfile_PlayerCoreDemo.asset",
                "player_core_demo",
                "Player Core Demo",
                BossDemoKind.PlayerCoreDemo,
                "Player locomotion, jump buffering, coyote time, dash, and combat responsiveness test arena.",
                PlayerCoreDemoScenePath,
                null,
                database,
                payloads,
                playerDef,
                overwrite,
                null, null, null, null, null, null, null, null, null);
        }

        private static void ConfigureDemoProfile(
            string assetPath,
            string id,
            string displayName,
            BossDemoKind demoKind,
            string description,
            string scenePath,
            BossDefinition boss,
            BossDatabase database,
            BossPayloadLibrary payloads,
            PlayerDefinition playerDef,
            bool overwrite,
            Object[] phases,
            Object[] attacks,
            Object[] timelines,
            Object[] projectiles,
            Object[] patterns,
            Object[] telegraphs,
            Object[] audioCues,
            Object[] vfx,
            Object[] cameraShakes)
        {
            BossDemoProfile profile = GetOrCreateAsset<BossDemoProfile>(assetPath, out bool created);
            if (created || overwrite)
            {
                Configure(profile, "id", id);
                Configure(profile, "displayName", displayName);
                Configure(profile, "demoKind", demoKind);
                Configure(profile, "description", description);
                Configure(profile, "scenePath", scenePath);
                Configure(profile, "bossDefinition", boss);
                Configure(profile, "database", database);
                Configure(profile, "payloadLibrary", payloads);
                Configure(profile, "playerDefinition", playerDef);
                if (phases != null) SetObjectArray(profile, "phases", phases);
                if (attacks != null) SetObjectArray(profile, "attacks", attacks);
                if (timelines != null) SetObjectArray(profile, "timelines", timelines);
                if (projectiles != null) SetObjectArray(profile, "projectiles", projectiles);
                if (patterns != null) SetObjectArray(profile, "patterns", patterns);
                if (telegraphs != null) SetObjectArray(profile, "telegraphs", telegraphs);
                if (audioCues != null) SetObjectArray(profile, "audioCues", audioCues);
                if (vfx != null) SetObjectArray(profile, "vfx", vfx);
                if (cameraShakes != null) SetObjectArray(profile, "cameraShakes", cameraShakes);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(profile.ScenePath)) Configure(profile, "scenePath", scenePath);
                if (profile.BossDefinition == null && boss != null) Configure(profile, "bossDefinition", boss);
                if (profile.Database == null && database != null) Configure(profile, "database", database);
                if (profile.PayloadLibrary == null && payloads != null) Configure(profile, "payloadLibrary", payloads);
                if (profile.PlayerDefinition == null && playerDef != null) Configure(profile, "playerDefinition", playerDef);
            }
        }

        private static GameObject CreateProjectilePrefab(bool overwrite)
        {
            string path = PrefabFolder + "/BossProjectile_Default.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !overwrite)
            {
                return existing;
            }

            Material projMat = GetOrCreateMaterial("Mat_Projectile", new Color(1f, 0.58f, 0.14f, 1f), 0.7f, 0.1f, false);

            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            instance.name = "BossProjectile_Default";
            instance.transform.localScale = Vector3.one * 0.42f;
            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null && projMat != null)
            {
                renderer.sharedMaterial = projMat;
            }

            SphereCollider collider = instance.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            Rigidbody body = instance.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            instance.AddComponent<BossProjectile>();
            instance.AddComponent<BossProjectileImpact>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        public static void ConfigureTimeline(AttackTimelineDefinition timeline, float duration,
            params (float time, AttackTimelineAction action, string payloadId, float value)[] entries)
        {
            SerializedObject serialized = new(timeline);
            serialized.FindProperty("duration").floatValue = duration;
            SerializedProperty list = serialized.FindProperty("entries");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("time").floatValue = entries[i].time;
                entry.FindPropertyRelative("action").enumValueIndex = (int)entries[i].action;
                entry.FindPropertyRelative("payloadId").stringValue = entries[i].payloadId;
                entry.FindPropertyRelative("value").floatValue = entries[i].value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(timeline);
        }

        public static void SetObjectArray(Object target, string propertyName, params Object[] values)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        public static void EnsureInObjectArray(Object target, string propertyName, params Object[] values)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;

            HashSet<Object> current = new();
            for (int i = 0; i < property.arraySize; i++)
            {
                Object obj = property.GetArrayElementAtIndex(i).objectReferenceValue;
                if (obj != null) current.Add(obj);
            }

            foreach (Object value in values)
            {
                if (value == null || current.Contains(value)) continue;
                int index = property.arraySize;
                property.InsertArrayElementAtIndex(index);
                property.GetArrayElementAtIndex(index).objectReferenceValue = value;
                current.Add(value);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        public static T GetOrCreateAsset<T>(string path, out bool created) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                created = false;
                return existing;
            }

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            created = true;
            return asset;
        }

        public static void Configure(Object target, string propertyName, object value)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null) return;

            switch (value)
            {
                case string stringValue: property.stringValue = stringValue; break;
                case float floatValue: property.floatValue = floatValue; break;
                case int intValue: property.intValue = intValue; break;
                case bool boolValue: property.boolValue = boolValue; break;
                case Color colorValue: property.colorValue = colorValue; break;
                case Enum enumValue: property.enumValueIndex = Convert.ToInt32(enumValue); break;
                case Object objectValue: property.objectReferenceValue = objectValue; break;
            }
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        public static void EnsureFolderHierarchy(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return;
            string[] parts = fullPath.Replace('\\', '/').Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}