#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TappyTale.EditorTools.GameStudio
{
    internal static class GameStudioAuditExporter
    {
        private const string Folder =
            "Assets/TappyTale/Editor/GameStudioTools/Generated";
        private const string PathJson =
            Folder + "/GameStudioTools_Audit.json";

        [Serializable]
        private sealed class AuditDto
        {
            public string generatedAtUtc;
            public int fileCount;
            public int cSharpCount;
            public int assetCount;
            public int prefabCount;
            public int sceneCount;
            public string[] rootModules;
            public string[] editorWindows;
            public string[] customEditors;
            public string[] menuItems;
            public ToolDto[] tools;
        }

        [Serializable]
        private sealed class ToolDto
        {
            public string id;
            public string displayName;
            public string group;
            public bool detected;
            public string[] folders;
            public string[] types;
            public string[] menuItems;
        }

        public static void Export(
            GameStudioScanResult scan)
        {
            if (scan == null)
            {
                return;
            }

            EnsureFolder(
                "Assets/TappyTale/Editor/GameStudioTools",
                "Generated");

            AuditDto dto = new()
            {
                generatedAtUtc =
                    DateTime.UtcNow.ToString("O"),
                fileCount = scan.FileCount,
                cSharpCount = scan.CSharpCount,
                assetCount = scan.AssetCount,
                prefabCount = scan.PrefabCount,
                sceneCount = scan.SceneCount,
                rootModules = scan.RootModules.ToArray(),
                editorWindows =
                    scan.EditorWindowTypes
                        .Select(x => x.FullName ?? x.Name)
                        .ToArray(),
                customEditors =
                    scan.CustomEditorTypes
                        .Select(x => x.FullName ?? x.Name)
                        .ToArray(),
                menuItems =
                    scan.MenuItems.ToArray(),
                tools =
                    scan.Tools.Select(
                        x => new ToolDto
                        {
                            id = x.Id,
                            displayName = x.DisplayName,
                            group = x.Group,
                            detected = x.Detected,
                            folders = x.DetectedFolders.ToArray(),
                            types = x.DetectedTypes.ToArray(),
                            menuItems = x.DetectedMenuItems.ToArray()
                        })
                    .ToArray()
            };

            string json =
                JsonUtility.ToJson(
                    dto,
                    true);

            File.WriteAllText(
                PathJson,
                json);

            AssetDatabase.ImportAsset(
                PathJson,
                ImportAssetOptions.ForceUpdate);

            UnityEngine.Object asset =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    PathJson);

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            Debug.Log(
                $"[Game Studio Tools] Audit exported to {PathJson}");
        }

        private static void EnsureFolder(
            string parent,
            string child)
        {
            string path =
                $"{parent}/{child}";

            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(
                    parent,
                    child);
            }
        }
    }
}
#endif
