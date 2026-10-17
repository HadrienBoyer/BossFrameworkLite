using UnityEngine;
using UnityEditor;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Custom inspector for <see cref="UIPopupManager"/>. Adds a "Disable All Popup" button and,
/// for each tracked popup, a select button (jumps to its GameObject in the Hierarchy) and an
/// enable button (shows that popup in the Editor).
/// </summary>
[CustomEditor(typeof(UIPopupManager))]
[MovedFrom(true, null, null, "PopupInspector")]
public class PopupInspector : Editor
{
    UIPopupManager manager;

    public override void OnInspectorGUI()
    {
        EditorGUILayout.Foldout(false, ">>>> INFO: ENUM and Popup List order should match");
        EditorGUILayout.Space(5);

        if (manager == null)
            manager = FindAnyObjectByType<UIPopupManager>();

        manager.Init();

        EditorGUILayout.Space(5);

        EditorGUILayout.Foldout(false, "Enable Popup");
        
        if (GUILayout.Button("Disable All Popup", GUILayout.Width(454)))
        {
            manager.DisableAllFromEditor();
        }

        EditorGUILayout.Space(3);

        for (int i = 0; i < manager.popups.Count; i++)
        {
            string menuName = manager.popups[i].gameObject.name;

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(">", GUILayout.Width(50)))
            {
                manager.SelectMenuFromEditor(i);
            }

            if (GUILayout.Button(menuName, GUILayout.Width(400)))
            {
                manager.EnablePopupFromEditor(i);
            }

            GUILayout.EndHorizontal();
        }


        EditorGUILayout.Space(20);

        DrawDefaultInspector();
    }
}
}
