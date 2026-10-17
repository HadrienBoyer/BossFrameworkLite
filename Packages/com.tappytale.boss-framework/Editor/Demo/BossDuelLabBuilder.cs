using TappyTale.BossFight.Combat;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Demo;
using TappyTale.BossFight.Editor.Install;
using TappyTale.BossFight.Feedback;
using TappyTale.BossFight.Player;
using TappyTale.BossFight.Preferences;
using TappyTale.BossFight.Telegraphs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TappyTale.BossFight.Editor.Demo
{
    public static class BossDuelLabBuilder
    {
        public const string DefaultProfilePath = BossFrameworkInstaller.DataFolder + "/DemoProfile_BossDuelLab.asset";
        public const string DefaultScenePath = BossFrameworkInstaller.DuelLabScenePath;

        [MenuItem("Tools/Tappy Tale/Boss Fight/Create Boss Duel Lab (Reference Demo)", priority = 20)]
        public static void CreateDuelLabScene()
        {
            BossFrameworkInstaller.InstallOrRepair(overwriteExisting: false);
            BossDemoProfile profile = AssetDatabase.LoadAssetAtPath<BossDemoProfile>(DefaultProfilePath);
            BuildSceneForProfile(profile, DefaultScenePath);
        }

        public static void BuildSceneForProfile(BossDemoProfile profile, string overrideScenePath = null)
        {
            BossFrameworkInstaller.InstallOrRepair(overwriteExisting: false);

            if (profile == null)
            {
                profile = AssetDatabase.LoadAssetAtPath<BossDemoProfile>(DefaultProfilePath);
            }

            string scenePath = !string.IsNullOrWhiteSpace(overrideScenePath)
                ? overrideScenePath
                : (profile != null && !string.IsNullOrWhiteSpace(profile.ScenePath) ? profile.ScenePath : DefaultScenePath);

            string folder = System.IO.Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            BossFrameworkInstaller.EnsureFolderHierarchy(folder);

            Material floorMat = BossFrameworkInstaller.GetOrCreateMaterial("Mat_ArenaFloor", new Color(0.14f, 0.16f, 0.2f, 1f));
            Material wallMat = BossFrameworkInstaller.GetOrCreateMaterial("Mat_ArenaWall", new Color(0.2f, 0.23f, 0.29f, 1f));
            Material bossMat = BossFrameworkInstaller.GetOrCreateMaterial("Mat_BossBody", new Color(0.82f, 0.22f, 0.24f, 1f));
            Material playerMat = BossFrameworkInstaller.GetOrCreateMaterial("Mat_PlayerBody", new Color(0.22f, 0.64f, 0.92f, 1f));

            BossDefinition bossDef = profile != null && profile.BossDefinition != null
                ? profile.BossDefinition
                : AssetDatabase.LoadAssetAtPath<BossDefinition>(BossFrameworkInstaller.DataFolder + "/Boss_Starter.asset");
            BossPayloadLibrary payloads = profile != null && profile.PayloadLibrary != null
                ? profile.PayloadLibrary
                : AssetDatabase.LoadAssetAtPath<BossPayloadLibrary>(BossFrameworkInstaller.DataFolder + "/BossPayloadLibrary.asset");
            PlayerDefinition playerDef = profile != null && profile.PlayerDefinition != null
                ? profile.PlayerDefinition
                : AssetDatabase.LoadAssetAtPath<PlayerDefinition>(BossFrameworkInstaller.DataFolder + "/Player_Starter.asset");
            BossPreferencesRegistry prefsRegistry = AssetDatabase.LoadAssetAtPath<BossPreferencesRegistry>(
                BossFrameworkInstaller.DataFolder + "/PreferencesRegistry_Default.asset");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Environment & Arena
            GameObject environmentRoot = new("Arena_Environment");
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Duel_Arena_Floor";
            ground.transform.SetParent(environmentRoot.transform, false);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(36f, 0.5f, 36f);
            ApplyMaterial(ground, floorMat);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                Vector3 pos = new(Mathf.Cos(angle) * 16.5f, 1.5f, Mathf.Sin(angle) * 16.5f);
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = $"Arena_Pillar_{i + 1:00}";
                pillar.transform.SetParent(environmentRoot.transform, false);
                pillar.transform.position = pos;
                pillar.transform.localScale = new Vector3(1.4f, 3f, 1.4f);
                pillar.transform.LookAt(new Vector3(0f, 1.5f, 0f));
                ApplyMaterial(pillar, wallMat);
            }

            // 2. Lighting
            GameObject lightObject = new("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.96f, 0.9f, 1f);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            // 3. Player
            GameObject player = CreateDuelPlayer(playerDef, playerMat, new Vector3(0f, 1.05f, -7.5f));

            // 4. Boss
            GameObject boss = CreateDuelBoss(bossDef, payloads, bossMat, player.transform, new Vector3(0f, 1.2f, 3.5f));

            // 5. Duel Camera
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 56f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 6.5f, -15f), Quaternion.Euler(22f, 0f, 0f));

            BossDuelCameraController cameraController = cameraObject.AddComponent<BossDuelCameraController>();
            SerializedObject camSo = new(cameraController);
            camSo.FindProperty("playerTarget").objectReferenceValue = player.transform;
            camSo.FindProperty("bossTarget").objectReferenceValue = boss.transform;
            camSo.FindProperty("baseOffset").vector3Value = new Vector3(0f, 9.5f, -12.5f);
            camSo.ApplyModifiedPropertiesWithoutUndo();

            PlayerController playerController = player.GetComponent<PlayerController>();
            SerializedObject playerSo = new(playerController);
            playerSo.FindProperty("cameraTransform").objectReferenceValue = cameraObject.transform;
            playerSo.FindProperty("bossDirector").objectReferenceValue = boss.GetComponent<BossDirector>();
            playerSo.ApplyModifiedPropertiesWithoutUndo();

            // 6. Duel Arena Controller (HUD + Loop + Save Data integration)
            GameObject managerObject = new("BossDuelArenaManager");
            BossDuelArenaController arenaController = managerObject.AddComponent<BossDuelArenaController>();
            SerializedObject arenaSo = new(arenaController);
            arenaSo.FindProperty("bossDirector").objectReferenceValue = boss.GetComponent<BossDirector>();
            arenaSo.FindProperty("playerController").objectReferenceValue = playerController;
            arenaSo.FindProperty("cameraController").objectReferenceValue = cameraController;
            arenaSo.FindProperty("demoProfile").objectReferenceValue = profile;
            arenaSo.FindProperty("preferencesRegistry").objectReferenceValue = prefsRegistry;
            arenaSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            Selection.activeGameObject = managerObject;
            Debug.Log($"[Boss Fight Framework] Boss Duel Lab created at '{scenePath}'.");
        }

        public static GameObject CreateDuelPlayer(PlayerDefinition playerDef, Material playerMat, Vector3 position)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Duel Player";
            player.transform.position = position;
            player.transform.rotation = Quaternion.identity;
            ApplyMaterial(player, playerMat);

            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            CharacterController character = player.AddComponent<CharacterController>();
            character.height = 2f;
            character.radius = 0.45f;
            character.center = Vector3.zero;

            player.AddComponent<PlayerMotor>();
            player.AddComponent<LegacyPlayerInputSource>();
            player.AddComponent<AudioSource>();
            player.AddComponent<PlayerAnimatorAdapter>();
            player.AddComponent<PlayerFeedbackAdapter>();
            PlayerController controller = player.AddComponent<PlayerController>();

            // Facing Visor
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "FacingVisor";
            visor.transform.SetParent(player.transform, false);
            visor.transform.localPosition = new Vector3(0f, 0.45f, 0.38f);
            visor.transform.localScale = new Vector3(0.55f, 0.2f, 0.25f);
            Object.DestroyImmediate(visor.GetComponent<Collider>());

            // Melee Hitbox child
            GameObject hitboxObj = new("PlayerMeleeHitbox");
            hitboxObj.transform.SetParent(player.transform, false);
            hitboxObj.transform.localPosition = new Vector3(0f, 0.1f, 1.35f);
            BoxCollider hitboxCol = hitboxObj.AddComponent<BoxCollider>();
            hitboxCol.isTrigger = true;
            hitboxCol.size = new Vector3(1.8f, 1.6f, 2.2f);
            Rigidbody rb = hitboxObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            PlayerMeleeHitbox meleeHitbox = hitboxObj.AddComponent<PlayerMeleeHitbox>();

            SerializedObject playerSo = new(controller);
            playerSo.FindProperty("definition").objectReferenceValue = playerDef;
            playerSo.FindProperty("meleeHitbox").objectReferenceValue = meleeHitbox;
            playerSo.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        public static GameObject CreateDuelBoss(
            BossDefinition bossDef,
            BossPayloadLibrary payloads,
            Material bossMat,
            Transform playerTarget,
            Vector3 position)
        {
            GameObject boss = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            boss.name = "Sovereign Boss";
            boss.transform.position = position;
            boss.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            boss.transform.localScale = new Vector3(1.25f, 1.35f, 1.25f);
            ApplyMaterial(boss, bossMat);

            BossDirector director = boss.AddComponent<BossDirector>();
            boss.AddComponent<BossDamageReceiver>();
            boss.AddComponent<BossDashExecutor>();
            boss.AddComponent<AudioSource>();
            boss.AddComponent<BossTimelineFeedbackAdapter>();
            boss.AddComponent<BossTelegraphVisualAdapter>();

            LockOnTarget lockOnTarget = boss.AddComponent<LockOnTarget>();
            SerializedObject lockSo = new(lockOnTarget);
            lockSo.FindProperty("priority").enumValueIndex = (int)LockOnPriority.Boss;
            lockSo.FindProperty("aimOffset").vector3Value = new Vector3(0f, 0.8f, 0f);
            lockSo.ApplyModifiedPropertiesWithoutUndo();

            // Crown / Shoulder visual indicator
            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crown.name = "BossMantle";
            crown.transform.SetParent(boss.transform, false);
            crown.transform.localPosition = new Vector3(0f, 0.65f, 0.25f);
            crown.transform.localScale = new Vector3(1.3f, 0.25f, 0.6f);
            Object.DestroyImmediate(crown.GetComponent<Collider>());

            // Boss Melee Hitbox
            GameObject hitboxObject = new("BossMeleeHitbox");
            hitboxObject.transform.SetParent(boss.transform, false);
            hitboxObject.transform.localPosition = new Vector3(0f, 0f, 1.6f);
            BoxCollider hitbox = hitboxObject.AddComponent<BoxCollider>();
            hitbox.isTrigger = true;
            hitbox.size = new Vector3(2.4f, 2f, 3.2f);
            Rigidbody body = hitboxObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            hitboxObject.AddComponent<BossMeleeHitbox>();

            SerializedObject directorObject = new(director);
            directorObject.FindProperty("target").objectReferenceValue = playerTarget;
            directorObject.FindProperty("definition").objectReferenceValue = bossDef;
            directorObject.FindProperty("payloadLibrary").objectReferenceValue = payloads;
            directorObject.ApplyModifiedPropertiesWithoutUndo();

            return boss;
        }

        private static void ApplyMaterial(GameObject target, Material material)
        {
            if (target == null || material == null) return;
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }
    }
}
