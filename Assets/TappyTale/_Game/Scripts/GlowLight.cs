using UnityEngine;
using UnityEngine.Events;
using System;
using UnityEngine.Scripting.APIUpdating;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MSJTDCC
{
/// <summary>
/// Named-state light switch: cycles a Light (and optionally a <see cref="MaterialLight"/>) through a
/// list of user-defined <see cref="GlowState"/> entries, each with its own color, intensity, sound,
/// and UnityEvent. Supports either an instant switch or a smooth color/intensity transition.
/// </summary>
[MovedFrom(true, null, null, "GlowLight")]
public class GlowLight : MonoBehaviour
{
    /// <summary>One named light configuration: color, intensity, on/off flag, and the sound/event fired when entered.</summary>
    [Serializable]
    public class GlowState
    {
        public string name = "State";
        public bool isOn = true;
        public Color color = Color.white;
        public float intensity = 1f;
        public AudioClip audioClip;
        public UnityEvent onEnter;
    }

    [Header("References")]
    [SerializeField] private Light glowLight;
    [SerializeField] private MaterialLight materialLight;
    [SerializeField] private AudioSource audioSource;

    [Header("States")]
    [SerializeField] private GlowState[] states = new GlowState[]
    {
        new GlowState { name = "On",  color = Color.green, intensity = 4f },
        new GlowState { name = "Off", color = Color.red,   intensity = 4f },
    };
    /// <summary>Index into <see cref="states"/> applied on <see cref="Start"/>.</summary>
    [SerializeField] private int initialStateIndex = 0;

    [Header("Transition")]
    /// <summary>If true, switching states lerps color/intensity over <see cref="transitionDuration"/> instead of applying instantly.</summary>
    [SerializeField] private bool smoothTransition = false;
    [SerializeField] private float transitionDuration = 0.5f;

    private int currentStateIndex = -1;
    private Color targetColor;
    private float targetIntensity;
    private Color startColor;
    private float startIntensity;
    private float transitionProgress = 1f;
    private bool targetIsOn;

    void Start()
    {
        if (states == null || states.Length == 0) return;
        int idx = Mathf.Clamp(initialStateIndex, 0, states.Length - 1);
        SwitchToState(idx);
    }

    void Update()
    {
        if (!smoothTransition || currentStateIndex < 0 || transitionProgress >= 1f) return;

        transitionProgress = Mathf.Min(transitionProgress + Time.deltaTime / transitionDuration, 1f);

        Color lerpedColor = Color.Lerp(startColor, targetColor, transitionProgress);
        float lerpedIntensity = Mathf.Lerp(startIntensity, targetIntensity, transitionProgress);

        if (glowLight != null)
        {
            glowLight.color = lerpedColor;
            glowLight.intensity = lerpedIntensity;
        }

        if (materialLight != null)
            materialLight.ApplyEmissionColor(lerpedColor);

        if (transitionProgress >= 1f && !targetIsOn)
            SetOn(false);
    }

    /// <summary>Switches to the state at <paramref name="index"/> in <see cref="states"/>, applying it immediately or beginning a smooth transition depending on <see cref="smoothTransition"/>.</summary>
    public void SwitchToState(int index)
    {
        if (states == null || index < 0 || index >= states.Length) return;

        currentStateIndex = index;
        GlowState state = states[index];

        targetColor = state.color;
        targetIntensity = state.intensity;
        targetIsOn = state.isOn;

        if (!smoothTransition)
        {
            ApplyImmediate(state);
            SetOn(state.isOn);
        }
        else
        {
            startColor = glowLight != null ? glowLight.color : targetColor;
            startIntensity = glowLight != null ? glowLight.intensity : targetIntensity;
            transitionProgress = 0f;

            if (state.isOn) SetOn(true);
        }

        if (audioSource != null && state.audioClip != null) audioSource.PlayOneShot(state.audioClip);
        state.onEnter?.Invoke();
    }

    /// <summary>Switches to the state named <paramref name="stateName"/> (matched against <see cref="GlowState.name"/>). Logs a warning if no state matches.</summary>
    public void SwitchToState(string stateName)
    {
        if (states == null) return;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].name == stateName)
            {
                SwitchToState(i);
                return;
            }
        }
        Debug.LogWarning($"GlowLight: state '{stateName}' not found.");
    }

    /// <summary>Convenience shortcut for <c>SwitchToState("On")</c>.</summary>
    public void TurnOn()  => SwitchToState("On");
    /// <summary>Convenience shortcut for <c>SwitchToState("Off")</c>.</summary>
    public void TurnOff() => SwitchToState("Off");
    /// <summary>Flips between <see cref="states"/> index 0 and 1 (the default "On"/"Off" entries).</summary>
    public void Toggle()  => SwitchToState(currentStateIndex == 0 ? 1 : 0);

    /// <summary>Switches to the next state in <see cref="states"/>, wrapping around to the first.</summary>
    public void NextState()
    {
        if (states == null || states.Length == 0) return;
        SwitchToState((currentStateIndex + 1) % states.Length);
    }

    /// <summary>Switches to the previous state in <see cref="states"/>, wrapping around to the last.</summary>
    public void PreviousState()
    {
        if (states == null || states.Length == 0) return;
        SwitchToState((currentStateIndex - 1 + states.Length) % states.Length);
    }

    private void ApplyImmediate(GlowState state)
    {
        if (glowLight != null)
        {
            glowLight.color = state.color;
            glowLight.intensity = state.intensity;
        }

        if (materialLight != null)
        {
            if (state.isOn) materialLight.SetOnColor(state.color);
            else            materialLight.SetOffColor(state.color);

            if (state.isOn) materialLight.TurnOn();
            else            materialLight.TurnOff();
        }
    }

    /// <summary>Directly enables/disables <see cref="glowLight"/>, bypassing state lookup (used internally to finish a fade-out transition).</summary>
    public void SetOn(bool value)
    {
        if (glowLight != null) glowLight.enabled = value;
    }

    /// <summary>Index of the currently active entry in <see cref="states"/>, or -1 before the first switch.</summary>
    public int CurrentStateIndex => currentStateIndex;
    /// <summary>Name of the currently active state, or an empty string before the first switch.</summary>
    public string CurrentStateName => (states != null && currentStateIndex >= 0) ? states[currentStateIndex].name : "";
}

#if UNITY_EDITOR
/// <summary>Custom inspector for <see cref="GlowLight"/> adding buttons to switch to any configured state, or step to the next/previous one, from the Editor.</summary>
[CustomEditor(typeof(GlowLight))]
[MovedFrom(true, null, null, "GlowLightEditor")]
public class GlowLightEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GlowLight gl = (GlowLight)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Switch State", EditorStyles.boldLabel);

        serializedObject.Update();
        SerializedProperty statesProp = serializedObject.FindProperty("states");

        if (statesProp != null && statesProp.arraySize > 0)
        {
            for (int i = 0; i < statesProp.arraySize; i++)
            {
                string label = statesProp.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (GUILayout.Button($"Switch to: {label}"))
                    gl.SwitchToState(i);
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Previous")) gl.PreviousState();
        if (GUILayout.Button("Next")) gl.NextState();
        EditorGUILayout.EndHorizontal();
    }
}
#endif
}
