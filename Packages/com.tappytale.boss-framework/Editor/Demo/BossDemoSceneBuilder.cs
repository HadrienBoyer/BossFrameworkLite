using TappyTale.BossFight.Combat;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Demo;
using TappyTale.BossFight.Editor.Install;
using TappyTale.BossFight.Feedback;
using TappyTale.BossFight.Player;
using TappyTale.BossFight.Telegraphs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TappyTale.BossFight.Editor.Demo
{
    public static class BossDemoSceneBuilder
    {
        private const string DemoFolder = BossFrameworkInstaller.DemoFolder;
        public const string ScenePath = BossFrameworkInstaller.BossFightDemoScenePath;

        [MenuItem("Tools/Tappy Tale/Boss Fight/Create Boss Fight Demo", priority = 21)]
        public static void CreateDemoScene()
        {
            BossFrameworkInstaller.InstallOrRepair(overwriteExisting: false);
            BossFrameworkInstaller.EnsureFolderHierarchy(DemoFolder);

            Material floorMat = BossFrameworkInstaller.GetOrCreateMaterial("Mat_ArenaFloor", new Color(0.14f, 0.16f, 0.2f, 1f));
            Material bossMat = BossFrameworkInstaller.GetOrCreateMaterial("Mat_BossBody", new Color(0.82f, 0.22f, 0.24f, 1f));
            Material playerMat = BossFrameworkInstaller.GetOrCreateMaterial("Mat_PlayerBody", new Color(0.22f, 0.64f, 0.92f, 1f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Arena";
            ground.transform.localScale = Vector3.one * 3.5f;
            Renderer groundRenderer = ground.GetComponent<Renderer>();
            if (groundRenderer != null && floorMat != null) groundRenderer.sharedMaterial = floorMat;

            PlayerDefinition playerDef = FindFirstAsset<PlayerDefinition>();
            GameObject player = BossDuelLabBuilder.CreateDuelPlayer(playerDef, playerMat, new Vector3(0f, 1.05f, -7f));
            player.AddComponent<BossDemoHealthReceiver>();

            BossDefinition bossDef = FindFirstAsset<BossDefinition>();
            BossPayloadLibrary payloads = FindFirstAsset<BossPayloadLibrary>();
            GameObject boss = BossDuelLabBuilder.CreateDuelBoss(bossDef, payloads, bossMat, player.transform, new Vector3(0f, 1.2f, 2f));

            GameObject cameraObject = new("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 12f, -15f), Quaternion.Euler(30f, 0f, 0f));
            camera.fieldOfView = 55f;

            BossDuelCameraController duelCam = cameraObject.AddComponent<BossDuelCameraController>();
            SerializedObject camSo = new(duelCam);
            camSo.FindProperty("playerTarget").objectReferenceValue = player.transform;
            camSo.FindProperty("bossTarget").objectReferenceValue = boss.transform;
            camSo.ApplyModifiedPropertiesWithoutUndo();

            PlayerController playerController = player.GetComponent<PlayerController>();
            SerializedObject playerSo = new(playerController);
            playerSo.FindProperty("cameraTransform").objectReferenceValue = cameraObject.transform;
            playerSo.FindProperty("bossDirector").objectReferenceValue = boss.GetComponent<BossDirector>();
            playerSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject lightObject = new("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            BossDemoProfile profile = AssetDatabase.LoadAssetAtPath<BossDemoProfile>(
                BossFrameworkInstaller.DataFolder + "/DemoProfile_BossFightDemo.asset");

            GameObject managerObject = new("BossDuelArenaManager");
            BossDuelArenaController arenaController = managerObject.AddComponent<BossDuelArenaController>();
            SerializedObject arenaSo = new(arenaController);
            arenaSo.FindProperty("bossDirector").objectReferenceValue = boss.GetComponent<BossDirector>();
            arenaSo.FindProperty("playerController").objectReferenceValue = playerController;
            arenaSo.FindProperty("cameraController").objectReferenceValue = duelCam;
            arenaSo.FindProperty("demoProfile").objectReferenceValue = profile;
            arenaSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = boss;
            Debug.Log($"[Boss Fight Framework] Boss Fight demo scene created at {ScenePath}.");
        }

        private static T FindFirstAsset<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}