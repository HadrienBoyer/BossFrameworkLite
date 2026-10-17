using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using MSJTDCC.Tweening;

namespace MSJTDCC
{
/// <summary>
/// A configurable one-shot animation: on play, offsets position/rotation/scale from their
/// stored original transform and tweens back, optionally looping. Values can be tuned entirely from
/// the Inspector or triggered via the public API/UnityEvents.
/// </summary>
[MovedFrom(true, null, null, "TweenAnimations")]
public class TweenAnimations : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private bool playOnStart = false;
    /// <summary>Delay in seconds before the whole animation sequence begins.</summary>
    [SerializeField] private float globalDelay = 0f;

    [Header("Position Animation")]
    [SerializeField] private bool enablePositionTween = true;
    /// <summary>Starting offset from the original position; the object tweens from (original + offset) back to original.</summary>
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private float positionDuration = 1f;
    [SerializeField] private float positionDelay = 0f;
    [SerializeField] private Ease positionEase = Ease.OutQuad;
    /// <summary>If true, animates localPosition instead of world position.</summary>
    [SerializeField] private bool useLocalPosition = false;

    [Header("Rotation Animation")]
    [SerializeField] private bool enableRotationTween = false;
    /// <summary>Starting rotation offset (Euler) from the original rotation; the object tweens back to original.</summary>
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;
    [SerializeField] private float rotationDuration = 1f;
    [SerializeField] private float rotationDelay = 0f;
    [SerializeField] private Ease rotationEase = Ease.OutQuad;

    [Header("Scale Animation")]
    [SerializeField] private bool enableScaleTween = false;
    [SerializeField] private Vector3 startScale = Vector3.one;
    [SerializeField] private Vector3 endScale = Vector3.one;
    [SerializeField] private float scaleDuration = 1f;
    [SerializeField] private float scaleDelay = 0f;
    [SerializeField] private Ease scaleEase = Ease.OutQuad;

    [Header("Options")]
    [SerializeField] private bool loopAnimation = false;
    [SerializeField] private LoopType loopType = LoopType.Restart;
    [SerializeField] private int loopCount = -1; // -1 = infinite
    /// <summary>If true, the tween continues to progress while <see cref="Time.timeScale"/> is 0 (e.g. during a pause).</summary>
    [SerializeField] private bool ignoreTimeScale = false;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent OnAnimationStart;
    public UnityEngine.Events.UnityEvent OnAnimationComplete;

    private Vector3 originalPosition;
    private Vector3 originalLocalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private Coroutine animationRoutine;
    private readonly List<TweenTrack> tracks = new List<TweenTrack>();

    private void Awake()
    {
        // Store original transform values
        StoreOriginalTransform();
    }

    private void StoreOriginalTransform()
    {
        originalPosition = transform.position;
        originalLocalPosition = transform.localPosition;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        if (playOnStart)
        {
            PlayAnimation();
        }
    }

    /// <summary>Starts (or restarts) the configured position/rotation/scale tween sequence from the Inspector settings.</summary>
    [ContextMenu("Play Animation")]
    public void PlayAnimation()
    {
        StopAnimation();

        // Coroutines can't be started on an inactive object; nothing to animate towards anyway.
        if (!isActiveAndEnabled)
            return;

        tracks.Clear();

        OnAnimationStart?.Invoke();

        // Position Animation
        if (enablePositionTween)
        {
            Vector3 target = useLocalPosition ? originalLocalPosition : originalPosition;
            Vector3 start = target + positionOffset;

            if (useLocalPosition)
                transform.localPosition = start;
            else
                transform.position = start;

            tracks.Add(TweenTrack.Move(transform, start, target, useLocalPosition, positionDuration, positionDelay, positionEase));
        }

        // Rotation Animation
        if (enableRotationTween)
        {
            Quaternion start = originalRotation * Quaternion.Euler(rotationOffset);
            transform.rotation = start;

            tracks.Add(TweenTrack.Rotate(transform, start.eulerAngles, originalRotation.eulerAngles, false, rotationDuration, rotationDelay, rotationEase));
        }

        // Scale Animation
        if (enableScaleTween)
        {
            transform.localScale = startScale;

            tracks.Add(TweenTrack.Scale(transform, startScale, endScale, scaleDuration, scaleDelay, scaleEase));
        }

        // Nothing enabled: report completion straight away, as an empty sequence did before.
        if (tracks.Count == 0)
        {
            OnAnimationComplete?.Invoke();
            return;
        }

        animationRoutine = StartCoroutine(TweenGroup.Run(
            tracks,
            globalDelay,
            ignoreTimeScale,
            loopAnimation ? loopCount : 1,
            loopType,
            OnRoutineComplete));
    }

    private void OnRoutineComplete()
    {
        animationRoutine = null;
        OnAnimationComplete?.Invoke();
    }

    /// <summary>Stops the currently running animation, if any, leaving the transform where it is.</summary>
    [ContextMenu("Stop Animation")]
    public void StopAnimation()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }
    }

    /// <summary>Stops any running animation and snaps position/rotation/scale back to the stored original transform.</summary>
    [ContextMenu("Reset to Original")]
    public void ResetToOriginal()
    {
        StopAnimation();
        transform.position = originalPosition;
        transform.localPosition = originalLocalPosition;
        transform.rotation = originalRotation;
        transform.localScale = originalScale;
    }

    // Public methods for external control
    /// <summary>Tweens from (original position + offset) to the original position over <paramref name="duration"/>, independent of the Inspector-configured position tween. Ignores the sequence's globalDelay.</summary>
    public void TweenPosition(Vector3 offset, float duration, float startDelay = 0f)
    {
        StopAnimation();
        Vector3 start = originalPosition + offset;
        transform.position = start;
        PlaySingle(TweenTrack.Move(transform, start, originalPosition, false, duration, 0f, Ease.OutQuad), startDelay);
    }

    /// <summary>Tweens from (original rotation + Euler offset) to the original rotation over <paramref name="duration"/>, independent of the Inspector-configured rotation tween.</summary>
    public void TweenRotation(Vector3 offsetRotation, float duration, float startDelay = 0f)
    {
        StopAnimation();
        Quaternion start = originalRotation * Quaternion.Euler(offsetRotation);
        transform.rotation = start;
        PlaySingle(TweenTrack.Rotate(transform, start.eulerAngles, originalRotation.eulerAngles, false, duration, 0f, Ease.OutQuad), startDelay);
    }

    /// <summary>Tweens local scale from <paramref name="startScale"/> to <paramref name="endScale"/> over <paramref name="duration"/>, independent of the Inspector-configured scale tween.</summary>
    public void TweenScale(Vector3 startScale, Vector3 endScale, float duration, float startDelay = 0f)
    {
        StopAnimation();
        transform.localScale = startScale;
        PlaySingle(TweenTrack.Scale(transform, startScale, endScale, duration, 0f, Ease.OutQuad), startDelay);
    }

    /// <summary>
    /// Runs a single ad-hoc track. Unlike <see cref="PlayAnimation"/> this does not raise
    /// OnAnimationStart/OnAnimationComplete — the standalone tweens never did.
    /// </summary>
    private void PlaySingle(TweenTrack track, float startDelay)
    {
        if (!isActiveAndEnabled)
            return;

        tracks.Clear();
        tracks.Add(track);

        animationRoutine = StartCoroutine(TweenGroup.Run(
            tracks, startDelay, ignoreTimeScale, 1, LoopType.Restart, ClearRoutine));
    }

    private void ClearRoutine()
    {
        animationRoutine = null;
    }

    /// <summary>Overrides the stored "original" transform values used as tween targets/starts. Useful for runtime-spawned objects whose original transform isn't known at <see cref="Awake"/>. Unspecified parameters are left unchanged.</summary>
    public void SetOriginalTransform(Vector3? position = null, Vector3? localPosition = null, Quaternion? rotation = null, Vector3? scale = null)
    {
        if (position.HasValue) originalPosition = position.Value;
        if (localPosition.HasValue) originalLocalPosition = localPosition.Value;
        if (rotation.HasValue) originalRotation = rotation.Value;
        if (scale.HasValue) originalScale = scale.Value;
    }

    /// <summary>Re-captures the current transform as the "original" that future tweens animate from/to.</summary>
    [ContextMenu("Store Current as Original")]
    public void StoreCurrentAsOriginal()
    {
        StoreOriginalTransform();
    }

    private void OnDestroy()
    {
        StopAnimation();
    }

    private void OnValidate()
    {
        // Ensure durations are positive
        positionDuration = Mathf.Max(0.1f, positionDuration);
        rotationDuration = Mathf.Max(0.1f, rotationDuration);
        scaleDuration = Mathf.Max(0.1f, scaleDuration);
    }
}
}