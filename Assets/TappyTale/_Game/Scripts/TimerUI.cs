using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Displays a running timer. Fully independent -- knows nothing about TimerController.
/// Feed it data by calling Tick() each frame from whatever owns the timer logic.
/// </summary>
[MovedFrom(true, null, null, "TimerUI")]
public class TimerUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root panel to show/hide. Leave empty to show/hide this GameObject instead.")]
    public GameObject uiRoot;
    public TMP_Text timeText;

    [Header("Format")]
    [Tooltip("mm:ss  |  mm:ss.f  |  s  |  s.f")]
    public TimeFormat format = TimeFormat.MinutesSeconds;

    [Header("Warning Color")]
    public bool useWarningColor = true;
    [Tooltip("Switch to warning color when this many seconds remain (countdown) or are elapsed (countup).")]
    [Min(0f)] public float warningThreshold = 5f;
    public Color normalColor = Color.white;
    public Color warningColor = Color.red;

    public enum TimeFormat { MinutesSeconds, MinutesSecondsTenths, Seconds, SecondsTenths }

    // ----------------------------------------------------
    //  Public API
    // ----------------------------------------------------

    /// <summary>Enable the UI panel and reset the text.</summary>
    public void Show()
    {
        Root.SetActive(true);
        SetText("--:--");
        SetColor(normalColor);
    }

    /// <summary>Disable the UI panel.</summary>
    public void Hide()
    {
        Root.SetActive(false);
    }

    /// <summary>
    /// Update the display. Call this every frame (or whenever the timer value changes).
    /// </summary>
    /// <param name="currentTime">Elapsed seconds since the timer started.</param>
    /// <param name="duration">Total timer duration in seconds.</param>
    /// <param name="countDown">True = display time remaining; false = display elapsed time.</param>
    public void Tick(float currentTime, float duration, bool countDown)
    {
        float display = countDown ? Mathf.Max(0f, duration - currentTime) : currentTime;

        SetText(Format(display));

        if (useWarningColor)
            SetColor(display <= warningThreshold ? warningColor : normalColor);
    }

    // ----------------------------------------------------
    //  Internals
    // ----------------------------------------------------

    GameObject Root => uiRoot != null ? uiRoot : gameObject;

    void SetText(string value)
    {
        if (timeText != null)
            timeText.text = value;
    }

    void SetColor(Color color)
    {
        if (timeText != null)
            timeText.color = color;
    }

    string Format(float seconds)
    {
        switch (format)
        {
            case TimeFormat.MinutesSeconds:
            {
                int m = Mathf.FloorToInt(seconds / 60f);
                int s = Mathf.FloorToInt(seconds % 60f);
                return $"{m:00}:{s:00}";
            }
            case TimeFormat.MinutesSecondsTenths:
            {
                int m = Mathf.FloorToInt(seconds / 60f);
                int s = Mathf.FloorToInt(seconds % 60f);
                int t = Mathf.FloorToInt((seconds % 1f) * 10f);
                return $"{m:00}:{s:00}.{t}";
            }
            case TimeFormat.Seconds:
                return $"{Mathf.FloorToInt(seconds):0}";
            case TimeFormat.SecondsTenths:
                return $"{seconds:0.0}";
            default:
                return $"{seconds:0.0}";
        }
    }
}
}
