namespace MSJTDCC.Tweening
{
/// <summary>
/// Easing curves used by this asset's tween components.
/// </summary>
/// <remarks>
/// <para>
/// The explicit numbering is load-bearing and must not be reordered. Earlier versions of this
/// asset used DOTween, whose <c>Ease</c> enum was serialized into prefabs and scenes as a plain
/// integer (for example <c>easeType: 6</c> in Lift.prefab). Unity matches serialized fields by
/// name and stores enums by value, so keeping these members at DOTween's original indices lets
/// projects built against the old version keep their easing after upgrading, with no migration.
/// </para>
/// <para>
/// Several members exist only to hold that numbering — see <see cref="EaseEvaluator.Evaluate"/>
/// for how <see cref="Flash"/>, <see cref="INTERNAL_Zero"/> and friends are handled.
/// </para>
/// </remarks>
public enum Ease
{
    /// <summary>No ease explicitly chosen; evaluates as <see cref="OutQuad"/>.</summary>
    Unset = 0,
    Linear = 1,
    InSine = 2,
    OutSine = 3,
    InOutSine = 4,
    InQuad = 5,
    OutQuad = 6,
    InOutQuad = 7,
    InCubic = 8,
    OutCubic = 9,
    InOutCubic = 10,
    InQuart = 11,
    OutQuart = 12,
    InOutQuart = 13,
    InQuint = 14,
    OutQuint = 15,
    InOutQuint = 16,
    InExpo = 17,
    OutExpo = 18,
    InOutExpo = 19,
    InCirc = 20,
    OutCirc = 21,
    InOutCirc = 22,
    InElastic = 23,
    OutElastic = 24,
    InOutElastic = 25,
    InBack = 26,
    OutBack = 27,
    InOutBack = 28,
    InBounce = 29,
    OutBounce = 30,
    InOutBounce = 31,

    /// <summary>Retained for index compatibility only; evaluates as <see cref="Linear"/>.</summary>
    Flash = 32,
    /// <summary>Retained for index compatibility only; evaluates as <see cref="Linear"/>.</summary>
    InFlash = 33,
    /// <summary>Retained for index compatibility only; evaluates as <see cref="Linear"/>.</summary>
    OutFlash = 34,
    /// <summary>Retained for index compatibility only; evaluates as <see cref="Linear"/>.</summary>
    InOutFlash = 35,
    /// <summary>Retained for index compatibility only; always evaluates to 0.</summary>
    INTERNAL_Zero = 36,
    /// <summary>Retained for index compatibility only; evaluates as <see cref="Linear"/>.</summary>
    INTERNAL_Custom = 37,
}

/// <summary>How a looping tween behaves when it reaches the end of a cycle.</summary>
/// <remarks>Numbering mirrors the values previously serialized by DOTween — see <see cref="Ease"/>.</remarks>
public enum LoopType
{
    /// <summary>Jump back to the start and play forwards again.</summary>
    Restart = 0,
    /// <summary>Play backwards, then forwards, alternating each cycle.</summary>
    Yoyo = 1,
    /// <summary>Play forwards again from where the previous cycle ended, accumulating the change.</summary>
    Incremental = 2,
}
}
