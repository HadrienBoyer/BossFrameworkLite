#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TappyTale.EditorTools
{
    public sealed class ChildPatternPlacementWindow : UnityEditor.EditorWindow
    {
        private enum PatternType
        {
            Line,
            Grid,
            Circle,
            Arc,
            Spiral,
            Wave,
            RandomBox,
            FibonacciSphere
        }

        private enum OrientationMode
        {
            KeepOriginal,
            RootRotation,
            FaceOutward,
            FaceTangent,
            FaceCenter
        }

        private enum ChildScope
        {
            DirectChildren,
            AllDescendants
        }

        private struct TransformSnapshot
        {
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
        }

        private struct TargetTransform
        {
            public Vector3 WorldPosition;
            public Quaternion WorldRotation;
            public Vector3 LocalScale;
        }

        private const string MenuPath = "Tools/Tappy Tale/Studio Tools/Child Pattern Placement";

        [SerializeField] private PatternType pattern = PatternType.Line;
        [SerializeField] private ChildScope childScope = ChildScope.DirectChildren;
        [SerializeField] private OrientationMode orientationMode = OrientationMode.KeepOriginal;
        [SerializeField] private bool livePreview = true;
        [SerializeField] private bool drawLinks = true;
        [SerializeField] private float previewSize = 0.18f;

        // Common layout.
        [SerializeField] private Vector3 positionOffset = Vector3.zero;
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;
        [SerializeField] private Vector3 perIndexPositionOffset = Vector3.zero;
        [SerializeField] private Vector3 perIndexRotationOffset = Vector3.zero;
        [SerializeField] private Vector3 perIndexScaleMultiplier = Vector3.one;

        // Line / wave.
        [SerializeField] private Vector3 lineDirection = Vector3.right;
        [SerializeField] private float lineSpacing = 1f;
        [SerializeField] private bool centerLine = true;
        [SerializeField] private float waveAmplitude = 1f;
        [SerializeField] private float waveFrequency = 1f;
        [SerializeField] private Vector3 waveAxis = Vector3.up;

        // Grid.
        [SerializeField] private int gridColumns = 4;
        [SerializeField] private Vector2 gridSpacing = Vector2.one;
        [SerializeField] private bool centerGrid = true;

        // Circle / arc.
        [SerializeField] private float radius = 3f;
        [SerializeField] private float startAngle = 0f;
        [SerializeField] private float arcAngle = 180f;
        [SerializeField] private bool closeCircle = false;

        // Spiral.
        [SerializeField] private float spiralStartRadius = 0.5f;
        [SerializeField] private float spiralEndRadius = 4f;
        [SerializeField] private float spiralTurns = 2f;
        [SerializeField] private float spiralHeight = 0f;

        // Random box.
        [SerializeField] private Vector3 randomBoxSize = new Vector3(5f, 1f, 5f);

        // Randomization.
        [SerializeField] private bool randomize = false;
        [SerializeField] private int seed = 12345;
        [SerializeField] private Vector3 positionJitter = Vector3.zero;
        [SerializeField] private Vector3 rotationJitter = Vector3.zero;
        [SerializeField] private Vector2 uniformScaleRange = Vector2.one;
        [SerializeField] private bool randomizeScalePerAxis = false;
        [SerializeField] private Vector2 scaleXRange = Vector2.one;
        [SerializeField] private Vector2 scaleYRange = Vector2.one;
        [SerializeField] private Vector2 scaleZRange = Vector2.one;

        // Ground projection.
        [SerializeField] private bool projectToGround = false;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float rayStartHeight = 10f;
        [SerializeField] private float rayDistance = 100f;
        [SerializeField] private float surfaceOffset = 0f;
        [SerializeField] private bool alignUpToGroundNormal = false;

        private Vector2 scroll;
        private GameObject cachedRoot;
        private readonly Dictionary<Transform, TransformSnapshot> snapshots = new();
        private readonly List<Transform> children = new();
        private readonly List<TargetTransform> previewTargets = new();

        [MenuItem(MenuPath)]
        public static void Open()
        {
            ChildPatternPlacementWindow window = GetWindow<ChildPatternPlacementWindow>();
            window.titleContent = new GUIContent("Child Patterns");
            window.minSize = new Vector2(390f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            SceneView.duringSceneGui += DuringSceneGui;
            RefreshSelectionCache(force: true);
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            SceneView.duringSceneGui -= DuringSceneGui;
        }

        private void OnSelectionChanged()
        {
            RefreshSelectionCache(force: true);
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnHierarchyChanged()
        {
            RefreshSelectionCache(force: true);
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            RefreshSelectionCache(force: false);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawHeader();

            GameObject root = GetValidSceneRoot();
            if (root == null)
            {
                DrawNoSelectionHelp();
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space(6f);
            DrawSelectionPanel(root);
            EditorGUILayout.Space(6f);

            EditorGUI.BeginChangeCheck();

            DrawPatternPanel();
            EditorGUILayout.Space(6f);
            DrawOffsetsPanel();
            EditorGUILayout.Space(6f);
            DrawOrientationPanel();
            EditorGUILayout.Space(6f);
            DrawRandomizationPanel();
            EditorGUILayout.Space(6f);
            DrawGroundPanel();
            EditorGUILayout.Space(6f);
            DrawPreviewPanel();

            if (EditorGUI.EndChangeCheck())
            {
                SanitizeSettings();
                BuildPreviewTargets();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(10f);
            DrawActions(root);
            EditorGUILayout.Space(12f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Tappy Tale • Child Pattern Placement", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select one GameObject in the open Scene. Its children are distributed using a non-destructive Scene preview, then committed with a single Undo operation.",
                MessageType.Info);
        }

        private void DrawNoSelectionHelp()
        {
            EditorGUILayout.Space(12f);
            EditorGUILayout.HelpBox(
                "Select a GameObject from the Hierarchy or Scene View. Project assets and prefab assets are intentionally ignored.",
                MessageType.Warning);
        }

        private void DrawSelectionPanel(GameObject root)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scene Selection", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Root", root, typeof(GameObject), true);
                }

                ChildScope newScope = (ChildScope)EditorGUILayout.EnumPopup("Children", childScope);
                if (newScope != childScope)
                {
                    childScope = newScope;
                    RefreshSelectionCache(force: true);
                }

                EditorGUILayout.LabelField("Affected Transforms", children.Count.ToString());

                if (GUILayout.Button("Refresh Children"))
                {
                    RefreshSelectionCache(force: true);
                }
            }
        }

        private void DrawPatternPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Pattern", EditorStyles.boldLabel);
                pattern = (PatternType)EditorGUILayout.EnumPopup("Type", pattern);

                switch (pattern)
                {
                    case PatternType.Line:
                        lineDirection = EditorGUILayout.Vector3Field("Direction", lineDirection);
                        lineSpacing = EditorGUILayout.FloatField("Spacing", lineSpacing);
                        centerLine = EditorGUILayout.Toggle("Center On Root", centerLine);
                        break;

                    case PatternType.Grid:
                        gridColumns = EditorGUILayout.IntField("Columns", gridColumns);
                        gridSpacing = EditorGUILayout.Vector2Field("Spacing X / Z", gridSpacing);
                        centerGrid = EditorGUILayout.Toggle("Center On Root", centerGrid);
                        break;

                    case PatternType.Circle:
                        radius = EditorGUILayout.FloatField("Radius", radius);
                        startAngle = EditorGUILayout.FloatField("Start Angle", startAngle);
                        closeCircle = EditorGUILayout.Toggle(new GUIContent("Duplicate Seam", "Places the final child at 360° instead of keeping all children evenly unique around the circle."), closeCircle);
                        break;

                    case PatternType.Arc:
                        radius = EditorGUILayout.FloatField("Radius", radius);
                        startAngle = EditorGUILayout.FloatField("Start Angle", startAngle);
                        arcAngle = EditorGUILayout.FloatField("Arc Angle", arcAngle);
                        break;

                    case PatternType.Spiral:
                        spiralStartRadius = EditorGUILayout.FloatField("Start Radius", spiralStartRadius);
                        spiralEndRadius = EditorGUILayout.FloatField("End Radius", spiralEndRadius);
                        spiralTurns = EditorGUILayout.FloatField("Turns", spiralTurns);
                        spiralHeight = EditorGUILayout.FloatField("Height", spiralHeight);
                        startAngle = EditorGUILayout.FloatField("Start Angle", startAngle);
                        break;

                    case PatternType.Wave:
                        lineDirection = EditorGUILayout.Vector3Field("Direction", lineDirection);
                        lineSpacing = EditorGUILayout.FloatField("Spacing", lineSpacing);
                        centerLine = EditorGUILayout.Toggle("Center On Root", centerLine);
                        waveAxis = EditorGUILayout.Vector3Field("Wave Axis", waveAxis);
                        waveAmplitude = EditorGUILayout.FloatField("Amplitude", waveAmplitude);
                        waveFrequency = EditorGUILayout.FloatField("Frequency", waveFrequency);
                        break;

                    case PatternType.RandomBox:
                        randomBoxSize = EditorGUILayout.Vector3Field("Box Size", randomBoxSize);
                        EditorGUILayout.HelpBox("Random Box always uses the Seed field from Randomization so its layout is deterministic.", MessageType.None);
                        break;

                    case PatternType.FibonacciSphere:
                        radius = EditorGUILayout.FloatField("Radius", radius);
                        break;
                }
            }
        }

        private void DrawOffsetsPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Offsets & Progression", EditorStyles.boldLabel);
                positionOffset = EditorGUILayout.Vector3Field("Position Offset", positionOffset);
                rotationOffset = EditorGUILayout.Vector3Field("Rotation Offset", rotationOffset);
                EditorGUILayout.Space(3f);
                perIndexPositionOffset = EditorGUILayout.Vector3Field("Per-Child Position", perIndexPositionOffset);
                perIndexRotationOffset = EditorGUILayout.Vector3Field("Per-Child Rotation", perIndexRotationOffset);
                perIndexScaleMultiplier = EditorGUILayout.Vector3Field("Per-Child Scale", perIndexScaleMultiplier);
            }
        }

        private void DrawOrientationPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Orientation", EditorStyles.boldLabel);
                orientationMode = (OrientationMode)EditorGUILayout.EnumPopup("Mode", orientationMode);
                EditorGUILayout.HelpBox(
                    "Face Outward / Center use the root as radial reference. Face Tangent follows the generated path direction.",
                    MessageType.None);
            }
        }

        private void DrawRandomizationPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Randomization", EditorStyles.boldLabel);
                randomize = EditorGUILayout.Toggle("Enable Jitter", randomize);
                seed = EditorGUILayout.IntField("Seed", seed);

                using (new EditorGUI.DisabledScope(!randomize))
                {
                    positionJitter = EditorGUILayout.Vector3Field("Position ±", positionJitter);
                    rotationJitter = EditorGUILayout.Vector3Field("Rotation ±", rotationJitter);
                    randomizeScalePerAxis = EditorGUILayout.Toggle("Scale Per Axis", randomizeScalePerAxis);

                    if (randomizeScalePerAxis)
                    {
                        scaleXRange = EditorGUILayout.Vector2Field("Scale X Min / Max", scaleXRange);
                        scaleYRange = EditorGUILayout.Vector2Field("Scale Y Min / Max", scaleYRange);
                        scaleZRange = EditorGUILayout.Vector2Field("Scale Z Min / Max", scaleZRange);
                    }
                    else
                    {
                        uniformScaleRange = EditorGUILayout.Vector2Field("Uniform Min / Max", uniformScaleRange);
                    }
                }
            }
        }

        private void DrawGroundPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Surface Projection", EditorStyles.boldLabel);
                projectToGround = EditorGUILayout.Toggle("Project To Ground", projectToGround);

                using (new EditorGUI.DisabledScope(!projectToGround))
                {
                    groundMask = LayerMaskField("Ground Layers", groundMask);
                    rayStartHeight = EditorGUILayout.FloatField("Ray Start Height", rayStartHeight);
                    rayDistance = EditorGUILayout.FloatField("Ray Distance", rayDistance);
                    surfaceOffset = EditorGUILayout.FloatField("Surface Offset", surfaceOffset);
                    alignUpToGroundNormal = EditorGUILayout.Toggle("Align Up To Normal", alignUpToGroundNormal);
                }
            }
        }

        private void DrawPreviewPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scene Preview", EditorStyles.boldLabel);
                livePreview = EditorGUILayout.Toggle("Show Preview", livePreview);
                drawLinks = EditorGUILayout.Toggle("Draw Path Links", drawLinks);
                previewSize = EditorGUILayout.Slider("Marker Size", previewSize, 0.02f, 1f);
            }
        }

        private void DrawActions(GameObject root)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(children.Count == 0))
                {
                    if (GUILayout.Button("Apply Pattern", GUILayout.Height(34f)))
                    {
                        ApplyPattern(root);
                    }

                    if (GUILayout.Button("Shuffle Seed", GUILayout.Height(34f), GUILayout.Width(110f)))
                    {
                        seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                        BuildPreviewTargets();
                        SceneView.RepaintAll();
                        Repaint();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(children.Count == 0))
                {
                    if (GUILayout.Button("Capture Current As Baseline"))
                    {
                        CaptureSnapshots();
                        BuildPreviewTargets();
                    }

                    if (GUILayout.Button("Restore Baseline"))
                    {
                        RestoreBaseline();
                    }
                }
            }
        }

        private void RefreshSelectionCache(bool force)
        {
            GameObject root = GetValidSceneRoot();
            if (!force && root == cachedRoot)
            {
                return;
            }

            cachedRoot = root;
            children.Clear();
            snapshots.Clear();
            previewTargets.Clear();

            if (root == null)
            {
                return;
            }

            CollectChildren(root.transform);
            CaptureSnapshots();
            BuildPreviewTargets();
        }

        private GameObject GetValidSceneRoot()
        {
            GameObject candidate = Selection.activeGameObject;
            if (candidate == null)
            {
                return null;
            }

            if (EditorUtility.IsPersistent(candidate))
            {
                return null;
            }

            Scene scene = candidate.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            return candidate;
        }

        private void CollectChildren(Transform root)
        {
            if (childScope == ChildScope.DirectChildren)
            {
                for (int i = 0; i < root.childCount; i++)
                {
                    children.Add(root.GetChild(i));
                }
                return;
            }

            foreach (Transform descendant in root.GetComponentsInChildren<Transform>(true))
            {
                if (descendant != root)
                {
                    children.Add(descendant);
                }
            }
        }

        private void CaptureSnapshots()
        {
            snapshots.Clear();
            foreach (Transform child in children)
            {
                if (child == null)
                {
                    continue;
                }

                snapshots[child] = new TransformSnapshot
                {
                    LocalPosition = child.localPosition,
                    LocalRotation = child.localRotation,
                    LocalScale = child.localScale
                };
            }
        }

        private void RestoreBaseline()
        {
            if (cachedRoot == null || children.Count == 0)
            {
                return;
            }

            Undo.RecordObjects(children.Cast<UnityEngine.Object>().ToArray(), "Restore Child Pattern Baseline");

            foreach (Transform child in children)
            {
                if (child == null || !snapshots.TryGetValue(child, out TransformSnapshot snapshot))
                {
                    continue;
                }

                child.localPosition = snapshot.LocalPosition;
                child.localRotation = snapshot.LocalRotation;
                child.localScale = snapshot.LocalScale;
                EditorUtility.SetDirty(child);
            }

            BuildPreviewTargets();
            SceneView.RepaintAll();
        }

        private void ApplyPattern(GameObject root)
        {
            BuildPreviewTargets();
            if (previewTargets.Count != children.Count)
            {
                return;
            }

            Undo.RecordObjects(children.Cast<UnityEngine.Object>().ToArray(), "Apply Child Placement Pattern");

            for (int i = 0; i < children.Count; i++)
            {
                Transform child = children[i];
                if (child == null)
                {
                    continue;
                }

                TargetTransform target = previewTargets[i];
                child.SetPositionAndRotation(target.WorldPosition, target.WorldRotation);
                child.localScale = target.LocalScale;
                EditorUtility.SetDirty(child);
            }

            CaptureSnapshots();
            BuildPreviewTargets();
            SceneView.RepaintAll();
            SceneView.lastActiveSceneView?.ShowNotification(new GUIContent($"Applied {pattern} to {children.Count} child object(s)."));
        }

        private void BuildPreviewTargets()
        {
            previewTargets.Clear();
            GameObject rootObject = GetValidSceneRoot();
            if (rootObject == null || children.Count == 0)
            {
                return;
            }

            Transform root = rootObject.transform;
            System.Random rng = new(seed);
            int count = children.Count;

            for (int i = 0; i < count; i++)
            {
                Transform child = children[i];
                if (child == null || !snapshots.TryGetValue(child, out TransformSnapshot snapshot))
                {
                    previewTargets.Add(default);
                    continue;
                }

                Vector3 localPatternPosition = GetPatternLocalPosition(i, count, rng);
                Vector3 localTangent = GetPatternLocalTangent(i, count);

                Quaternion globalOffsetRotation = Quaternion.Euler(rotationOffset);
                Vector3 localPosition = globalOffsetRotation * localPatternPosition;
                localPosition += positionOffset;
                localPosition += Vector3.Scale(perIndexPositionOffset, Vector3.one * i);

                if (randomize)
                {
                    localPosition += RandomVector3(rng, -Abs(positionJitter), Abs(positionJitter));
                }

                Quaternion localRotation = GetBaseLocalRotation(root, snapshot, localPosition, localTangent);
                localRotation = Quaternion.Euler(perIndexRotationOffset * i) * localRotation;
                localRotation = Quaternion.Euler(rotationOffset) * localRotation;

                if (randomize)
                {
                    Vector3 eulerJitter = RandomVector3(rng, -Abs(rotationJitter), Abs(rotationJitter));
                    localRotation = Quaternion.Euler(eulerJitter) * localRotation;
                }

                Vector3 localScale = ApplyIndexScale(snapshot.LocalScale, i);
                if (randomize)
                {
                    localScale = Vector3.Scale(localScale, GetRandomScale(rng));
                }

                Vector3 worldPosition = root.TransformPoint(localPosition);
                Quaternion worldRotation = root.rotation * localRotation;

                if (projectToGround && TryProjectToSurface(root, worldPosition, worldRotation, out Vector3 projectedPosition, out Quaternion projectedRotation))
                {
                    worldPosition = projectedPosition;
                    worldRotation = projectedRotation;
                }

                previewTargets.Add(new TargetTransform
                {
                    WorldPosition = worldPosition,
                    WorldRotation = worldRotation,
                    LocalScale = localScale
                });
            }
        }

        private Vector3 GetPatternLocalPosition(int index, int count, System.Random rng)
        {
            switch (pattern)
            {
                case PatternType.Line:
                    return GetLinePosition(index, count);

                case PatternType.Grid:
                    return GetGridPosition(index, count);

                case PatternType.Circle:
                    return GetCirclePosition(index, count, 360f, closeCircle);

                case PatternType.Arc:
                    return GetCirclePosition(index, count, arcAngle, true);

                case PatternType.Spiral:
                    return GetSpiralPosition(index, count);

                case PatternType.Wave:
                    {
                        Vector3 basePosition = GetLinePosition(index, count);
                        float phase = count <= 1 ? 0f : (float)index / (count - 1);
                        Vector3 axis = SafeDirection(waveAxis, Vector3.up);
                        return basePosition + axis * (Mathf.Sin(phase * Mathf.PI * 2f * waveFrequency) * waveAmplitude);
                    }

                case PatternType.RandomBox:
                    {
                        Vector3 half = Abs(randomBoxSize) * 0.5f;
                        return RandomVector3(rng, -half, half);
                    }

                case PatternType.FibonacciSphere:
                    return GetFibonacciSpherePosition(index, count);

                default:
                    return Vector3.zero;
            }
        }

        private Vector3 GetPatternLocalTangent(int index, int count)
        {
            if (count <= 1)
            {
                return SafeDirection(lineDirection, Vector3.forward);
            }

            int previous = Mathf.Max(0, index - 1);
            int next = Mathf.Min(count - 1, index + 1);

            // RandomBox has no meaningful deterministic path tangent.
            if (pattern == PatternType.RandomBox)
            {
                return Vector3.forward;
            }

            System.Random tangentRng = new(seed);
            Vector3 previousPosition = GetPatternLocalPosition(previous, count, tangentRng);
            tangentRng = new System.Random(seed);
            Vector3 nextPosition = GetPatternLocalPosition(next, count, tangentRng);
            return SafeDirection(nextPosition - previousPosition, Vector3.forward);
        }

        private Vector3 GetLinePosition(int index, int count)
        {
            Vector3 direction = SafeDirection(lineDirection, Vector3.right);
            float centeredIndex = centerLine ? index - (count - 1) * 0.5f : index;
            return direction * (centeredIndex * lineSpacing);
        }

        private Vector3 GetGridPosition(int index, int count)
        {
            int columns = Mathf.Max(1, gridColumns);
            int rows = Mathf.CeilToInt(count / (float)columns);
            int x = index % columns;
            int z = index / columns;

            float px = x * gridSpacing.x;
            float pz = z * gridSpacing.y;

            if (centerGrid)
            {
                int usedColumns = Mathf.Min(columns, count);
                px -= (usedColumns - 1) * gridSpacing.x * 0.5f;
                pz -= (rows - 1) * gridSpacing.y * 0.5f;
            }

            return new Vector3(px, 0f, pz);
        }

        private Vector3 GetCirclePosition(int index, int count, float totalAngle, bool includeEnd)
        {
            float denominator = includeEnd ? Mathf.Max(1, count - 1) : Mathf.Max(1, count);
            float t = count <= 1 ? 0f : index / denominator;
            float angle = (startAngle + totalAngle * t) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        private Vector3 GetSpiralPosition(int index, int count)
        {
            float t = count <= 1 ? 0f : index / (float)(count - 1);
            float currentRadius = Mathf.Lerp(spiralStartRadius, spiralEndRadius, t);
            float angle = (startAngle + 360f * spiralTurns * t) * Mathf.Deg2Rad;
            float y = spiralHeight * t;
            return new Vector3(Mathf.Cos(angle) * currentRadius, y, Mathf.Sin(angle) * currentRadius);
        }

        private Vector3 GetFibonacciSpherePosition(int index, int count)
        {
            if (count <= 1)
            {
                return Vector3.up * radius;
            }

            const float goldenAngle = Mathf.PI * (3f - 2.2360679775f); // pi * (3 - sqrt(5))
            float y = 1f - (index / (float)(count - 1)) * 2f;
            float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float theta = goldenAngle * index;
            return new Vector3(Mathf.Cos(theta) * radial, y, Mathf.Sin(theta) * radial) * radius;
        }

        private Quaternion GetBaseLocalRotation(Transform root, TransformSnapshot snapshot, Vector3 localPosition, Vector3 localTangent)
        {
            switch (orientationMode)
            {
                case OrientationMode.KeepOriginal:
                    return snapshot.LocalRotation;

                case OrientationMode.RootRotation:
                    return Quaternion.identity;

                case OrientationMode.FaceOutward:
                    return LookRotationSafe(localPosition, Vector3.up, snapshot.LocalRotation);

                case OrientationMode.FaceCenter:
                    return LookRotationSafe(-localPosition, Vector3.up, snapshot.LocalRotation);

                case OrientationMode.FaceTangent:
                    return LookRotationSafe(localTangent, Vector3.up, snapshot.LocalRotation);

                default:
                    return snapshot.LocalRotation;
            }
        }

        private bool TryProjectToSurface(Transform root, Vector3 worldPosition, Quaternion worldRotation, out Vector3 projectedPosition, out Quaternion projectedRotation)
        {
            Vector3 origin = worldPosition + Vector3.up * Mathf.Max(0f, rayStartHeight);
            float distance = Mathf.Max(0.01f, rayDistance + Mathf.Max(0f, rayStartHeight));
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, groundMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                Transform hitTransform = hit.collider.transform;
                if (hitTransform == root || hitTransform.IsChildOf(root))
                {
                    continue;
                }

                projectedPosition = hit.point + hit.normal * surfaceOffset;
                projectedRotation = alignUpToGroundNormal
                    ? Quaternion.FromToRotation(worldRotation * Vector3.up, hit.normal) * worldRotation
                    : worldRotation;
                return true;
            }

            projectedPosition = worldPosition;
            projectedRotation = worldRotation;
            return false;
        }

        private Vector3 ApplyIndexScale(Vector3 baseScale, int index)
        {
            Vector3 multiplier = Vector3.one;
            multiplier.x = Mathf.Pow(Mathf.Max(0.0001f, perIndexScaleMultiplier.x), index);
            multiplier.y = Mathf.Pow(Mathf.Max(0.0001f, perIndexScaleMultiplier.y), index);
            multiplier.z = Mathf.Pow(Mathf.Max(0.0001f, perIndexScaleMultiplier.z), index);
            return Vector3.Scale(baseScale, multiplier);
        }

        private Vector3 GetRandomScale(System.Random rng)
        {
            if (randomizeScalePerAxis)
            {
                return new Vector3(
                    NextFloat(rng, scaleXRange.x, scaleXRange.y),
                    NextFloat(rng, scaleYRange.x, scaleYRange.y),
                    NextFloat(rng, scaleZRange.x, scaleZRange.y));
            }

            float scale = NextFloat(rng, uniformScaleRange.x, uniformScaleRange.y);
            return Vector3.one * scale;
        }

        private void DuringSceneGui(SceneView sceneView)
        {
            if (!livePreview)
            {
                return;
            }

            GameObject root = GetValidSceneRoot();
            if (root == null)
            {
                return;
            }

            if (previewTargets.Count != children.Count)
            {
                BuildPreviewTargets();
            }

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            for (int i = 0; i < previewTargets.Count; i++)
            {
                TargetTransform target = previewTargets[i];
                float size = HandleUtility.GetHandleSize(target.WorldPosition) * previewSize;

                Handles.DrawWireCube(target.WorldPosition, Vector3.one * size);
                Handles.ArrowHandleCap(0, target.WorldPosition, target.WorldRotation, size * 1.8f, EventType.Repaint);

                if (drawLinks && i > 0)
                {
                    Handles.DrawDottedLine(previewTargets[i - 1].WorldPosition, target.WorldPosition, 4f);
                }

                Handles.Label(target.WorldPosition + Vector3.up * size, i.ToString());
            }

            if (pattern == PatternType.RandomBox)
            {
                Matrix4x4 previousMatrix = Handles.matrix;
                Handles.matrix = root.transform.localToWorldMatrix * Matrix4x4.TRS(positionOffset, Quaternion.Euler(rotationOffset), Vector3.one);
                Handles.DrawWireCube(Vector3.zero, Abs(randomBoxSize));
                Handles.matrix = previousMatrix;
            }
        }

        private void SanitizeSettings()
        {
            gridColumns = Mathf.Max(1, gridColumns);
            lineSpacing = Mathf.Max(0f, lineSpacing);
            radius = Mathf.Max(0f, radius);
            spiralStartRadius = Mathf.Max(0f, spiralStartRadius);
            spiralEndRadius = Mathf.Max(0f, spiralEndRadius);
            previewSize = Mathf.Clamp(previewSize, 0.02f, 1f);
            rayStartHeight = Mathf.Max(0f, rayStartHeight);
            rayDistance = Mathf.Max(0.01f, rayDistance);

            uniformScaleRange = OrderedRange(uniformScaleRange, 0.0001f);
            scaleXRange = OrderedRange(scaleXRange, 0.0001f);
            scaleYRange = OrderedRange(scaleYRange, 0.0001f);
            scaleZRange = OrderedRange(scaleZRange, 0.0001f);
        }

        private static Vector2 OrderedRange(Vector2 range, float minimum)
        {
            float min = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
            float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            return new Vector2(min, max);
        }

        private static Quaternion LookRotationSafe(Vector3 forward, Vector3 up, Quaternion fallback)
        {
            if (forward.sqrMagnitude < 0.000001f)
            {
                return fallback;
            }

            forward.Normalize();
            if (Mathf.Abs(Vector3.Dot(forward, up.normalized)) > 0.999f)
            {
                up = Vector3.forward;
            }

            return Quaternion.LookRotation(forward, up);
        }

        private static Vector3 SafeDirection(Vector3 direction, Vector3 fallback)
        {
            return direction.sqrMagnitude < 0.000001f ? fallback.normalized : direction.normalized;
        }

        private static Vector3 RandomVector3(System.Random rng, Vector3 min, Vector3 max)
        {
            return new Vector3(
                NextFloat(rng, min.x, max.x),
                NextFloat(rng, min.y, max.y),
                NextFloat(rng, min.z, max.z));
        }

        private static float NextFloat(System.Random rng, float a, float b)
        {
            float min = Mathf.Min(a, b);
            float max = Mathf.Max(a, b);
            return min + (float)rng.NextDouble() * (max - min);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static LayerMask LayerMaskField(string label, LayerMask layerMask)
        {
            string[] layerNames = UnityEditorInternal.InternalEditorUtility.layers;
            int maskWithoutEmpty = 0;

            for (int i = 0; i < layerNames.Length; i++)
            {
                int layer = LayerMask.NameToLayer(layerNames[i]);
                if ((layerMask.value & (1 << layer)) != 0)
                {
                    maskWithoutEmpty |= 1 << i;
                }
            }

            maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layerNames);

            int newMask = 0;
            for (int i = 0; i < layerNames.Length; i++)
            {
                if ((maskWithoutEmpty & (1 << i)) == 0)
                {
                    continue;
                }

                int layer = LayerMask.NameToLayer(layerNames[i]);
                newMask |= 1 << layer;
            }

            layerMask.value = newMask;
            return layerMask;
        }
    }
}
#endif
