using UnityEditor;
using UnityEngine;

namespace TappyTale.Systems.Cloudscape.Editor
{
    public static class MenuItems
    {
        [MenuItem("GameObject/Tappy Tale/Cloudscape/Stylized Clouds", priority = 10)]
        public static void CreatePrefab()
        {
            string[] guids = AssetDatabase.FindAssets($"t:Prefab StylizedClouds_Template");

            if (guids.Length != 0)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "Stylized Clouds";
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var child = instance.transform.GetChild(0);
                child.gameObject.hideFlags = HideFlags.HideInHierarchy;
                Undo.RegisterCreatedObjectUndo(instance, "Create Stylized Clouds");
                Selection.activeObject = instance;
            }
            else
            {
                Debug.Log("Error: Cloud not find StylizedClouds_Template prefab");
                return;
            }
        }
    }
}
