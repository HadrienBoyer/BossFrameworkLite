using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
/// <summary>
/// Pressure-plate style switch: turns on while a tagged object (e.g. the player) stands on
/// it, or latches on permanently, transitioning over time with matching visuals/audio/UI
/// progress feedback.
/// </summary>
[MovedFrom(true, null, null, "StandSwitch")]
public class StandSwitch : MonoBehaviour
{
    public enum StandMode
    {
        TurnOnAndStayOn,
        TurnOnWhileStanding
    }

    /// <summary>Enables verbose Debug.Log output for interaction enable/disable, useful while wiring up a scene.</summary>
    public bool showLogs = false;

    [Header("Mode")]
    [SerializeField] private StandMode mode = StandMode.TurnOnWhileStanding;
    [SerializeField] private bool disableInteractionOnStart = false;
    /// <summary>If true, the switch snaps off (bypassing the timed transition) when <see cref="DisableInteraction"/> is called.</summary>
    [SerializeField] private bool turnOffOnDisable = true;

    [Header("Timing")]
    [SerializeField] private float turnOnDuration = 5f;
    [SerializeField] private float turnOffDuration = 5f;
    [SerializeField] private float detectionDelay = 0.1f; // Delay before starting transition

    [Header("Trigger Detection")]
    [SerializeField] private string[] triggerDetectionTags = new string[] { "Player" };

    [Header("Visuals")]
    /// <summary>Transform moved/rotated between <see cref="offStateTransform"/> and <see cref="onStateTransform"/> as the switch transitions.</summary>
    [SerializeField] private Transform switchTransform;

    [Header("State Transforms")]
    [SerializeField] private Transform onStateTransform;
    [SerializeField] private Transform offStateTransform;

    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material onMaterial;
    [SerializeField] private Material offMaterial;
    [SerializeField] private Material disableMaterial;

    [Header("UI Progress")]
    [SerializeField] private Image progressFillImage; // UI Image for progress visualization
    [SerializeField] private bool showUIProgress = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip onEnterSound;
    [SerializeField] private AudioClip onExitSound;
    /// <summary>Looping clip played on <see cref="audioSource"/> while the switch is mid-transition.</summary>
    [SerializeField] private AudioClip fillLoopSound;
    [SerializeField] private float fillLoopVolume = 0.5f;
    [SerializeField] private AudioClip enableSound;
    [SerializeField] private AudioClip disableSound;

    [Header("Events")]
    public UnityEvent OnSwitchTurnedOn;
    public UnityEvent OnSwitchTurnedOff;
    public UnityEvent<float> OnProgressChanged; // Parameter: current progress (0-1)
    /// <summary>Fires when a valid object starts standing on the switch, before the detection delay elapses.</summary>
    public UnityEvent OnDetectionStarted;
    /// <summary>Fires when a pending detection is cancelled (e.g. the object leaves before <see cref="detectionDelay"/> elapses).</summary>
    public UnityEvent OnDetectionEnded;

    private bool isOn;
    private bool targetState;
    private float transitionProgress; // 0 to 1
    private bool isTransitioning;
    private bool isDetecting;
    private float detectionTimer;
    private int playerCount;

    // Track material state to prevent multiple calls
    private bool materialIsOnState;
    private bool isFillingLoopPlaying;
    private bool isDisabled;

    private void Awake() {

        if (!audioSource)
            audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        
        if (disableInteractionOnStart)
        {
            DisableInteraction();
        }

        // Initialize UI progress
        UpdateUIProgress();
    }

    private void Update()
    {
        // Handle detection delay
        UpdateDetection();

        // Handle transition
        if (isTransitioning)
        {
            float previousProgress = transitionProgress;
            float targetDuration = targetState ? turnOnDuration : turnOffDuration;

            // Calculate progress direction
            float progressDirection = targetState ? 1f : -1f;

            // Update progress based on direction and time
            transitionProgress += (progressDirection * Time.deltaTime) / targetDuration;

            // Clamp progress between 0 and 1
            transitionProgress = Mathf.Clamp01(transitionProgress);

            // Update visuals based on current progress
            UpdateVisuals(transitionProgress);

            // Update UI progress
            UpdateUIProgress();

            // Check material transition (only at exact 0 or 1)
            CheckMaterialTransition();

            // Handle fill loop sound
            UpdateFillLoopSound();

            // Invoke progress changed event
            if (transitionProgress != previousProgress)
            {
                OnProgressChanged?.Invoke(transitionProgress);
            }

            // Check if transition is complete
            if ((targetState && Mathf.Approximately(transitionProgress, 1f)) ||
                (!targetState && Mathf.Approximately(transitionProgress, 0f)))
            {
                CompleteTransition();
            }
        }
    }

    /// <summary>Re-enables the switch's trigger collider and restores its material to reflect the current on/off state.</summary>
    public void EnableInteraction()
    {
        if (showLogs)
            Debug.Log("Enabling StandSwitch interaction");

        playerCount = 0;
        GetComponent<Collider>().enabled = true;
        targetRenderer.material = isOn ? onMaterial : offMaterial;
        PlaySound(enableSound);
    }

    /// <summary>Disables the switch's trigger collider (stops detecting standing objects) and optionally forces it off.</summary>
    public void DisableInteraction()
    {
        if (showLogs)
            Debug.Log("Disabling StandSwitch interaction");

        playerCount = 0;

        if (turnOffOnDisable)
            ApplyStateImmediate(false);

        GetComponent<Collider>().enabled = false;

        if (targetRenderer && disableMaterial)
        {
            targetRenderer.material = disableMaterial;
        }

        PlaySound(disableSound);
    }

    private void UpdateDetection()
    {
        if (isDetecting)
        {
            detectionTimer += Time.deltaTime;

            // Start transition when delay is complete
            if (detectionTimer >= detectionDelay)
            {
                StartTransition();
            }
        }
    }

    private void StartTransition()
    {
        isDetecting = false;

        // If we're already at the target state, do nothing
        if ((targetState && Mathf.Approximately(transitionProgress, 1f)) ||
            (!targetState && Mathf.Approximately(transitionProgress, 0f)))
            return;

        // Start transition
        isTransitioning = true;
    }

    private void UpdateVisuals(float progress)
    {
        // Update switch transform position and rotation
        if (switchTransform && onStateTransform && offStateTransform)
        {
            switchTransform.position = Vector3.Lerp(
                offStateTransform.position,
                onStateTransform.position,
                progress
            );

            switchTransform.rotation = Quaternion.Slerp(
                offStateTransform.rotation,
                onStateTransform.rotation,
                progress
            );
        }
    }

    private void UpdateUIProgress()
    {
        if (!progressFillImage || !showUIProgress)
            return;

        // Update fill amount
        progressFillImage.fillAmount = transitionProgress;
    }

    private void CheckMaterialTransition()
    {
        if (!targetRenderer)
            return;

        if (isDisabled)
        {
            if (disableMaterial && targetRenderer.material != disableMaterial)
                targetRenderer.material = disableMaterial;
            return;
        }

        if (onMaterial == null || offMaterial == null)
            return;

        // Determine material state based on exact progress values
        bool shouldBeOnMaterial = false;

        if (Mathf.Approximately(transitionProgress, 1f))
        {
            // At progress 1, always ON
            shouldBeOnMaterial = true;
        }
        else if (Mathf.Approximately(transitionProgress, 0f))
        {
            // At progress 0, always OFF
            shouldBeOnMaterial = false;
        }
        else
        {
            // For any intermediate progress (0 < progress < 1), keep current material state
            // Don't change the material
            return;
        }

        // Only change material if state differs
        if (shouldBeOnMaterial != materialIsOnState)
        {
            materialIsOnState = shouldBeOnMaterial;
            targetRenderer.material = shouldBeOnMaterial ? onMaterial : offMaterial;
        }
    }

    private void CompleteTransition()
    {
        isTransitioning = false;
        bool newState = targetState;

        // Stop fill loop sound
        StopFillLoopSound();

        // Ensure final state is applied
        if (newState != isOn)
        {
            isOn = newState;

            // Update material to final state (should already be correct from CheckMaterialTransition)
            if (targetRenderer && !isDisabled)
            {
                materialIsOnState = isOn;
                targetRenderer.material = isOn ? onMaterial : offMaterial;
            }

            // Play appropriate sound and invoke event
            if (isOn)
            {
                PlaySound(onEnterSound);
                OnSwitchTurnedOn?.Invoke();
            }
            else
            {
                PlaySound(onExitSound);
                OnSwitchTurnedOff?.Invoke();
            }
        }

        // Ensure final position/rotation
        UpdateVisuals(isOn ? 1f : 0f);

        // Update UI progress
        UpdateUIProgress();

    }

    /* ===================== TRIGGER ===================== */

    private void OnTriggerEnter(Collider other)
    {
        if (isDisabled || !IsValidTriggerDetection(other))
            return;

        if (isOn && mode == StandMode.TurnOnAndStayOn)
            return;

        playerCount++;

        if (playerCount == 1)
        {
            // Set target state
            targetState = true;

            // Start detection delay
            StartDetection();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (isDisabled || !IsValidTriggerDetection(other))
            return;

        playerCount--;

        if (playerCount <= 0 /*&& mode == StandMode.TurnOnWhileStanding*/)
        {
            // Set target state
            targetState = false;

            if (mode == StandMode.TurnOnAndStayOn && isOn)
                return;


            // Start detection delay
            StartDetection();
        }
    }

    private void StartDetection()
    {
        // Stop any current transition
        isTransitioning = false;

        // Start detection timer
        isDetecting = true;
        detectionTimer = 0f;

        OnDetectionStarted?.Invoke();

        // If detection delay is 0, start immediately
        if (detectionDelay <= 0f)
        {
            StartTransition();
        }
    }

    private void CancelDetection()
    {
        if (isDetecting)
        {
            isDetecting = false;
            detectionTimer = 0f;
            OnDetectionEnded?.Invoke();
        }
    }

    private void ApplyStateImmediate(bool state)
    {
        // Cancel any detection or transition
        CancelDetection();
        isTransitioning = false;

        bool previousState = isOn;
        isOn = state;
        targetState = state;
        transitionProgress = state ? 1f : 0f;
        materialIsOnState = state;

        UpdateVisuals(transitionProgress);

        // Update material to correct state
        if (targetRenderer)
            targetRenderer.material = state ? onMaterial : offMaterial;

        // Update UI progress
        UpdateUIProgress();

        // Invoke events if state changed
        if (previousState != state)
        {
            if (state)
                OnSwitchTurnedOn?.Invoke();
            else
                OnSwitchTurnedOff?.Invoke();
        }

        OnProgressChanged?.Invoke(transitionProgress);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!switchTransform)
            switchTransform = transform;
    }
#endif

    // Public methods for external control
    /// <summary>Snaps the switch fully on immediately, skipping the timed transition.</summary>
    [ContextMenu("Turn On")]
    public void TurnOn()
    {
        ApplyStateImmediate(true);
    }

    /// <summary>Snaps the switch fully off immediately, skipping the timed transition.</summary>
    [ContextMenu("Turn Off")]
    public void TurnOff()
    {
        ApplyStateImmediate(false);
    }

    /// <summary>Snaps the switch to the given on/off state immediately, skipping the timed transition.</summary>
    public void SetStateImmediate(bool on)
    {
        ApplyStateImmediate(on);
    }

    /// <summary>Snaps the switch to the opposite of its current state immediately, skipping the timed transition.</summary>
    [ContextMenu("Toggle State")]
    public void ToggleStateImmediate()
    {
        ApplyStateImmediate(!isOn);
    }

    /// <summary>Assigns the UI Image used to visualize transition progress and immediately refreshes it.</summary>
    public void SetUIProgressImage(Image image)
    {
        progressFillImage = image;
        UpdateUIProgress();
    }

    /// <summary>Shows or hides the progress UI Image.</summary>
    public void ShowUIProgress(bool show)
    {
        showUIProgress = show;
        if (progressFillImage)
            progressFillImage.gameObject.SetActive(show);
    }

    // Gradient/color progress display isn't implemented (progressFillImage uses its own
    // fillAmount); these are kept so existing UnityEvent wiring in prefabs doesn't break.
    /// <summary>Currently a no-op kept for compatibility with existing UnityEvent wiring; progress display doesn't support gradients.</summary>
    public void SetUseGradient(bool useGrad)
    {
        UpdateUIProgress();
    }

    /// <summary>Currently a no-op kept for compatibility with existing UnityEvent wiring; progress display doesn't support gradients.</summary>
    public void SetProgressColors(Color startColor, Color endColor)
    {
        UpdateUIProgress();
    }

    /// <summary>Currently a no-op kept for compatibility with existing UnityEvent wiring; progress display doesn't support gradients.</summary>
    public void SetProgressGradient(Gradient gradient)
    {
        UpdateUIProgress();
    }

    // Helper methods
    /// <summary>Current transition progress, from 0 (fully off) to 1 (fully on).</summary>
    public float GetCurrentProgress()
    {
        return transitionProgress;
    }

    /// <summary>True if the switch is currently fully on (not mid-transition).</summary>
    public bool IsOn()
    {
        return isOn;
    }

    /// <summary>True if the switch is currently mid-transition between on and off.</summary>
    public bool IsTransitioning()
    {
        return isTransitioning;
    }

    /// <summary>True if a valid object is standing on the switch but the detection delay hasn't elapsed yet.</summary>
    public bool IsDetecting()
    {
        return isDetecting;
    }

    /// <summary>Progress through the detection delay, from 0 to 1, while <see cref="IsDetecting"/> is true.</summary>
    public float GetDetectionProgress()
    {
        return Mathf.Clamp01(detectionTimer / detectionDelay);
    }

    private bool IsValidTriggerDetection(Collider other)
    {
        if (!other || triggerDetectionTags == null || triggerDetectionTags.Length == 0)
            return false;

        for (int i = 0; i < triggerDetectionTags.Length; i++)
        {
            string tagName = triggerDetectionTags[i];

            if (!string.IsNullOrWhiteSpace(tagName) && other.CompareTag(tagName))
                return true;
        }

        return false;
    }

    /* ===================== AUDIO ===================== */

    private void PlaySound(AudioClip clip)
    {
        if (!audioSource || !clip)
            return;

        audioSource.PlayOneShot(clip);
    }

    private void UpdateFillLoopSound()
    {
        if (!audioSource || !fillLoopSound)
            return;

        // Start loop sound when transitioning
        if (!isFillingLoopPlaying)
        {
            audioSource.clip = fillLoopSound;
            audioSource.volume = fillLoopVolume;
            audioSource.loop = true;
            audioSource.Play();
            isFillingLoopPlaying = true;
        }
    }

    private void StopFillLoopSound()
    {
        if (!audioSource)
            return;

        if (isFillingLoopPlaying && audioSource.clip == fillLoopSound)
        {
            audioSource.Stop();
            audioSource.loop = false;
            isFillingLoopPlaying = false;
        }
    }
}
}