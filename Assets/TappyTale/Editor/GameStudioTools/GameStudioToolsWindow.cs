#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
//using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace TappyTale.EditorTools.GameStudio
{
    public sealed class GameStudioToolsWindow :
        EditorWindow
    {
        private const string MenuPath =
            "Tools/Tappy Tale/Game Studio Tools";

        private static readonly string[] TrafficObjectTypes =
        {
            "TrafficWorld",
            "TrafficSettings",
            "TrafficSpawnCatalog",
            "TrafficLane",
            "PedestrianRoutineProfile"
        };

        private static readonly string[] CrowdObjectTypes =
        {
            "CrowdGenerator",
            "CrowdShowController",
            "CrowdFormationPreset",
            "CrowdReactionPreset"
        };

        private static readonly string[] ZombieObjectTypes =
        {
            "ZombieGC2Bridge",
            "ZombieStateActionsRelay",
            "ZombieProfile"
        };

        private static readonly string[] AudioObjectTypes =
        {
            "RadioVoiceFilter",
            "RadioFilterPreset"
        };

        private static readonly string[] PerformanceObjectTypes =
        {
            "CachedShadowManager",
            "CachedShadowLight",
            "DynamicShadowCaster",
            "PerformanceProfile"
        };

        [SerializeField]
        private string m_SelectedGroup =
            GameStudioGroups.Home;
        [SerializeField] private string m_SelectedToolId;
        // private OdinMenuTree m_GroupTabs;
        private string m_GroupTabsSignature;
        [SerializeField] private Vector2 m_ContentScroll;
        [SerializeField] private bool m_ShowLegacyActions;
        [SerializeField] private bool m_CompactSidebar;

        private GameStudioScanResult m_Scan;
        private readonly EmbeddedEditorWindowHost m_EmbeddedHost =
            new();
        private readonly GameStudioInlineInspector m_InlineInspector =
            new();

        [MenuItem(MenuPath, priority = -100)]
        public static void Open()
        {
            GameStudioToolsWindow window =
                GetWindow<GameStudioToolsWindow>();

            window.titleContent =
                new GUIContent("Game Studio Tools");
            window.minSize =
                new Vector2(920f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            Rescan();
        }

        private void OnDisable()
        {
            m_EmbeddedHost.Dispose();
            m_InlineInspector.Dispose();
        }

        private void OnGUI()
        {
            EnsureScan();

            DrawTopBar();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSidebar();

                using (new EditorGUILayout.VerticalScope())
                {
                    DrawContent();
                }
            }
        }

        private void DrawTopBar()
        {
            using (new EditorGUILayout.HorizontalScope(
                       EditorStyles.toolbar))
            {
                GUILayout.Label(
                    "TAPPY TALE • GAME STUDIO TOOLS",
                    EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                string installed =
                    m_Scan == null
                        ? "0"
                        : m_Scan.Tools.Count(x => x.Detected)
                            .ToString();

                GUILayout.Label(
                    $"{installed} modules",
                    EditorStyles.miniLabel);

                if (GUILayout.Button(
                    "Rescan",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(64f)))
                {
                    Rescan();
                }

                m_CompactSidebar =
                    GUILayout.Toggle(
                        m_CompactSidebar,
                        "Compact",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(68f));
            }
        }

        private static Texture GroupIcon(string group)
        {
            string icon = group == GameStudioGroups.Home ? "d_UnityEditor.SceneHierarchyWindow"
                : group == GameStudioGroups.World ? "Terrain Icon"
                : group == GameStudioGroups.Characters ? "Avatar Icon"
                : (group == GameStudioGroups.Audio || group == GameStudioGroups.BarkGenerator) ? "AudioSource Icon"
                : group == GameStudioGroups.Performance ? "Light Icon"
                : group == GameStudioGroups.Gameplay ? "Animator Icon"
                : group == GameStudioGroups.Design ? "d_Grid.PaintTool"
                : group == GameStudioGroups.Audit ? "console.infoicon"
                : "Prefab Icon";
            return EditorGUIUtility.IconContent(icon).image;
        }

        private void SyncGroupTab()
        {
            // if (m_GroupTabs == null) return;
            // var item = m_GroupTabs.MenuItems.FirstOrDefault(tab =>
            //     string.Equals(tab.Value as string, m_SelectedGroup, StringComparison.Ordinal));
            // if (item == null || m_GroupTabs.Selection.Contains(item)) return;
            // m_GroupTabs.Selection.Clear();
            // m_GroupTabs.Selection.Add(item);
        }

        private void OnInspectorUpdate()
        {
            if (m_EmbeddedHost.HostedType != null) Repaint();
        }

        private void DrawSidebar()
        {
            var groups = GameStudioGroups.Ordered.Where(group =>
                group == GameStudioGroups.Home || group == GameStudioGroups.Audit ||
                HasDetectedTools(group)).ToArray();
            // string signature = m_CompactSidebar + "|" + string.Join("|", groups);
            // if (m_GroupTabs == null || signature != m_GroupTabsSignature)
            // {
            //     m_GroupTabsSignature = signature;
            //     m_GroupTabs = new OdinMenuTree(false);
            //     m_GroupTabs.Config.DrawSearchToolbar = false;
            //     m_GroupTabs.Config.AutoHandleKeyboardNavigation = true;
            //     m_GroupTabs.Config.SelectMenuItemsOnMouseDown = true;
            //     m_GroupTabs.DefaultMenuStyle.Height = 36;
            //     m_GroupTabs.DefaultMenuStyle.IconSize = 20;
            //     m_GroupTabs.DefaultMenuStyle.IconPadding = 10;
            //     m_GroupTabs.DefaultMenuStyle.Offset = 8;
            //     m_GroupTabs.DefaultMenuStyle.Borders = true;
            //     foreach (string group in groups)
            //         m_GroupTabs.Add(m_CompactSidebar ? CompactLabel(group) : group,
            //             group, GroupIcon(group));
            SyncGroupTab();


            using (new EditorGUILayout.VerticalScope(
                GUILayout.Width(m_CompactSidebar ? 138f : 205f)))
            {
                GUILayout.Space(6f);
                //m_GroupTabs.DrawMenuTree();
                //var selected = m_GroupTabs.Selection.FirstOrDefault();
                // if (selected?.Value is string group && group != m_SelectedGroup)
                // {
                //     SelectGroup(group);
                //     Repaint();
                // }
            }
        }

        private void DrawContent()
        {
            m_ContentScroll =
                EditorGUILayout.BeginScrollView(
                    m_ContentScroll);

            switch (m_SelectedGroup)
            {
                case GameStudioGroups.Home:
                    DrawHome();
                    break;

                case GameStudioGroups.Audit:
                    DrawAudit();
                    break;

                default:
                    DrawGroup(m_SelectedGroup);
                    break;
            }

            EditorGUILayout.Space(20f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHome()
        {
            EditorGUILayout.LabelField(
                "Game Studio Tools",
                EditorStyles.largeLabel);

            EditorGUILayout.HelpBox(
                "One dockable cockpit for TappyTale editor extensions. " +
                "Existing EditorWindows are embedded when safe; " +
                "components and ScriptableObjects are inspected inline; " +
                "legacy menu actions remain available only as fallback.",
                MessageType.Info);

            EditorGUILayout.Space(6f);

            DrawSummaryCards();

            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField(
                "Recommended DED workflow",
                EditorStyles.boldLabel);

            DrawActionRow(
                "Traffic & Crowd",
                "Build ambient pedestrians, cars, lanes and anchors.",
                () => OpenTool("traffic"));

            DrawActionRow(
                "DED Crowd Builder",
                "Build crowd formations and Zombie Ou Pouki production blockout.",
                () => OpenTool("crowd-builder"));

            DrawActionRow(
                "GC2 Zombie Factory",
                "Generate/configure zombie entities and bridges.",
                () => OpenTool("zombies"));

            DrawActionRow(
                "Performance",
                "Centralize shadow/light/runtime optimization tools.",
                () => OpenTool("performance"));

            EditorGUILayout.Space(10f);

            if (m_Scan != null &&
                m_Scan.RootModules.Count > 0)
            {
                EditorGUILayout.LabelField(
                    "Detected Assets/TappyTale roots",
                    EditorStyles.boldLabel);

                EditorGUILayout.LabelField(
                    string.Join(
                        "  •  ",
                        m_Scan.RootModules),
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawSummaryCards()
        {
            if (m_Scan == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetric(
                    "C#",
                    m_Scan.CSharpCount.ToString());
                DrawMetric(
                    "Editor Windows",
                    m_Scan.EditorWindowTypes.Count.ToString());
                DrawMetric(
                    "Menu Actions",
                    m_Scan.MenuItems.Count.ToString());
                DrawMetric(
                    "Prefabs",
                    m_Scan.PrefabCount.ToString());
                DrawMetric(
                    "Assets",
                    m_Scan.AssetCount.ToString());
            }
        }

        private void DrawGroup(string group)
        {
            List<GameStudioToolDefinition> tools =
                m_Scan.Tools
                    .Where(
                        x =>
                            x.Detected &&
                            string.Equals(
                                x.Group,
                                group,
                                StringComparison.Ordinal))
                    .OrderBy(x => x.DisplayName)
                    .ToList();

            if (tools.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No installed TappyTale tool was detected in this group.",
                    MessageType.Info);
                return;
            }

            if (string.IsNullOrWhiteSpace(m_SelectedToolId) ||
                tools.All(x => x.Id != m_SelectedToolId))
            {
                m_SelectedToolId =
                    tools[0].Id;
            }

            DrawToolToolbar(tools);

            GameStudioToolDefinition selected =
                tools.FirstOrDefault(
                    x => x.Id == m_SelectedToolId);

            if (selected == null)
            {
                return;
            }

            DrawTool(selected);
        }

        private void DrawToolToolbar(
            IReadOnlyList<GameStudioToolDefinition> tools)
        {
            using (new EditorGUILayout.HorizontalScope(
                       EditorStyles.toolbar))
            {
                for (int i = 0; i < tools.Count; i++)
                {
                    GameStudioToolDefinition tool =
                        tools[i];

                    bool selected =
                        tool.Id == m_SelectedToolId;

                    if (GUILayout.Toggle(
                        selected,
                        tool.DisplayName,
                        EditorStyles.toolbarButton) &&
                        !selected)
                    {
                        m_SelectedToolId = tool.Id;
                        m_EmbeddedHost.Dispose();
                        m_InlineInspector.Dispose();
                    }
                }
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawTool(
            GameStudioToolDefinition tool)
        {
            EditorGUILayout.LabelField(
                tool.DisplayName,
                EditorStyles.largeLabel);

            EditorGUILayout.LabelField(
                tool.Description,
                EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(4f);

            DrawDetectedStatus(tool);

            if (tool.Id == "traffic")
            {
                DrawTraffic(tool);
                return;
            }

            if (tool.Id == "crowd-builder")
            {
                DrawCrowd(tool);
                return;
            }

            if (tool.Id == "zombies")
            {
                DrawInlineObjectTool(
                    tool,
                    ZombieObjectTypes);
                return;
            }

            if (tool.Id == "radio")
            {
                DrawInlineObjectTool(
                    tool,
                    AudioObjectTypes);
                return;
            }

            if (tool.Id == "performance")
            {
                DrawPerformance(tool);
                return;
            }

            if (tool.Id == "bark-generator")
            {
                DrawEmbeddedWindow(tool);
                return;
            }

            DrawGenericTool(tool);
        }

        private void DrawTraffic(
            GameStudioToolDefinition tool)
        {
            DrawEmbeddedWindow(tool);

            EditorGUILayout.Space(8f);

            DrawInlineBrowsers(
                TrafficObjectTypes,
                "Traffic project / scene objects");

            m_InlineInspector.DrawInspector();

            DrawLegacyActions(tool);
        }

        private void DrawCrowd(
            GameStudioToolDefinition tool)
        {
            DrawEmbeddedWindow(tool);

            EditorGUILayout.Space(8f);

            DrawInlineBrowsers(
                CrowdObjectTypes,
                "Crowd project / scene objects");

            m_InlineInspector.DrawInspector();

            DrawLegacyActions(tool);
        }

        private void DrawPerformance(
            GameStudioToolDefinition tool)
        {
            DrawEmbeddedWindow(tool);

            EditorGUILayout.Space(8f);

            DrawInlineBrowsers(
                PerformanceObjectTypes,
                "Performance project / scene objects");

            m_InlineInspector.DrawInspector();

            DrawLegacyActions(tool);
        }

        private void DrawInlineObjectTool(
            GameStudioToolDefinition tool,
            IReadOnlyList<string> types)
        {
            DrawEmbeddedWindow(tool);

            EditorGUILayout.Space(8f);

            DrawInlineBrowsers(
                types,
                "Project / scene objects");

            m_InlineInspector.DrawInspector();

            DrawLegacyActions(tool);
        }

        private void DrawGenericTool(
            GameStudioToolDefinition tool)
        {
            DrawEmbeddedWindow(tool);
            DrawLegacyActions(tool);
        }

        private void DrawEmbeddedWindow(
            GameStudioToolDefinition tool)
        {
            Type type =
                ResolvePreferredWindowType(tool);

            if (type == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "Embedded Tool",
                    EditorStyles.boldLabel);

                if (m_EmbeddedHost.HostedType != type)
                {
                    m_EmbeddedHost.TryHost(type);
                }

                if (m_EmbeddedHost.HostedType == type)
                {
                    m_EmbeddedHost.Draw();
                }

                if (!string.IsNullOrEmpty(
                        m_EmbeddedHost.Error))
                {
                    EditorGUILayout.HelpBox(
                        m_EmbeddedHost.Error,
                        MessageType.Warning);
                }
            }
        }

        private void DrawInlineBrowsers(
            IReadOnlyList<string> typeNames,
            string title)
        {
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    title,
                    EditorStyles.boldLabel);

                for (int i = 0; i < typeNames.Count; i++)
                {
                    m_InlineInspector.DrawTypeBrowser(
                        typeNames[i],
                        ObjectNames.NicifyVariableName(
                            typeNames[i]));
                }
            }
        }

        private void DrawDetectedStatus(
            GameStudioToolDefinition tool)
        {
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        "Status",
                        GUILayout.Width(54f));

                    EditorGUILayout.LabelField(
                        tool.StatusLabel,
                        EditorStyles.boldLabel);
                }

                if (tool.DetectedFolders.Count > 0)
                {
                    EditorGUILayout.LabelField(
                        "Folders: " +
                        string.Join(
                            ", ",
                            tool.DetectedFolders),
                        EditorStyles.wordWrappedMiniLabel);
                }

                if (tool.DetectedTypes.Count > 0)
                {
                    EditorGUILayout.LabelField(
                        "Types: " +
                        string.Join(
                            ", ",
                            tool.DetectedTypes.Take(6)),
                        EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void DrawLegacyActions(
            GameStudioToolDefinition tool)
        {
            List<string> menus =
                tool.DetectedMenuItems.ToList();

            if (!string.IsNullOrWhiteSpace(
                    tool.PreferredMenuPath) &&
                !menus.Contains(
                    tool.PreferredMenuPath))
            {
                menus.Insert(
                    0,
                    tool.PreferredMenuPath);
            }

            if (menus.Count == 0)
            {
                return;
            }

            m_ShowLegacyActions =
                EditorGUILayout.Foldout(
                    m_ShowLegacyActions,
                    "Legacy / direct menu actions",
                    true);

            if (!m_ShowLegacyActions)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                foreach (string menu in menus.Distinct())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            menu,
                            EditorStyles.miniLabel);

                        if (GUILayout.Button(
                            "Run",
                            GUILayout.Width(50f)))
                        {
                            if (!EditorApplication
                                .ExecuteMenuItem(menu))
                            {
                                Debug.LogWarning(
                                    "[Game Studio Tools] Menu item not found: " +
                                    menu);
                            }
                        }
                    }
                }
            }
        }

        private void DrawAudit()
        {
            EditorGUILayout.LabelField(
                "Assets/TappyTale Audit",
                EditorStyles.largeLabel);

            EditorGUILayout.HelpBox(
                "This tab scans the actual project folder, compiled TappyTale EditorWindow types and Tools/Tappy Tale MenuItem declarations. " +
                "It is the authoritative project-side inventory for deciding what can live inside this hub.",
                MessageType.Info);

            if (m_Scan == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "Summary",
                    EditorStyles.boldLabel);

                EditorGUILayout.LabelField(
                    $"Files: {m_Scan.FileCount}");
                EditorGUILayout.LabelField(
                    $"C# scripts: {m_Scan.CSharpCount}");
                EditorGUILayout.LabelField(
                    $"Scriptable assets: {m_Scan.AssetCount}");
                EditorGUILayout.LabelField(
                    $"Prefabs: {m_Scan.PrefabCount}");
                EditorGUILayout.LabelField(
                    $"Scenes: {m_Scan.SceneCount}");
                EditorGUILayout.LabelField(
                    $"TappyTale EditorWindows: {m_Scan.EditorWindowTypes.Count}");
                EditorGUILayout.LabelField(
                    $"TappyTale custom Editors: {m_Scan.CustomEditorTypes.Count}");
                EditorGUILayout.LabelField(
                    $"Tappy Tale menu items: {m_Scan.MenuItems.Count}");
            }

            DrawAuditSection(
                "Root modules",
                m_Scan.RootModules);

            DrawAuditSection(
                "Editor Windows",
                m_Scan.EditorWindowTypes
                    .Select(x => x.FullName ?? x.Name));

            DrawAuditSection(
                "Custom Editors",
                m_Scan.CustomEditorTypes
                    .Select(x => x.FullName ?? x.Name));

            DrawAuditSection(
                "Tools > Tappy Tale menu items",
                m_Scan.MenuItems);

            EditorGUILayout.Space(8f);

            if (GUILayout.Button(
                "Export Audit JSON",
                GUILayout.Height(28f)))
            {
                GameStudioAuditExporter.Export(m_Scan);
            }
        }

        private static void DrawAuditSection(
            string title,
            IEnumerable<string> values)
        {
            string[] list =
                values
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .OrderBy(x => x)
                    .ToArray();

            if (list.Length == 0)
            {
                return;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                title,
                EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                for (int i = 0; i < list.Length; i++)
                {
                    EditorGUILayout.SelectableLabel(
                        list[i],
                        EditorStyles.miniLabel,
                        GUILayout.Height(17f));
                }
            }
        }

        private Type ResolvePreferredWindowType(
            GameStudioToolDefinition tool)
        {
            if (!string.IsNullOrWhiteSpace(
                    tool.PreferredWindowTypeName))
            {
                Type exact =
                    GameStudioProjectScanner
                        .FindTypeBySimpleOrFullName(
                            tool.PreferredWindowTypeName);

                if (exact != null &&
                    typeof(EditorWindow)
                        .IsAssignableFrom(exact))
                {
                    return exact;
                }
            }

            foreach (string detected in
                     tool.DetectedTypes)
            {
                Type type =
                    GameStudioProjectScanner
                        .FindTypeBySimpleOrFullName(
                            detected);

                if (type != null &&
                    typeof(EditorWindow)
                        .IsAssignableFrom(type))
                {
                    return type;
                }
            }

            return null;
        }

        private bool HasDetectedTools(string group)
        {
            return m_Scan != null &&
                   m_Scan.Tools.Any(
                       x =>
                           x.Detected &&
                           x.Group == group);
        }

        private void SelectGroup(string group)
        {
            if (m_SelectedGroup == group)
            {
                return;
            }

            m_SelectedGroup = group;
            SyncGroupTab();
            m_SelectedToolId = null;
            m_ContentScroll = Vector2.zero;
            m_EmbeddedHost.Dispose();
            m_InlineInspector.Dispose();
        }

        private void OpenTool(string id)
        {
            if (m_Scan == null)
            {
                return;
            }

            GameStudioToolDefinition tool =
                m_Scan.Tools.FirstOrDefault(
                    x => x.Id == id && x.Detected);

            if (tool == null)
            {
                ShowNotification(
                    new GUIContent(
                        "Tool not detected in this project."));
                return;
            }

            m_SelectedGroup = tool.Group;
            SyncGroupTab();
            m_SelectedToolId = tool.Id;
            m_ContentScroll = Vector2.zero;
            m_EmbeddedHost.Dispose();
            m_InlineInspector.Dispose();
        }

        private static void DrawActionRow(
            string title,
            string description,
            Action action)
        {
            using (new EditorGUILayout.HorizontalScope(
                       EditorStyles.helpBox))
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(
                        title,
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        description,
                        EditorStyles.wordWrappedMiniLabel);
                }

                if (GUILayout.Button(
                    "Open",
                    GUILayout.Width(70f),
                    GUILayout.Height(34f)))
                {
                    action?.Invoke();
                }
            }
        }

        private static void DrawMetric(
            string title,
            string value)
        {
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox,
                       GUILayout.MinWidth(100f)))
            {
                EditorGUILayout.LabelField(
                    value,
                    EditorStyles.largeLabel);
                EditorGUILayout.LabelField(
                    title,
                    EditorStyles.miniLabel);
            }
        }

        private static string CompactLabel(
            string group)
        {
            return group switch
            {
                GameStudioGroups.Home => "Home",
                GameStudioGroups.World => "World",
                GameStudioGroups.Characters => "Chars",
                GameStudioGroups.Audio => "Audio",
                GameStudioGroups.Performance => "Perf",
                GameStudioGroups.Gameplay => "Play",
                GameStudioGroups.Design => "Design",
                GameStudioGroups.Modules => "Mods",
                GameStudioGroups.Audit => "Audit",
                _ => group
            };
        }

        private void EnsureScan()
        {
            if (m_Scan == null)
            {
                Rescan();
            }
        }

        private void Rescan()
        {
            m_EmbeddedHost.Dispose();
            m_InlineInspector.Dispose();
            m_Scan =
                GameStudioProjectScanner.Scan();
            Repaint();
        }
    }
}
#endif
