using System;
using System.Collections.Generic;
using NUnit.Framework;
using TappyTale.BossFight.Attacks;
using TappyTale.BossFight.Combat;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Editor.Install;
using TappyTale.BossFight.Phases;
using TappyTale.BossFight.Player;
using TappyTale.BossFight.Preferences;
using TappyTale.BossFight.Projectiles;
using UnityEditor;
using UnityEngine;

namespace TappyTale.BossFight.Tests.EditMode
{
    public sealed class BossFrameworkEditModeTests
    {
        // =====================================================================
        // 1. PLAYER INPUT BUFFER TESTS
        // =====================================================================
        [Test]
        public void PlayerInputBuffer_StoresConsumesAndExpiresActionsDeterministically()
        {
            PlayerInputBuffer buffer = new();
            buffer.BufferJump(0.12f);
            buffer.BufferDash(0.15f);
            buffer.BufferLightAttack(0.16f);
            buffer.BufferParry(0.14f);

            Assert.IsTrue(buffer.HasJump);
            Assert.IsTrue(buffer.HasDash);
            Assert.IsTrue(buffer.HasLightAttack);
            Assert.IsTrue(buffer.HasParry);

            // Consuming jump clears only jump
            Assert.IsTrue(buffer.ConsumeJump());
            Assert.IsFalse(buffer.HasJump);
            Assert.IsTrue(buffer.HasDash);

            // Advancing time past dash/parry/attack buffer expires them
            buffer.Tick(0.20f);
            Assert.IsFalse(buffer.HasDash);
            Assert.IsFalse(buffer.HasLightAttack);
            Assert.IsFalse(buffer.HasParry);
        }

        // =====================================================================
        // 2. PLAYER RUNTIME (DASH, ATTACK, PARRY, DAMAGE, DISABLE CLEANUP)
        // =====================================================================
        [Test]
        public void PlayerRuntime_Dash_Parry_Damage_And_DisableCleanup_AreDeterministic()
        {
            PlayerDefinition def = ScriptableObject.CreateInstance<PlayerDefinition>();
            GameObject playerGo = new("TestPlayer");
            try
            {
                playerGo.AddComponent<CharacterController>();
                PlayerMotor motor = playerGo.AddComponent<PlayerMotor>();
                PlayerInputBuffer buffer = new();
                PlayerRuntime runtime = new(def, motor, null, buffer);

                // Dash activates Dashing state and invulnerability
                buffer.BufferDash(0.15f);
                runtime.Tick(new Vector2(0f, 1f), 0.016f);
                Assert.AreEqual(PlayerState.Dashing, runtime.Blackboard.State);
                Assert.IsTrue(runtime.Blackboard.IsInvulnerable);

                // Damage while invulnerable is rejected
                bool damagedWhileInvuln = runtime.ApplyDamage(new DamageInfo(25f, null, DamageTeam.Boss, attackId: "BossSlash"));
                Assert.IsFalse(damagedWhileInvuln);
                Assert.AreEqual(def.MaxHealth, runtime.Blackboard.CurrentHealth, 0.001f);

                // SetDisabled(true) deterministically cleans up all transient flags (invulnerability, hitbox, parry, velocity)
                runtime.SetDisabled(true);
                Assert.AreEqual(PlayerState.Disabled, runtime.Blackboard.State);
                Assert.IsFalse(runtime.Blackboard.IsInvulnerable);
                Assert.IsFalse(runtime.Blackboard.IsAttackHitboxActive);
                Assert.IsFalse(runtime.Blackboard.IsParryWindowActive);
                Assert.AreEqual(Vector3.zero, runtime.Blackboard.HorizontalVelocity);

                // Re-enabling restores active state and allows taking damage from Boss team
                runtime.SetDisabled(false);
                bool damaged = runtime.ApplyDamage(new DamageInfo(30f, null, DamageTeam.Boss, attackId: "BossSlash"));
                Assert.IsTrue(damaged);
                Assert.AreEqual(def.MaxHealth - 30f, runtime.Blackboard.CurrentHealth, 0.001f);

                // Friendly fire from Player team is ignored
                bool selfHit = runtime.ApplyDamage(new DamageInfo(30f, null, DamageTeam.Player, attackId: "Self"));
                Assert.IsFalse(selfHit);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerGo);
                UnityEngine.Object.DestroyImmediate(def);
            }
        }

        // =====================================================================
        // 3. BOSS HEALTH, PHASES, REACTION WINDOWS, STAGGER & DEATH CLEANUP
        // =====================================================================
        [Test]
        public void BossRuntime_PhaseTransitions_ParryStagger_PunishMultiplier_And_DeathCleanup()
        {
            BossDefinition bossDef = ScriptableObject.CreateInstance<BossDefinition>();
            BossPhaseDefinition phase1 = ScriptableObject.CreateInstance<BossPhaseDefinition>();
            BossPhaseDefinition phase2 = ScriptableObject.CreateInstance<BossPhaseDefinition>();
            BossAttackDefinition attack = ScriptableObject.CreateInstance<BossAttackDefinition>();
            AttackTimelineDefinition timeline = ScriptableObject.CreateInstance<AttackTimelineDefinition>();
            GameObject bossGo = new("TestBoss");

            try
            {
                ConfigureField(phase1, "id", "phase_1");
                ConfigureField(phase1, "enterAtNormalizedHealth", 1.0f);
                ConfigureField(phase2, "id", "phase_2");
                ConfigureField(phase2, "enterAtNormalizedHealth", 0.5f);
                ConfigureField(phase2, "invulnerabilityOnEnter", 0f);

                BossFrameworkInstaller.ConfigureTimeline(timeline, 2.0f,
                    (0.1f, AttackTimelineAction.OpenParryWindow, string.Empty, 0.5f));
                ConfigureField(attack, "id", "slash");
                ConfigureField(attack, "timeline", timeline);
                ConfigureField(attack, "canBeParried", true);
                ConfigureField(attack, "interruptOnParry", true);
                ConfigureField(attack, "staggerDurationOnParry", 1.5f);
                ConfigureField(attack, "punishDamageMultiplier", 2.0f);

                SetArrayField(phase1, "attacks", attack);
                SetArrayField(phase2, "attacks", attack);
                ConfigureField(bossDef, "maxHealth", 200f);
                SetArrayField(bossDef, "phases", phase1, phase2);

                BossBlackboard blackboard = new();
                blackboard.ResetState(bossDef.MaxHealth);
                BossServiceRegistry services = new();
                BossContext ctx = new(bossGo, bossGo.transform, null, blackboard, services);
                using BossRuntime runtime = new(ctx, bossDef);
                runtime.Start();

                Assert.AreEqual("phase_1", runtime.Phases.CurrentPhase.Definition.Id);

                // Start attack timeline and advance to 0.15s -> opens parry window from timeline entry
                runtime.Timeline.Play(attack);
                runtime.Tick(0.15f);
                Assert.IsTrue(runtime.ReactionWindows.IsParryWindowOpen);

                // Trigger parry -> interrupts timeline, staggers boss, applies 2.0x punish multiplier
                Assert.IsTrue(runtime.ReactionWindows.TryParry());
                Assert.IsFalse(runtime.ReactionWindows.IsParryWindowOpen);
                Assert.IsTrue(runtime.ReactionWindows.IsStaggered);
                Assert.IsFalse(runtime.Timeline.IsRunning, "InterruptOnParry should cancel the running attack timeline.");

                // Apply 60 base damage during 2.0x stagger -> deals 120 damage -> health drops from 200 to 80 (40% <= 50% threshold)
                bool applied = runtime.Health.ApplyDamage(new BossDamageRequest(60f, "PlayerSword"));
                Assert.IsTrue(applied);
                Assert.AreEqual(80f, runtime.Context.Blackboard.CurrentHealth, 0.01f);

                // Tick runtime -> Phase controller transitions to phase_2
                runtime.Tick(0.05f);
                Assert.AreEqual("phase_2", runtime.Phases.CurrentPhase.Definition.Id);

                // Lethal damage kills boss and stops all active windows/timelines
                runtime.ReactionWindows.OpenPerfectDodgeWindow(0.5f);
                runtime.Health.ApplyDamage(new BossDamageRequest(100f, "PlayerFinisher"));
                Assert.IsTrue(runtime.Health.IsDead);
                Assert.IsFalse(runtime.IsRunning);
                Assert.IsFalse(runtime.ReactionWindows.IsPerfectDodgeWindowOpen);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bossGo);
                UnityEngine.Object.DestroyImmediate(timeline);
                UnityEngine.Object.DestroyImmediate(attack);
                UnityEngine.Object.DestroyImmediate(phase2);
                UnityEngine.Object.DestroyImmediate(phase1);
                UnityEngine.Object.DestroyImmediate(bossDef);
            }
        }

        // =====================================================================
        // 4. PROJECTILE PATTERN DETERMINISM TESTS
        // =====================================================================
        [Test]
        public void ProjectilePatternDefinition_EvaluateSamples_GeneratesDeterministicCountsAndDirections()
        {
            ProjectilePatternDefinition pattern = ScriptableObject.CreateInstance<ProjectilePatternDefinition>();
            try
            {
                ConfigureField(pattern, "kind", ProjectilePatternKind.Radial);
                ConfigureField(pattern, "count", 8);
                ConfigureField(pattern, "waves", 2);
                ConfigureField(pattern, "waveInterval", 0.25f);
                ConfigureField(pattern, "spiralStepAngle", 15f);

                List<PatternShotSample> samplesA = new();
                List<PatternShotSample> samplesB = new();
                pattern.EvaluateSamples(Vector3.forward, samplesA);
                pattern.EvaluateSamples(Vector3.forward, samplesB);

                Assert.AreEqual(16, samplesA.Count);
                Assert.AreEqual(samplesA.Count, samplesB.Count);

                for (int i = 0; i < samplesA.Count; i++)
                {
                    Assert.AreEqual(samplesA[i].Direction.x, samplesB[i].Direction.x, 0.0001f);
                    Assert.AreEqual(samplesA[i].Direction.z, samplesB[i].Direction.z, 0.0001f);
                    Assert.AreEqual(samplesA[i].DelaySeconds, samplesB[i].DelaySeconds, 0.0001f);
                    Assert.AreEqual(1f, samplesA[i].Direction.magnitude, 0.001f);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pattern);
            }
        }

        // =====================================================================
        // 5. SAVE DATA / PLAYERPREFS SERVICE & SNAPSHOT TESTS
        // =====================================================================
        [Test]
        public void BossPreferencesService_IsolatesProfiles_RestoresDefaults_And_RoundTripsJsonSnapshots()
        {
            InMemoryPreferencesStore store = new();
            BossPreferencesService service = new(store);

            // Default values returned when unset
            Assert.AreEqual(0, service.GetInt("duel_lab", BossPreferencesService.KeyVictories));
            Assert.AreEqual(0.85f, service.GetFloat("duel_lab", BossPreferencesService.KeySfxVolume), 0.001f);
            Assert.IsFalse(service.IsModifiedFromDefault("duel_lab", service.RegisteredKeys[0]));

            // Write to profile 'duel_lab' does not affect profile 'default'
            service.SetInt("duel_lab", BossPreferencesService.KeyVictories, 7);
            service.SetFloat("duel_lab", BossPreferencesService.KeyBestVictoryTime, 42.5f);
            service.SetString("duel_lab", BossPreferencesService.KeyLastBossId, "sovereign_warden");

            Assert.AreEqual(7, service.GetInt("duel_lab", BossPreferencesService.KeyVictories));
            Assert.AreEqual(0, service.GetInt("default", BossPreferencesService.KeyVictories));

            // Export JSON from 'duel_lab' and import into 'playtest'
            string json = service.ExportSnapshotJson("duel_lab", prettyPrint: false);
            Assert.IsFalse(string.IsNullOrWhiteSpace(json));

            int imported = service.ImportSnapshotJson(json, "playtest");
            Assert.Greater(imported, 0);
            Assert.AreEqual(7, service.GetInt("playtest", BossPreferencesService.KeyVictories));
            Assert.AreEqual(42.5f, service.GetFloat("playtest", BossPreferencesService.KeyBestVictoryTime), 0.001f);
            Assert.AreEqual("sovereign_warden", service.GetString("playtest", BossPreferencesService.KeyLastBossId));

            // Reset single key
            service.ResetKeyToDefault("playtest", BossPreferencesService.KeyVictories, deleteKey: true);
            Assert.AreEqual(0, service.GetInt("playtest", BossPreferencesService.KeyVictories));
            Assert.AreEqual(42.5f, service.GetFloat("playtest", BossPreferencesService.KeyBestVictoryTime), 0.001f);

            // Reset profile clears 'playtest' without touching 'duel_lab'
            service.ResetProfile("playtest", deleteKeys: true);
            Assert.AreEqual(0f, service.GetFloat("playtest", BossPreferencesService.KeyBestVictoryTime), 0.001f);
            Assert.AreEqual(7, service.GetInt("duel_lab", BossPreferencesService.KeyVictories));

            // Namespace guard rejects non-framework keys on PlayerPrefsStore
            PlayerPrefsStore realStore = new();
            Assert.Throws<ArgumentException>(() => realStore.SetInt("unrelated.studio.key", 1));
        }

        private static void ConfigureField(UnityEngine.Object target, string fieldName, object value)
        {
            SerializedObject so = new(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            Assert.IsNotNull(prop, $"Missing property '{fieldName}' on {target.GetType().Name}");
            switch (value)
            {
                case string s: prop.stringValue = s; break;
                case float f: prop.floatValue = f; break;
                case int i: prop.intValue = i; break;
                case bool b: prop.boolValue = b; break;
                case Enum e: prop.enumValueIndex = Convert.ToInt32(e); break;
                case UnityEngine.Object o: prop.objectReferenceValue = o; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArrayField(UnityEngine.Object target, string fieldName, params UnityEngine.Object[] items)
        {
            SerializedObject so = new(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            prop.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
