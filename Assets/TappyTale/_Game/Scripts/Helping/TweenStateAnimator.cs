using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;
using MSJTDCC.Tweening;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MSJTDCC
{
/// <summary>
/// Drives a transform through a named list of <see cref="TweenState"/> poses, tweening between them
/// on request (<see cref="PlayState"/>, <see cref="PlayNextState"/>/<see cref="PlayPreviousState"/>)
/// and optionally playing a sound per state.
/// </summary>
[MovedFrom(true, null, null, "TweenStateAnimator")]
public class TweenStateAnimator : MonoBehaviour
{
    /// <summary>A named transform pose (position/rotation/scale) with its own tween timing, easing, and optional sound/events.</summary>
    [Serializable]
    public class TweenState
    {
        public string stateName = "State";
        public bool isMinimized;

        [Header("Position")]
        public bool enablePosition = true;
        public Vector3 position;

        [Header("Rotation")]
        public bool enableRotation = false;
        public Vector3 rotationEuler;

        [Header("Scale")]
        public bool enableScale = false;
        public Vector3 scale = Vector3.one;

        [Header("Tween")]
        public float duration = 0.5f;
        /// <summary>Delay before the tween starts; the state's sound and onStateStart event also wait for this delay.</summary>
        public float delay = 0f;
        public Ease easeType = Ease.OutQuad;

        [Header("Audio")]
        public AudioClip stateSound;
        [Range(0f, 1f)]
        public float soundVolume = 1f;

        [Header("Events")]
        public UnityEvent onStateStart;
        public UnityEvent onStateComplete;
    }

    [Header("States")]
    [SerializeField] private List<TweenState> states = new List<TweenState>();

    [Header("Startup")]
    /// <summary>If true, the transform snaps to <see cref="startingStateIndex"/> immediately on <see cref="Start"/>.</summary>
    [SerializeField] private bool useStartingState = false;
    [SerializeField] private int startingStateIndex = 0;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    private Coroutine activeRoutine;
    private int currentStateIndex = -1;
    private TweenState pendingSoundState;
    private readonly List<TweenTrack> tracks = new List<TweenTrack>();

    private void Start()
    {
        if (useStartingState)
        {
            ApplyStateInstantly(startingStateIndex);
            currentStateIndex = startingStateIndex;
        }
    }

    /// <summary>Tweens to <see cref="startingStateIndex"/>. No-op if the GameObject is inactive.</summary>
    [ContextMenu("Play Starting State")]
    public void PlayStartingState()
    {
        if(gameObject.activeSelf == false)
        {
            return;
        }

        PlayState(startingStateIndex);
    }

    /// <summary>Tweens to the state at <paramref name="index"/>, killing any tween already in progress. No-op if the GameObject is inactive or the index is out of range.</summary>
    public void PlayState(int index)
    {
        if(gameObject.activeSelf == false)
        {
            return;
        }

        if (!IsStateIndexValid(index))
        {
            return;
        }

        StopActiveTween();

        TweenState state = states[index];

        if (state.delay > 0f)
        {
            pendingSoundState = state;
            Invoke(nameof(InvokePlayStateSound), state.delay);
        }
        else
        {
            PlayStateSound(state);
            state.onStateStart?.Invoke();
        }

        if (!state.enablePosition && !state.enableRotation && !state.enableScale)
        {
            return;
        }

        if (!isActiveAndEnabled)
        {
            return;
        }

        activeRoutine = StartCoroutine(PlayStateRoutine(state));
        currentStateIndex = index;
    }

    /// <summary>
    /// Waits out the state's delay, then tweens position/rotation/scale together over its duration.
    /// </summary>
    /// <remarks>
    /// The start values are read after the delay rather than when the state is requested, so a
    /// delayed state animates from wherever the transform actually is when it begins to move.
    /// </remarks>
    private IEnumerator PlayStateRoutine(TweenState state)
    {
        if (state.delay > 0f)
        {
            yield return new WaitForSeconds(state.delay);
        }

        tracks.Clear();

        if (state.enablePosition)
        {
            tracks.Add(TweenTrack.Move(transform, transform.localPosition, state.position, true, state.duration, 0f, state.easeType));
        }

        if (state.enableRotation)
        {
            tracks.Add(TweenTrack.Rotate(transform, transform.localEulerAngles, state.rotationEuler, true, state.duration, 0f, state.easeType));
        }

        if (state.enableScale)
        {
            tracks.Add(TweenTrack.Scale(transform, transform.localScale, state.scale, state.duration, 0f, state.easeType));
        }

        yield return TweenGroup.Run(tracks, 0f, false, 1, LoopType.Restart, null);

        activeRoutine = null;
        state.onStateComplete?.Invoke();
    }

    /// <summary>Tweens to the next state in the list, wrapping around to index 0 after the last.</summary>
    public void PlayNextState()
    {
        if(gameObject.activeSelf == false)
        {
            return;
        }

        if (states.Count == 0)
        {
            return;
        }

        int nextIndex = currentStateIndex + 1;
        if (nextIndex >= states.Count)
        {
            nextIndex = 0;
        }

        PlayState(nextIndex);
    }

    /// <summary>Tweens to the previous state in the list, wrapping around to the last after index 0.</summary>
    public void PlayPreviousState()
    {
        if(gameObject.activeSelf == false)
        {
            return;
        }

        if (states.Count == 0)
        {
            return;
        }

        int previousIndex = currentStateIndex - 1;
        if (previousIndex < 0)
        {
            previousIndex = states.Count - 1;
        }

        PlayState(previousIndex);
    }

    /// <summary>Snaps directly to the state at <paramref name="index"/> without tweening, killing any active tween. Does not invoke state events.</summary>
    public void ApplyStateInstantly(int index)
    {
        if(gameObject.activeSelf == false)
        {
            return;
        }
        
        if (!IsStateIndexValid(index))
        {
            return;
        }

        TweenState state = states[index];

        StopActiveTween();

        if (state.enablePosition)
        {
            transform.localPosition = state.position;
        }

        if (state.enableRotation)
        {
            transform.localRotation = Quaternion.Euler(state.rotationEuler);
        }

        if (state.enableScale)
        {
            transform.localScale = state.scale;
        }

        currentStateIndex = index;
    }

    /// <summary>Like <see cref="ApplyStateInstantly"/>, but also fires the state's onStateStart and onStateComplete events.</summary>
    public void ApplyStateInstantlyWithEvents(int index)
    {
        ApplyStateInstantly(index);

        if (!IsStateIndexValid(index)) return;

        TweenState state = states[index];
        state.onStateStart?.Invoke();
        state.onStateComplete?.Invoke();
    }

    /// <summary>Stops any in-progress tween and cancels a pending delayed state sound.</summary>
    public void StopActiveTween()
    {
        CancelInvoke(nameof(InvokePlayStateSound));
        pendingSoundState = null;

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
    }

    /// <summary>Index of the state last reached via <see cref="PlayState"/>/<see cref="ApplyStateInstantly"/>, or -1 if none has been applied yet.</summary>
    public int GetCurrentStateIndex()
    {
        return currentStateIndex;
    }

    /// <summary>Number of configured states.</summary>
    public int GetStateCount()
    {
        return states.Count;
    }

    private bool IsStateIndexValid(int index)
    {
        return index >= 0 && index < states.Count;
    }

    private void InvokePlayStateSound()
    {
        if (pendingSoundState == null) return;
        PlayStateSound(pendingSoundState);
        pendingSoundState.onStateStart?.Invoke();
        pendingSoundState = null;
    }

    private void PlayStateSound(TweenState state)
    {
        if (state == null || state.stateSound == null)
        {
            return;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            AudioSource.PlayClipAtPoint(state.stateSound, transform.position, state.soundVolume);
        }
        else
        {
            audioSource.PlayOneShot(state.stateSound, state.soundVolume);            
        }
    }

    private void OnDestroy()
    {
        StopActiveTween();
    }

    private void OnValidate()
    {
        if (startingStateIndex < 0)
        {
            startingStateIndex = 0;
        }

        for (int i = 0; i < states.Count; i++)
        {
            states[i].duration = Mathf.Max(0f, states[i].duration);
            states[i].delay = Mathf.Max(0f, states[i].delay);
        }
    }
}

#if UNITY_EDITOR
/// <summary>Custom inspector for <see cref="TweenStateAnimator"/>: lets designers add/remove/reorder states, copy the current transform into a state, and jump to a state for preview.</summary>
[CustomEditor(typeof(TweenStateAnimator))]
[MovedFrom(true, null, null, "TweenStateAnimatorEditor")]
public class TweenStateAnimatorEditor : Editor
{
    private SerializedProperty statesProperty;
    private SerializedProperty useStartingStateProperty;
    private SerializedProperty startingStateIndexProperty;

    private void OnEnable()
    {
        statesProperty = serializedObject.FindProperty("states");
        useStartingStateProperty = serializedObject.FindProperty("useStartingState");
        startingStateIndexProperty = serializedObject.FindProperty("startingStateIndex");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Startup", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useStartingStateProperty);
        EditorGUILayout.PropertyField(startingStateIndexProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("States", EditorStyles.boldLabel);

        for (int i = 0; i < statesProperty.arraySize; i++)
        {
            SerializedProperty state = statesProperty.GetArrayElementAtIndex(i);
            DrawStateBlock(state, i);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Add State"))
        {
            statesProperty.InsertArrayElementAtIndex(statesProperty.arraySize);
            SerializedProperty newState = statesProperty.GetArrayElementAtIndex(statesProperty.arraySize - 1);
            newState.FindPropertyRelative("stateName").stringValue = $"State {statesProperty.arraySize}";
            newState.FindPropertyRelative("enablePosition").boolValue = true;
            newState.FindPropertyRelative("position").vector3Value = ((TweenStateAnimator)target).transform.localPosition;
            newState.FindPropertyRelative("enableRotation").boolValue = false;
            newState.FindPropertyRelative("rotationEuler").vector3Value = ((TweenStateAnimator)target).transform.localRotation.eulerAngles;
            newState.FindPropertyRelative("enableScale").boolValue = false;
            newState.FindPropertyRelative("scale").vector3Value = ((TweenStateAnimator)target).transform.localScale;
            newState.FindPropertyRelative("duration").floatValue = 0.5f;
            newState.FindPropertyRelative("delay").floatValue = 0f;
            newState.FindPropertyRelative("easeType").enumValueIndex = (int)Ease.OutQuad;
            newState.FindPropertyRelative("soundVolume").floatValue = 1f;
            newState.FindPropertyRelative("isMinimized").boolValue = false;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawStateBlock(SerializedProperty state, int index)
    {
        TweenStateAnimator animator = (TweenStateAnimator)target;

        SerializedProperty stateName = state.FindPropertyRelative("stateName");
        SerializedProperty isMinimized = state.FindPropertyRelative("isMinimized");
        SerializedProperty enablePosition = state.FindPropertyRelative("enablePosition");
        SerializedProperty position = state.FindPropertyRelative("position");
        SerializedProperty enableRotation = state.FindPropertyRelative("enableRotation");
        SerializedProperty rotationEuler = state.FindPropertyRelative("rotationEuler");
        SerializedProperty enableScale = state.FindPropertyRelative("enableScale");
        SerializedProperty scale = state.FindPropertyRelative("scale");
        SerializedProperty duration = state.FindPropertyRelative("duration");
        SerializedProperty delay = state.FindPropertyRelative("delay");
        SerializedProperty easeType = state.FindPropertyRelative("easeType");
        SerializedProperty stateSound = state.FindPropertyRelative("stateSound");
        SerializedProperty soundVolume = state.FindPropertyRelative("soundVolume");
        SerializedProperty onStateStart = state.FindPropertyRelative("onStateStart");
        SerializedProperty onStateComplete = state.FindPropertyRelative("onStateComplete");

        Color prevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f, 1f);
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prevColor;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"State {index}", EditorStyles.boldLabel);
        if (GUILayout.Button(isMinimized.boolValue ? "Expand" : "Minimize", GUILayout.Width(80f)))
        {
            isMinimized.boolValue = !isMinimized.boolValue;
        }
        if (GUILayout.Button("Remove", GUILayout.Width(70f)))
        {
            statesProperty.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(stateName);

        if (!isMinimized.boolValue)
        {
            EditorGUILayout.PropertyField(enablePosition);
            EditorGUILayout.PropertyField(position);
            if (GUILayout.Button("Copy Current Position"))
            {
                Undo.RecordObject(target, "Copy Current Position");
                position.vector3Value = animator.transform.localPosition;
            }

            EditorGUILayout.PropertyField(enableRotation);
            EditorGUILayout.PropertyField(rotationEuler);
            if (GUILayout.Button("Copy Current Rotation"))
            {
                Undo.RecordObject(target, "Copy Current Rotation");
                rotationEuler.vector3Value = animator.transform.localRotation.eulerAngles;
            }

            EditorGUILayout.PropertyField(enableScale);
            EditorGUILayout.PropertyField(scale);
            if (GUILayout.Button("Copy Current Scale"))
            {
                Undo.RecordObject(target, "Copy Current Scale");
                scale.vector3Value = animator.transform.localScale;
            }

            EditorGUILayout.PropertyField(duration);
            EditorGUILayout.PropertyField(delay);
            EditorGUILayout.PropertyField(easeType);

            EditorGUILayout.PropertyField(stateSound);
            EditorGUILayout.Slider(soundVolume, 0f, 1f);

            EditorGUILayout.PropertyField(onStateStart);
            EditorGUILayout.PropertyField(onStateComplete);
        }

        if (GUILayout.Button("Go To This State"))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(animator.transform, "Go To Tween State");
            animator.ApplyStateInstantly(index);
            EditorUtility.SetDirty(animator.transform);
            serializedObject.Update();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }
}
#endif
}