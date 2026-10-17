using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TappyTale.EditorTools
{
    [InitializeOnLoad]
    public static class HierarchyColorizer
    {
        static readonly Regex bgTagRegex = new Regex(@"\[bg=(#[0-9a-fA-F]{6})\]", RegexOptions.IgnoreCase);
        static readonly Regex colorTagRegex = new Regex(@"<color=(#[0-9a-fA-F]{6})>", RegexOptions.IgnoreCase);

        static HierarchyColorizer()
        {
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyGUI;
        }

        static void OnHierarchyGUI(EntityId entityId, Rect selectionRect)
        {
            GameObject go = EditorUtility.EntityIdToObject(entityId) as GameObject;
            if (go == null) return;

            string name = go.name;

            // 배경색 추출
            Match bgMatch = bgTagRegex.Match(name);
            bool hasBgColor = false;
            Color bgColor = new Color(0, 0, 0, 0);
            if (bgMatch.Success && ColorUtility.TryParseHtmlString(bgMatch.Groups[1].Value, out Color parsedBg))
            {
                hasBgColor = true;
                bgColor = parsedBg;
            }

            // 텍스트 색상 추출
            Match colorMatch = colorTagRegex.Match(name);
            bool hasTextColor = false;
            Color textColor = Color.white;
            if (colorMatch.Success && ColorUtility.TryParseHtmlString(colorMatch.Groups[1].Value, out Color parsedColor))
            {
                hasTextColor = true;
                textColor = parsedColor;
            }

            // 태그 제거
            string cleanName = bgTagRegex.Replace(name, "");
            cleanName = colorTagRegex.Replace(cleanName, "");
            cleanName = cleanName.Replace("</color>", "");

            Rect labelRect = new Rect(selectionRect.x + 16, selectionRect.y, selectionRect.width - 16, selectionRect.height);

            // 배경 그리기
            if (hasBgColor)
            {
                EditorGUI.DrawRect(selectionRect, bgColor);
            }

            if (hasTextColor)
            {
                var style = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = textColor },
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip
                };

                EditorGUI.LabelField(labelRect, cleanName, style);
                GUI.changed = true;
            }
            else
            {
                // ✅ Unity 기본 스타일로 자동 렌더링되도록 아무것도 하지 않음
                // Unity가 기본 텍스트를 그리도록 두기 위해 return
                return;
            }
        }


        public static string StripTags(string name)
        {
            name = Regex.Replace(name, @"<color=#[0-9a-fA-F]{6}>", "");
            name = name.Replace("</color>", "");
            name = Regex.Replace(name, @"\[bg=#[0-9a-fA-F]{6}\]", "");
            return name.Trim();
        }
    }

    public class HierarchyTools : EditorWindow
    {
        private string groupTag = "Group";
        private GameObject parent_A;
        private GameObject parent_B;
        private GameObject parent_C;
        private Color textColor = Color.white;
        private Color bgColor = new Color(0, 0, 0, 0);
        private bool applyTextColor = true;
        private bool applyBgColor = true;

        private enum Tab { Colors, Grouping, Transform }
        private Tab currentTab = Tab.Colors;
        private Vector2 scrollPos;
        private Vector2 scrollColors;
        private Vector2 scrollGrouping;
        private Vector2 scrollTransform;

        [MenuItem("Tools/Tappy Tale/Hierarchy Tools 🎨")]
        public static void ShowWindow()
        {
            GetWindow<HierarchyTools>("Hierarchy Tools 🎨");
        }

        void OnGUI()
        {
            currentTab = (Tab)GUILayout.Toolbar((int)currentTab, new string[] { "🎨 Colors", "🧱 Grouping", "🧭 Transform" });
            GUILayout.Space(5);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            switch (currentTab)
            {
                case Tab.Colors:
                    DrawColorTab();
                    break;
                case Tab.Grouping:
                    DrawGroupingTab();
                    break;
                case Tab.Transform:
                    DrawTransformTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawColorTab()
        {
            scrollColors = EditorGUILayout.BeginScrollView(scrollColors);
            GUILayout.Label("🎨 Hierarchy Colors", EditorStyles.boldLabel);
            applyTextColor = EditorGUILayout.Toggle("Apply Text Color", applyTextColor);
            if (applyTextColor)
                textColor = EditorGUILayout.ColorField("Text Color", textColor);

            applyBgColor = EditorGUILayout.Toggle("Apply Background Color", applyBgColor);
            if (applyBgColor)
                bgColor = EditorGUILayout.ColorField("Background Color", bgColor);

            if (GUILayout.Button("Apply Color Tags to Selected"))
                ApplyColorTags();

            if (GUILayout.Button("Clear Tags from Selected"))
                ClearTags();

            GUILayout.Space(10);
            GUILayout.Label("🎯 Presets", EditorStyles.boldLabel);
            if (GUILayout.Button("📘 UI Preset"))
                ApplyPreset("#FFFFFF", "#007ACC");

            if (GUILayout.Button("⚠️ Warning Preset"))
                ApplyPreset("#000000", "#FF4444");

            if (GUILayout.Button("🧪 Enemy Preset"))
                ApplyPreset("#FF4444", "#330000");
            EditorGUILayout.EndScrollView();
        }

        void DrawGroupingTab()
        {
            scrollGrouping = EditorGUILayout.BeginScrollView(scrollGrouping);
            GUILayout.Label("📁 Group Tagging", EditorStyles.boldLabel);
            groupTag = EditorGUILayout.TextField("Group Tag", groupTag);
            if (GUILayout.Button("Apply Group Tag to Selected"))
                ApplyGroupTag();
            if (GUILayout.Button("Remove Group Tag from Selected"))
                RemoveGroupTag();

            GUILayout.Space(10);
            GUILayout.Label("🔗 Parent Assignment", EditorStyles.boldLabel);

            parent_A = (GameObject)EditorGUILayout.ObjectField("A Parent", parent_A, typeof(GameObject), true);
            if (GUILayout.Button("Make Children of A Parent"))
                MakeChildrenOf(parent_A);

            parent_B = (GameObject)EditorGUILayout.ObjectField("B Parent", parent_B, typeof(GameObject), true);
            if (GUILayout.Button("Make Children of B Parent"))
                MakeChildrenOf(parent_B);

            parent_C = (GameObject)EditorGUILayout.ObjectField("C Parent", parent_C, typeof(GameObject), true);
            if (GUILayout.Button("Make Children of C Parent"))
                MakeChildrenOf(parent_C);

            GUILayout.Space(10);
            GUILayout.Label("⬆️⬇️ Reorder", EditorStyles.boldLabel);
            if (GUILayout.Button("Move to Top"))
                MoveSelectedToTop();
            if (GUILayout.Button("Move to Bottom"))
                MoveSelectedToBottom();

            EditorGUILayout.EndScrollView();
        }

        void DrawTransformTab()
        {
            scrollTransform = EditorGUILayout.BeginScrollView(scrollTransform);

            GUILayout.Label("🧭 Transform Utilities", EditorStyles.boldLabel);
            if (GUILayout.Button("Reset All Transform"))
                ResetSelectedTransforms();
            if (GUILayout.Button("Reset Position Only"))
                ResetPositionOnly();
            if (GUILayout.Button("Reset Rotation Only"))
                ResetRotationOnly();
            if (GUILayout.Button("Reset Scale Only"))
                ResetScaleOnly();

            EditorGUILayout.EndScrollView();
        }

        void ApplyGroupTag()
        {
            string tag = $"[{groupTag}] ";
            foreach (GameObject obj in Selection.gameObjects)
            {
                Undo.RecordObject(obj, "Apply Group Tag");
                string name = HierarchyColorizer.StripTags(obj.name);
                if (!name.StartsWith(tag))
                    obj.name = tag + name;
            }
        }

        void RemoveGroupTag()
        {
            string tag = $"[{groupTag}] ";
            foreach (GameObject obj in Selection.gameObjects)
            {
                Undo.RecordObject(obj, "Remove Group Tag");
                string name = HierarchyColorizer.StripTags(obj.name);
                if (name.StartsWith(tag))
                    obj.name = name.Substring(tag.Length);
                else
                    obj.name = name;
            }
        }

        void MakeChildrenOf(GameObject parent)
        {
            if (parent == null)
            {
                Debug.LogWarning("❗ Parent target is not assigned.");
                return;
            }

            foreach (GameObject obj in Selection.gameObjects)
            {
                if (obj == parent) continue;
                Undo.SetTransformParent(obj.transform, parent.transform, "Set Parent");
            }

            Debug.Log($"✅ Moved {Selection.gameObjects.Length} object(s) under '{parent.name}'.");
        }

        void MoveSelectedToTop()
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                Undo.RecordObject(obj.transform, "Move To Top");
                obj.transform.SetAsFirstSibling();
            }
        }

        void MoveSelectedToBottom()
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                Undo.RecordObject(obj.transform, "Move To Bottom");
                obj.transform.SetAsLastSibling();
            }
        }

        void ApplyColorTags()
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                Undo.RecordObject(obj, "Apply Color Tags");
                string newName = HierarchyColorizer.StripTags(obj.name);

                if (applyTextColor)
                {
                    string hex = ColorUtility.ToHtmlStringRGB(textColor);
                    newName = $"<color=#{hex}>{newName}</color>";
                }

                if (applyBgColor)
                {
                    string hex = ColorUtility.ToHtmlStringRGB(bgColor);
                    newName = $"[bg=#{hex}]{newName}";
                }

                obj.name = newName;
            }
        }

        void ApplyPreset(string textHex, string bgHex)
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                Undo.RecordObject(obj, "Apply Preset");
                string cleanName = HierarchyColorizer.StripTags(obj.name);
                obj.name = $"[bg={bgHex}]<color={textHex}>{cleanName}</color>";
            }
        }

        void ClearTags()
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                Undo.RecordObject(obj, "Clear Tags");
                obj.name = HierarchyColorizer.StripTags(obj.name);
            }
        }

        void ResetSelectedTransforms()
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                Undo.RecordObject(obj.transform, "Reset Transform");
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
                obj.transform.localScale = Vector3.one;
            }

            Debug.Log($"✅ Reset All Transform on {Selection.gameObjects.Length} object(s).");
        }

        void ResetPositionOnly()
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                Undo.RecordObject(obj.transform, "Reset Position");
                obj.transform.localPosition = Vector3.zero;
            }

            Debug.Log($"📍 Reset Position on {Selection.gameObjects.Length} object(s).");
        }

        void ResetRotationOnly()
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                Undo.RecordObject(obj.transform, "Reset Rotation");
                obj.transform.localRotation = Quaternion.identity;
            }

            Debug.Log($"🔄 Reset Rotation on {Selection.gameObjects.Length} object(s).");
        }

        void ResetScaleOnly()
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                Undo.RecordObject(obj.transform, "Reset Scale");
                obj.transform.localScale = Vector3.one;
            }

            Debug.Log($"📐 Reset Scale on {Selection.gameObjects.Length} object(s).");
        }
    }
}
