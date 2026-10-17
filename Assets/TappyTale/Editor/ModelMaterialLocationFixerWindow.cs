#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TappyTale.EditorTools
{
    public sealed class ModelMaterialLocationFixerWindow : UnityEditor.EditorWindow
    {
        private static readonly HashSet<string> SupportedModelExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".fbx",
                ".obj",
                ".dae",
                ".3ds",
                ".dxf",
                ".blend",
                ".ma",
                ".mb",
                ".max"
            };

        [SerializeField]
        private DefaultAsset rootFolder;

        [SerializeField]
        private Vector2 scrollPosition;

        private readonly List<ModelScanResult> scanResults = new();

        private string statusMessage = "Sélectionne un dossier contenant les modèles à analyser.";
        private MessageType statusType = MessageType.Info;

        [MenuItem("Tools/Tappy Tale/Models/Fix Material Location")]
        private static void OpenWindow()
        {
            ModelMaterialLocationFixerWindow window =
                GetWindow<ModelMaterialLocationFixerWindow>();

            window.titleContent = new GUIContent("Material Location Fixer");
            window.minSize = new Vector2(620f, 420f);
            window.Show();
        }

        [MenuItem("Assets/Tappy Tale/Fix Model Material Location", true)]
        private static bool ValidateFixSelectedFolder()
        {
            return TryGetSelectedFolderPath(out _);
        }

        [MenuItem("Assets/Tappy Tale/Fix Model Material Location")]
        private static void FixSelectedFolder()
        {
            if (!TryGetSelectedFolderPath(out string folderPath))
            {
                EditorUtility.DisplayDialog(
                    "Material Location Fixer",
                    "Sélectionne un dossier situé sous Assets/.",
                    "OK");

                return;
            }

            int modifiedCount = FixFolderImmediately(folderPath);

            EditorUtility.DisplayDialog(
                "Material Location Fixer",
                modifiedCount > 0
                    ? $"{modifiedCount} modèle(s) corrigé(s) dans :\n{folderPath}"
                    : $"Aucun modèle ne nécessitait de correction dans :\n{folderPath}",
                "OK");
        }

        private void OnEnable()
        {
            TryAssignSelectedFolder();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawFolderSelection();
            DrawActions();
            DrawStatus();
            DrawResults();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField(
                "Model Material Location Fixer",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Cet outil analyse récursivement les modèles d’un dossier et remplace " +
                "leur ancien réglage Material Location par InPrefab. " +
                "Les fichiers FBX sources ne sont pas modifiés, seuls leurs réglages " +
                "d’import Unity dans les fichiers .meta sont mis à jour.",
                MessageType.Info);

            EditorGUILayout.Space(4f);
        }

        private void DrawFolderSelection()
        {
            EditorGUI.BeginChangeCheck();

            rootFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Root Folder",
                    "Dossier sous Assets/ à analyser récursivement."),
                rootFolder,
                typeof(DefaultAsset),
                false);

            if (EditorGUI.EndChangeCheck())
            {
                scanResults.Clear();

                if (rootFolder == null)
                {
                    SetStatus(
                        "Sélectionne un dossier contenant les modèles à analyser.",
                        MessageType.Info);
                }
                else if (!TryGetRootFolderPath(out _))
                {
                    rootFolder = null;

                    SetStatus(
                        "L’objet sélectionné doit être un dossier situé sous Assets/.",
                        MessageType.Error);
                }
                else
                {
                    SetStatus(
                        "Dossier sélectionné. Lance le scan pour afficher les modèles concernés.",
                        MessageType.Info);
                }
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = rootFolder != null;

            if (GUILayout.Button("Scanner", GUILayout.Height(30f)))
            {
                ScanSelectedFolder();
            }

            GUI.enabled = rootFolder != null && scanResults.Count > 0;

            if (GUILayout.Button("Corriger les modèles", GUILayout.Height(30f)))
            {
                FixScannedModels();
            }

            GUI.enabled = rootFolder != null;

            if (GUILayout.Button("Scanner et corriger", GUILayout.Height(30f)))
            {
                ScanSelectedFolder();

                if (scanResults.Count > 0)
                {
                    FixScannedModels();
                }
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatus()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        private void DrawResults()
        {
            if (scanResults.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField(
                $"Modèles à corriger : {scanResults.Count}",
                EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (ModelScanResult result in scanResults)
            {
                DrawResult(result);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawResult(ModelScanResult result)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUIContent icon = EditorGUIUtility.IconContent("Mesh Icon");
            GUILayout.Label(icon, GUILayout.Width(22f), GUILayout.Height(22f));

            EditorGUILayout.BeginVertical();

            EditorGUILayout.LabelField(
                Path.GetFileName(result.AssetPath),
                EditorStyles.boldLabel);

            EditorGUILayout.SelectableLabel(
                result.AssetPath,
                EditorStyles.miniLabel,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Select", GUILayout.Width(60f)))
            {
                UnityEngine.Object asset =
                    AssetDatabase.LoadMainAssetAtPath(result.AssetPath);

                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ScanSelectedFolder()
        {
            if (!TryGetRootFolderPath(out string folderPath))
            {
                SetStatus(
                    "Sélectionne un dossier valide situé sous Assets/.",
                    MessageType.Error);

                return;
            }

            scanResults.Clear();

            List<string> modelPaths = FindModelAssetPaths(folderPath);

            try
            {
                for (int index = 0; index < modelPaths.Count; index++)
                {
                    string assetPath = modelPaths[index];

                    EditorUtility.DisplayProgressBar(
                        "Material Location Fixer",
                        $"Analyse de {Path.GetFileName(assetPath)}",
                        modelPaths.Count == 0
                            ? 1f
                            : (float)index / modelPaths.Count);

                    ModelImporter importer =
                        AssetImporter.GetAtPath(assetPath) as ModelImporter;

                    if (importer == null)
                    {
                        continue;
                    }

                    if (importer.materialLocation ==
                        ModelImporterMaterialLocation.InPrefab)
                    {
                        continue;
                    }

                    scanResults.Add(new ModelScanResult(assetPath));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (modelPaths.Count == 0)
            {
                SetStatus(
                    $"Aucun fichier de modèle trouvé dans {folderPath}.",
                    MessageType.Warning);

                return;
            }

            if (scanResults.Count == 0)
            {
                SetStatus(
                    $"{modelPaths.Count} modèle(s) analysé(s). " +
                    "Tous utilisent déjà Material Location: InPrefab.",
                    MessageType.Info);

                return;
            }

            SetStatus(
                $"{modelPaths.Count} modèle(s) analysé(s), " +
                $"{scanResults.Count} modèle(s) à corriger.",
                MessageType.Warning);
        }

        private void FixScannedModels()
        {
            if (scanResults.Count == 0)
            {
                SetStatus(
                    "Aucun modèle à corriger. Lance d’abord un scan.",
                    MessageType.Info);

                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Corriger Material Location",
                $"Modifier les réglages d’import de {scanResults.Count} modèle(s) ?\n\n" +
                "Material Location sera défini sur InPrefab.\n" +
                "Unity réimportera ensuite les modèles concernés.",
                "Corriger",
                "Annuler");

            if (!confirmed)
            {
                return;
            }

            List<string> pathsToFix = new(scanResults.Count);

            foreach (ModelScanResult result in scanResults)
            {
                pathsToFix.Add(result.AssetPath);
            }

            int modifiedCount = ApplyFix(pathsToFix);

            scanResults.Clear();

            SetStatus(
                modifiedCount > 0
                    ? $"{modifiedCount} modèle(s) corrigé(s) et réimporté(s)."
                    : "Aucun modèle n’a été modifié.",
                modifiedCount > 0
                    ? MessageType.Info
                    : MessageType.Warning);
        }

        private static int FixFolderImmediately(string folderPath)
        {
            List<string> paths = FindModelAssetPaths(folderPath);
            return ApplyFix(paths);
        }

        private static int ApplyFix(IReadOnlyList<string> assetPaths)
        {
            if (assetPaths.Count == 0)
            {
                return 0;
            }

            List<string> modifiedPaths = new();
            int undoGroup = Undo.GetCurrentGroup();

            Undo.SetCurrentGroupName("Fix Model Material Locations");

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int index = 0; index < assetPaths.Count; index++)
                {
                    string assetPath = assetPaths[index];

                    EditorUtility.DisplayProgressBar(
                        "Material Location Fixer",
                        $"Correction de {Path.GetFileName(assetPath)}",
                        (float)index / assetPaths.Count);

                    ModelImporter importer =
                        AssetImporter.GetAtPath(assetPath) as ModelImporter;

                    if (importer == null)
                    {
                        continue;
                    }

                    if (importer.materialLocation ==
                        ModelImporterMaterialLocation.InPrefab)
                    {
                        continue;
                    }

                    Undo.RecordObject(
                        importer,
                        $"Fix Material Location: {Path.GetFileName(assetPath)}");

                    importer.materialLocation =
                        ModelImporterMaterialLocation.InPrefab;

                    EditorUtility.SetDirty(importer);

                    // Écrit les nouveaux réglages dans le fichier .meta sans
                    // lancer immédiatement un reimport pour chaque modèle.
                    AssetDatabase.WriteImportSettingsIfDirty(assetPath);

                    modifiedPaths.Add(assetPath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                EditorUtility.DisplayDialog(
                    "Material Location Fixer",
                    "Une erreur est survenue pendant la correction.\n\n" +
                    exception.Message,
                    "OK");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }

            ReimportModifiedModels(modifiedPaths);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[MaterialLocationFixer] {modifiedPaths.Count} modèle(s) corrigé(s).");

            return modifiedPaths.Count;
        }

        private static void ReimportModifiedModels(IReadOnlyList<string> assetPaths)
        {
            for (int index = 0; index < assetPaths.Count; index++)
            {
                string assetPath = assetPaths[index];

                try
                {
                    EditorUtility.DisplayProgressBar(
                        "Material Location Fixer",
                        $"Réimport de {Path.GetFileName(assetPath)}",
                        assetPaths.Count == 0
                            ? 1f
                            : (float)index / assetPaths.Count);

                    AssetDatabase.ImportAsset(
                        assetPath,
                        ImportAssetOptions.ForceUpdate);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"[MaterialLocationFixer] Échec du réimport de " +
                        $"{assetPath}:\n{exception}");
                }
            }

            EditorUtility.ClearProgressBar();
        }

        private static List<string> FindModelAssetPaths(string rootPath)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Model",
                new[] { rootPath });

            List<string> paths = new(guids.Length);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (!IsSupportedModelFile(assetPath))
                {
                    continue;
                }

                paths.Add(assetPath);
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private static bool IsSupportedModelFile(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            string extension = Path.GetExtension(assetPath);
            return SupportedModelExtensions.Contains(extension);
        }

        private bool TryGetRootFolderPath(out string folderPath)
        {
            folderPath = rootFolder != null
                ? AssetDatabase.GetAssetPath(rootFolder)
                : string.Empty;

            return IsValidAssetFolder(folderPath);
        }

        private void TryAssignSelectedFolder()
        {
            if (!TryGetSelectedFolderPath(out string folderPath))
            {
                return;
            }

            rootFolder =
                AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
        }

        private static bool TryGetSelectedFolderPath(out string folderPath)
        {
            folderPath = string.Empty;

            UnityEngine.Object selectedObject = Selection.activeObject;

            if (selectedObject == null)
            {
                return false;
            }

            string selectedPath = AssetDatabase.GetAssetPath(selectedObject);

            if (!IsValidAssetFolder(selectedPath))
            {
                return false;
            }

            folderPath = selectedPath;
            return true;
        }

        private static bool IsValidAssetFolder(string assetPath)
        {
            return !string.IsNullOrWhiteSpace(assetPath)
                    && assetPath.StartsWith(
                        "Assets",
                        StringComparison.Ordinal)
                    && AssetDatabase.IsValidFolder(assetPath);
        }

        private void SetStatus(string message, MessageType messageType)
        {
            statusMessage = message;
            statusType = messageType;
            Repaint();
        }

        [Serializable]
        private sealed class ModelScanResult
        {
            [SerializeField]
            private string assetPath;

            public string AssetPath => assetPath;

            public ModelScanResult(string assetPath)
            {
                this.assetPath = assetPath;
            }
        }
    }
}

#endif
