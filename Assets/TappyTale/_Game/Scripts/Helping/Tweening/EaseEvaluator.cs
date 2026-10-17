using UnityEngine;

namespace MSJTDCC.Tweening
{
/// <summary>
/// Turns a normalised time value into an eased value, for every curve in <see cref="Ease"/>.
/// </summary>
/// <remarks>
/// Pure maths with no Unity object dependencies, so it can be reasoned about and tested on its
/// own. The curves are the standard Penner equations using the same default constants the asset
/// previously got from DOTween, so existing animations keep their original feel.
/// </remarks>
public static class EaseEvaluator
{
    // Overshoot constants for the Back curves. c1 is Penner's canonical value; c2 and c3 are the
    // derived constants used by the Out and InOut variants.
    private const float BackC1 = 1.70158f;
    private const float BackC2 = BackC1 * 1.525f;
    private const float BackC3 = BackC1 + 1f;

    // Elastic amplitude. Note this is *not* the textbook value of 1 — the library this asset
    // previously used defaulted its elastic curves to the same constant as the Back curves, which
    // makes them noticeably springier. Matching it keeps existing animations identical.
    private const float ElasticAmplitude = BackC1;
    // Period over a normalised duration. The InOut variant runs at half speed and uses a
    // correspondingly longer period.
    private const float ElasticPeriod = 0.3f;
    private const float ElasticPeriodInOut = ElasticPeriod * 1.5f;

    // Phase shift, derived from amplitude and period, that keeps the curve starting at 0 and
    // ending at 1.
    private static readonly float ElasticShift =
        ElasticPeriod / (2f * Mathf.PI) * Mathf.Asin(1f / ElasticAmplitude);
    private static readonly float ElasticShiftInOut =
        ElasticPeriodInOut / (2f * Mathf.PI) * Mathf.Asin(1f / ElasticAmplitude);

    // Bounce curve segment constants.
    private const float BounceN1 = 7.5625f;
    private const float BounceD1 = 2.75f;

    /// <summary>
    /// Evaluates <paramref name="ease"/> at normalised time <paramref name="t"/>.
    /// </summary>
    /// <param name="ease">The curve to evaluate.</param>
    /// <param name="t">Normalised time, expected in the 0–1 range.</param>
    /// <returns>
    /// The eased value. Usually 0–1, but the Back and Elastic curves deliberately overshoot
    /// outside that range — callers must interpolate with an unclamped lerp to preserve it.
    /// </returns>
    public static float Evaluate(Ease ease, float t)
    {
        switch (ease)
        {
            // An unset ease means "whatever the default is", which was OutQuad.
            case Ease.Unset:
            case Ease.OutQuad:
                return 1f - (1f - t) * (1f - t);

            case Ease.Linear:
                return t;

            case Ease.InSine:
                return 1f - Mathf.Cos((t * Mathf.PI) / 2f);
            case Ease.OutSine:
                return Mathf.Sin((t * Mathf.PI) / 2f);
            case Ease.InOutSine:
                return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;

            case Ease.InQuad:
                return t * t;
            case Ease.InOutQuad:
                return t < 0.5f
                    ? 2f * t * t
                    : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            case Ease.InCubic:
                return t * t * t;
            case Ease.OutCubic:
                return 1f - Mathf.Pow(1f - t, 3f);
            case Ease.InOutCubic:
                return t < 0.5f
                    ? 4f * t * t * t
                    : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

            case Ease.InQuart:
                return t * t * t * t;
            case Ease.OutQuart:
                return 1f - Mathf.Pow(1f - t, 4f);
            case Ease.InOutQuart:
                return t < 0.5f
                    ? 8f * t * t * t * t
                    : 1f - Mathf.Pow(-2f * t + 2f, 4f) / 2f;

            case Ease.InQuint:
                return t * t * t * t * t;
            case Ease.OutQuint:
                return 1f - Mathf.Pow(1f - t, 5f);
            case Ease.InOutQuint:
                return t < 0.5f
                    ? 16f * t * t * t * t * t
                    : 1f - Mathf.Pow(-2f * t + 2f, 5f) / 2f;

            case Ease.InExpo:
                return Mathf.Approximately(t, 0f) ? 0f : Mathf.Pow(2f, 10f * t - 10f);
            case Ease.OutExpo:
                return Mathf.Approximately(t, 1f) ? 1f : 1f - Mathf.Pow(2f, -10f * t);
            case Ease.InOutExpo:
                if (Mathf.Approximately(t, 0f)) return 0f;
                if (Mathf.Approximately(t, 1f)) return 1f;
                return t < 0.5f
                    ? Mathf.Pow(2f, 20f * t - 10f) / 2f
                    : (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f;

            case Ease.InCirc:
                return 1f - Mathf.Sqrt(1f - Mathf.Pow(t, 2f));
            case Ease.OutCirc:
                return Mathf.Sqrt(1f - Mathf.Pow(t - 1f, 2f));
            case Ease.InOutCirc:
                return t < 0.5f
                    ? (1f - Mathf.Sqrt(1f - Mathf.Pow(2f * t, 2f))) / 2f
                    : (Mathf.Sqrt(1f - Mathf.Pow(-2f * t + 2f, 2f)) + 1f) / 2f;

            case Ease.InElastic:
            {
                if (t == 0f) return 0f;
                if (t == 1f) return 1f;
                float x = t - 1f;
                return -(ElasticAmplitude * Mathf.Pow(2f, 10f * x)
                         * Mathf.Sin((x - ElasticShift) * (2f * Mathf.PI) / ElasticPeriod));
            }
            case Ease.OutElastic:
            {
                if (t == 0f) return 0f;
                if (t == 1f) return 1f;
                return ElasticAmplitude * Mathf.Pow(2f, -10f * t)
                       * Mathf.Sin((t - ElasticShift) * (2f * Mathf.PI) / ElasticPeriod) + 1f;
            }
            case Ease.InOutElastic:
            {
                // Guard t=0 explicitly: the half-speed remap below would otherwise leave a small
                // non-zero residue at the very start of the curve.
                if (t == 0f) return 0f;

                float x = t * 2f;
                if (x == 2f) return 1f;

                if (x < 1f)
                {
                    x -= 1f;
                    return -0.5f * (ElasticAmplitude * Mathf.Pow(2f, 10f * x)
                                    * Mathf.Sin((x - ElasticShiftInOut) * (2f * Mathf.PI) / ElasticPeriodInOut));
                }

                x -= 1f;
                return ElasticAmplitude * Mathf.Pow(2f, -10f * x)
                       * Mathf.Sin((x - ElasticShiftInOut) * (2f * Mathf.PI) / ElasticPeriodInOut) * 0.5f + 1f;
            }

            case Ease.InBack:
                return BackC3 * t * t * t - BackC1 * t * t;
            case Ease.OutBack:
                return 1f + BackC3 * Mathf.Pow(t - 1f, 3f) + BackC1 * Mathf.Pow(t - 1f, 2f);
            case Ease.InOutBack:
                return t < 0.5f
                    ? (Mathf.Pow(2f * t, 2f) * ((BackC2 + 1f) * 2f * t - BackC2)) / 2f
                    : (Mathf.Pow(2f * t - 2f, 2f) * ((BackC2 + 1f) * (t * 2f - 2f) + BackC2) + 2f) / 2f;

            case Ease.InBounce:
                return 1f - OutBounce(1f - t);
            case Ease.OutBounce:
                return OutBounce(t);
            case Ease.InOutBounce:
                return t < 0.5f
                    ? (1f - OutBounce(1f - 2f * t)) / 2f
                    : (1f + OutBounce(2f * t - 1f)) / 2f;

            // Held at zero by definition.
            case Ease.INTERNAL_Zero:
                return 0f;

            // The Flash family and INTERNAL_Custom need parameters that were supplied outside the
            // enum (flash count, or a custom curve). Nothing in this asset uses them; they exist
            // so the surrounding members keep their serialized indices. Linear is the neutral
            // fallback if someone picks one from the dropdown.
            case Ease.Flash:
            case Ease.InFlash:
            case Ease.OutFlash:
            case Ease.InOutFlash:
            case Ease.INTERNAL_Custom:
            default:
                return t;
        }
    }

    private static float OutBounce(float t)
    {
        if (t < 1f / BounceD1)
            return BounceN1 * t * t;

        if (t < 2f / BounceD1)
        {
            t -= 1.5f / BounceD1;
            return BounceN1 * t * t + 0.75f;
        }

        if (t < 2.5f / BounceD1)
        {
            t -= 2.25f / BounceD1;
            return BounceN1 * t * t + 0.9375f;
        }

        t -= 2.625f / BounceD1;
        return BounceN1 * t * t + 0.984375f;
    }
}
}
