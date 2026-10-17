#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace TappyTale.EditorTools.GameStudio
{
    [Serializable]
    internal sealed class GameStudioToolDefinition
    {
        public string Id;
        public string DisplayName;
        public string Group;
        public string Description;
        public string PreferredWindowTypeName;
        public string PreferredMenuPath;
        public string[] FolderHints = Array.Empty<string>();
        public string[] TypeHints = Array.Empty<string>();
        public string[] MenuHints = Array.Empty<string>();
        public bool IsKnownTool;
        public bool Detected;
        public readonly List<string> DetectedFolders = new();
        public readonly List<string> DetectedTypes = new();
        public readonly List<string> DetectedMenuItems = new();

        public string StatusLabel => Detected ? "Installed" : "Not detected";
    }

    internal static class GameStudioGroups
    {
        public const string Home = "Home";
        public const string World = "World";
        public const string Characters = "Characters";
        public const string Audio = "Audio";
        public const string BarkGenerator = "Bark Generator";
        public const string Performance = "Performance";
        public const string Gameplay = "Gameplay";
        public const string Design = "Design";
        public const string Modules = "Modules";
        public const string Audit = "Audit";

        public static readonly string[] Ordered =
        {
            Home,
            World,
            Characters,
            Audio,
            BarkGenerator,
            Performance,
            Gameplay,
            Design,
            Modules,
            Audit
        };
    }
}
#endif
