using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MSJTDCC
{
/// <summary>
/// Animates a transform from <see cref="fromState"/> to <see cref="toState"/> while one or more
/// <see cref="interactableObjects"/> report a held interaction, reversing if released before completion
/// (when <see cref="resetOnRelease"/> is set).
/// </summary>
[MovedFrom(true, null, null, "HoldTweenStateAnimator")]
public class HoldTweenStateAnimator : MonoBehaviour
{
    /// <summary>A transform pose (position/rotation/scale) endpoint used by the hold animation. Each axis is independently enabled.</summary>
    [Serializable]
    public class HoldState
    {
        public string name = "State";

        [Header("Position")]
        public bool enablePosition = true;
        public Vector3 position;

        [Header("Rotation")]
        public bool enableRotation = false;
        public Vector3 rotationEuler;

        [Header("Scale")]
        public bool enableScale = false;
        public Vector3 scale = Vector3.one;
    }

    [Header("References")]
    /// <summary>InteractableObjects whose onHoldStart/onHoldCancelled events drive this animator.</summary>
    public InteractableObject[] interactableObjects;
    /// <summary>If true, disables interaction on all <see cref="interactableObjects"/> once the hold completes.</summary>
    [SerializeField] private bool disableOnComplete = false;

    [Header("States")]
    [SerializeField] private HoldState fromState = new();
    [SerializeField] private HoldState toState = new();

    [Header("Settings")]
    /// <summary>Seconds of continuous holding required to go from <see cref="fromState"/> to <see cref="toState"/>.</summary>
    [SerializeField] private float holdDuration = 2f;
    /// <summary>If true, progress animates back toward <see cref="fromState"/> when the hold is released before completion.</summary>
    [SerializeField] private bool resetOnRelease = false;
    /// <summary>Multiplier on how fast progress reverses when <see cref="resetOnRelease"/> is active.</summary>
    [SerializeField] private float resetSpeed = 1f;
    /// <summary>Easing curve applied to hold progress (0-1) before it's used to interpolate the transform.</summary>
    [SerializeField] private AnimationCurve progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip holdLoopSound;
    [SerializeField] private AudioClip completeSound;

    [Header("Events")]
    public UnityEvent onHoldStart;
    public UnityEvent onProgressComplete;
    public UnityEvent onHoldReleased;

    private float _progress = 0f;
    private bool _isHolding = false;
    private bool _isComplete = false;

    /// <summary>Current hold progress from 0 (at <see cref="fromState"/>) to 1 (at <see cref="toState"/>).</summary>
    public float Progress => _progress;
    /// <summary>True once progress has reached 1 and the complete sound/event has fired.</summary>
    public bool IsComplete => _isComplete;
    /// <summary>True while a hold interaction is currently in progress.</summary>
    public bool IsHolding => _isHolding;

    private void Start()
    {
        foreach (var io in interactableObjects)
        {
            if (io == null) continue;
            io.onHoldStart.AddListener(BeginHold);
            io.onHoldCancelled.AddListener(ReleaseHold);
        }

        ApplyProgress(0f);
    }

    private void OnDestroy()
    {
        foreach (var io in interactableObjects)
        {
            if (io == null) continue;
            io.onHoldStart.RemoveListener(BeginHold);
            io.onHoldCancelled.RemoveListener(ReleaseHold);
        }
    }

    private void Update()
    {
        if (_isComplete) return;

        if (_isHolding)
        {
            _progress = Mathf.MoveTowards(_progress, 1f, Time.deltaTime / holdDuration);
            ApplyProgress(_progress);

            if (_progress >= 1f)
            {
                _isComplete = true;
                _isHolding = false;
                StopLoopSound();
                PlayCompleteSound();
                if (disableOnComplete)
                    foreach (var io in interactableObjects)
                        if (io != null) io.DisableInteraction();
                onProgressComplete?.Invoke();
            }
        }
        else if (resetOnRelease && _progress > 0f)
        {
            _progress = Mathf.MoveTowards(_progress, 0f, Time.deltaTime / holdDuration * resetSpeed);
            ApplyProgress(_progress);
        }
    }

    /// <summary>Starts the hold; called from a listening InteractableObject's onHoldStart event.</summary>
    public void BeginHold()
    {
        if (_isComplete || _isHolding) return;
        _isHolding = true;
        PlayLoopSound();
        onHoldStart?.Invoke();
    }

    /// <summary>Ends the hold early; called from a listening InteractableObject's onHoldCancelled event.</summary>
    public void ReleaseHold()
    {
        if (!_isHolding) return;
        _isHolding = false;
        StopLoopSound();
        onHoldReleased?.Invoke();
    }

    /// <summary>Resets hold/complete state and snaps the transform back to <see cref="fromState"/>.</summary>
    public void ResetProgress()
    {
        _isHolding = false;
        _isComplete = false;
        _progress = 0f;
        ApplyProgress(0f);
    }

    private void PlayLoopSound()
    {
        if (audioSource == null || holdLoopSound == null) return;
        audioSource.clip = holdLoopSound;
        audioSource.loop = true;
        audioSource.Play();
    }

    private void StopLoopSound()
    {
        if (audioSource == null) return;
        audioSource.loop = false;
        audioSource.Stop();
    }

    private void PlayCompleteSound()
    {
        if (audioSource == null || completeSound == null) return;
        audioSource.loop = false;
        audioSource.PlayOneShot(completeSound);
    }

    /// <summary>Applies the interpolated pose at progress <paramref name="t"/> (0-1) without changing hold state; used by the custom inspector's preview slider.</summary>
    public void PreviewAt(float t)
    {
        ApplyProgress(t);
    }

    private void ApplyProgress(float t)
    {
        float easedT = progressCurve.Evaluate(t);

        if (fromState.enablePosition || toState.enablePosition)
            transform.localPosition = Vector3.Lerp(fromState.position, toState.position, easedT);

        if (fromState.enableRotation || toState.enableRotation)
            transform.localRotation = Quaternion.Lerp(
                Quaternion.Euler(fromState.rotationEuler),
                Quaternion.Euler(toState.rotationEuler),
                easedT);

        if (fromState.enableScale || toState.enableScale)
            transform.localScale = Vector3.Lerp(fromState.scale, toState.scale, easedT);
    }
}

#if UNITY_EDITOR
/// <summary>Custom inspector for <see cref="HoldTweenStateAnimator"/>: draws from/to state blocks with copy-current buttons and a play-mode preview.</summary>
[CustomEditor(typeof(HoldTweenStateAnimator))]
[MovedFrom(true, null, null, "HoldTweenStateAnimatorEditor")]
public class HoldTweenStateAnimatorEditor : Editor
{
    private SerializedProperty _interactableObject;
    private SerializedProperty _disableOnComplete;
    private SerializedProperty _fromState;
    private SerializedProperty _toState;
    private SerializedProperty _holdDuration;
    private SerializedProperty _resetOnRelease;
    private SerializedProperty _resetSpeed;
    private SerializedProperty _progressCurve;
    private SerializedProperty _audioSource;
    private SerializedProperty _holdLoopSound;
    private SerializedProperty _completeSound;
    private SerializedProperty _onHoldStart;
    private SerializedProperty _onProgressComplete;
    private SerializedProperty _onHoldReleased;

    private void OnEnable()
    {
        _interactableObject = serializedObject.FindProperty("interactableObjects");
        _disableOnComplete  = serializedObject.FindProperty("disableOnComplete");
        _fromState          = serializedObject.FindProperty("fromState");
        _toState            = serializedObject.FindProperty("toState");
        _holdDuration       = serializedObject.FindProperty("holdDuration");
        _resetOnRelease     = serializedObject.FindProperty("resetOnRelease");
        _resetSpeed         = serializedObject.FindProperty("resetSpeed");
        _progressCurve      = serializedObject.FindProperty("progressCurve");
        _audioSource        = serializedObject.FindProperty("audioSource");
        _holdLoopSound      = serializedObject.FindProperty("holdLoopSound");
        _completeSound      = serializedObject.FindProperty("completeSound");
        _onHoldStart        = serializedObject.FindProperty("onHoldStart");
        _onProgressComplete = serializedObject.FindProperty("onProgressComplete");
        _onHoldReleased     = serializedObject.FindProperty("onHoldReleased");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        HoldTweenStateAnimator t = (HoldTweenStateAnimator)target;

        EditorGUILayout.LabelField("States", EditorStyles.boldLabel);
        DrawStateBlock(_fromState, "From State", t);
        DrawStateBlock(_toState,   "To State",   t);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_holdDuration);
        EditorGUILayout.PropertyField(_resetOnRelease);
        if (_resetOnRelease.boolValue)
            EditorGUILayout.PropertyField(_resetSpeed);
        EditorGUILayout.PropertyField(_progressCurve);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_audioSource);
        EditorGUILayout.PropertyField(_holdLoopSound);
        EditorGUILayout.PropertyField(_completeSound);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_interactableObject);
        EditorGUILayout.PropertyField(_disableOnComplete);
        EditorGUILayout.PropertyField(_onHoldStart);
        EditorGUILayout.PropertyField(_onProgressComplete);
        EditorGUILayout.PropertyField(_onHoldReleased);

        EditorGUILayout.Space(6);
        DrawPreviewSection(t);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawStateBlock(SerializedProperty state, string label, HoldTweenStateAnimator t)
    {
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f, 1f);
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prev;

        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        SerializedProperty name          = state.FindPropertyRelative("name");
        SerializedProperty enablePos     = state.FindPropertyRelative("enablePosition");
        SerializedProperty position      = state.FindPropertyRelative("position");
        SerializedProperty enableRot     = state.FindPropertyRelative("enableRotation");
        SerializedProperty rotation      = state.FindPropertyRelative("rotationEuler");
        SerializedProperty enableScale   = state.FindPropertyRelative("enableScale");
        SerializedProperty scale         = state.FindPropertyRelative("scale");

        EditorGUILayout.PropertyField(name);

        EditorGUILayout.Space(2);

        // Position
        EditorGUILayout.PropertyField(enablePos);
        EditorGUI.BeginDisabledGroup(!enablePos.boolValue);
        EditorGUILayout.PropertyField(position);
        if (GUILayout.Button("Copy Current Position"))
        {
            Undo.RecordObject(t, $"Copy Position to {label}");
            position.vector3Value = t.transform.localPosition;
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(2);

        // Rotation
        EditorGUILayout.PropertyField(enableRot);
        EditorGUI.BeginDisabledGroup(!enableRot.boolValue);
        EditorGUILayout.PropertyField(rotation);
        if (GUILayout.Button("Copy Current Rotation"))
        {
            Undo.RecordObject(t, $"Copy Rotation to {label}");
            rotation.vector3Value = t.transform.localRotation.eulerAngles;
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(2);

        // Scale
        EditorGUILayout.PropertyField(enableScale);
        EditorGUI.BeginDisabledGroup(!enableScale.boolValue);
        EditorGUILayout.PropertyField(scale);
        if (GUILayout.Button("Copy Current Scale"))
        {
            Undo.RecordObject(t, $"Copy Scale to {label}");
            scale.vector3Value = t.transform.localScale;
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(2);

        bool isFrom = state.propertyPath.Contains("fromState");
        if (GUILayout.Button($"Go To This State"))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(t.transform, $"Go To {label}");
            t.PreviewAt(isFrom ? 0f : 1f);
            EditorUtility.SetDirty(t.transform);
            serializedObject.Update();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    private void DrawPreviewSection(HoldTweenStateAnimator t)
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        float newP = EditorGUILayout.Slider("Progress", t.Progress, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(t.transform, "Preview Hold Progress");
            t.PreviewAt(newP);
            EditorUtility.SetDirty(t.transform);
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Begin Hold"))   t.BeginHold();
            if (GUILayout.Button("Release Hold")) t.ReleaseHold();
            if (GUILayout.Button("Reset"))        t.ResetProgress();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"Progress: {t.Progress:F2}   Complete: {t.IsComplete}   Holding: {t.IsHolding}");
        }
    }
}
#endif
}
