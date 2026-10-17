using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TappyTale.BossFight.Attacks;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Editor.Demo;
using TappyTale.BossFight.Editor.Install;
using TappyTale.BossFight.Editor.Player;
using TappyTale.BossFight.Editor.Validation;
using TappyTale.BossFight.Feedback;
using TappyTale.BossFight.Phases;
using TappyTale.BossFight.Player;
using TappyTale.BossFight.Preferences;
using TappyTale.BossFight.Projectiles;
using TappyTale.BossFight.Telegraphs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TappyTale.BossFight.Editor.Dashboard
{
    public enum DashboardSection
    {
        Overview,
        Bosses,
        Phases,
        AttacksAndTimelines,
        ProjectilesAndPatterns,
        Telegraphs,
        Feedback,
        Player,
        ScenesAndDemos,
        SaveDataPlayerPrefs,
        Validation,
        SettingsAndDebug
    }

    public sealed class BossFrameworkDashboard : EditorWindow
    {
        private const string PackageEditorPath = "Packages/com.tappytale.boss-framework/Editor/Dashboard";

        [SerializeField] private DashboardSection currentSection = DashboardSection.Overview;
        [SerializeField] private Object selectedAsset;
        [SerializeField] private BossDemoProfile activeDemoContext;
        [SerializeField] private bool filterByDemoContext;
        [SerializeField] private string assetSearchQuery = string.Empty;
        [SerializeField] private string activeSaveProfile = "default";
        [SerializeField] private string prefsSearchQuery = string.Empty;
        [SerializeField] private string prefsCategoryFilter = "All";

        private BossValidationReport _lastValidationReport;
        private BossPreferencesService _preferencesService;

        private VisualElement _topActionsContainer;
        private ScrollView _sidebarContainer;
        private VisualElement _sectionHeaderContainer;
        private VisualElement _contentAreaContainer;
        private VisualElement _statusBarContainer;
        private Label _statusLeftLabel;
        private Label _statusRightLabel;

        private readonly Dictionary<DashboardSection, Button> _navButtons = new();

        [MenuItem("Tools/Tappy Tale/Boss Fight Framework", priority = 1)]
        public static void OpenStudioWindow()
        {
            BossFrameworkDashboard window = GetWindow<BossFrameworkDashboard>("Boss Fight Framework");
            window.minSize = new Vector2(1080f, 680f);
            window.Show();
        }

        [MenuItem("Tools/Tappy Tale/Boss Fight/Dashboard", priority = 2)]
        public static void OpenWindow() => OpenStudioWindow();

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (rootVisualElement != null && rootVisualElement.childCount > 0)
            {
                RebuildWorkspace();
            }
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{PackageEditorPath}/BossFrameworkDashboard.uss");
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{PackageEditorPath}/BossFrameworkDashboard.uxml");
            if (visualTree != null)
            {
                visualTree.CloneTree(rootVisualElement);
                _topActionsContainer = rootVisualElement.Q<VisualElement>("top-actions");
                _sidebarContainer = rootVisualElement.Q<ScrollView>("sidebar");
                _sectionHeaderContainer = rootVisualElement.Q<VisualElement>("section-header");
                _contentAreaContainer = rootVisualElement.Q<VisualElement>("content-area");
                _statusBarContainer = rootVisualElement.Q<VisualElement>("status-bar");
            }
            else
            {
                BuildFallbackShell();
            }

            EnsurePreferencesService();
            BuildTopBarActions();
            BuildSidebarNavigation();
            BuildStatusBar();
            RebuildWorkspace();
        }

        private void BuildFallbackShell()
        {
            VisualElement root = new() { name = "root-container" };
            root.AddToClassList("root-container");
            root.style.flexGrow = 1f;

            VisualElement topBar = new() { name = "top-bar" };
            topBar.AddToClassList("top-bar");
            _topActionsContainer = new VisualElement { name = "top-actions" };
            _topActionsContainer.AddToClassList("top-actions");
            topBar.Add(new Label("TAPPY TALE — Boss Fight Framework"));
            topBar.Add(_topActionsContainer);

            VisualElement bodySplit = new() { name = "body-split" };
            bodySplit.AddToClassList("body-split");
            bodySplit.style.flexGrow = 1f;
            bodySplit.style.flexDirection = FlexDirection.Row;

            _sidebarContainer = new ScrollView { name = "sidebar" };
            _sidebarContainer.AddToClassList("sidebar");

            VisualElement mainWorkspace = new() { name = "main-workspace" };
            mainWorkspace.AddToClassList("main-workspace");
            mainWorkspace.style.flexGrow = 1f;

            _sectionHeaderContainer = new VisualElement { name = "section-header" };
            _sectionHeaderContainer.AddToClassList("section-header");

            _contentAreaContainer = new VisualElement { name = "content-area" };
            _contentAreaContainer.AddToClassList("content-area");
            _contentAreaContainer.style.flexGrow = 1f;

            mainWorkspace.Add(_sectionHeaderContainer);
            mainWorkspace.Add(_contentAreaContainer);

            bodySplit.Add(_sidebarContainer);
            bodySplit.Add(mainWorkspace);

            _statusBarContainer = new VisualElement { name = "status-bar" };
            _statusBarContainer.AddToClassList("status-bar");

            root.Add(topBar);
            root.Add(bodySplit);
            root.Add(_statusBarContainer);
            rootVisualElement.Add(root);
        }

        private void EnsurePreferencesService()
        {
            BossPreferencesRegistry registry = BossFrameworkValidator.LoadAssets<BossPreferencesRegistry>().FirstOrDefault();
            if (registry != null)
            {
                registry.EnsureBuiltInDefaults();
            }
            _preferencesService = new BossPreferencesService(new PlayerPrefsStore(), registry);
        }

        private void BuildTopBarActions()
        {
            if (_topActionsContainer == null) return;
            _topActionsContainer.Clear();

            // Demo Context selector
            List<BossDemoProfile> profiles = BossFrameworkValidator.LoadAssets<BossDemoProfile>().ToList();
            List<string> profileChoices = new() { "All Project Assets" };
            profileChoices.AddRange(profiles.Select(p => $"Demo: {p.DisplayName}"));

            int currentIndex = 0;
            if (filterByDemoContext && activeDemoContext != null)
            {
                int idx = profiles.IndexOf(activeDemoContext);
                if (idx >= 0) currentIndex = idx + 1;
            }

            PopupField<string> contextDropdown = new("Context", profileChoices, currentIndex)
            {
                style = { minWidth = 230f, marginRight = 8f }
            };
            contextDropdown.labelElement.style.minWidth = 52f;
            contextDropdown.RegisterValueChangedCallback(evt =>
            {
                int selectedIdx = profileChoices.IndexOf(evt.newValue);
                if (selectedIdx <= 0)
                {
                    filterByDemoContext = false;
                    activeDemoContext = null;
                }
                else
                {
                    filterByDemoContext = true;
                    activeDemoContext = profiles[selectedIdx - 1];
                }
                RebuildWorkspace();
            });
            _topActionsContainer.Add(contextDropdown);

            Button installBtn = new(() =>
            {
                BossFrameworkInstaller.InstallOrRepair(overwriteExisting: false);
                _lastValidationReport = BossFrameworkValidator.ValidateProject();
                BuildTopBarActions();
                RebuildWorkspace();
            })
            { text = "Install / Repair" };
            installBtn.AddToClassList("secondary-button");
            _topActionsContainer.Add(installBtn);

            Button validateBtn = new(() =>
            {
                _lastValidationReport = BossFrameworkValidator.ValidateProject();
                SelectSection(DashboardSection.Validation);
            })
            { text = "Validate" };
            validateBtn.AddToClassList("secondary-button");
            _topActionsContainer.Add(validateBtn);

            Button duelLabBtn = new(() =>
            {
                BossDuelLabBuilder.CreateDuelLabScene();
                RebuildWorkspace();
            })
            { text = "Launch Boss Duel Lab" };
            duelLabBtn.AddToClassList("primary-button");
            _topActionsContainer.Add(duelLabBtn);
        }

        private void BuildSidebarNavigation()
        {
            if (_sidebarContainer == null) return;
            _sidebarContainer.Clear();
            _navButtons.Clear();

            AddSidebarGroup("STUDIO");
            AddNavButton(DashboardSection.Overview, "Overview & Quick Start");
            AddNavButton(DashboardSection.ScenesAndDemos, "Scenes & Demos");

            AddSidebarGroup("BOSS FRAMEWORK");
            AddNavButton(DashboardSection.Bosses, "Bosses & Catalog");
            AddNavButton(DashboardSection.Phases, "Phases");
            AddNavButton(DashboardSection.AttacksAndTimelines, "Attacks & Timelines");
            AddNavButton(DashboardSection.ProjectilesAndPatterns, "Projectiles & Patterns");
            AddNavButton(DashboardSection.Telegraphs, "Telegraphs");
            AddNavButton(DashboardSection.Feedback, "Feedback (Audio/VFX/Shake)");

            AddSidebarGroup("PLAYER FRAMEWORK");
            AddNavButton(DashboardSection.Player, "Player & Animator Builder");

            AddSidebarGroup("DATA & DIAGNOSTICS");
            AddNavButton(DashboardSection.SaveDataPlayerPrefs, "Save Data / PlayerPrefs");
            AddNavButton(DashboardSection.Validation, "Validation & Repair");
            AddNavButton(DashboardSection.SettingsAndDebug, "Settings & Live Debug");

            UpdateNavSelectionStyles();
        }

        private void AddSidebarGroup(string title)
        {
            Label groupLabel = new(title);
            groupLabel.AddToClassList("sidebar-group-label");
            _sidebarContainer.Add(groupLabel);
        }

        private void AddNavButton(DashboardSection section, string title)
        {
            Button btn = new(() => SelectSection(section))
            {
                text = title
            };
            btn.AddToClassList("nav-button");
            _sidebarContainer.Add(btn);
            _navButtons[section] = btn;
        }

        private void SelectSection(DashboardSection section, Object assetToSelect = null)
        {
            currentSection = section;
            if (assetToSelect != null)
            {
                selectedAsset = assetToSelect;
            }
            UpdateNavSelectionStyles();
            RebuildWorkspace();
        }

        private void UpdateNavSelectionStyles()
        {
            foreach (KeyValuePair<DashboardSection, Button> kvp in _navButtons)
            {
                if (kvp.Key == currentSection)
                {
                    kvp.Value.AddToClassList("nav-button-active");
                }
                else
                {
                    kvp.Value.RemoveFromClassList("nav-button-active");
                }
            }
        }

        private void BuildStatusBar()
        {
            if (_statusBarContainer == null) return;
            _statusBarContainer.Clear();
            _statusLeftLabel = new Label("Ready.");
            _statusRightLabel = new Label("Package: com.tappytale.boss-framework v1.0.0 | Target: Unity 6000.4 URP");
            _statusBarContainer.Add(_statusLeftLabel);
            _statusBarContainer.Add(_statusRightLabel);
        }

        private void SetStatus(string message)
        {
            if (_statusLeftLabel != null)
            {
                _statusLeftLabel.text = message;
            }
        }

        private void RebuildWorkspace()
        {
            if (_sectionHeaderContainer == null || _contentAreaContainer == null) return;
            _sectionHeaderContainer.Clear();
            _contentAreaContainer.Clear();

            switch (currentSection)
            {
                case DashboardSection.Overview:
                    BuildSectionHeader("Overview & Quick Start", "Central hub for Tappy Tale Boss Fight Framework — configure data, generate URP demos, and validate combat loops.");
                    BuildOverviewWorkspace();
                    break;
                case DashboardSection.Bosses:
                    BuildSectionHeader("Bosses & Catalog", "Configure BossDefinition, BossDatabase, and BossPayloadLibrary assets. Zero runtime state is stored in ScriptableObjects.");
                    BuildAssetBrowserWorkspace(
                        new[] { typeof(BossDefinition), typeof(BossDatabase), typeof(BossPayloadLibrary) },
                        "Create Boss Definition",
                        () => CreateNewScriptableObject<BossDefinition>("Boss_New"),
                        BuildBossCustomExtras);
                    break;
                case DashboardSection.Phases:
                    BuildSectionHeader("Boss Phases", "Configure health thresholds, speed/cooldown multipliers, entry invulnerability, and phase attack pools.");
                    BuildAssetBrowserWorkspace(
                        new[] { typeof(BossPhaseDefinition) },
                        "Create Phase",
                        () => CreateNewScriptableObject<BossPhaseDefinition>("Phase_New"),
                        BuildPhaseCustomExtras);
                    break;
                case DashboardSection.AttacksAndTimelines:
                    BuildSectionHeader("Attacks & Timelines", "Design BossAttackDefinitions and ordered AttackTimelineDefinitions with visual timeline tracks and reaction windows.");
                    BuildAssetBrowserWorkspace(
                        new[] { typeof(BossAttackDefinition), typeof(AttackTimelineDefinition) },
                        "Create Attack",
                        () => CreateNewScriptableObject<BossAttackDefinition>("Attack_New"),
                        BuildAttackAndTimelineCustomExtras,
                        ("Create Timeline", () => CreateNewScriptableObject<AttackTimelineDefinition>("Timeline_New")));
                    break;
                case DashboardSection.ProjectilesAndPatterns:
                    BuildSectionHeader("Projectiles & Patterns", "Configure pooled parriable projectiles and deterministic Single, Burst, Spread, Radial, and Spiral patterns.");
                    BuildAssetBrowserWorkspace(
                        new[] { typeof(ProjectilePatternDefinition), typeof(ProjectileDefinition) },
                        "Create Pattern",
                        () => CreateNewScriptableObject<ProjectilePatternDefinition>("Pattern_New"),
                        BuildPatternCustomExtras,
                        ("Create Projectile", () => CreateNewScriptableObject<ProjectileDefinition>("Projectile_New")));
                    break;
                case DashboardSection.Telegraphs:
                    BuildSectionHeader("Telegraphs", "Configure ground warning shapes (Circle, Ring, Cone, Line, Box) and warning/danger colors.");
                    BuildAssetBrowserWorkspace(
                        new[] { typeof(TelegraphDefinition) },
                        "Create Telegraph",
                        () => CreateNewScriptableObject<TelegraphDefinition>("Telegraph_New"),
                        BuildTelegraphCustomExtras);
                    break;
                case DashboardSection.Feedback:
                    BuildSectionHeader("Combat Feedback (Audio, VFX, Camera Shake)", "Configure Audio Cues (with procedural tone fallback), VFX bursts, and Camera Shake definitions.");
                    BuildAssetBrowserWorkspace(
                        new[] { typeof(AudioCueDefinition), typeof(VfxDefinition), typeof(CameraShakeDefinition) },
                        "Create Audio Cue",
                        () => CreateNewScriptableObject<AudioCueDefinition>("Audio_New"),
                        BuildFeedbackCustomExtras,
                        ("Create VFX", () => CreateNewScriptableObject<VfxDefinition>("Vfx_New")),
                        ("Create Shake", () => CreateNewScriptableObject<CameraShakeDefinition>("Shake_New")));
                    break;
                case DashboardSection.Player:
                    BuildSectionHeader("Player Framework & Animator Builder", "Configure locomotion, jump buffer, coyote time, dash, melee/parry combat, lock-on, and generate Animator Controllers.");
                    BuildAssetBrowserWorkspace(
                        new[] { typeof(PlayerDefinition) },
                        "Create Player Definition",
                        () => CreateNewScriptableObject<PlayerDefinition>("Player_New"),
                        BuildPlayerCustomExtras);
                    break;
                case DashboardSection.ScenesAndDemos:
                    BuildSectionHeader("Scenes & Demo Profiles", "Inspect demo manifests, open or regenerate scenes safely, duplicate demos into isolated folders, or reset to factory defaults.");
                    BuildScenesAndDemosWorkspace();
                    break;
                case DashboardSection.SaveDataPlayerPrefs:
                    BuildSectionHeader("Save Data & PlayerPrefs Inspector", "Inspect, edit, reset, and export/import scoped Tappy Tale PlayerPrefs keys across save profiles.");
                    BuildSaveDataWorkspace();
                    break;
                case DashboardSection.Validation:
                    BuildSectionHeader("Project Validation & Auto-Repair", "Verify Boss, Phase, Attack, Timeline, Payload, Player, and Demo integrity with non-destructive 1-click fixes.");
                    BuildValidationWorkspace();
                    break;
                case DashboardSection.SettingsAndDebug:
                    BuildSectionHeader("Settings & Live Runtime Debugger", "Inspect live Boss & Player runtime state during Play Mode and trigger combat test actions.");
                    BuildSettingsAndDebugWorkspace();
                    break;
            }
        }

        private void BuildSectionHeader(string title, string subtitle)
        {
            VisualElement textCol = new();
            Label titleLabel = new(title);
            titleLabel.AddToClassList("section-title");
            Label descLabel = new(subtitle);
            descLabel.AddToClassList("section-description");
            textCol.Add(titleLabel);
            textCol.Add(descLabel);
            _sectionHeaderContainer.Add(textCol);

            if (filterByDemoContext && activeDemoContext != null)
            {
                Label contextBadge = new($"Filtered by Demo: {activeDemoContext.DisplayName}");
                contextBadge.AddToClassList("badge-ok");
                _sectionHeaderContainer.Add(contextBadge);
            }
        }

        // =====================================================================
        // 1. OVERVIEW SECTION
        // =====================================================================
        private void BuildOverviewWorkspace()
        {
            ScrollView scroll = new() { style = { flexGrow = 1f, paddingLeft = 18f, paddingRight = 18f, paddingTop = 14f, paddingBottom = 14f } };

            // Summary metrics row
            VisualElement metricsRow = new() { style = { flexDirection = FlexDirection.Row, marginBottom = 12f } };
            int bossCount = BossFrameworkValidator.LoadAssets<BossDefinition>().Count();
            int phaseCount = BossFrameworkValidator.LoadAssets<BossPhaseDefinition>().Count();
            int attackCount = BossFrameworkValidator.LoadAssets<BossAttackDefinition>().Count();
            int timelineCount = BossFrameworkValidator.LoadAssets<AttackTimelineDefinition>().Count();
            int patternCount = BossFrameworkValidator.LoadAssets<ProjectilePatternDefinition>().Count();
            int playerCount = BossFrameworkValidator.LoadAssets<PlayerDefinition>().Count();

            metricsRow.Add(CreateMetricCard("Bosses", bossCount.ToString(), () => SelectSection(DashboardSection.Bosses)));
            metricsRow.Add(CreateMetricCard("Phases", phaseCount.ToString(), () => SelectSection(DashboardSection.Phases)));
            metricsRow.Add(CreateMetricCard("Attacks", attackCount.ToString(), () => SelectSection(DashboardSection.AttacksAndTimelines)));
            metricsRow.Add(CreateMetricCard("Timelines", timelineCount.ToString(), () => SelectSection(DashboardSection.AttacksAndTimelines)));
            metricsRow.Add(CreateMetricCard("Patterns", patternCount.ToString(), () => SelectSection(DashboardSection.ProjectilesAndPatterns)));
            metricsRow.Add(CreateMetricCard("Player Profiles", playerCount.ToString(), () => SelectSection(DashboardSection.Player)));
            scroll.Add(metricsRow);

            // Flagship Boss Duel Lab Card
            VisualElement duelCard = new();
            duelCard.AddToClassList("card");
            Label duelTitle = new("Reference Demo: Boss Duel Lab");
            duelTitle.AddToClassList("card-title");
            Label duelSub = new("Core gameplay loop: Read Telegraph Threat -> React (Parry [E/RMB] or Perfect Dodge [LShift]) -> Interrupt/Stagger Boss -> Punish Recovery [J/K/LMB] -> Victory / Defeat / Restart [R]. Runs 100% on URP primitives without external art dependencies.");
            duelSub.AddToClassList("card-subtitle");
            duelSub.style.whiteSpace = WhiteSpace.Normal;
            duelCard.Add(duelTitle);
            duelCard.Add(duelSub);

            VisualElement duelActions = new() { style = { flexDirection = FlexDirection.Row, marginTop = 6f } };
            Button buildDuelBtn = new(() =>
            {
                BossDuelLabBuilder.CreateDuelLabScene();
                SetStatus("Created and opened Boss Duel Lab scene.");
                RebuildWorkspace();
            })
            { text = "Create / Open Boss Duel Lab" };
            buildDuelBtn.AddToClassList("primary-button");

            Button buildBossDemoBtn = new(() =>
            {
                BossDemoSceneBuilder.CreateDemoScene();
                SetStatus("Created and opened Boss Fight Demo scene.");
                RebuildWorkspace();
            })
            { text = "Create Boss Fight Demo" };
            buildBossDemoBtn.AddToClassList("secondary-button");

            Button buildPlayerDemoBtn = new(() =>
            {
                PlayerCoreDemoBuilder.CreateDemo();
                SetStatus("Created and opened Player Core Demo scene.");
                RebuildWorkspace();
            })
            { text = "Create Player Core Demo" };
            buildPlayerDemoBtn.AddToClassList("secondary-button");

            Button playBtn = new(() =>
            {
                if (!File.Exists(BossFrameworkInstaller.DuelLabScenePath))
                {
                    BossDuelLabBuilder.CreateDuelLabScene();
                }
                else
                {
                    EditorSceneManager.OpenScene(BossFrameworkInstaller.DuelLabScenePath);
                }
                EditorApplication.isPlaying = true;
            })
            { text = "Play Boss Duel Lab Now" };
            playBtn.AddToClassList("secondary-button");

            duelActions.Add(buildDuelBtn);
            duelActions.Add(playBtn);
            duelActions.Add(buildBossDemoBtn);
            duelActions.Add(buildPlayerDemoBtn);
            duelCard.Add(duelActions);
            scroll.Add(duelCard);

            // Architecture & Controls Quick Reference
            VisualElement archCard = new();
            archCard.AddToClassList("card");
            Label archTitle = new("Default Controls & Architecture Guarantees");
            archTitle.AddToClassList("card-title");
            Label archBody = new(
                "• Movement: WASD / Arrows (Camera-Relative)   |   Jump / Double Jump: Space (with Coyote Time & Jump Buffer)\n" +
                "• Dash / Perfect Dodge: Left Shift / C   |   Parry / Deflect: E / Right Mouse Button / F\n" +
                "• Light Attack: J / Left Mouse Button   |   Heavy Attack: K / R   |   Lock-On Toggle: Tab / Middle Mouse (Q/Z to switch target)\n" +
                "• Data Separation: ScriptableObjects are read-only at runtime; all mutable combat state lives in BossRuntime and PlayerRuntime.\n" +
                "• Save Data: Scoped strictly to 'tappytale.bossframework.<profile>.<key>' — never invokes PlayerPrefs.DeleteAll().");
            archBody.style.whiteSpace = WhiteSpace.Normal;
            archBody.style.fontSize = 11f;
            archBody.style.color = new Color(0.76f, 0.8f, 0.88f);
            archCard.Add(archTitle);
            archCard.Add(archBody);
            scroll.Add(archCard);

            _contentAreaContainer.Add(scroll);
        }

        private VisualElement CreateMetricCard(string label, string countText, Action onClick)
        {
            VisualElement card = new()
            {
                style =
                {
                    flexGrow = 1f,
                    backgroundColor = new Color(0.12f, 0.14f, 0.18f),
                    borderTopWidth = 1f, borderBottomWidth = 1f, borderLeftWidth = 1f, borderRightWidth = 1f,
                    borderTopColor = new Color(0.18f, 0.21f, 0.27f),
                    borderBottomColor = new Color(0.18f, 0.21f, 0.27f),
                    borderLeftColor = new Color(0.18f, 0.21f, 0.27f),
                    borderRightColor = new Color(0.18f, 0.21f, 0.27f),
                    borderTopLeftRadius = 6f, borderTopRightRadius = 6f, borderBottomLeftRadius = 6f, borderBottomRightRadius = 6f,
                    paddingLeft = 12f, paddingRight = 12f, paddingTop = 10f, paddingBottom = 10f,
                    marginRight = 8f
                }
            };

            Label num = new(countText) { style = { fontSize = 20f, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.95f, 0.96f, 0.98f) } };
            Label title = new(label) { style = { fontSize = 11f, color = new Color(0.6f, 0.65f, 0.75f), marginTop = 2f } };
            card.Add(num);
            card.Add(title);
            card.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());
            return card;
        }

        // =====================================================================
        // GENERIC SPLIT ASSET BROWSER + SERIALIZEDOBJECT INSPECTOR + EXTRAS
        // =====================================================================
        private void BuildAssetBrowserWorkspace(
            Type[] assetTypes,
            string primaryCreateLabel,
            Func<Object> primaryCreateAction,
            Action<VisualElement, Object> customExtrasBuilder,
            params (string label, Func<Object> action)[] additionalCreateButtons)
        {
            // Left Pane: Search + Create/Duplicate/Delete + Asset List
            VisualElement leftPane = new();
            leftPane.AddToClassList("asset-list-pane");

            VisualElement toolbar = new();
            toolbar.AddToClassList("asset-list-toolbar");

            TextField searchField = new("Search") { value = assetSearchQuery };
            searchField.labelElement.style.minWidth = 45f;
            searchField.RegisterValueChangedCallback(evt =>
            {
                assetSearchQuery = evt.newValue;
                RebuildWorkspace();
            });
            toolbar.Add(searchField);

            VisualElement createRow = new() { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 6f } };
            if (!string.IsNullOrEmpty(primaryCreateLabel) && primaryCreateAction != null)
            {
                Button createBtn = new(() =>
                {
                    Object created = primaryCreateAction.Invoke();
                    if (created != null)
                    {
                        selectedAsset = created;
                        RebuildWorkspace();
                    }
                })
                { text = "+ " + primaryCreateLabel };
                createBtn.AddToClassList("primary-button");
                createBtn.style.marginBottom = 4f;
                createRow.Add(createBtn);
            }

            if (additionalCreateButtons != null)
            {
                foreach ((string label, Func<Object> action) extra in additionalCreateButtons)
                {
                    Button extraBtn = new(() =>
                    {
                        Object created = extra.action.Invoke();
                        if (created != null)
                        {
                            selectedAsset = created;
                            RebuildWorkspace();
                        }
                    })
                    { text = "+ " + extra.label };
                    extraBtn.AddToClassList("secondary-button");
                    extraBtn.style.marginBottom = 4f;
                    createRow.Add(extraBtn);
                }
            }
            toolbar.Add(createRow);
            leftPane.Add(toolbar);

            // Load matching assets
            List<Object> assets = LoadFilteredAssets(assetTypes);
            if (selectedAsset == null || !assets.Contains(selectedAsset))
            {
                selectedAsset = assets.FirstOrDefault();
            }

            ScrollView listScroll = new() { style = { flexGrow = 1f } };
            if (assets.Count == 0)
            {
                Label emptyLabel = new("No matching assets found.\nClick Install / Repair or Create above.")
                {
                    style = { paddingLeft = 12f, paddingTop = 12f, color = new Color(0.6f, 0.65f, 0.72f), whiteSpace = WhiteSpace.Normal }
                };
                listScroll.Add(emptyLabel);
            }
            else
            {
                foreach (Object asset in assets)
                {
                    VisualElement row = new();
                    row.AddToClassList("asset-row");
                    if (asset == selectedAsset)
                    {
                        row.AddToClassList("asset-row-selected");
                    }

                    Label nameLabel = new(asset.name)
                    {
                        style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12f, color = new Color(0.93f, 0.95f, 0.98f) }
                    };
                    string path = AssetDatabase.GetAssetPath(asset);
                    Label metaLabel = new($"{asset.GetType().Name} • {Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty)}")
                    {
                        style = { fontSize = 10f, color = new Color(0.58f, 0.63f, 0.72f), marginTop = 2f }
                    };

                    row.Add(nameLabel);
                    row.Add(metaLabel);
                    Object captured = asset;
                    row.RegisterCallback<ClickEvent>(_ =>
                    {
                        selectedAsset = captured;
                        RebuildWorkspace();
                    });
                    listScroll.Add(row);
                }
            }
            leftPane.Add(listScroll);

            // Right Pane: Inspector + Custom Visual Editors + Reference Graph
            ScrollView rightPane = new();
            rightPane.AddToClassList("inspector-pane");

            if (selectedAsset == null)
            {
                rightPane.Add(new Label("Select an asset on the left to inspect and edit its data."));
            }
            else
            {
                BuildAssetHeaderCard(rightPane, selectedAsset);
                customExtrasBuilder?.Invoke(rightPane, selectedAsset);
                BuildSerializedObjectInspectorCard(rightPane, selectedAsset);
                BuildAssetReferencesCard(rightPane, selectedAsset);
            }

            _contentAreaContainer.Add(leftPane);
            _contentAreaContainer.Add(rightPane);
        }

        private List<Object> LoadFilteredAssets(Type[] types)
        {
            List<Object> result = new();
            HashSet<ScriptableObject> contextAssets = filterByDemoContext && activeDemoContext != null
                ? new HashSet<ScriptableObject>(activeDemoContext.CollectAllReferencedDefinitions())
                : null;

            foreach (Type type in types)
            {
                string[] guids = AssetDatabase.FindAssets($"t:{type.Name}");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Object asset = AssetDatabase.LoadAssetAtPath(path, type);
                    if (asset == null) continue;

                    if (contextAssets != null && asset is ScriptableObject soAsset && !contextAssets.Contains(soAsset))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(assetSearchQuery))
                    {
                        if (asset.name.IndexOf(assetSearchQuery, StringComparison.OrdinalIgnoreCase) < 0 &&
                            path.IndexOf(assetSearchQuery, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }
                    }

                    if (!result.Contains(asset))
                    {
                        result.Add(asset);
                    }
                }
            }
            return result;
        }

        private void BuildAssetHeaderCard(VisualElement parent, Object asset)
        {
            VisualElement card = new();
            card.AddToClassList("card");

            string assetPath = AssetDatabase.GetAssetPath(asset);
            VisualElement topRow = new() { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };

            VisualElement titleCol = new();
            Label title = new($"{asset.name} ({asset.GetType().Name})");
            title.AddToClassList("card-title");
            Label pathLabel = new(assetPath);
            pathLabel.AddToClassList("card-subtitle");
            titleCol.Add(title);
            titleCol.Add(pathLabel);
            topRow.Add(titleCol);

            VisualElement actionsRow = new() { style = { flexDirection = FlexDirection.Row } };

            Button pingBtn = new(() =>
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            })
            { text = "Ping in Project" };
            pingBtn.AddToClassList("secondary-button");

            Button duplicateBtn = new(() =>
            {
                Object copy = DuplicateAsset(asset);
                if (copy != null)
                {
                    selectedAsset = copy;
                    RebuildWorkspace();
                }
            })
            { text = "Duplicate" };
            duplicateBtn.AddToClassList("secondary-button");

            Button deleteBtn = new(() =>
            {
                if (EditorUtility.DisplayDialog(
                        "Delete Asset",
                        $"Are you sure you want to delete '{asset.name}' ({assetPath})?",
                        "Delete",
                        "Cancel"))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                    selectedAsset = null;
                    RebuildWorkspace();
                }
            })
            { text = "Delete" };
            deleteBtn.AddToClassList("secondary-button");

            actionsRow.Add(pingBtn);
            actionsRow.Add(duplicateBtn);
            actionsRow.Add(deleteBtn);
            topRow.Add(actionsRow);

            card.Add(topRow);
            parent.Add(card);
        }

        private void BuildSerializedObjectInspectorCard(VisualElement parent, Object asset)
        {
            VisualElement card = new();
            card.AddToClassList("card");
            Label header = new("Data Configuration (SerializedObject)");
            header.AddToClassList("card-title");
            card.Add(header);

            SerializedObject serializedObject = new(asset);
            VisualElement fieldsContainer = new();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script") continue;

                PropertyField field = new(iterator.Copy())
                {
                    style = { marginBottom = 3f }
                };
                field.Bind(serializedObject);
                fieldsContainer.Add(field);
            }

            card.Add(fieldsContainer);
            parent.Add(card);
        }

        private void BuildAssetReferencesCard(VisualElement parent, Object targetAsset)
        {
            VisualElement card = new();
            card.AddToClassList("card");
            Label title = new("Incoming & Outgoing References");
            title.AddToClassList("card-title");
            card.Add(title);

            string targetPath = AssetDatabase.GetAssetPath(targetAsset);
            string[] outgoingDeps = AssetDatabase.GetDependencies(targetPath, false);

            Label outTitle = new($"Outgoing Dependencies ({outgoingDeps.Count(p => p != targetPath)}):")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11f, marginTop = 4f, marginBottom = 2f }
            };
            card.Add(outTitle);

            foreach (string depPath in outgoingDeps)
            {
                if (depPath == targetPath || depPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                Object depObj = AssetDatabase.LoadMainAssetAtPath(depPath);
                if (depObj == null) continue;
                Button linkBtn = new(() => JumpToAsset(depObj))
                {
                    text = $"→ {depObj.name} ({depObj.GetType().Name}) [{depPath}]"
                };
                linkBtn.AddToClassList("secondary-button");
                linkBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                linkBtn.style.marginBottom = 2f;
                card.Add(linkBtn);
            }

            // Scan framework assets for incoming references
            List<Object> incomingRefs = FindIncomingFrameworkReferences(targetAsset);
            Label inTitle = new($"Referenced By Framework Assets / Demos ({incomingRefs.Count}):")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11f, marginTop = 8f, marginBottom = 2f }
            };
            card.Add(inTitle);

            foreach (Object incoming in incomingRefs)
            {
                string inPath = AssetDatabase.GetAssetPath(incoming);
                Button linkBtn = new(() => JumpToAsset(incoming))
                {
                    text = $"← {incoming.name} ({incoming.GetType().Name}) [{inPath}]"
                };
                linkBtn.AddToClassList("secondary-button");
                linkBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                linkBtn.style.marginBottom = 2f;
                card.Add(linkBtn);
            }

            parent.Add(card);
        }

        private List<Object> FindIncomingFrameworkReferences(Object targetAsset)
        {
            List<Object> results = new();
            string targetPath = AssetDatabase.GetAssetPath(targetAsset);
            if (string.IsNullOrEmpty(targetPath)) return results;

            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == targetPath) continue;
                string[] deps = AssetDatabase.GetDependencies(path, false);
                if (deps.Contains(targetPath))
                {
                    Object main = AssetDatabase.LoadMainAssetAtPath(path);
                    if (main != null) results.Add(main);
                }
            }
            return results;
        }

        private void JumpToAsset(Object asset)
        {
            if (asset == null) return;
            selectedAsset = asset;
            if (asset is BossDefinition or BossDatabase or BossPayloadLibrary) SelectSection(DashboardSection.Bosses, asset);
            else if (asset is BossPhaseDefinition) SelectSection(DashboardSection.Phases, asset);
            else if (asset is BossAttackDefinition or AttackTimelineDefinition) SelectSection(DashboardSection.AttacksAndTimelines, asset);
            else if (asset is ProjectileDefinition or ProjectilePatternDefinition) SelectSection(DashboardSection.ProjectilesAndPatterns, asset);
            else if (asset is TelegraphDefinition) SelectSection(DashboardSection.Telegraphs, asset);
            else if (asset is AudioCueDefinition or VfxDefinition or CameraShakeDefinition) SelectSection(DashboardSection.Feedback, asset);
            else if (asset is PlayerDefinition) SelectSection(DashboardSection.Player, asset);
            else if (asset is BossDemoProfile) SelectSection(DashboardSection.ScenesAndDemos, asset);
            else
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
        }

        // =====================================================================
        // SPECIALIZED EDITORS PER SECTION
        // =====================================================================
        private void BuildBossCustomExtras(VisualElement parent, Object asset)
        {
            if (asset is not BossDefinition boss) return;

            VisualElement card = new();
            card.AddToClassList("card");
            Label title = new($"Boss Combat Hierarchy — {boss.DisplayName} ({boss.MaxHealth:0} HP)");
            title.AddToClassList("card-title");
            card.Add(title);

            if (boss.Phases == null || boss.Phases.Count == 0)
            {
                card.Add(new Label("No phases assigned to this BossDefinition."));
            }
            else
            {
                for (int i = 0; i < boss.Phases.Count; i++)
                {
                    BossPhaseDefinition phase = boss.Phases[i];
                    if (phase == null) continue;

                    VisualElement phaseBox = new()
                    {
                        style =
                        {
                            backgroundColor = new Color(0.15f, 0.17f, 0.22f),
                            paddingLeft = 10f, paddingRight = 10f, paddingTop = 8f, paddingBottom = 8f,
                            borderTopLeftRadius = 4f, borderTopRightRadius = 4f, borderBottomLeftRadius = 4f, borderBottomRightRadius = 4f,
                            marginBottom = 6f
                        }
                    };

                    VisualElement phaseRow = new() { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };
                    Label phaseLabel = new($"Phase {i + 1}: {phase.DisplayName} (Enters <= {phase.EnterAtNormalizedHealth * 100f:0}% HP | Speed x{phase.MoveSpeedMultiplier:0.00})")
                    {
                        style = { unityFontStyleAndWeight = FontStyle.Bold }
                    };
                    Button editPhaseBtn = new(() => SelectSection(DashboardSection.Phases, phase)) { text = "Inspect Phase" };
                    editPhaseBtn.AddToClassList("secondary-button");
                    phaseRow.Add(phaseLabel);
                    phaseRow.Add(editPhaseBtn);
                    phaseBox.Add(phaseRow);

                    if (phase.Attacks != null)
                    {
                        foreach (BossAttackDefinition attack in phase.Attacks)
                        {
                            if (attack == null) continue;
                            Button atkBtn = new(() => SelectSection(DashboardSection.AttacksAndTimelines, attack))
                            {
                                text = $"   ↳ Attack: {attack.DisplayName} ({attack.Kind}, CD {attack.Cooldown:0.0}s, Range {attack.MinimumRange:0}-{attack.MaximumRange:0}m, Priority {attack.Priority})"
                            };
                            atkBtn.AddToClassList("secondary-button");
                            atkBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                            atkBtn.style.marginTop = 3f;
                            phaseBox.Add(atkBtn);
                        }
                    }

                    card.Add(phaseBox);
                }
            }

            parent.Add(card);
        }

        private void BuildPhaseCustomExtras(VisualElement parent, Object asset)
        {
            if (asset is not BossPhaseDefinition phase) return;

            VisualElement card = new();
            card.AddToClassList("card");
            Label title = new($"Phase Health Threshold Preview ({phase.EnterAtNormalizedHealth * 100f:0}% HP)");
            title.AddToClassList("card-title");
            card.Add(title);

            ProgressBar bar = new()
            {
                lowValue = 0f,
                highValue = 100f,
                value = Mathf.Clamp01(phase.EnterAtNormalizedHealth) * 100f,
                title = $"Activates at <= {phase.EnterAtNormalizedHealth * 100f:0}% Boss Health"
            };
            card.Add(bar);
            parent.Add(card);
        }

        private void BuildAttackAndTimelineCustomExtras(VisualElement parent, Object asset)
        {
            AttackTimelineDefinition timeline = asset as AttackTimelineDefinition;
            BossAttackDefinition parentAttack = asset as BossAttackDefinition;
            if (parentAttack != null)
            {
                timeline = parentAttack.Timeline;
            }

            if (timeline == null) return;

            VisualElement card = new();
            card.AddToClassList("card");

            VisualElement headerRow = new() { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };
            Label title = new($"Visual Timeline Track Editor — {timeline.name} ({timeline.Duration:0.00}s)");
            title.AddToClassList("card-title");
            headerRow.Add(title);

            if (parentAttack != null)
            {
                Button openTimelineBtn = new(() => SelectSection(DashboardSection.AttacksAndTimelines, timeline))
                { text = "Select Timeline Asset" };
                openTimelineBtn.AddToClassList("secondary-button");
                headerRow.Add(openTimelineBtn);
            }
            card.Add(headerRow);

            // Visual Timeline Bar
            float totalDuration = Mathf.Max(0.1f, timeline.Duration);
            VisualElement trackContainer = new()
            {
                style =
                {
                    backgroundColor = new Color(0.09f, 0.11f, 0.14f),
                    paddingLeft = 10f, paddingRight = 10f, paddingTop = 10f, paddingBottom = 10f,
                    borderTopLeftRadius = 4f, borderTopRightRadius = 4f, borderBottomLeftRadius = 4f, borderBottomRightRadius = 4f,
                    marginBottom = 10f
                }
            };

            if (timeline.Entries != null)
            {
                for (int i = 0; i < timeline.Entries.Count; i++)
                {
                    AttackTimelineEntry entry = timeline.Entries[i];
                    float startPct = Mathf.Clamp01(entry.Time / totalDuration) * 100f;
                    float durationSeconds = IsWindowOrDurationAction(entry.Action) && entry.Value > 0f ? entry.Value : 0.06f;
                    float widthPct = Mathf.Clamp(durationSeconds / totalDuration * 100f, 3f, 100f - startPct);

                    VisualElement lane = new()
                    {
                        style =
                        {
                            height = 22f,
                            backgroundColor = new Color(0.14f, 0.16f, 0.21f),
                            marginBottom = 4f,
                            borderTopLeftRadius = 3f, borderTopRightRadius = 3f, borderBottomLeftRadius = 3f, borderBottomRightRadius = 3f,
                            justifyContent = Justify.Center
                        }
                    };

                    VisualElement bar = new()
                    {
                        style =
                        {
                            position = Position.Absolute,
                            left = Length.Percent(startPct),
                            width = Length.Percent(widthPct),
                            top = 2f,
                            bottom = 2f,
                            backgroundColor = GetActionColor(entry.Action),
                            borderTopLeftRadius = 3f, borderTopRightRadius = 3f, borderBottomLeftRadius = 3f, borderBottomRightRadius = 3f
                        }
                    };

                    Label laneLabel = new($"  [{entry.Time:0.00}s] {entry.Action} {(string.IsNullOrEmpty(entry.PayloadId) ? string.Empty : $"({entry.PayloadId})")} val={entry.Value:0.##}")
                    {
                        style = { fontSize = 10f, color = Color.white, unityFontStyleAndWeight = FontStyle.Bold }
                    };

                    lane.Add(bar);
                    lane.Add(laneLabel);
                    trackContainer.Add(lane);
                }
            }
            card.Add(trackContainer);

            // Quick Add / Sort Timeline Actions
            VisualElement btnRow = new() { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            AddQuickTimelineActionButton(btnRow, timeline, "+ Telegraph", AttackTimelineAction.Telegraph, "default_telegraph", 0.5f);
            AddQuickTimelineActionButton(btnRow, timeline, "+ Parry Window", AttackTimelineAction.OpenParryWindow, string.Empty, 0.35f);
            AddQuickTimelineActionButton(btnRow, timeline, "+ Perfect Dodge Window", AttackTimelineAction.OpenPerfectDodgeWindow, string.Empty, 0.35f);
            AddQuickTimelineActionButton(btnRow, timeline, "+ Melee Hit", AttackTimelineAction.MeleeHit, string.Empty, 18f);
            AddQuickTimelineActionButton(btnRow, timeline, "+ Projectile", AttackTimelineAction.SpawnProjectile, "default_projectile", 0f);
            AddQuickTimelineActionButton(btnRow, timeline, "+ Pattern", AttackTimelineAction.SpawnPattern, "radial_burst", 0f);
            AddQuickTimelineActionButton(btnRow, timeline, "+ Camera Shake", AttackTimelineAction.CameraShake, "shake_light", 0.25f);

            Button sortBtn = new(() => SortTimelineEntriesByTime(timeline)) { text = "Sort Entries by Time" };
            sortBtn.AddToClassList("secondary-button");
            btnRow.Add(sortBtn);

            card.Add(btnRow);
            parent.Add(card);
        }

        private static bool IsWindowOrDurationAction(AttackTimelineAction action)
        {
            return action is AttackTimelineAction.Telegraph
                or AttackTimelineAction.OpenParryWindow
                or AttackTimelineAction.OpenPerfectDodgeWindow
                or AttackTimelineAction.InvulnerabilityWindow;
        }

        private static Color GetActionColor(AttackTimelineAction action)
        {
            return action switch
            {
                AttackTimelineAction.Telegraph => new Color(0.85f, 0.5f, 0.12f, 0.85f),
                AttackTimelineAction.OpenParryWindow => new Color(0.9f, 0.72f, 0.12f, 0.9f),
                AttackTimelineAction.OpenPerfectDodgeWindow => new Color(0.18f, 0.7f, 0.9f, 0.9f),
                AttackTimelineAction.MeleeHit => new Color(0.88f, 0.22f, 0.22f, 0.9f),
                AttackTimelineAction.SpawnProjectile => new Color(0.92f, 0.38f, 0.16f, 0.9f),
                AttackTimelineAction.SpawnPattern => new Color(0.75f, 0.25f, 0.85f, 0.9f),
                AttackTimelineAction.Dash => new Color(0.28f, 0.65f, 0.45f, 0.9f),
                AttackTimelineAction.CameraShake => new Color(0.55f, 0.45f, 0.75f, 0.9f),
                _ => new Color(0.35f, 0.42f, 0.55f, 0.9f)
            };
        }

        private void AddQuickTimelineActionButton(
            VisualElement parent,
            AttackTimelineDefinition timeline,
            string label,
            AttackTimelineAction action,
            string defaultPayload,
            float defaultValue)
        {
            Button btn = new(() =>
            {
                Undo.RecordObject(timeline, "Add Timeline Action");
                SerializedObject so = new(timeline);
                SerializedProperty list = so.FindProperty("entries");
                int idx = list.arraySize;
                list.InsertArrayElementAtIndex(idx);
                SerializedProperty elem = list.GetArrayElementAtIndex(idx);
                elem.FindPropertyRelative("time").floatValue = Mathf.Clamp(timeline.Duration * 0.5f, 0f, timeline.Duration);
                elem.FindPropertyRelative("action").enumValueIndex = (int)action;
                elem.FindPropertyRelative("payloadId").stringValue = defaultPayload;
                elem.FindPropertyRelative("value").floatValue = defaultValue;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(timeline);
                RebuildWorkspace();
            })
            { text = label };
            btn.AddToClassList("secondary-button");
            btn.style.marginBottom = 4f;
            parent.Add(btn);
        }

        private void SortTimelineEntriesByTime(AttackTimelineDefinition timeline)
        {
            if (timeline == null || timeline.Entries == null) return;
            Undo.RecordObject(timeline, "Sort Timeline Entries");
            List<(float time, AttackTimelineAction action, string payloadId, float value)> sorted = timeline.Entries
                .OrderBy(e => e.Time)
                .Select(e => (e.Time, e.Action, e.PayloadId, e.Value))
                .ToList();
            BossFrameworkInstaller.ConfigureTimeline(timeline, timeline.Duration, sorted.ToArray());
            RebuildWorkspace();
        }

        private void BuildPatternCustomExtras(VisualElement parent, Object asset)
        {
            if (asset is not ProjectilePatternDefinition pattern) return;

            VisualElement card = new();
            card.AddToClassList("card");
            Label title = new($"2D Pattern Trajectory Preview — {pattern.Id} ({pattern.Kind}, {pattern.Waves} wave(s) x {pattern.Count} projectiles)");
            title.AddToClassList("card-title");
            card.Add(title);

            IMGUIContainer previewCanvas = new(() =>
            {
                Rect rect = GUILayoutUtility.GetRect(260f, 220f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(rect, new Color(0.08f, 0.1f, 0.13f, 1f));

                Vector2 center = rect.center;
                float scale = Mathf.Min(rect.width, rect.height) * 0.36f;

                Handles.BeginGUI();
                Handles.color = new Color(1f, 1f, 1f, 0.12f);
                Handles.DrawWireDisc(center, Vector3.forward, scale);
                Handles.DrawWireDisc(center, Vector3.forward, scale * 0.5f);

                List<PatternShotSample> samples = new();
                pattern.EvaluateSamples(Vector3.forward, samples);
                int totalWaves = Mathf.Max(1, pattern.Waves);
                for (int i = 0; i < samples.Count; i++)
                {
                    PatternShotSample sample = samples[i];
                    float waveRatio = sample.WaveIndex / (float)totalWaves;
                    Handles.color = Color.Lerp(new Color(1f, 0.58f, 0.14f, 0.95f), new Color(0.25f, 0.8f, 1f, 0.95f), waveRatio);

                    Vector2 dir = new Vector2(sample.Direction.x, -sample.Direction.z).normalized;
                    Vector2 start = center + dir * 10f;
                    Vector2 end = center + dir * (scale * (0.65f + 0.3f * waveRatio));
                    Handles.DrawLine(start, end);
                    Handles.DrawSolidDisc(end, Vector3.forward, 3.2f);
                }

                Handles.color = new Color(0.9f, 0.25f, 0.25f, 1f);
                Handles.DrawSolidDisc(center, Vector3.forward, 5f);
                Handles.EndGUI();
            });
            card.Add(previewCanvas);
            parent.Add(card);
        }

        private void BuildTelegraphCustomExtras(VisualElement parent, Object asset)
        {
            if (asset is not TelegraphDefinition telegraph) return;

            VisualElement card = new();
            card.AddToClassList("card");
            Label title = new($"Telegraph Shape & Color Preview — {telegraph.Id} ({telegraph.Shape}, Radius {telegraph.Radius:0.0}m, Duration {telegraph.Duration:0.00}s)");
            title.AddToClassList("card-title");
            card.Add(title);

            IMGUIContainer preview = new(() =>
            {
                Rect rect = GUILayoutUtility.GetRect(240f, 140f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(rect, new Color(0.08f, 0.1f, 0.13f, 1f));
                Handles.BeginGUI();
                Vector2 leftCenter = new(rect.x + rect.width * 0.3f, rect.center.y);
                Vector2 rightCenter = new(rect.x + rect.width * 0.7f, rect.center.y);
                float r = Mathf.Min(rect.height * 0.35f, 45f);

                Handles.color = telegraph.Color;
                Handles.DrawSolidDisc(leftCenter, Vector3.forward, r);
                Handles.color = telegraph.DangerColor;
                Handles.DrawSolidDisc(rightCenter, Vector3.forward, r);
                Handles.EndGUI();

                GUI.Label(new Rect(leftCenter.x - 45f, rect.yMax - 22f, 95f, 18f), "Warning Phase");
                GUI.Label(new Rect(rightCenter.x - 45f, rect.yMax - 22f, 95f, 18f), "Danger Phase");
            });
            card.Add(preview);
            parent.Add(card);
        }

        private void BuildFeedbackCustomExtras(VisualElement parent, Object asset)
        {
            if (asset is AudioCueDefinition audioCue)
            {
                VisualElement card = new();
                card.AddToClassList("card");
                Label title = new($"Audio Cue Preview — {audioCue.Id}");
                title.AddToClassList("card-title");
                bool hasClip = audioCue.Clips != null && audioCue.Clips.Length > 0 && audioCue.Clips[0] != null;
                Label sub = new(hasClip
                    ? $"Assigned AudioClip: {audioCue.Clips[0].name}"
                    : $"No AudioClip assigned — Procedural synthesized tone fallback ({audioCue.ProceduralFallbackFrequency:0} Hz, {audioCue.ProceduralFallbackDuration:0.00}s) will play automatically.");
                sub.AddToClassList("card-subtitle");
                card.Add(title);
                card.Add(sub);
                parent.Add(card);
            }
        }

        private void BuildPlayerCustomExtras(VisualElement parent, Object asset)
        {
            if (asset is not PlayerDefinition playerDef) return;

            VisualElement card = new();
            card.AddToClassList("card");
            Label title = new("Animator Controller Builder (Free Locomotion + Lock-On Strafe)");
            title.AddToClassList("card-title");
            Label sub = new("Generates a complete AnimatorController with 1D Free Locomotion BlendTree, 2D Lock-On Strafe BlendTree, combat action states, and transitions. PlayerController also works 100% without an Animator assigned.");
            sub.AddToClassList("card-subtitle");
            sub.style.whiteSpace = WhiteSpace.Normal;
            card.Add(title);
            card.Add(sub);

            Button buildAnimatorBtn = new(() =>
            {
                PlayerAnimatorBuilder.BuildAnimatorController(playerDef);
                RebuildWorkspace();
            })
            { text = "Generate / Update Animator Controller" };
            buildAnimatorBtn.AddToClassList("primary-button");
            card.Add(buildAnimatorBtn);

            List<string> clipReport = PlayerAnimatorBuilder.ValidateClips(playerDef);
            foreach (string line in clipReport)
            {
                Label note = new(line) { style = { fontSize = 11f, color = new Color(0.72f, 0.78f, 0.86f), marginTop = 2f } };
                card.Add(note);
            }

            parent.Add(card);
        }

        // =====================================================================
        // 9. SCENES & DEMOS WORKSPACE
        // =====================================================================
        private void BuildScenesAndDemosWorkspace()
        {
            ScrollView scroll = new() { style = { flexGrow = 1f, paddingLeft = 18f, paddingRight = 18f, paddingTop = 14f, paddingBottom = 14f } };

            List<BossDemoProfile> profiles = BossFrameworkValidator.LoadAssets<BossDemoProfile>().ToList();
            if (profiles.Count == 0)
            {
                VisualElement emptyCard = new();
                emptyCard.AddToClassList("card");
                emptyCard.Add(new Label("No BossDemoProfile assets found. Click below to install starter demo profiles."));
                Button installBtn = new(() =>
                {
                    BossFrameworkInstaller.InstallOrRepair(false);
                    RebuildWorkspace();
                })
                { text = "Install Starter Demo Profiles" };
                installBtn.AddToClassList("primary-button");
                emptyCard.Add(installBtn);
                scroll.Add(emptyCard);
                _contentAreaContainer.Add(scroll);
                return;
            }

            foreach (BossDemoProfile profile in profiles)
            {
                VisualElement card = new();
                card.AddToClassList("card");

                bool sceneExists = !string.IsNullOrWhiteSpace(profile.ScenePath) && File.Exists(profile.ScenePath);

                VisualElement headerRow = new() { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };
                Label title = new($"{profile.DisplayName} ({profile.Id})");
                title.AddToClassList("card-title");
                Label badge = new(sceneExists ? "SCENE READY" : "SCENE NOT GENERATED");
                badge.AddToClassList(sceneExists ? "badge-ok" : "badge-warn");
                headerRow.Add(title);
                headerRow.Add(badge);
                card.Add(headerRow);

                Label desc = new(profile.Description);
                desc.AddToClassList("card-subtitle");
                desc.style.whiteSpace = WhiteSpace.Normal;
                card.Add(desc);

                Label info = new(
                    $"Scene Path: {profile.ScenePath}\n" +
                    $"Boss: {(profile.BossDefinition != null ? profile.BossDefinition.DisplayName : "None")}   |   " +
                    $"Player: {(profile.PlayerDefinition != null ? profile.PlayerDefinition.DisplayName : "None")}   |   " +
                    $"Payload Library: {(profile.PayloadLibrary != null ? profile.PayloadLibrary.name : "None")}")
                {
                    style = { fontSize = 11f, color = new Color(0.75f, 0.8f, 0.88f), marginBottom = 8f }
                };
                card.Add(info);

                VisualElement actions = new() { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
                BossDemoProfile captured = profile;

                Button generateBtn = new(() =>
                {
                    BossFrameworkValidator.GenerateSceneForDemoProfile(captured);
                    RebuildWorkspace();
                })
                { text = sceneExists ? "Rebuild / Repair Scene" : "Create Demo Scene" };
                generateBtn.AddToClassList("primary-button");
                actions.Add(generateBtn);

                if (sceneExists)
                {
                    Button openBtn = new(() =>
                    {
                        EditorSceneManager.OpenScene(captured.ScenePath);
                    })
                    { text = "Open Scene" };
                    openBtn.AddToClassList("secondary-button");
                    actions.Add(openBtn);
                }

                Button filterBtn = new(() =>
                {
                    filterByDemoContext = true;
                    activeDemoContext = captured;
                    BuildTopBarActions();
                    SelectSection(DashboardSection.Bosses);
                })
                { text = "Set as Active Demo Context" };
                filterBtn.AddToClassList("secondary-button");
                actions.Add(filterBtn);

                Button dupBtn = new(() =>
                {
                    BossDemoProfile copy = BossDemoDuplicator.DuplicateDemoProfile(captured);
                    if (copy != null)
                    {
                        activeDemoContext = copy;
                        filterByDemoContext = true;
                        BuildTopBarActions();
                        RebuildWorkspace();
                    }
                })
                { text = "Duplicate Demo (Isolated Copy)" };
                dupBtn.AddToClassList("secondary-button");
                actions.Add(dupBtn);

                Button resetBtn = new(() =>
                {
                    if (EditorUtility.DisplayDialog(
                            "Reset Demo to Defaults",
                            $"Reset '{captured.DisplayName}' starter assets and regenerate its scene?",
                            "Reset",
                            "Cancel"))
                    {
                        BossFrameworkInstaller.InstallOrRepair(overwriteExisting: true);
                        BossFrameworkValidator.GenerateSceneForDemoProfile(captured);
                        RebuildWorkspace();
                    }
                })
                { text = "Reset Demo to Factory Defaults" };
                resetBtn.AddToClassList("secondary-button");
                actions.Add(resetBtn);

                card.Add(actions);
                scroll.Add(card);
            }

            _contentAreaContainer.Add(scroll);
        }

        // =====================================================================
        // 10. SAVE DATA / PLAYERPREFS WORKSPACE
        // =====================================================================
        private void BuildSaveDataWorkspace()
        {
            EnsurePreferencesService();

            ScrollView scroll = new() { style = { flexGrow = 1f, paddingLeft = 18f, paddingRight = 18f, paddingTop = 14f, paddingBottom = 14f } };

            // Top Profile & Controls Card
            VisualElement controlsCard = new();
            controlsCard.AddToClassList("card");
            Label controlsTitle = new("Save Profile & Scoped PlayerPrefs Controls");
            controlsTitle.AddToClassList("card-title");
            Label controlsSub = new("All keys are strictly namespaced under 'tappytale.bossframework.<profile>.<key>'. PlayerPrefs.DeleteAll() is never called.");
            controlsSub.AddToClassList("card-subtitle");
            controlsCard.Add(controlsTitle);
            controlsCard.Add(controlsSub);

            VisualElement filterRow = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexWrap = Wrap.Wrap } };

            List<string> profiles = new() { "default", "duel_lab", "playtest", "slot_1", "slot_2" };
            if (!profiles.Contains(activeSaveProfile)) profiles.Add(activeSaveProfile);

            PopupField<string> profilePopup = new("Save Profile", profiles, activeSaveProfile)
            {
                style = { minWidth = 200f, marginRight = 10f }
            };
            profilePopup.labelElement.style.minWidth = 75f;
            profilePopup.RegisterValueChangedCallback(evt =>
            {
                activeSaveProfile = evt.newValue;
                RebuildWorkspace();
            });
            filterRow.Add(profilePopup);

            List<string> categories = new() { "All", "Duel Telemetry", "Audio & Feedback", "HUD & Readability", "Debug" };
            PopupField<string> categoryPopup = new("Category", categories, prefsCategoryFilter)
            {
                style = { minWidth = 175f, marginRight = 10f }
            };
            categoryPopup.labelElement.style.minWidth = 60f;
            categoryPopup.RegisterValueChangedCallback(evt =>
            {
                prefsCategoryFilter = evt.newValue;
                RebuildWorkspace();
            });
            filterRow.Add(categoryPopup);

            TextField searchField = new("Filter Key")
            {
                value = prefsSearchQuery,
                style = { minWidth = 220f, marginRight = 10f }
            };
            searchField.labelElement.style.minWidth = 65f;
            searchField.RegisterValueChangedCallback(evt =>
            {
                prefsSearchQuery = evt.newValue;
                RebuildWorkspace();
            });
            filterRow.Add(searchField);

            Button resetProfileBtn = new(() =>
            {
                if (EditorUtility.DisplayDialog(
                        "Reset Profile Keys",
                        $"Reset all registered Tappy Tale keys in profile '{activeSaveProfile}' back to their defaults?",
                        "Reset Profile",
                        "Cancel"))
                {
                    _preferencesService.ResetProfile(activeSaveProfile, deleteKeys: true);
                    SetStatus($"Reset profile '{activeSaveProfile}'.");
                    RebuildWorkspace();
                }
            })
            { text = $"Reset Profile '{activeSaveProfile}'" };
            resetProfileBtn.AddToClassList("secondary-button");
            filterRow.Add(resetProfileBtn);

            Button exportJsonBtn = new(() =>
            {
                string json = _preferencesService.ExportSnapshotJson(activeSaveProfile, prettyPrint: true);
                EditorGUIUtility.systemCopyBuffer = json;
                string savePath = EditorUtility.SaveFilePanel("Export PlayerPrefs Snapshot", Application.dataPath, $"BossPrefs_{activeSaveProfile}.json", "json");
                if (!string.IsNullOrEmpty(savePath))
                {
                    File.WriteAllText(savePath, json);
                    SetStatus($"Exported snapshot to {savePath} (and copied to clipboard).");
                }
                else
                {
                    SetStatus("Copied PlayerPrefs JSON snapshot to clipboard.");
                }
            })
            { text = "Export JSON Snapshot" };
            exportJsonBtn.AddToClassList("secondary-button");
            filterRow.Add(exportJsonBtn);

            Button importJsonBtn = new(() =>
            {
                string openPath = EditorUtility.OpenFilePanel("Import PlayerPrefs Snapshot", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(openPath) && File.Exists(openPath))
                {
                    string json = File.ReadAllText(openPath);
                    int count = _preferencesService.ImportSnapshotJson(json, activeSaveProfile);
                    SetStatus($"Imported {count} preference entries into profile '{activeSaveProfile}'.");
                    RebuildWorkspace();
                }
            })
            { text = "Import JSON Snapshot" };
            importJsonBtn.AddToClassList("secondary-button");
            filterRow.Add(importJsonBtn);

            controlsCard.Add(filterRow);
            scroll.Add(controlsCard);

            // Live Preferences Table Card
            VisualElement tableCard = new();
            tableCard.AddToClassList("card");
            Label tableTitle = new($"Registered Keys in Profile '{activeSaveProfile}'");
            tableTitle.AddToClassList("card-title");
            tableCard.Add(tableTitle);

            if (_preferencesService.RegisteredKeys != null)
            {
                foreach (PreferenceKeyDefinition entry in _preferencesService.RegisteredKeys)
                {
                    if (prefsCategoryFilter != "All" && !string.Equals(entry.Category, prefsCategoryFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (!string.IsNullOrWhiteSpace(prefsSearchQuery) &&
                        entry.Key.IndexOf(prefsSearchQuery, StringComparison.OrdinalIgnoreCase) < 0 &&
                        entry.Description.IndexOf(prefsSearchQuery, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    BuildPreferenceEntryRow(tableCard, entry);
                }
            }

            scroll.Add(tableCard);
            _contentAreaContainer.Add(scroll);
        }

        private void BuildPreferenceEntryRow(VisualElement parent, PreferenceKeyDefinition entry)
        {
            string fullKey = entry.BuildFullKey(activeSaveProfile);
            bool hasSavedValue = _preferencesService.HasSavedValue(activeSaveProfile, entry);

            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.SpaceBetween,
                    paddingTop = 6f, paddingBottom = 6f, paddingLeft = 8f, paddingRight = 8f,
                    borderBottomWidth = 1f, borderBottomColor = new Color(0.18f, 0.21f, 0.27f)
                }
            };

            VisualElement infoCol = new() { style = { width = 340f } };
            Label nameLabel = new($"{entry.Key} [{entry.Category}] ({entry.ValueType})")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12f }
            };
            Label keyLabel = new($"{fullKey} — {entry.Description}")
            {
                style = { fontSize = 10f, color = new Color(0.58f, 0.64f, 0.75f), whiteSpace = WhiteSpace.Normal }
            };
            infoCol.Add(nameLabel);
            infoCol.Add(keyLabel);
            row.Add(infoCol);

            VisualElement valueCol = new() { style = { flexGrow = 1f, marginLeft = 10f, marginRight = 10f } };
            switch (entry.ValueType)
            {
                case PreferenceValueType.Int:
                {
                    IntegerField field = new("Value") { value = _preferencesService.GetInt(activeSaveProfile, entry.Key) };
                    field.labelElement.style.minWidth = 45f;
                    field.RegisterValueChangedCallback(evt =>
                    {
                        _preferencesService.SetInt(activeSaveProfile, entry.Key, evt.newValue);
                        SetStatus($"Updated {fullKey} = {evt.newValue}");
                    });
                    valueCol.Add(field);
                    break;
                }
                case PreferenceValueType.Float:
                {
                    FloatField field = new("Value") { value = _preferencesService.GetFloat(activeSaveProfile, entry.Key) };
                    field.labelElement.style.minWidth = 45f;
                    field.RegisterValueChangedCallback(evt =>
                    {
                        _preferencesService.SetFloat(activeSaveProfile, entry.Key, evt.newValue);
                        SetStatus($"Updated {fullKey} = {evt.newValue:0.###}");
                    });
                    valueCol.Add(field);
                    break;
                }
                default:
                {
                    TextField field = new("Value") { value = _preferencesService.GetString(activeSaveProfile, entry.Key) };
                    field.labelElement.style.minWidth = 45f;
                    field.RegisterValueChangedCallback(evt =>
                    {
                        _preferencesService.SetString(activeSaveProfile, entry.Key, evt.newValue);
                        SetStatus($"Updated {fullKey} = '{evt.newValue}'");
                    });
                    valueCol.Add(field);
                    break;
                }
            }
            row.Add(valueCol);

            VisualElement actionCol = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            Label stateBadge = new(hasSavedValue ? "STORED" : $"DEFAULT ({entry.FormatDefaultValue()})");
            stateBadge.AddToClassList(hasSavedValue ? "badge-ok" : "badge-warn");
            stateBadge.style.marginRight = 6f;
            actionCol.Add(stateBadge);

            Button resetKeyBtn = new(() =>
            {
                _preferencesService.ResetKeyToDefault(activeSaveProfile, entry.Key, deleteKey: true);
                SetStatus($"Reset {fullKey} to default ({entry.FormatDefaultValue()}).");
                RebuildWorkspace();
            })
            { text = "Reset" };
            resetKeyBtn.AddToClassList("secondary-button");
            actionCol.Add(resetKeyBtn);

            row.Add(actionCol);
            parent.Add(row);
        }

        // =====================================================================
        // 11. VALIDATION & AUTO-REPAIR WORKSPACE
        // =====================================================================
        private void BuildValidationWorkspace()
        {
            _lastValidationReport ??= BossFrameworkValidator.ValidateProject();

            ScrollView scroll = new() { style = { flexGrow = 1f, paddingLeft = 18f, paddingRight = 18f, paddingTop = 14f, paddingBottom = 14f } };

            VisualElement summaryCard = new();
            summaryCard.AddToClassList("card");
            VisualElement topRow = new() { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };

            Label summaryLabel = new($"Errors: {_lastValidationReport.ErrorCount}   |   Warnings: {_lastValidationReport.WarningCount}   |   Info: {_lastValidationReport.InfoCount}");
            summaryLabel.AddToClassList("card-title");
            topRow.Add(summaryLabel);

            VisualElement btnRow = new() { style = { flexDirection = FlexDirection.Row } };
            Button runBtn = new(() =>
            {
                _lastValidationReport = BossFrameworkValidator.ValidateProject();
                RebuildWorkspace();
            })
            { text = "Re-Run Full Validation" };
            runBtn.AddToClassList("primary-button");

            Button fixAllBtn = new(() =>
            {
                int fixedCount = BossFrameworkValidator.FixAllAutoFixable(_lastValidationReport);
                _lastValidationReport = BossFrameworkValidator.ValidateProject();
                SetStatus($"Executed {fixedCount} safe auto-fix actions.");
                RebuildWorkspace();
            })
            { text = "Auto-Fix All Fixable Issues" };
            fixAllBtn.AddToClassList("secondary-button");

            btnRow.Add(runBtn);
            btnRow.Add(fixAllBtn);
            topRow.Add(btnRow);
            summaryCard.Add(topRow);
            scroll.Add(summaryCard);

            foreach (BossValidationIssue issue in _lastValidationReport.Issues)
            {
                VisualElement issueCard = new();
                issueCard.AddToClassList("card");

                VisualElement row = new() { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };

                VisualElement leftCol = new() { style = { flexShrink = 1f } };
                VisualElement badgeRow = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4f } };
                Label badge = new($"{issue.Severity.ToString().ToUpperInvariant()} • {issue.Category}");
                badge.AddToClassList(issue.Severity switch
                {
                    BossValidationSeverity.Error => "badge-err",
                    BossValidationSeverity.Warning => "badge-warn",
                    _ => "badge-ok"
                });
                badgeRow.Add(badge);
                leftCol.Add(badgeRow);

                Label msg = new(issue.Message) { style = { fontSize = 12f, whiteSpace = WhiteSpace.Normal } };
                Label path = new(issue.AssetPath) { style = { fontSize = 10f, color = new Color(0.58f, 0.64f, 0.74f), marginTop = 2f } };
                leftCol.Add(msg);
                if (!string.IsNullOrEmpty(issue.AssetPath)) leftCol.Add(path);
                row.Add(leftCol);

                VisualElement actions = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                if (!string.IsNullOrEmpty(issue.AssetPath))
                {
                    Object asset = AssetDatabase.LoadMainAssetAtPath(issue.AssetPath);
                    if (asset != null)
                    {
                        Button pingBtn = new(() => JumpToAsset(asset)) { text = "Inspect Asset" };
                        pingBtn.AddToClassList("secondary-button");
                        actions.Add(pingBtn);
                    }
                }

                if (issue.CanAutoFix)
                {
                    BossValidationIssue captured = issue;
                    Button fixBtn = new(() =>
                    {
                        captured.FixAction.Invoke();
                        AssetDatabase.SaveAssets();
                        _lastValidationReport = BossFrameworkValidator.ValidateProject();
                        RebuildWorkspace();
                    })
                    { text = captured.FixLabel };
                    fixBtn.AddToClassList("primary-button");
                    actions.Add(fixBtn);
                }

                row.Add(actions);
                issueCard.Add(row);
                scroll.Add(issueCard);
            }

            _contentAreaContainer.Add(scroll);
        }

        // =====================================================================
        // 12. SETTINGS & LIVE RUNTIME DEBUG WORKSPACE
        // =====================================================================
        private void BuildSettingsAndDebugWorkspace()
        {
            ScrollView scroll = new() { style = { flexGrow = 1f, paddingLeft = 18f, paddingRight = 18f, paddingTop = 14f, paddingBottom = 14f } };

            VisualElement debugCard = new();
            debugCard.AddToClassList("card");
            Label title = new("Live PlayMode Runtime Debugger");
            title.AddToClassList("card-title");
            debugCard.Add(title);

            if (!Application.isPlaying)
            {
                Label info = new("Enter Play Mode in Boss Duel Lab (or any Boss Fight scene) to inspect live BossRuntime & PlayerRuntime state and trigger combat test commands.");
                info.AddToClassList("card-subtitle");
                debugCard.Add(info);
            }
            else
            {
                BossDirector bossDirector = FindAnyObjectByType<BossDirector>();
                PlayerController playerController = FindAnyObjectByType<PlayerController>();

                if (bossDirector != null && bossDirector.Runtime != null)
                {
                    BossRuntime br = bossDirector.Runtime;
                    BossBlackboard bb = br.Context.Blackboard;
                    Label bossInfo = new(
                        $"Active Boss: {bossDirector.Definition?.DisplayName ?? bossDirector.name}\n" +
                        $"Health: {bb.CurrentHealth:0} / {bb.MaxHealth:0} ({bb.NormalizedHealth * 100f:0}%)\n" +
                        $"Current Phase: {br.Phases?.CurrentPhase?.Definition?.DisplayName ?? "None"}   |   " +
                        $"Staggered: {bb.IsStaggered}   |   Invulnerable: {br.Health.IsInvulnerable}\n" +
                        $"Parry Window Open: {br.ReactionWindows.IsParryWindowOpen}   |   Perfect Dodge Window Open: {br.ReactionWindows.IsPerfectDodgeWindowOpen}")
                    {
                        style = { fontSize = 12f, marginBottom = 8f, whiteSpace = WhiteSpace.Normal }
                    };
                    debugCard.Add(bossInfo);

                    VisualElement bossActions = new() { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginBottom = 10f } };
                    Button damageBossBtn = new(() =>
                    {
                        br.Health.ApplyDamage(new TappyTale.BossFight.Combat.BossDamageRequest(75f, "EditorDebug"));
                        RebuildWorkspace();
                    })
                    { text = "Deal 75 Damage to Boss" };
                    damageBossBtn.AddToClassList("primary-button");

                    Button staggerBossBtn = new(() =>
                    {
                        br.ReactionWindows.TriggerStagger(2.5f);
                        RebuildWorkspace();
                    })
                    { text = "Force Stagger (2.5s)" };
                    staggerBossBtn.AddToClassList("secondary-button");

                    Button resetFightBtn = new(() =>
                    {
                        bossDirector.ResetFight();
                        playerController?.ResetPlayer();
                        RebuildWorkspace();
                    })
                    { text = "Reset Fight" };
                    resetFightBtn.AddToClassList("secondary-button");

                    bossActions.Add(damageBossBtn);
                    bossActions.Add(staggerBossBtn);
                    bossActions.Add(resetFightBtn);
                    debugCard.Add(bossActions);
                }

                if (playerController != null && playerController.Runtime != null)
                {
                    PlayerBlackboard pb = playerController.Runtime.Blackboard;
                    Label playerInfo = new(
                        $"Player State: {pb.State}   |   Health: {pb.CurrentHealth:0}/{pb.MaxHealth:0} ({pb.NormalizedHealth * 100f:0}%)\n" +
                        $"Grounded: {pb.IsGrounded}   |   Time Since Grounded: {pb.TimeSinceGrounded:0.00}s   |   Air Jumps Left: {pb.AirJumpsRemaining}\n" +
                        $"Invulnerable: {pb.IsInvulnerable}   |   Parry Active: {pb.IsParryWindowActive}   |   Hard Locked: {pb.IsHardLocked}")
                    {
                        style = { fontSize = 12f, whiteSpace = WhiteSpace.Normal }
                    };
                    debugCard.Add(playerInfo);
                }
            }

            scroll.Add(debugCard);
            _contentAreaContainer.Add(scroll);
        }

        // =====================================================================
        // ASSET CREATION & DUPLICATION HELPERS
        // =====================================================================
        private static T CreateNewScriptableObject<T>(string defaultName) where T : ScriptableObject
        {
            BossFrameworkInstaller.EnsureFolderHierarchy(BossFrameworkInstaller.DataFolder);
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath($"{BossFrameworkInstaller.DataFolder}/{defaultName}.asset");
            T instance = CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, uniquePath);
            BossFrameworkInstaller.Configure(instance, "id", Path.GetFileNameWithoutExtension(uniquePath).ToLowerInvariant());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(instance);
            return instance;
        }

        private static Object DuplicateAsset(Object source)
        {
            if (source == null) return null;
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath)) return null;
            string newPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            if (!AssetDatabase.CopyAsset(sourcePath, newPath)) return null;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Object copy = AssetDatabase.LoadMainAssetAtPath(newPath);
            if (copy != null)
            {
                BossFrameworkInstaller.Configure(copy, "id", Path.GetFileNameWithoutExtension(newPath).ToLowerInvariant());
            }
            return copy;
        }
    }
}
