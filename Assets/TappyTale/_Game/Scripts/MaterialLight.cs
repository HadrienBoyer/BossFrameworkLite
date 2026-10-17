using UnityEngine;
using UnityEditor;
using System.Xml.Serialization;
using System.Runtime.CompilerServices;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Drives a material's emission color and an optional Light between an on/off state, with
/// audio feedback and a BoxCollider that can be toggled independently (see <see cref="Enable"/>/<see cref="Disable"/>).
/// </summary>
[MovedFrom(true, null, null, "MaterialLight")]
public class MaterialLight : MonoBehaviour
{
	[Header("Target")]
	[SerializeField] private Renderer targetRenderer;

	[Header("Audio")]
	[SerializeField] private AudioSource audioSource;
	[SerializeField] private AudioClip turnOnAudio;
	[SerializeField] private AudioClip turnOffAudio;

    [Header("Light")]
    [SerializeField] private Light targetLight;
    /// <summary>If true, <see cref="targetLight"/> is disabled entirely when off; otherwise it stays enabled and just switches to <see cref="offColor"/>.</summary>
    [SerializeField] private bool disableLightIfOff = false;

	[Header("Colors")]
	[SerializeField] private Color onColor = Color.green;
	[SerializeField] private Color offColor = Color.red;
	/// <summary>Emission color used instead of <see cref="offColor"/> when <see cref="disableLightIfOff"/> is true and the light is off.</summary>
	[SerializeField] private Color disabledColor = Color.black;

	[Header("Interaction")]
	[SerializeField] private BoxCollider boxCollider;

	[Header("State")]
	[SerializeField] private bool isOn = false;

	// Material instance used to change emission at runtime
	[SerializeField] private Material materialInstance;

	// Emission property name (URP typically uses _EmissionColor)
	private string emissionProperty = "_EmissionColor";

	public UnityEvent onTurnOn;
	public UnityEvent onTurnOff;

	/// <summary>Sets the color used for the on state without applying it immediately.</summary>
	public void SetOnColor(Color c) { onColor = c; }
	/// <summary>Sets the color used for the off state without applying it immediately.</summary>
	public void SetOffColor(Color c) { offColor = c; }

	/// <summary>Directly sets the target material's emission color, bypassing the on/off state (used by <see cref="GlowLight"/> for smooth transitions).</summary>
	public void ApplyEmissionColor(Color c)
	{
		if (materialInstance == null) return;
		materialInstance.EnableKeyword("_EMISSION");
		materialInstance.SetColor(emissionProperty, c);
	}


	void Start()
	{
		if (targetRenderer != null && materialInstance == null)
		{
			// Use instance so we don't modify the shared asset
			materialInstance = targetRenderer.material;
		}

		if(boxCollider == null)
		 	boxCollider = GetComponent<BoxCollider>();

		if(isOn)
			TurnOn();
		else
			TurnOff();
	}

	/// <summary>Switches to the on state: updates emission/light color and plays <see cref="turnOnAudio"/>.</summary>
	[ContextMenu("Turn On")]
	public void TurnOn()
	{
		isOn = true;
		UpdateMaterialEmission();
        UpdateLight();

		if(audioSource)
			audioSource?.PlayOneShot(turnOnAudio);

		onTurnOn?.Invoke();
	}

	/// <summary>Switches to the off state: updates emission/light color and plays <see cref="turnOffAudio"/>.</summary>
	[ContextMenu("Turn Off")]
	public void TurnOff()
	{
		isOn = false;
		UpdateMaterialEmission();
        UpdateLight();

		if(audioSource)
			audioSource?.PlayOneShot(turnOffAudio);

		onTurnOff?.Invoke();
	}

	/// <summary>Enables <see cref="boxCollider"/> (e.g. to allow interaction again), independent of on/off state.</summary>
	[ContextMenu("Enable")]
	public void Enable()
	{
		if (boxCollider != null)
			boxCollider.enabled = true;
	}

	/// <summary>Disables <see cref="boxCollider"/> (e.g. to block interaction), independent of on/off state.</summary>
	[ContextMenu("Disable")]
	public void Disable()
	{
		if (boxCollider != null)
			boxCollider.enabled = false;
	}

	/// <summary>Flips between the on and off states.</summary>
	[ContextMenu("Toggle")]
	public void Toggle()
	{
		isOn = !isOn;
		
		if(isOn)
			TurnOn();
		else
			TurnOff();
	}

	void UpdateMaterialEmission()
	{
		if (materialInstance == null) return;

		Color c = isOn ? onColor : offColor;

		// Enable emission keyword so emission is visible
		materialInstance.EnableKeyword("_EMISSION");

		if (materialInstance.HasProperty(emissionProperty))
			materialInstance.SetColor(emissionProperty, c);
		else if (materialInstance.HasProperty("_EmissionColor"))
			materialInstance.SetColor("_EmissionColor", c);

		if(disableLightIfOff && !isOn)
		{
			if (materialInstance.HasProperty(emissionProperty))
			materialInstance.SetColor(emissionProperty, disabledColor);
			else if (materialInstance.HasProperty("_EmissionColor"))
				materialInstance.SetColor("_EmissionColor", disabledColor);
		}
	}

    /// <summary>Applies the current on/off state to <see cref="targetLight"/> (enabled state and color).</summary>
    public void UpdateLight(){

        if (targetLight != null)
        {
            if (isOn)
            {
                targetLight.enabled = true;

            }
            else
            {
                if (disableLightIfOff)
                {
                    targetLight.enabled = false;
                }
                else
                {
                    targetLight.enabled = true;

                }
            }

			targetLight.color = isOn ? onColor : offColor;
        }
    }
}

#if UNITY_EDITOR

/// <summary>Custom inspector for <see cref="MaterialLight"/> adding buttons to trigger its on/off/toggle/enable/disable actions from the Editor.</summary>
[CustomEditor(typeof(MaterialLight))]
[MovedFrom(true, null, null, "MaterialLightEditor")]
public class MaterialLightEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		MaterialLight ml = (MaterialLight)target;

		EditorGUILayout.Space();

		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("Turn On"))
		{
			Undo.RecordObject(ml, "Turn On MaterialLight");
			ml.TurnOn();
			EditorUtility.SetDirty(ml);
		}

		if (GUILayout.Button("Turn Off"))
		{
			Undo.RecordObject(ml, "Turn Off MaterialLight");
			ml.TurnOff();
			EditorUtility.SetDirty(ml);
		}

		if (GUILayout.Button("Toggle"))
		{
			Undo.RecordObject(ml, "Toggle MaterialLight");
			ml.Toggle();
			EditorUtility.SetDirty(ml);
		}
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("Enable"))
		{
			Undo.RecordObject(ml, "Enable MaterialLight");
			ml.Enable();
			EditorUtility.SetDirty(ml);
		}

		if (GUILayout.Button("Disable"))
		{
			Undo.RecordObject(ml, "Disable MaterialLight");
			ml.Disable();
			EditorUtility.SetDirty(ml);
		}
		EditorGUILayout.EndHorizontal();
	}
}
#endif
}
