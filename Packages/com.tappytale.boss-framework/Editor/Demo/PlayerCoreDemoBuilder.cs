using TappyTale.BossFight.Combat;
using TappyTale.BossFight.Demo;
using TappyTale.BossFight.Editor.Install;
using TappyTale.BossFight.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TappyTale.BossFight.Editor.Demo
{
    public static class PlayerCoreDemoBuilder
    {
        private const string DataFolder = BossFrameworkInstaller.DataFolder;
        private const string DemoFolder = BossFrameworkInstaller.DemoFolder;
        private const string DefinitionPath = DataFolder + "/Player_Starter.asset";
        public const string ScenePath = BossFrameworkInstaller.PlayerCoreDemoScenePath;

        [MenuItem("Tools/Tappy Tale/Boss Fight/Create Player Core Demo", priority = 22)]
        public static void CreateDemo()
        {
            BossFrameworkInstaller.InstallOrRepair(overwriteExisting: false);
            BossFrameworkInstaller.EnsureFolderHierarchy(DataFolder);
            BossFrameworkInstaller.EnsureFolderHierarchy(DemoFolder);
            PlayerDefinition definition = LoadOrCreateDefinition();

            Material floorMat = BossFrameworkInstaller.GetOrCreateMaterial("Mat_ArenaFloor", new Color(0.14f, 0.16f, 0.2f, 1f));
            Material wallMat = BossFrameworkInstaller.GetOrCreateMaterial("Mat_ArenaWall", new Color(0.2f, 0.23f, 0.29f, 1f));
            Material playerMat = BossFrameworkInstaller.GetOrCreateMaterial("Mat_PlayerBody", new Color(0.22f, 0.64f, 0.92f, 1f));
            Material dummyMat = BossFrameworkInstaller.GetOrCreateMaterial("Mat_BossBody", new Color(0.82f, 0.22f, 0.24f, 1f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Player Test Arena";
            ground.transform.localScale = Vector3.one * 4f;
            Renderer groundRenderer = ground.GetComponent<Renderer>();
            if (groundRenderer != null && floorMat != null) groundRenderer.sharedMaterial = floorMat;

            // Jump & Coyote Time test platforms
            for (int i = 0; i < 3; i++)
            {
                GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                platform.name = $"Jump_Platform_{i + 1:00}";
                platform.transform.position = new Vector3(-6f - i * 4f, 0.6f + i * 0.85f, 4f);
                platform.transform.localScale = new Vector3(2.8f, 1.2f, 2.8f);
                Renderer platRenderer = platform.GetComponent<Renderer>();
                if (platRenderer != null && wallMat != null) platRenderer.sharedMaterial = wallMat;
            }

            // Target Lock-On & Combat Training Dummy
            GameObject trainingDummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            trainingDummy.name = "Combat_Training_Dummy";
            trainingDummy.transform.position = new Vector3(0f, 1.1f, 6f);
            Renderer dummyRenderer = trainingDummy.GetComponent<Renderer>();
            if (dummyRenderer != null && dummyMat != null) dummyRenderer.sharedMaterial = dummyMat;
            BossDemoHealthReceiver dummyReceiver = trainingDummy.AddComponent<BossDemoHealthReceiver>();
            SerializedObject dummySo = new(dummyReceiver);
            dummySo.FindProperty("team").enumValueIndex = (int)DamageTeam.Boss;
            dummySo.FindProperty("maxHealth").floatValue = 500f;
            dummySo.ApplyModifiedPropertiesWithoutUndo();
            trainingDummy.AddComponent<LockOnTarget>();

            GameObject player = BossDuelLabBuilder.CreateDuelPlayer(definition, playerMat, new Vector3(0f, 1.05f, 0f));

            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 8f, -10f),
                Quaternion.Euler(30f, 0f, 0f));

            BossDuelCameraController cameraController = cameraObject.AddComponent<BossDuelCameraController>();
            SerializedObject camSo = new(cameraController);
            camSo.FindProperty("playerTarget").objectReferenceValue = player.transform;
            camSo.FindProperty("bossTarget").objectReferenceValue = trainingDummy.transform;
            camSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject lightObject = new("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            PlayerController controller = player.GetComponent<PlayerController>();
            SerializedObject serialized = new(controller);
            serialized.FindProperty("definition").objectReferenceValue = definition;
            serialized.FindProperty("cameraTransform").objectReferenceValue = cameraObject.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = player;
            Debug.Log($"[Boss Fight Framework] Player Core demo created at {ScenePath}. Move with WASD, jump with Space, dash with Left Shift, attack with J/K, lock-on with Tab.");
        }

        private static PlayerDefinition LoadOrCreateDefinition()
        {
            PlayerDefinition existing = AssetDatabase.LoadAssetAtPath<PlayerDefinition>(DefinitionPath);
            if (existing != null)
            {
                return existing;
            }

            PlayerDefinition definition = ScriptableObject.CreateInstance<PlayerDefinition>();
            AssetDatabase.CreateAsset(definition, DefinitionPath);
            AssetDatabase.SaveAssets();
            return definition;
        }
    }
}
