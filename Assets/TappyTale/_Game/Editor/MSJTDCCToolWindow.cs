using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Welcome window for the Top-Down Character Controller asset. Opens automatically the first
/// time this package's Editor scripts compile in a project (i.e. right after import), and can
/// be reopened any time via Tools/MSJ-TDCC/ToolWindow. Also surfaces a documentation link, a
/// one-click setup for the layers this asset expects, and a one-day-later rating reminder.
/// </summary>
[MovedFrom(true, null, null, "MSJTDCCToolWindow")]
public class MSJTDCCToolWindow : EditorWindow
{
    private const string WindowTitle = "MSJ-TDCC";
    private const string AssetTitle = "Top-Down Character Controller";
    private const string AssetStorePageUrl = "https://assetstore.unity.com/preview/389880/1399806";

    /// <summary>How long after the first import to ask for a rating.</summary>
    private static readonly TimeSpan FirstRatePromptDelay = TimeSpan.FromDays(1);
    /// <summary>How far "Remind Me Later" pushes the next ask.</summary>
    private static readonly TimeSpan RateSnoozeDelay = TimeSpan.FromDays(3);
    /// <summary>Seconds between due-date checks. The prompt is day-scale, so this can be lazy.</summary>
    private const double RateCheckIntervalSeconds = 60;
    /// <summary>Quiet period after a domain reload, so an overdue prompt doesn't ambush project open.</summary>
    private const double RateStartupGraceSeconds = 20;

    private static readonly Color AccentGreen = new Color(0.35f, 0.75f, 0.35f);
    private static readonly Color RateButtonColor = new Color(0.30f, 0.68f, 0.36f);

    /// <summary>
    /// Layers this asset's prefabs are serialized against, and the exact index each was defined
    /// at in the source project. Setup tries to recreate them at the same index so the bundled
    /// prefabs' existing LayerMask/layer references (which are stored by index, not name) still
    /// resolve correctly after import into a new project.
    /// </summary>
    private static readonly (int index, string name)[] RequiredLayers =
    {
        (3, "Player"),
        (6, "Interactable"),
    };

    /// <summary>Tag required by <see cref="AdvancedFootstepSystem"/>'s per-surface sound lookup.</summary>
    private const string RequiredTag = "Ground_Concrete";

    // EditorPrefs are shared across every project on the machine, so each key is salted with a
    // hash of the project's Assets path to get per-project behaviour out of a global store.
    //
    // The salt uses an explicit FNV-1a rather than string.GetHashCode(): on CoreCLR, string
    // hashing is randomised per process, which would give this project a different key on every
    // Editor launch — and the welcome window would then reopen every single time.
    private static string ProjectSalt => StableHash(Application.dataPath).ToString("x8");
    private static string ShownPrefKey => $"MSJTDCC_WelcomeShown_{ProjectSalt}";
    /// <summary>UTC ticks of the next rating prompt, as a string (EditorPrefs has no long type).</summary>
    private static string RateDuePrefKey => $"MSJTDCC_RateDue_{ProjectSalt}";
    /// <summary>Set once the user has rated or opted out; no further prompts after this.</summary>
    private static string RateSettledPrefKey => $"MSJTDCC_RateSettled_{ProjectSalt}";

    private static double nextRateCheckTime;
    private static bool ratePromptQueued;

    private Vector2 scroll;
    private GUIStyle rateButtonStyle;

    [MenuItem("Tools/MSJ-TDCC/ToolWindow")]
    public static void Open()
    {
        var window = GetWindow<MSJTDCCToolWindow>(true, WindowTitle, true);
        window.minSize = new Vector2(440, 490);
        window.maxSize = new Vector2(440, 490);
    }

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        ShowOnFirstImport();

        // Safety net for projects that already had the asset before the reminder existed: without
        // this they would carry a "welcome shown" flag but no due date, and never be asked.
        if (!EditorPrefs.GetBool(RateSettledPrefKey, false) &&
            string.IsNullOrEmpty(EditorPrefs.GetString(RateDuePrefKey, string.Empty)))
        {
            ScheduleRatePrompt(FirstRatePromptDelay);
        }

        // Domain reloads cover Editor start and every recompile, but a project can sit open for
        // days without either. Polling as well means the reminder still lands on time.
        nextRateCheckTime = EditorApplication.timeSinceStartup + RateStartupGraceSeconds;
        EditorApplication.update += PollRatePrompt;
    }

    private static void ShowOnFirstImport()
    {
        if (EditorPrefs.GetBool(ShownPrefKey, false))
            return;

        EditorPrefs.SetBool(ShownPrefKey, true);
        ScheduleRatePrompt(FirstRatePromptDelay);
        EditorApplication.delayCall += Open;
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619;
            }
            return hash;
        }
    }

    #region Rating prompt

    private static void ScheduleRatePrompt(TimeSpan delay)
    {
        EditorPrefs.SetString(RateDuePrefKey, DateTime.UtcNow.Add(delay).Ticks.ToString());
    }

    /// <summary>Stops all future rating prompts for this project.</summary>
    private static void SettleRatePrompt()
    {
        EditorPrefs.SetBool(RateSettledPrefKey, true);
        EditorPrefs.DeleteKey(RateDuePrefKey);
    }

    private static void PollRatePrompt()
    {
        if (EditorApplication.timeSinceStartup < nextRateCheckTime)
            return;

        nextRateCheckTime = EditorApplication.timeSinceStartup + RateCheckIntervalSeconds;

        if (EditorPrefs.GetBool(RateSettledPrefKey, false))
        {
            EditorApplication.update -= PollRatePrompt;
            return;
        }

        if (ratePromptQueued || !IsRatePromptDue())
            return;

        // Never interrupt a compile or a play-mode transition; the next poll will catch it.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        ratePromptQueued = true;
        EditorApplication.delayCall += ShowRatePrompt;
    }

    private static bool IsRatePromptDue()
    {
        string raw = EditorPrefs.GetString(RateDuePrefKey, string.Empty);
        if (string.IsNullOrEmpty(raw) || !long.TryParse(raw, out long ticks))
            return false;

        return DateTime.UtcNow.Ticks >= ticks;
    }

    private static void ShowRatePrompt()
    {
        ratePromptQueued = false;

        if (EditorPrefs.GetBool(RateSettledPrefKey, false) || !IsRatePromptDue())
            return;

        int choice = EditorUtility.DisplayDialogComplex(
            $"Enjoying {AssetTitle}?",
            "If this asset has been useful, a rating on the Asset Store genuinely helps — it takes " +
            "less than a minute and makes a real difference for a small publisher.\n\nThanks for your support!",
            "Rate Now",
            "Remind Me Later",
            "Don't Show Again");

        switch (choice)
        {
            case 0:
                OpenRatingPage();
                break;

            case 1:
                ScheduleRatePrompt(RateSnoozeDelay);
                break;

            default:
                SettleRatePrompt();
                break;
        }
    }

    /// <summary>Opens the Asset Store page and stops any further rating prompts.</summary>
    private static void OpenRatingPage()
    {
        SettleRatePrompt();
        Application.OpenURL(AssetStorePageUrl);
    }

    /// <summary>
    /// Clears this project's welcome/rating state and makes the rating prompt due immediately, so
    /// the whole flow can be checked without waiting a day. The prompt appears within a minute.
    /// </summary>
    [MenuItem("Tools/MSJ-TDCC/Reset Prompts (Testing)")]
    private static void ResetPrompts()
    {
        EditorPrefs.DeleteKey(ShownPrefKey);
        EditorPrefs.DeleteKey(RateSettledPrefKey);
        EditorPrefs.SetString(RateDuePrefKey, DateTime.UtcNow.Ticks.ToString());

        ratePromptQueued = false;
        nextRateCheckTime = 0;
        EditorApplication.update -= PollRatePrompt;
        EditorApplication.update += PollRatePrompt;

        Debug.Log("MSJ-TDCC: prompt state reset. The rating prompt is now due, and the welcome " +
                  "window will reopen on the next script recompile or Editor restart.");
    }

    #endregion

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Space(14);

        GUILayout.Label(AssetTitle, new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter
        });

        GUILayout.Space(4);

        GUILayout.Label("Asset imported successfully!", new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = AccentGreen }
        });

        GUILayout.Space(12);
        DrawSeparator();
        GUILayout.Space(12);

        GUILayout.Label("Documentation", EditorStyles.boldLabel);
        if (GUILayout.Button("Open Readme", GUILayout.Height(24)))
        {
            OpenReadme();
        }

        GUILayout.Space(14);
        DrawSeparator();
        GUILayout.Space(12);

        GUILayout.Label("Project Setup", EditorStyles.boldLabel);
        bool allLayersOk = true;
        foreach (var (index, name) in RequiredLayers)
        {
            LayerStatus status = GetLayerStatus(index, name);
            DrawChecklistRow($"Layer \"{name}\"", status == LayerStatus.Ok, status == LayerStatus.Conflict ? $"slot {index} used by another layer" : null);
            allLayersOk &= status == LayerStatus.Ok;
        }
        bool tagOk = TagExists(RequiredTag);
        DrawChecklistRow($"Tag \"{RequiredTag}\"", tagOk);

        GUILayout.Space(6);

        using (new EditorGUI.DisabledScope(allLayersOk && tagOk))
        {
            if (GUILayout.Button("Auto-Add Layers & Tag", GUILayout.Height(24)))
            {
                AutoSetupLayersAndTag();
            }
        }

        GUILayout.Space(14);
        DrawSeparator();
        GUILayout.Space(12);

        GUILayout.Label("Enjoying this asset?", EditorStyles.boldLabel);
        GUILayout.Space(4);

        // Built lazily: GUI.skin is only valid inside OnGUI.
        rateButtonStyle ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            hover = { textColor = Color.white },
            active = { textColor = Color.white },
            focused = { textColor = Color.white },
        };

        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = RateButtonColor;
        if (GUILayout.Button("★   Rate this Asset on the Asset Store", rateButtonStyle, GUILayout.Height(44)))
        {
            OpenRatingPage();
        }
        GUI.backgroundColor = previousBackground;

        GUILayout.Space(16);

        if (GUILayout.Button("Close", GUILayout.Height(28)))
        {
            Close();
        }

        GUILayout.Space(10);

        EditorGUILayout.EndScrollView();
    }

    private static void OpenReadme()
    {
        string path = Path.Combine(Application.dataPath, "MSJ-TDCC", "Readme.md");
        if (File.Exists(path))
            EditorUtility.OpenWithDefaultApp(path);
        else
            Debug.LogWarning($"MSJ-TDCC: Readme not found at {path}");
    }

    private enum LayerStatus { Ok, Missing, Conflict }

    private static LayerStatus GetLayerStatus(int index, string expectedName)
    {
        string current = LayerNameAt(index);
        if (current == expectedName) return LayerStatus.Ok;
        if (string.IsNullOrEmpty(current)) return LayerStatus.Missing;
        return LayerStatus.Conflict;
    }

    private static string LayerNameAt(int index)
    {
        SerializedObject tagManager = GetTagManager();
        SerializedProperty layers = tagManager.FindProperty("layers");
        return layers.GetArrayElementAtIndex(index).stringValue;
    }

    private static bool TagExists(string tag)
    {
        foreach (string existing in UnityEditorInternal.InternalEditorUtility.tags)
        {
            if (existing == tag) return true;
        }
        return false;
    }

    /// <summary>
    /// Creates any missing required layers at their expected index (skipping ones already
    /// correct, and refusing to overwrite a slot already occupied by a different layer name),
    /// and adds the required tag if missing.
    /// </summary>
    private static void AutoSetupLayersAndTag()
    {
        SerializedObject tagManager = GetTagManager();
        SerializedProperty layersProp = tagManager.FindProperty("layers");

        int conflicts = 0;
        foreach (var (index, name) in RequiredLayers)
        {
            SerializedProperty slot = layersProp.GetArrayElementAtIndex(index);
            if (slot.stringValue == name)
                continue;

            if (!string.IsNullOrEmpty(slot.stringValue))
            {
                conflicts++;
                continue;
            }

            slot.stringValue = name;
        }

        SerializedProperty tagsProp = tagManager.FindProperty("tags");
        bool tagExists = false;
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == RequiredTag)
            {
                tagExists = true;
                break;
            }
        }
        if (!tagExists)
        {
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = RequiredTag;
        }

        tagManager.ApplyModifiedProperties();
        tagManager.Update();

        if (conflicts > 0)
        {
            EditorUtility.DisplayDialog(
                "MSJ-TDCC Setup",
                $"Added what it could, but {conflicts} required layer slot(s) are already used by a different layer in this project. " +
                "Check the checklist above and resolve those manually in Edit > Project Settings > Tags and Layers.",
                "OK");
        }
    }

    private static SerializedObject GetTagManager()
    {
        return new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
    }

    private static void DrawChecklistRow(string label, bool isOk, string detail = null)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(isOk ? "✔" : "✕", new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = isOk ? AccentGreen : new Color(0.8f, 0.35f, 0.3f) },
            fixedWidth = 18
        });
        GUILayout.Label(detail != null ? $"{label} ({detail})" : label, EditorStyles.label);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawSeparator()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
    }
}
}
