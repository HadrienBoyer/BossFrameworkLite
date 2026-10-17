#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TappyTale.EditorTools.GameStudio
{
    internal sealed class GameStudioInlineInspector : IDisposable
    {
        private UnityEngine.Object m_Target;
        private UnityEditor.Editor m_Editor;
        private Vector2 m_Scroll;

        public UnityEngine.Object Target => m_Target;

        public void DrawTypeBrowser(
            string typeHint,
            string title)
        {
            Type type =
                GameStudioProjectScanner.FindTypeBySimpleOrFullName(
                    typeHint);

            if (type == null ||
                !typeof(UnityEngine.Object)
                    .IsAssignableFrom(type))
            {
                EditorGUILayout.HelpBox(
                    $"{typeHint} is not compiled in the project.",
                    MessageType.None);
                return;
            }

            EditorGUILayout.LabelField(
                title,
                EditorStyles.boldLabel);

            List<UnityEngine.Object> objects =
                FindObjects(type);

            if (objects.Count == 0)
            {
                EditorGUILayout.LabelField(
                    "No matching object found.",
                    EditorStyles.miniLabel);
                return;
            }

            foreach (UnityEngine.Object obj in objects.Take(24))
            {
                if (obj == null) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUIStyle style =
                        obj == m_Target
                            ? EditorStyles.miniButtonMid
                            : UnityEditor.EditorStyles.miniButton;

                    if (GUILayout.Button(
                        obj.name,
                        style,
                        GUILayout.Height(21f)))
                    {
                        SetTarget(obj);
                    }

                    if (GUILayout.Button(
                        "Ping",
                        GUILayout.Width(46f)))
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }
            }

            if (objects.Count > 24)
            {
                EditorGUILayout.LabelField(
                    $"+ {objects.Count - 24} more",
                    EditorStyles.miniLabel);
            }
        }

        public void DrawInspector()
        {
            if (m_Target == null)
            {
                return;
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"Inspector • {m_Target.name}",
                        EditorStyles.boldLabel);

                    if (GUILayout.Button(
                        "Clear",
                        GUILayout.Width(52f)))
                    {
                        SetTarget(null);
                        return;
                    }
                }

                m_Scroll =
                    EditorGUILayout.BeginScrollView(
                        m_Scroll,
                        GUILayout.MinHeight(180f),
                        GUILayout.MaxHeight(480f));

                if (m_Editor != null)
                {
                    m_Editor.OnInspectorGUI();
                }

                EditorGUILayout.EndScrollView();
            }
        }

        public void Dispose()
        {
            SetTarget(null);
        }

        private void SetTarget(
            UnityEngine.Object target)
        {
            if (m_Target == target)
            {
                return;
            }

            if (m_Editor != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    m_Editor);
            }

            m_Target = target;
            m_Editor =
                target != null
                    ? UnityEditor.Editor.CreateEditor(target)
                    : null;
        }

        private static List<UnityEngine.Object> FindObjects(
            Type type)
        {
            List<UnityEngine.Object> output = new();

            if (typeof(Component)
                .IsAssignableFrom(type))
            {
                UnityEngine.Object[] candidates =
                    Resources.FindObjectsOfTypeAll(type);

                for (int i = 0; i < candidates.Length; i++)
                {
                    UnityEngine.Object candidate = candidates[i];

                    if (candidate is not Component component)
                    {
                        continue;
                    }

                    if (EditorUtility.IsPersistent(component))
                    {
                        continue;
                    }

                    if (!component.gameObject.scene.IsValid() ||
                        !component.gameObject.scene.isLoaded)
                    {
                        continue;
                    }

                    output.Add(component);
                }
            }

            string[] guids =
                AssetDatabase.FindAssets(
                    $"t:{type.Name}",
                    new[] { "Assets/TappyTale" });

            foreach (string guid in guids)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(guid);

                UnityEngine.Object asset =
                    AssetDatabase.LoadAssetAtPath(
                        path,
                        type);

                if (asset != null &&
                    !output.Contains(asset))
                {
                    output.Add(asset);
                }
            }

            return output
                .Where(x => x != null)
                .OrderBy(x => x.name)
                .ToList();
        }
    }
}
#endif
