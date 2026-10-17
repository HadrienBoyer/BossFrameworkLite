#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TappyTale.EditorTools.GameStudio
{
    internal sealed class GameStudioScanResult
    {
        public DateTime ScanTimeUtc;
        public int FileCount;
        public int CSharpCount;
        public int AssetCount;
        public int PrefabCount;
        public int SceneCount;
        public readonly List<string> RootModules = new();
        public readonly List<Type> EditorWindowTypes = new();
        public readonly List<Type> CustomEditorTypes = new();
        public readonly List<string> MenuItems = new();
        public readonly List<string> ScriptPaths = new();
        public readonly List<GameStudioToolDefinition> Tools = new();
    }

    internal static class GameStudioProjectScanner
    {
        private const string Root = "Assets/TappyTale";

        private static readonly Regex MenuRegex = new(
            @"MenuItem\s*\(\s*""([^""]+)""",
            RegexOptions.Compiled);

        public static GameStudioScanResult Scan()
        {
            GameStudioScanResult result = new()
            {
                ScanTimeUtc = DateTime.UtcNow
            };

            ScanFileSystem(result);
            ScanCompiledEditorTypes(result);
            ResolveKnownTools(result);
            AddDiscoveredWindowsAsTools(result);

            return result;
        }

        private static void ScanFileSystem(GameStudioScanResult result)
        {
            string absoluteRoot = Path.GetFullPath(Root);

            if (!Directory.Exists(absoluteRoot))
            {
                return;
            }

            string[] files = Directory.GetFiles(
                absoluteRoot,
                "*",
                SearchOption.AllDirectories);

            result.FileCount = files.Length;

            HashSet<string> menus = new(
                StringComparer.Ordinal);

            HashSet<string> roots = new(
                StringComparer.OrdinalIgnoreCase);

            foreach (string absoluteFile in files)
            {
                string normalized =
                    absoluteFile.Replace('\\', '/');

                int assetsIndex =
                    normalized.IndexOf(
                        "/Assets/",
                        StringComparison.OrdinalIgnoreCase);

                string assetPath =
                    assetsIndex >= 0
                        ? normalized[(assetsIndex + 1)..]
                        : normalized;

                string extension =
                    Path.GetExtension(assetPath);

                switch (extension.ToLowerInvariant())
                {
                    case ".cs":
                        result.CSharpCount++;
                        result.ScriptPaths.Add(assetPath);
                        ExtractMenuItems(absoluteFile, menus);
                        break;
                    case ".asset":
                        result.AssetCount++;
                        break;
                    case ".prefab":
                        result.PrefabCount++;
                        break;
                    case ".unity":
                        result.SceneCount++;
                        break;
                }

                string relative =
                    assetPath.StartsWith(
                        Root + "/",
                        StringComparison.OrdinalIgnoreCase)
                        ? assetPath[(Root.Length + 1)..]
                        : string.Empty;

                if (!string.IsNullOrWhiteSpace(relative))
                {
                    string first =
                        relative.Split('/')[0];

                    if (!string.IsNullOrWhiteSpace(first))
                    {
                        roots.Add(first);
                    }
                }
            }

            result.MenuItems.AddRange(
                menus.OrderBy(x => x));

            result.RootModules.AddRange(
                roots.OrderBy(x => x));
        }

        private static void ExtractMenuItems(
            string absoluteFile,
            HashSet<string> menus)
        {
            try
            {
                string text =
                    File.ReadAllText(absoluteFile);

                MatchCollection matches =
                    MenuRegex.Matches(text);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count < 2) continue;

                    string path =
                        match.Groups[1].Value.Trim();

                    if (path.StartsWith(
                        "Tools/Tappy Tale",
                        StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith(
                        "Tools/Tappy Tale",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        menus.Add(path);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[Game Studio Tools] Failed to scan {absoluteFile}: " +
                    exception.Message);
            }
        }

        private static void ScanCompiledEditorTypes(
            GameStudioScanResult result)
        {
            foreach (Type type in
                     TypeCache.GetTypesDerivedFrom<EditorWindow>())
            {
                if (IsTappyTaleType(type))
                {
                    result.EditorWindowTypes.Add(type);
                }
            }

            foreach (Type type in
                     TypeCache.GetTypesWithAttribute<CustomEditor>())
            {
                if (IsTappyTaleType(type))
                {
                    result.CustomEditorTypes.Add(type);
                }
            }

            result.EditorWindowTypes.Sort(
                (a, b) =>
                    string.Compare(
                        a.FullName,
                        b.FullName,
                        StringComparison.Ordinal));

            result.CustomEditorTypes.Sort(
                (a, b) =>
                    string.Compare(
                        a.FullName,
                        b.FullName,
                        StringComparison.Ordinal));
        }

        private static void ResolveKnownTools(
            GameStudioScanResult result)
        {
            List<GameStudioToolDefinition> tools =
                GameStudioKnownTools.Create();

            foreach (GameStudioToolDefinition tool in tools)
            {
                foreach (string folder in tool.FolderHints)
                {
                    if (AssetDatabase.IsValidFolder(folder))
                    {
                        tool.DetectedFolders.Add(folder);
                    }
                }

                foreach (Type type in result.EditorWindowTypes)
                {
                    if (MatchesAnyTypeHint(type, tool.TypeHints))
                    {
                        tool.DetectedTypes.Add(
                            type.FullName ?? type.Name);
                    }
                }

                foreach (string menu in result.MenuItems)
                {
                    if (MatchesAny(
                        menu,
                        tool.MenuHints))
                    {
                        tool.DetectedMenuItems.Add(menu);
                    }
                }

                if (tool.TypeHints != null)
                {
                    foreach (string hint in tool.TypeHints)
                    {
                        Type type =
                            FindTypeBySimpleOrFullName(hint);

                        if (type != null &&
                            !tool.DetectedTypes.Contains(
                                type.FullName ?? type.Name))
                        {
                            tool.DetectedTypes.Add(
                                type.FullName ?? type.Name);
                        }
                    }
                }

                tool.Detected =
                    tool.DetectedFolders.Count > 0 ||
                    tool.DetectedTypes.Count > 0 ||
                    tool.DetectedMenuItems.Count > 0;

                result.Tools.Add(tool);
            }
        }

        private static void AddDiscoveredWindowsAsTools(
            GameStudioScanResult result)
        {
            HashSet<string> represented =
                new(
                    result.Tools
                        .SelectMany(x => x.DetectedTypes),
                    StringComparer.Ordinal);

            foreach (Type type in result.EditorWindowTypes)
            {
                if (type == typeof(GameStudioToolsWindow))
                {
                    continue;
                }

                string fullName =
                    type.FullName ?? type.Name;

                if (represented.Contains(fullName))
                {
                    continue;
                }

                result.Tools.Add(
                    new GameStudioToolDefinition
                    {
                        Id = "dynamic-" + fullName,
                        DisplayName = Nicify(type.Name),
                        Group = InferGroup(type),
                        Description =
                            $"Discovered TappyTale EditorWindow: {fullName}",
                        PreferredWindowTypeName = fullName,
                        TypeHints = new[] { fullName },
                        IsKnownTool = false,
                        Detected = true,
                        DetectedTypes = { fullName }
                    });
            }
        }

        public static Type FindTypeBySimpleOrFullName(
            string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            foreach (Assembly assembly in
                     AppDomain.CurrentDomain.GetAssemblies())
            {
                Type direct =
                    assembly.GetType(
                        typeName,
                        false,
                        false);

                if (direct != null)
                {
                    return direct;
                }

                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types
                        .Where(x => x != null)
                        .ToArray();
                }
                catch
                {
                    continue;
                }

                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (type == null) continue;

                    if (string.Equals(
                        type.Name,
                        typeName,
                        StringComparison.Ordinal) ||
                        string.Equals(
                        type.FullName,
                        typeName,
                        StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        private static bool MatchesAnyTypeHint(
            Type type,
            IReadOnlyList<string> hints)
        {
            if (type == null || hints == null)
            {
                return false;
            }

            string full =
                type.FullName ?? type.Name;

            for (int i = 0; i < hints.Count; i++)
            {
                if (full.IndexOf(
                    hints[i],
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesAny(
            string value,
            IReadOnlyList<string> hints)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                hints == null)
            {
                return false;
            }

            for (int i = 0; i < hints.Count; i++)
            {
                if (value.IndexOf(
                    hints[i],
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTappyTaleType(Type type)
        {
            if (type == null) return false;

            string full =
                type.FullName ?? string.Empty;

            return full.StartsWith(
                       "TappyTale.",
                       StringComparison.Ordinal) ||
                   full.StartsWith(
                       "Tappy Tale.",
                       StringComparison.Ordinal);
        }

        private static string Nicify(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Tool";
            }

            string clean =
                name.EndsWith("Window", StringComparison.Ordinal)
                    ? name[..^6]
                    : name;

            return ObjectNames.NicifyVariableName(clean);
        }

        private static string InferGroup(Type type)
        {
            string value =
                (type.FullName ?? type.Name).ToLowerInvariant();

            if (value.Contains("traffic") ||
                value.Contains("crowd") ||
                value.Contains("world") ||
                value.Contains("scene"))
            {
                return GameStudioGroups.World;
            }

            if (value.Contains("zombie") ||
                value.Contains("character"))
            {
                return GameStudioGroups.Characters;
            }

            if (value.Contains("audio") ||
                value.Contains("radio") ||
                value.Contains("voice"))
            {
                return GameStudioGroups.Audio;
            }

            if (value.Contains("performance") ||
                value.Contains("optimizer") ||
                value.Contains("shadow"))
            {
                return GameStudioGroups.Performance;
            }

            if (value.Contains("boss") ||
                value.Contains("controller") ||
                value.Contains("combat"))
            {
                return GameStudioGroups.Gameplay;
            }

            if (value.Contains("placement") ||
                value.Contains("pattern") ||
                value.Contains("blockout"))
            {
                return GameStudioGroups.Design;
            }

            return GameStudioGroups.Modules;
        }
    }
}
#endif
