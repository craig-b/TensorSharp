using System;
using System.Globalization;

namespace TensorSharp.Models
{
    /// <summary>Normalizes raw knob values (env var or --set input) to the
    /// canonical form the options binder consumes ("true"/"false" or a
    /// decimal integer).
    ///
    /// Bool values accept exactly the canonical dialect tokens
    /// (<see cref="KnobResolver.TryParseBoolToken"/>); the normalized value is
    /// property-sense, so inverted <c>DISABLE_*</c> vars come out negated.
    /// Unrecognized values return false: --set fails fast on them, and the
    /// env config source leaves the property null so <see cref="KnobResolver"/>
    /// warns and applies the default at model construction.</summary>
    public static class KnobValue
    {
        public static bool TryNormalize(KnobDef knob, string raw, out string normalized)
        {
            normalized = null;
            if (knob == null || string.IsNullOrEmpty(raw))
                return false;

            if (knob.Kind == KnobKind.Int)
            {
                if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
                    && v >= knob.IntMin.Value)
                {
                    normalized = v.ToString(CultureInfo.InvariantCulture);
                    return true;
                }
                return false;
            }

            if (!KnobResolver.TryParseBoolToken(raw, out bool token))
                return false;
            bool value = knob.Dialect.Value == BoolDialect.InvertedDisable ? !token : token;
            normalized = value ? "true" : "false";
            return true;
        }
    }
}
