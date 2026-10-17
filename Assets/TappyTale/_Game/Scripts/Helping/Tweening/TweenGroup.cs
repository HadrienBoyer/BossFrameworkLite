using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MSJTDCC.Tweening
{
/// <summary>
/// One animated property within a <see cref="TweenGroup"/>: how long to wait, how long to run,
/// which curve to follow, and what to do with the eased value.
/// </summary>
public struct TweenTrack
{
    /// <summary>Seconds to wait, after the group starts, before this track begins moving.</summary>
    public float delay;
    /// <summary>Seconds this track takes to travel from its start value to its end value.</summary>
    public float duration;
    /// <summary>Curve applied to this track's normalised progress.</summary>
    public Ease ease;
    /// <summary>
    /// Receives the eased progress and writes the resulting value. Usually 0–1, but may fall
    /// outside that range for overshooting curves or <see cref="LoopType.Incremental"/>, so
    /// implementations must use unclamped interpolation.
    /// </summary>
    public Action<float> apply;

    /// <summary>Seconds from the group's start until this track finishes.</summary>
    public float EndTime => delay + duration;

    public TweenTrack(float delay, float duration, Ease ease, Action<float> apply)
    {
        this.delay = delay;
        this.duration = duration;
        this.ease = ease;
        this.apply = apply;
    }

    /// <summary>Tweens a transform's world or local position between two points.</summary>
    public static TweenTrack Move(Transform target, Vector3 from, Vector3 to, bool local, float duration, float delay, Ease ease)
    {
        if (local)
        {
            return new TweenTrack(delay, duration, ease,
                t => target.localPosition = Vector3.LerpUnclamped(from, to, t));
        }

        return new TweenTrack(delay, duration, ease,
            t => target.position = Vector3.LerpUnclamped(from, to, t));
    }

    /// <summary>
    /// Tweens a transform's world or local rotation between two Euler angles, taking the shortest
    /// way round on each axis.
    /// </summary>
    /// <remarks>
    /// Interpolating the Euler angles rather than slerping the quaternions is deliberate: it
    /// matches the path the asset's animations took under DOTween's default rotate mode, so
    /// existing content (the lift, the switch levers) moves exactly as it did before.
    /// </remarks>
    public static TweenTrack Rotate(Transform target, Vector3 fromEuler, Vector3 toEuler, bool local, float duration, float delay, Ease ease)
    {
        Vector3 delta = new Vector3(
            Mathf.DeltaAngle(fromEuler.x, toEuler.x),
            Mathf.DeltaAngle(fromEuler.y, toEuler.y),
            Mathf.DeltaAngle(fromEuler.z, toEuler.z));

        if (local)
        {
            return new TweenTrack(delay, duration, ease,
                t => target.localRotation = Quaternion.Euler(fromEuler + delta * t));
        }

        return new TweenTrack(delay, duration, ease,
            t => target.rotation = Quaternion.Euler(fromEuler + delta * t));
    }

    /// <summary>Tweens a transform's local scale between two values.</summary>
    public static TweenTrack Scale(Transform target, Vector3 from, Vector3 to, float duration, float delay, Ease ease)
    {
        return new TweenTrack(delay, duration, ease,
            t => target.localScale = Vector3.LerpUnclamped(from, to, t));
    }

    /// <summary>Tweens a single float and hands each value to <paramref name="setter"/>.</summary>
    public static TweenTrack Float(float from, float to, Action<float> setter, float duration, float delay, Ease ease)
    {
        return new TweenTrack(delay, duration, ease,
            t => setter(Mathf.LerpUnclamped(from, to, t)));
    }
}

/// <summary>
/// Runs a set of <see cref="TweenTrack"/>s in parallel from a single coroutine.
/// </summary>
/// <remarks>
/// <para>
/// This replaces the third-party tweening library the asset used to depend on. It is deliberately
/// only as capable as this asset needs: every animation here starts all of its tracks together and
/// offsets them with per-track delays, so there is no chaining, no timeline and no global tween
/// registry to maintain.
/// </para>
/// <para>
/// Because the work happens in a coroutine, it is owned by the calling MonoBehaviour — disabling
/// that component or its GameObject stops the animation where it stands.
/// </para>
/// </remarks>
public static class TweenGroup
{
    /// <summary>
    /// Drives every track to completion, then invokes <paramref name="onComplete"/>.
    /// </summary>
    /// <param name="tracks">The properties to animate. Runs them all in parallel.</param>
    /// <param name="startDelay">Seconds to wait before the group begins.</param>
    /// <param name="ignoreTimeScale">If true, runs on unscaled time so it keeps going while paused.</param>
    /// <param name="loopCount">How many times to play. Negative means forever.</param>
    /// <param name="loopType">What happens at the end of each cycle when looping.</param>
    /// <param name="onComplete">Invoked once every cycle has finished. Never invoked when looping forever.</param>
    public static IEnumerator Run(
        IList<TweenTrack> tracks,
        float startDelay,
        bool ignoreTimeScale,
        int loopCount,
        LoopType loopType,
        Action onComplete)
    {
        if (tracks == null || tracks.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        if (startDelay > 0f)
        {
            if (ignoreTimeScale)
                yield return new WaitForSecondsRealtime(startDelay);
            else
                yield return new WaitForSeconds(startDelay);
        }

        float groupDuration = 0f;
        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].EndTime > groupDuration)
                groupDuration = tracks[i].EndTime;
        }

        // Nothing to animate over time — land on the final value and finish, rather than spinning
        // a zero-length loop forever.
        if (groupDuration <= 0f)
        {
            ApplyAll(tracks, 0f, 0, loopType);
            onComplete?.Invoke();
            yield break;
        }

        bool loopsForever = loopCount < 0;
        int totalCycles = Mathf.Max(1, loopCount);

        for (int cycle = 0; loopsForever || cycle < totalCycles; cycle++)
        {
            float elapsed = 0f;

            while (elapsed < groupDuration)
            {
                ApplyAll(tracks, elapsed, cycle, loopType);
                yield return null;
                elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            }

            // Land exactly on the end of the cycle; the last frame's delta almost never does.
            ApplyAll(tracks, groupDuration, cycle, loopType);
        }

        onComplete?.Invoke();
    }

    /// <summary>Convenience overload for a one-shot, time-scaled group.</summary>
    public static IEnumerator Run(IList<TweenTrack> tracks, float startDelay, Action onComplete)
    {
        return Run(tracks, startDelay, false, 1, LoopType.Restart, onComplete);
    }

    private static void ApplyAll(IList<TweenTrack> tracks, float elapsed, int cycle, LoopType loopType)
    {
        // Yoyo plays odd cycles backwards, which means easing the reversed progress rather than
        // reversing the eased result — the two differ for every non-symmetric curve.
        bool reversed = loopType == LoopType.Yoyo && (cycle & 1) == 1;

        for (int i = 0; i < tracks.Count; i++)
        {
            TweenTrack track = tracks[i];
            if (track.apply == null)
                continue;

            float progress;
            if (track.duration <= 0f)
                progress = elapsed >= track.delay ? 1f : 0f;
            else
                progress = Mathf.Clamp01((elapsed - track.delay) / track.duration);

            if (reversed)
                progress = 1f - progress;

            float eased = EaseEvaluator.Evaluate(track.ease, progress);

            // Incremental keeps the change from previous cycles and adds this cycle's on top,
            // so the caller's unclamped lerp carries the value past its nominal end.
            if (loopType == LoopType.Incremental)
                eased += cycle;

            track.apply(eased);
        }
    }
}
}
