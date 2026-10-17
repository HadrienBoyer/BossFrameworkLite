#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace TappyTale.EditorTools
{
    public sealed class BatchTextureSourceResizerWindow : UnityEditor.EditorWindow
    {
        private const int DefaultMaximumSize = 1024;

        private static readonly HashSet<string> SupportedExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".png",
                ".tga",
                ".tif",
                ".tiff"
            };

        [SerializeField]
        private DefaultAsset rootFolder;

        [SerializeField]
        private int maximumSize = DefaultMaximumSize;

        [SerializeField]
        private bool includeSubfolders = true;

        [SerializeField]
        private bool createBackups = true;

        [SerializeField]
        private bool skipNormalMaps = false;

        [SerializeField]
        private bool preserveAlpha = true;

        [MenuItem("Tools/Tappy Tale/Textures/Batch Resize Source Textures")]
        private static void Open()
        {
            BatchTextureSourceResizerWindow window =
                GetWindow<BatchTextureSourceResizerWindow>();

            window.titleContent = new GUIContent("Texture Resizer");
            window.minSize = new Vector2(460f, 340f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField(
                "Batch Source Texture Resizer",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Redimensionne physiquement les fichiers sources PNG, TGA, TIF "
                + "et TIFF. Les fichiers .meta et les réglages d’importation "
                + "sont conservés.",
                MessageType.Info);

            EditorGUILayout.Space(6f);

            rootFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Root Folder",
                rootFolder,
                typeof(DefaultAsset),
                false);

            maximumSize = EditorGUILayout.IntPopup(
                "Maximum Size",
                maximumSize,
                new[]
                {
                    "256 px",
                    "512 px",
                    "1024 px",
                    "2048 px",
                    "4096 px"
                },
                new[]
                {
                    256,
                    512,
                    1024,
                    2048,
                    4096
                });

            includeSubfolders = EditorGUILayout.Toggle(
                "Include Subfolders",
                includeSubfolders);

            createBackups = EditorGUILayout.Toggle(
                "Create Backups",
                createBackups);

            skipNormalMaps = EditorGUILayout.Toggle(
                "Skip Normal Maps",
                skipNormalMaps);

            preserveAlpha = EditorGUILayout.Toggle(
                "Preserve Alpha",
                preserveAlpha);

            EditorGUILayout.Space(10f);

            string folderPath = GetSelectedFolderPath();

            using (new EditorGUI.DisabledScope(
                       string.IsNullOrWhiteSpace(folderPath)))
            {
                if (GUILayout.Button(
                        "Analyze Folder",
                        GUILayout.Height(30f)))
                {
                    Analyze(folderPath);
                }

                EditorGUILayout.Space(4f);

                GUI.backgroundColor = new Color(0.65f, 1f, 0.65f);

                if (GUILayout.Button(
                        $"Resize Sources to Maximum {maximumSize}px",
                        GUILayout.Height(36f)))
                {
                    ResizeFolder(folderPath);
                }

                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(8f);

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                EditorGUILayout.HelpBox(
                    "Sélectionne un dossier situé dans Assets.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField(
                    "Selected path",
                    folderPath);
            }

            EditorGUILayout.Space(6f);

            EditorGUILayout.HelpBox(
                "Cette opération remplace les images originales. Active "
                + "Create Backups ou commit le projet dans Git avant de lancer "
                + "le traitement.",
                MessageType.Warning);
        }

        private string GetSelectedFolderPath()
        {
            if (rootFolder == null)
            {
                return string.Empty;
            }

            string assetPath = AssetDatabase.GetAssetPath(rootFolder);

            if (string.IsNullOrWhiteSpace(assetPath)
                || !AssetDatabase.IsValidFolder(assetPath))
            {
                return string.Empty;
            }

            return assetPath.Replace('\\', '/');
        }

        private void Analyze(string rootAssetPath)
        {
            List<TextureFileEntry> entries =
                FindTextureFiles(rootAssetPath);

            int oversizedCount = 0;
            long totalSourceBytes = 0;

            foreach (TextureFileEntry entry in entries)
            {
                totalSourceBytes += new FileInfo(entry.AbsolutePath).Length;

                if (!TryGetTextureSize(
                        entry.AssetPath,
                        out int width,
                        out int height))
                {
                    continue;
                }

                if (Mathf.Max(width, height) > maximumSize)
                {
                    oversizedCount++;
                }
            }

            string sizeText = EditorUtility.FormatBytes(totalSourceBytes);

            EditorUtility.DisplayDialog(
                "Texture analysis",
                $"Supported textures: {entries.Count}\n"
                + $"Larger than {maximumSize}px: {oversizedCount}\n"
                + $"Source size: {sizeText}",
                "OK");
        }

        private void ResizeFolder(string rootAssetPath)
        {
            List<TextureFileEntry> entries =
                FindTextureFiles(rootAssetPath);

            if (entries.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Texture Resizer",
                    "Aucune texture compatible trouvée.",
                    "OK");

                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Resize source textures",
                $"Redimensionner les textures dépassant {maximumSize}px ?\n\n"
                + $"Textures compatibles trouvées : {entries.Count}\n"
                + $"Dossier : {rootAssetPath}",
                "Resize",
                "Cancel");

            if (!confirmed)
            {
                return;
            }

            string backupRoot = createBackups
                ? CreateBackupRoot()
                : string.Empty;

            int resizedCount = 0;
            int skippedCount = 0;
            int failedCount = 0;

            var changedAssets = new List<string>();

            try
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    TextureFileEntry entry = entries[index];

                    float progress = entries.Count <= 1
                        ? 1f
                        : index / (float)(entries.Count - 1);

                    bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                        "Resizing source textures",
                        $"{index + 1}/{entries.Count} - {entry.AssetPath}",
                        progress);

                    if (cancelled)
                    {
                        break;
                    }

                    ResizeResult result = ResizeTextureFile(
                        entry,
                        rootAssetPath,
                        backupRoot);

                    switch (result)
                    {
                        case ResizeResult.Resized:
                            resizedCount++;
                            changedAssets.Add(entry.AssetPath);
                            break;

                        case ResizeResult.Skipped:
                            skippedCount++;
                            break;

                        case ResizeResult.Failed:
                            failedCount++;
                            break;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ReimportChangedAssets(changedAssets);

            string backupMessage = createBackups
                ? $"\nBackup : {backupRoot}"
                : string.Empty;

            EditorUtility.DisplayDialog(
                "Texture resize complete",
                $"Resized: {resizedCount}\n"
                + $"Skipped: {skippedCount}\n"
                + $"Failed: {failedCount}"
                + backupMessage,
                "OK");

            Debug.Log(
                $"[BatchTextureSourceResizer] Complete. "
                + $"Resized={resizedCount}, "
                + $"Skipped={skippedCount}, "
                + $"Failed={failedCount}.");
        }

        private ResizeResult ResizeTextureFile(
            TextureFileEntry entry,
            string rootAssetPath,
            string backupRoot)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(entry.AssetPath) as TextureImporter;

            if (importer == null)
            {
                Debug.LogWarning(
                    $"[BatchTextureSourceResizer] No TextureImporter: "
                    + entry.AssetPath);

                return ResizeResult.Failed;
            }

            if (skipNormalMaps
                && importer.textureType == TextureImporterType.NormalMap)
            {
                return ResizeResult.Skipped;
            }

            if (!TryGetTextureSize(
                    entry.AssetPath,
                    out int sourceWidth,
                    out int sourceHeight))
            {
                Debug.LogWarning(
                    $"[BatchTextureSourceResizer] Cannot read dimensions: "
                    + entry.AssetPath);

                return ResizeResult.Failed;
            }

            int largestDimension = Mathf.Max(sourceWidth, sourceHeight);

            if (largestDimension <= maximumSize)
            {
                return ResizeResult.Skipped;
            }

            CalculateTargetSize(
                sourceWidth,
                sourceHeight,
                maximumSize,
                out int targetWidth,
                out int targetHeight);

            string importerJson = EditorJsonUtility.ToJson(importer);

            Texture2D resizedTexture = null;

            try
            {
                PrepareImporterForSourceRead(importer);

                Texture2D sourceTexture =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(entry.AssetPath);

                if (sourceTexture == null)
                {
                    Debug.LogError(
                        $"[BatchTextureSourceResizer] Cannot load texture: "
                        + entry.AssetPath);

                    return ResizeResult.Failed;
                }

                resizedTexture = ResizeTexture(
                    sourceTexture,
                    targetWidth,
                    targetHeight,
                    preserveAlpha);

                if (resizedTexture == null)
                {
                    return ResizeResult.Failed;
                }

                byte[] encodedBytes = EncodeTexture(
                    resizedTexture,
                    entry.Extension);

                if (encodedBytes == null || encodedBytes.Length == 0)
                {
                    Debug.LogError(
                        $"[BatchTextureSourceResizer] Encoding failed: "
                        + entry.AssetPath);

                    return ResizeResult.Failed;
                }

                if (createBackups)
                {
                    BackupSourceFile(
                        entry,
                        rootAssetPath,
                        backupRoot);
                }

                File.WriteAllBytes(
                    entry.AbsolutePath,
                    encodedBytes);

                Debug.Log(
                    $"[BatchTextureSourceResizer] "
                    + $"{entry.AssetPath}: "
                    + $"{sourceWidth}x{sourceHeight} -> "
                    + $"{targetWidth}x{targetHeight}");

                return ResizeResult.Resized;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[BatchTextureSourceResizer] Failed: "
                    + $"{entry.AssetPath}\n{exception}");

                return ResizeResult.Failed;
            }
            finally
            {
                if (resizedTexture != null)
                {
                    DestroyImmediate(resizedTexture);
                }

                RestoreImporter(entry.AssetPath, importerJson);
            }
        }

        private static void PrepareImporterForSourceRead(
            TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;

            importer.maxTextureSize = 16384;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;

            importer.SaveAndReimport();
        }

        private static void RestoreImporter(
            string assetPath,
            string importerJson)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (importer == null)
            {
                return;
            }

            EditorJsonUtility.FromJsonOverwrite(
                importerJson,
                importer);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static Texture2D ResizeTexture(
            Texture2D source,
            int width,
            int height,
            bool useAlpha)
        {
            RenderTextureDescriptor descriptor =
                new RenderTextureDescriptor(
                    width,
                    height,
                    GraphicsFormat.R8G8B8A8_UNorm,
                    0)
                {
                    msaaSamples = 1,
                    mipCount = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    sRGB = false
                };

            RenderTexture renderTexture =
                RenderTexture.GetTemporary(descriptor);

            RenderTexture previous =
                RenderTexture.active;

            try
            {
                renderTexture.filterMode = FilterMode.Bilinear;

                Graphics.Blit(
                    source,
                    renderTexture);

                RenderTexture.active = renderTexture;

                TextureFormat format = useAlpha
                    ? TextureFormat.RGBA32
                    : TextureFormat.RGB24;

                var result = new Texture2D(
                    width,
                    height,
                    format,
                    false,
                    true);

                result.ReadPixels(
                    new Rect(0f, 0f, width, height),
                    0,
                    0,
                    false);

                result.Apply(false, false);

                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static byte[] EncodeTexture(
            Texture2D texture,
            string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".png":
                    return ImageConversion.EncodeToPNG(texture);

                case ".tga":
                    return ImageConversion.EncodeToTGA(texture);

                case ".tif":
                case ".tiff":
                    return TiffRgbaEncoder.Encode(texture);

                default:
                    throw new NotSupportedException(
                        $"Unsupported texture format: {extension}");
            }
        }

        private void BackupSourceFile(
            TextureFileEntry entry,
            string rootAssetPath,
            string backupRoot)
        {
            string rootAbsolutePath =
                AssetPathToAbsolutePath(rootAssetPath);

            string relativePath = Path.GetRelativePath(
                rootAbsolutePath,
                entry.AbsolutePath);

            string destinationPath =
                Path.Combine(backupRoot, relativePath);

            string destinationDirectory =
                Path.GetDirectoryName(destinationPath);

            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(
                entry.AbsolutePath,
                destinationPath,
                true);
        }

        private static string CreateBackupRoot()
        {
            string timestamp =
                DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string path = Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName,
                "TextureResizeBackups",
                timestamp);

            Directory.CreateDirectory(path);
            return path;
        }

        private List<TextureFileEntry> FindTextureFiles(
            string rootAssetPath)
        {
            string rootAbsolutePath =
                AssetPathToAbsolutePath(rootAssetPath);

            SearchOption searchOption = includeSubfolders
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            return Directory
                .EnumerateFiles(rootAbsolutePath, "*.*", searchOption)
                .Where(path =>
                    SupportedExtensions.Contains(
                        Path.GetExtension(path)))
                .Select(path =>
                {
                    string absolutePath =
                        Path.GetFullPath(path);

                    string assetPath =
                        AbsolutePathToAssetPath(absolutePath);

                    return new TextureFileEntry(
                        assetPath,
                        absolutePath,
                        Path.GetExtension(absolutePath));
                })
                .OrderBy(entry => entry.AssetPath)
                .ToList();
        }

        private static bool TryGetTextureSize(
            string assetPath,
            out int width,
            out int height)
        {
            width = 0;
            height = 0;

            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (importer == null)
            {
                return false;
            }

            importer.GetSourceTextureWidthAndHeight(
                out width,
                out height);

            return width > 0 && height > 0;
        }

        private static void CalculateTargetSize(
            int sourceWidth,
            int sourceHeight,
            int maxSize,
            out int targetWidth,
            out int targetHeight)
        {
            float scale = Mathf.Min(
                maxSize / (float)sourceWidth,
                maxSize / (float)sourceHeight);

            scale = Mathf.Min(scale, 1f);

            targetWidth = Mathf.Max(
                1,
                Mathf.RoundToInt(sourceWidth * scale));

            targetHeight = Mathf.Max(
                1,
                Mathf.RoundToInt(sourceHeight * scale));
        }

        private static void ReimportChangedAssets(
            IReadOnlyCollection<string> changedAssets)
        {
            if (changedAssets.Count == 0)
            {
                return;
            }

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (string assetPath in changedAssets)
                {
                    AssetDatabase.ImportAsset(
                        assetPath,
                        ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceUpdate);
            }
        }

        private static string AssetPathToAbsolutePath(
            string assetPath)
        {
            string projectRoot =
                Directory.GetParent(Application.dataPath)!.FullName;

            return Path.GetFullPath(
                Path.Combine(projectRoot, assetPath));
        }

        private static string AbsolutePathToAssetPath(
            string absolutePath)
        {
            string normalizedAbsolute =
                Path.GetFullPath(absolutePath)
                    .Replace('\\', '/');

            string normalizedDataPath =
                Path.GetFullPath(Application.dataPath)
                    .Replace('\\', '/');

            if (!normalizedAbsolute.StartsWith(
                    normalizedDataPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"File is outside Assets: {absolutePath}");
            }

            return "Assets"
                   + normalizedAbsolute.Substring(
                       normalizedDataPath.Length);
        }

        private readonly struct TextureFileEntry
        {
            public TextureFileEntry(
                string assetPath,
                string absolutePath,
                string extension)
            {
                AssetPath = assetPath;
                AbsolutePath = absolutePath;
                Extension = extension;
            }

            public string AssetPath { get; }

            public string AbsolutePath { get; }

            public string Extension { get; }
        }

        private enum ResizeResult
        {
            Resized,
            Skipped,
            Failed
        }

        private static class TiffRgbaEncoder
        {
            private const ushort TiffMagicNumber = 42;
            private const ushort EntryCount = 13;

            private const ushort TypeShort = 3;
            private const ushort TypeLong = 4;

            private const ushort TagImageWidth = 256;
            private const ushort TagImageLength = 257;
            private const ushort TagBitsPerSample = 258;
            private const ushort TagCompression = 259;
            private const ushort TagPhotometricInterpretation = 262;
            private const ushort TagStripOffsets = 273;
            private const ushort TagSamplesPerPixel = 277;
            private const ushort TagRowsPerStrip = 278;
            private const ushort TagStripByteCounts = 279;
            private const ushort TagPlanarConfiguration = 284;
            private const ushort TagResolutionUnit = 296;
            private const ushort TagExtraSamples = 338;
            private const ushort TagSampleFormat = 339;

            public static byte[] Encode(Texture2D texture)
            {
                if (texture == null)
                {
                    throw new ArgumentNullException(nameof(texture));
                }

                Color32[] pixels = texture.GetPixels32();

                int width = texture.width;
                int height = texture.height;
                int pixelDataSize = checked(width * height * 4);

                const int headerSize = 8;
                const int ifdSize =
                    2 + EntryCount * 12 + 4;

                int bitsPerSampleOffset =
                    headerSize + ifdSize;

                int pixelDataOffset =
                    bitsPerSampleOffset + 8;

                using var stream = new MemoryStream(
                    pixelDataOffset + pixelDataSize);

                using var writer = new BinaryWriter(stream);

                WriteHeader(writer, headerSize);
                writer.Write(EntryCount);

                WriteLongEntry(
                    writer,
                    TagImageWidth,
                    (uint)width);

                WriteLongEntry(
                    writer,
                    TagImageLength,
                    (uint)height);

                WriteOffsetEntry(
                    writer,
                    TagBitsPerSample,
                    TypeShort,
                    4,
                    (uint)bitsPerSampleOffset);

                WriteShortEntry(
                    writer,
                    TagCompression,
                    1);

                WriteShortEntry(
                    writer,
                    TagPhotometricInterpretation,
                    2);

                WriteLongEntry(
                    writer,
                    TagStripOffsets,
                    (uint)pixelDataOffset);

                WriteShortEntry(
                    writer,
                    TagSamplesPerPixel,
                    4);

                WriteLongEntry(
                    writer,
                    TagRowsPerStrip,
                    (uint)height);

                WriteLongEntry(
                    writer,
                    TagStripByteCounts,
                    (uint)pixelDataSize);

                WriteShortEntry(
                    writer,
                    TagPlanarConfiguration,
                    1);

                WriteShortEntry(
                    writer,
                    TagResolutionUnit,
                    1);

                WriteShortEntry(
                    writer,
                    TagExtraSamples,
                    2);

                WriteShortEntry(
                    writer,
                    TagSampleFormat,
                    1);

                writer.Write(0u);

                writer.Write((ushort)8);
                writer.Write((ushort)8);
                writer.Write((ushort)8);
                writer.Write((ushort)8);

                WritePixelsTopToBottom(
                    writer,
                    pixels,
                    width,
                    height);

                return stream.ToArray();
            }

            private static void WriteHeader(
                BinaryWriter writer,
                uint firstIfdOffset)
            {
                writer.Write((byte)'I');
                writer.Write((byte)'I');
                writer.Write(TiffMagicNumber);
                writer.Write(firstIfdOffset);
            }

            private static void WriteShortEntry(
                BinaryWriter writer,
                ushort tag,
                ushort value)
            {
                writer.Write(tag);
                writer.Write(TypeShort);
                writer.Write(1u);
                writer.Write(value);
                writer.Write((ushort)0);
            }

            private static void WriteLongEntry(
                BinaryWriter writer,
                ushort tag,
                uint value)
            {
                writer.Write(tag);
                writer.Write(TypeLong);
                writer.Write(1u);
                writer.Write(value);
            }

            private static void WriteOffsetEntry(
                BinaryWriter writer,
                ushort tag,
                ushort type,
                uint count,
                uint offset)
            {
                writer.Write(tag);
                writer.Write(type);
                writer.Write(count);
                writer.Write(offset);
            }

            private static void WritePixelsTopToBottom(
                BinaryWriter writer,
                IReadOnlyList<Color32> pixels,
                int width,
                int height)
            {
                for (int y = height - 1; y >= 0; y--)
                {
                    int rowOffset = y * width;

                    for (int x = 0; x < width; x++)
                    {
                        Color32 pixel = pixels[rowOffset + x];

                        writer.Write(pixel.r);
                        writer.Write(pixel.g);
                        writer.Write(pixel.b);
                        writer.Write(pixel.a);
                    }
                }
            }
        }
    }
}

#endif
