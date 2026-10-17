using System.Collections.Generic;
using System.IO;
using TappyTale.BossFight.Attacks;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Editor.Install;
using TappyTale.BossFight.Phases;
using TappyTale.BossFight.Player;
using UnityEditor;
using UnityEngine;

namespace TappyTale.BossFight.Editor.Demo
{
    public static class BossDemoDuplicator
    {
        public static BossDemoProfile DuplicateDemoProfile(BossDemoProfile sourceProfile, string customFolderName = null)
        {
            if (sourceProfile == null)
            {
                Debug.LogWarning("[Boss Fight Framework] Cannot duplicate a null BossDemoProfile.");
                return null;
            }

            string cleanName = string.IsNullOrWhiteSpace(customFolderName)
                ? $"{sourceProfile.Id}_Copy"
                : customFolderName.Trim().Replace(' ', '_');

            string targetRoot = $"{BossFrameworkInstaller.RootFolder}/Demos/{cleanName}";
            int suffix = 1;
            while (AssetDatabase.IsValidFolder(targetRoot))
            {
                suffix++;
                targetRoot = $"{BossFrameworkInstaller.RootFolder}/Demos/{cleanName}_{suffix}";
            }

            BossFrameworkInstaller.EnsureFolderHierarchy(targetRoot);
            string targetDataFolder = $"{targetRoot}/Data";
            BossFrameworkInstaller.EnsureFolderHierarchy(targetDataFolder);

            Dictionary<Object, Object> remappedAssets = new();

            // 1. Copy Boss & Phases & Attacks & Timelines if present
            BossDefinition copiedBoss = null;
            if (sourceProfile.BossDefinition != null)
            {
                copiedBoss = CloneAssetToFolder(sourceProfile.BossDefinition, targetDataFolder, remappedAssets);
                List<BossPhaseDefinition> copiedPhases = new();
                if (sourceProfile.BossDefinition.Phases != null)
                {
                    foreach (BossPhaseDefinition phase in sourceProfile.BossDefinition.Phases)
                    {
                        if (phase == null) continue;
                        BossPhaseDefinition copiedPhase = CloneAssetToFolder(phase, targetDataFolder, remappedAssets);
                        List<BossAttackDefinition> copiedAttacks = new();
                        if (phase.Attacks != null)
                        {
                            foreach (BossAttackDefinition attack in phase.Attacks)
                            {
                                if (attack == null) continue;
                                BossAttackDefinition copiedAttack = CloneAssetToFolder(attack, targetDataFolder, remappedAssets);
                                if (attack.Timeline != null)
                                {
                                    AttackTimelineDefinition copiedTimeline = CloneAssetToFolder(attack.Timeline, targetDataFolder, remappedAssets);
                                    BossFrameworkInstaller.Configure(copiedAttack, "timeline", copiedTimeline);
                                }
                                copiedAttacks.Add(copiedAttack);
                            }
                        }
                        BossFrameworkInstaller.SetObjectArray(copiedPhase, "attacks", copiedAttacks.ToArray());
                        copiedPhases.Add(copiedPhase);
                    }
                }
                BossFrameworkInstaller.SetObjectArray(copiedBoss, "phases", copiedPhases.ToArray());
            }

            // 2. Copy Player Definition if present
            PlayerDefinition copiedPlayer = null;
            if (sourceProfile.PlayerDefinition != null)
            {
                copiedPlayer = CloneAssetToFolder(sourceProfile.PlayerDefinition, targetDataFolder, remappedAssets);
            }

            // 3. Copy Payload Library if present
            BossPayloadLibrary copiedPayloads = null;
            if (sourceProfile.PayloadLibrary != null)
            {
                copiedPayloads = CloneAssetToFolder(sourceProfile.PayloadLibrary, targetDataFolder, remappedAssets);
            }

            // 4. Create new Demo Profile
            string newProfilePath = $"{targetRoot}/DemoProfile_{Path.GetFileName(targetRoot)}.asset";
            BossDemoProfile newProfile = ScriptableObject.CreateInstance<BossDemoProfile>();
            AssetDatabase.CreateAsset(newProfile, newProfilePath);

            string newScenePath = $"{targetRoot}/{Path.GetFileName(targetRoot)}.unity";
            BossFrameworkInstaller.Configure(newProfile, "id", Path.GetFileName(targetRoot).ToLowerInvariant());
            BossFrameworkInstaller.Configure(newProfile, "displayName", $"{sourceProfile.DisplayName} ({Path.GetFileName(targetRoot)})");
            BossFrameworkInstaller.Configure(newProfile, "description", $"Duplicated from {sourceProfile.DisplayName}.");
            BossFrameworkInstaller.Configure(newProfile, "scenePath", newScenePath);
            BossFrameworkInstaller.Configure(newProfile, "bossDefinition", copiedBoss);
            BossFrameworkInstaller.Configure(newProfile, "database", sourceProfile.Database);
            BossFrameworkInstaller.Configure(newProfile, "payloadLibrary", copiedPayloads != null ? copiedPayloads : sourceProfile.PayloadLibrary);
            BossFrameworkInstaller.Configure(newProfile, "playerDefinition", copiedPlayer);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 5. Generate isolated scene wired to the duplicated assets
            BossDuelLabBuilder.BuildSceneForProfile(newProfile, newScenePath);
            Debug.Log($"[Boss Fight Framework] Duplicated demo profile '{sourceProfile.DisplayName}' to '{newProfilePath}' with scene '{newScenePath}'.");
            return newProfile;
        }

        private static T CloneAssetToFolder<T>(T source, string folderPath, Dictionary<Object, Object> cache) where T : Object
        {
            if (source == null) return null;
            if (cache.TryGetValue(source, out Object existingCopy) && existingCopy is T typedCopy)
            {
                return typedCopy;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            string fileName = Path.GetFileName(sourcePath);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"{source.name}.asset";
            }

            string targetPath = $"{folderPath}/{fileName}";
            if (AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                T loaded = AssetDatabase.LoadAssetAtPath<T>(targetPath);
                cache[source] = loaded;
                return loaded;
            }

            return source;
        }
    }
}
