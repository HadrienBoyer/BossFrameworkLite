using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MSJTDCC
{
/// <summary>Fades a UI Image's alpha in or out at a constant rate over a set duration.</summary>
[MovedFrom(true, null, null, "FadeImg")]
public class FadeImg : MonoBehaviour
{
    public enum FadeType { FadeIn, FadeOut }

    public Image uiImage;
    public float fadeInDuration = 2.0f;
    public float fadeOutDuration = 2.0f;
    /// <summary>Alpha value used as the "fully visible" end of the fade.</summary>
    public float maxAlphaValue = 1.0f;
    /// <summary>If true, automatically starts a fade (<see cref="startFadeType"/>) on <see cref="Start"/>.</summary>
    public bool fadeOnStart = true;
    public FadeType startFadeType = FadeType.FadeIn;

    private float targetAlpha;
    private float currentAlpha;
    private float fadeSpeed;
    private bool isFading = false;

    private void Start()
    {
        if (!fadeOnStart) return;

        if (startFadeType == FadeType.FadeIn)
            StartFadeIn();
        else
            StartFadeOut();
    }

    private void Update()
    {
        if (!isFading) return;

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        Color c = uiImage.color;
        uiImage.color = new Color(c.r, c.g, c.b, currentAlpha);

        if (Mathf.Approximately(currentAlpha, targetAlpha))
            isFading = false;

    }

    /// <summary>Starts fading <see cref="uiImage"/> in from transparent to <see cref="maxAlphaValue"/> over <see cref="fadeInDuration"/>.</summary>
    public void StartFadeIn()
    {
        SetFade(0f, maxAlphaValue, fadeInDuration);
    }

    /// <summary>Starts fading <see cref="uiImage"/> out from <see cref="maxAlphaValue"/> to transparent over <see cref="fadeOutDuration"/>.</summary>
    public void StartFadeOut()
    {
        SetFade(maxAlphaValue, 0f, fadeOutDuration);
    }

    private void SetFade(float startAlpha, float endAlpha, float duration)
    {
        currentAlpha = startAlpha;
        uiImage.color = new Color(uiImage.color.r, uiImage.color.g, uiImage.color.b, currentAlpha);
        targetAlpha = endAlpha;
        fadeSpeed = duration > 0f ? Mathf.Abs(endAlpha - startAlpha) / duration : float.MaxValue;
        isFading = true;
    }
}

#if UNITY_EDITOR
/// <summary>Adds Fade In / Fade Out preview buttons to the FadeImg inspector.</summary>
[CustomEditor(typeof(FadeImg))]
[MovedFrom(true, null, null, "FadeImgEditor")]
public class FadeImgEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        FadeImg fade = (FadeImg)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);

        if (GUILayout.Button("Fade In"))
            fade.StartFadeIn();

        if (GUILayout.Button("Fade Out"))
            fade.StartFadeOut();
    }
}
#endif
}
