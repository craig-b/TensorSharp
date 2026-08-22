using System;
using System.Globalization;

namespace TensorSharp.Models
{
    /// <summary>Normalizes raw knob values (env var or --set input) to the
    /// canonical form the options binder consumes ("true"/"false" or a
    /// decimal integer).
    ///
    /// Read sites within one <see cref="BoolDialect"/> family differ slightly
    /// in which tokens they accept (e.g. one loose site treats only "0" as
    /// off, another also "false"), so normalization is deliberately partial:
    /// a value is mapped only when every site in the family agrees on it, and
    /// anything ambiguous returns false so the caller leaves the options
    /// property null and the site's own env read decides — byte-identical by
    /// construction.</summary>
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

            switch (knob.Dialect.Value)
            {
                case BoolDialect.LooseZeroOnly:
                case BoolDialect.LooseZeroOrFalse:
                    // Both loose families: "0" → off; anything not in
                    // {"0","false"} → on. "false" is off in one family only →
                    // ambiguous here; KnobResolver applies the exact dialect.
                    if (raw == "0") { normalized = "false"; return true; }
                    if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)) return false;
                    normalized = "true";
                    return true;

                case BoolDialect.StrictOptIn:
                    // All sites: "1" → on, "0" → off. Word forms vary per site.
                    if (raw == "1") { normalized = "true"; return true; }
                    if (raw == "0") { normalized = "false"; return true; }
                    return false;

                case BoolDialect.InvertedDisableOne:
                case BoolDialect.InvertedDisableOneOrTrue:
                    // DISABLE_* var: "1" disables everywhere, "0" disables
                    // nowhere. "true" disables in one family only → ambiguous.
                    if (raw == "1") { normalized = "false"; return true; }
                    if (raw == "0") { normalized = "true"; return true; }
                    return false;

                case BoolDialect.OnRequiresExactlyOneWhenSet:
                    // Set → on only when exactly "1".
                    normalized = raw == "1" ? "true" : "false";
                    return true;

                default:
                    return false;
            }
        }
    }
}
