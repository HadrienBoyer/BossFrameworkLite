using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// A two-state toggle driven by UnityEvents, with optional per-direction limits on how many
/// times it can be switched on/off before it locks out further interaction.
/// </summary>
[MovedFrom(true, null, null, "AdvancedToggle")]
public class AdvancedToggle : MonoBehaviour
{
    /// <summary>If true, <see cref="SetState"/> is applied once on <see cref="Start"/> without counting against limits or firing limit events.</summary>
    [SerializeField] private bool updateOnStart = false;
    [SerializeField] private bool isOn = false;
    public enum ToggleMode
    {
        NormalToggle,   // Unlimited toggling
        OnOffLimited,   // Both On and Off limited
        OnLimited,      // Only On limited
        OffLimited      // Only Off limited
    }

    [Header("Settings")]
    [SerializeField] private ToggleMode mode = ToggleMode.NormalToggle;
    /// <summary>Max number of on-events allowed before <see cref="OnLimitReached"/> fires (used when <see cref="mode"/> is OnLimited or OnOffLimited).</summary>
    [SerializeField] private int maxOnEvents = 1;
    /// <summary>Max number of off-events allowed before <see cref="OffLimitReached"/> fires (used when <see cref="mode"/> is OffLimited or OnOffLimited).</summary>
    [SerializeField] private int maxOffEvents = 1;


    [Header("Events")]
    public UnityEvent OnToggledOn;
    public UnityEvent OnToggledOff;
    /// <summary>Fires once the on-count reaches <see cref="maxOnEvents"/>.</summary>
    public UnityEvent OnLimitReached;
    /// <summary>Fires once the off-count reaches <see cref="maxOffEvents"/>.</summary>
    public UnityEvent OffLimitReached;

    [SerializeField] private int onEventCount = 0;
    [SerializeField] private int offEventCount = 0;
    private InteractableObject interactableObject;

    public void Start()
    {
        TryGetComponent(out interactableObject);

        if (updateOnStart)
            SetState(isOn);
    }

    /// <summary>
    /// Toggle the state manually
    /// </summary>
    public void Toggle()
    {
        if (isOn)
            SetState(false);
        else
            SetState(true);
    }

    /// <summary>
    /// Force a specific state
    /// </summary>
    public void SetState(bool newState)
    {
        if (isOn == newState && !updateOnStart) return; // Already in desired state

        // Check limits before changing state — block interaction if limit reached
        if (!updateOnStart)
        {
            if (newState == true)
            {
                bool onLimited = mode == ToggleMode.OnLimited || mode == ToggleMode.OnOffLimited;
                if (onLimited && onEventCount >= maxOnEvents) return;
            }
            else
            {
                bool offLimited = mode == ToggleMode.OffLimited || mode == ToggleMode.OnOffLimited;
                if (offLimited && offEventCount >= maxOffEvents) return;
            }
        }

        isOn = newState;


        if (isOn)
        {
            OnToggledOn?.Invoke();

            bool onLimited = mode == ToggleMode.OnLimited || mode == ToggleMode.OnOffLimited;
            if (onLimited && !updateOnStart)
            {
                onEventCount++;
                if (onEventCount >= maxOnEvents)
                {
                    OnLimitReached?.Invoke();
                    // OnOffLimited: disable only when both limits are exhausted
                    if (mode == ToggleMode.OnOffLimited && offEventCount >= maxOffEvents)
                        interactableObject?.DisableInteraction();
                }
            }
        }
        else
        {
            OnToggledOff?.Invoke();

            bool offLimited = mode == ToggleMode.OffLimited || mode == ToggleMode.OnOffLimited;
            if (offLimited && !updateOnStart)
            {
                offEventCount++;
                if (offEventCount >= maxOffEvents)
                {
                    OffLimitReached?.Invoke();
                    // OnOffLimited: disable only when both limits are exhausted
                    if (mode == ToggleMode.OnOffLimited && onEventCount >= maxOnEvents)
                        interactableObject?.DisableInteraction();
                }
            }
        }

        updateOnStart = false;
    }

    /// <summary>Intentionally empty stub kept for backward compatibility with existing prefab UnityEvent wiring (e.g. OnClick). Volumetric glow support was removed.</summary>
    public void SetGlowToOnColor()
    {
        // Volumetric glow removed; this method remains for compatibility.
    }

    /// <summary>Intentionally empty stub kept for backward compatibility with existing prefab UnityEvent wiring (e.g. OnClick). Volumetric glow support was removed.</summary>
    public void SetGlowToOffColor()
    {
        // Volumetric glow removed; this method remains for compatibility.
    }

    /// <summary>
    /// Reset counters (useful if you want to reuse limited modes)
    /// </summary>
    public void ResetCounters()
    {
        onEventCount = 0;
        offEventCount = 0;
    }

    /// <summary>Sets both event limits to zero, causing the next toggle in either direction to immediately hit its limit.</summary>
    public void ForceSetLimitsToZero()
    {
        maxOnEvents = 0;
        maxOffEvents = 0;
    }

    /// <summary>
    /// Get current state
    /// </summary>
    public bool IsOn() => isOn;
}
}
