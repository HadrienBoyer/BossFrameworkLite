using UnityEngine;
using UnityEditor;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Custom inspector for <see cref="UiManager"/>. Groups its own fields under a collapsible
/// "Menu Settings" section, and the linked <see cref="UIPopupManager"/>'s fields (via the
/// <c>popups</c> reference) under a collapsible "Popup Settings" section — each with quick
/// select/enable buttons for its tracked menus/popups.
/// </summary>
[CustomEditor(typeof(UiManager))]
[MovedFrom(true, null, null, "UIInspector")]
public class UIInspector : Editor
{
    private bool showMenuSettings = true;
    private bool showPopupSettings = true;

    UiManager manager;

    public override void OnInspectorGUI()
    {
        if (manager == null)
            manager = (UiManager)target;

        manager.Init();
        serializedObject.Update();

        EditorGUILayout.Space(5);

        showMenuSettings = EditorGUILayout.Foldout(showMenuSettings, "Menu Settings", true, EditorStyles.foldoutHeader);
        if (showMenuSettings)
        {
            EditorGUI.indentLevel++;
            DrawMenuSettings();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);

        showPopupSettings = EditorGUILayout.Foldout(showPopupSettings, "Popup Settings", true, EditorStyles.foldoutHeader);
        if (showPopupSettings)
        {
            EditorGUI.indentLevel++;
            DrawPopupSettings();
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMenuSettings()
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bg"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("menuParent"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hud"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("blackout"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("popups"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showBannerUiList"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showBgUiList"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("history"), true);

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox("ENUM (UIList) and Menu List order should match.", MessageType.Info);
        EditorGUILayout.LabelField("Press to enable menu", EditorStyles.miniBoldLabel);

        for (int i = 0; i < manager.menues.Count; i++)
        {
            if (manager.menues[i] == null) continue;

            string menuName = manager.menues[i].gameObject.name;

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(">", GUILayout.Width(30)))
                manager.SelectMenuFromEditor(i);

            if (GUILayout.Button(menuName))
                manager.EnableMenuFromEditor(i);

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawPopupSettings()
    {
        UIPopupManager popupManager = manager.popups;
        if (popupManager == null)
        {
            EditorGUILayout.HelpBox("No UIPopupManager assigned — set the \"Popups\" field under Menu Settings.", MessageType.Info);
            return;
        }

        popupManager.Init();
        SerializedObject popupSO = new SerializedObject(popupManager);
        popupSO.Update();

        EditorGUILayout.PropertyField(popupSO.FindProperty("popupsParent"));
        EditorGUILayout.PropertyField(popupSO.FindProperty("msgBox"));
        EditorGUILayout.PropertyField(popupSO.FindProperty("objectInspector"));
        EditorGUILayout.PropertyField(popupSO.FindProperty("codeMachine"));
        EditorGUILayout.PropertyField(popupSO.FindProperty("uiModeMenu"), true);
        EditorGUILayout.PropertyField(popupSO.FindProperty("history"), true);

        popupSO.ApplyModifiedProperties();

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox("ENUM (UIPopupList) and Popup List order should match.", MessageType.Info);

        if (GUILayout.Button("Disable All Popup"))
            popupManager.DisableAllFromEditor();

        EditorGUILayout.Space(3);

        for (int i = 0; i < popupManager.popups.Count; i++)
        {
            if (popupManager.popups[i] == null) continue;

            string popupName = popupManager.popups[i].gameObject.name;

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(">", GUILayout.Width(30)))
                popupManager.SelectMenuFromEditor(i);

            if (GUILayout.Button(popupName))
                popupManager.EnablePopupFromEditor(i);

            EditorGUILayout.EndHorizontal();
        }
    }
}
}
