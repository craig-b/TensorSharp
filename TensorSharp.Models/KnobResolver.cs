using System;
using System.Reflection;

namespace TensorSharp.Models
{
    /// <summary>Resolves the unset knobs of an options record from their
    /// environment variables, once, at model construction. This replaces the
    /// per-site <c>_opts.X ?? envRead()</c> fallbacks: after resolution every
    /// bool knob is non-null and int knobs are non-null whenever their env var
    /// held a valid value (sites keep their non-env defaults behind
    /// <c>??</c>). The parse per knob is the registry dialect, byte-for-byte
    /// the accepted-token set of the read site it replaced.
    ///
    /// Properties already set (by a host, a preset, or the config layer) are
    /// never touched, so explicit values keep precedence over env. The input
    /// record is cloned, never mutated — passing the shared
    /// <see cref="ModelOptions.Default"/> is safe.</summary>
    public static class KnobResolver
    {
        /// <summary>Returns a copy of <paramref name="options"/> with unset
        /// knobs filled from the environment. Model-scope knobs are resolved
        /// always; Qwen35-scope knobs only when the instance is a
        /// <see cref="Qwen35Options"/>.</summary>
        public static ModelOptions Resolve(ModelOptions options)
        {
            options ??= ModelOptions.Default;
            ModelOptions clone = options is Qwen35Options q ? q with { } : options with { };
            Type type = clone.GetType();
            foreach (KnobDef knob in KnobRegistry.All)
            {
                if (!knob.EnvResolvedAtCreate)
                    continue;
                if (knob.Scope == KnobScope.Qwen35 && clone is not Qwen35Options)
                    continue;
                PropertyInfo prop = type.GetProperty(knob.Property);
                if (prop.GetValue(clone) != null)
                    continue;
                string raw = Environment.GetEnvironmentVariable(knob.EnvVar);
                if (knob.Kind == KnobKind.Bool)
                    prop.SetValue(clone, ParseBool(knob.Dialect.Value, raw));
                else if (raw != null && int.TryParse(raw, out int v) && v >= knob.IntMin.Value)
                    prop.SetValue(clone, v);
            }
            return clone;
        }

        static bool ParseBool(BoolDialect dialect, string raw) => dialect switch
        {
            BoolDialect.LooseZeroOnly =>
                !string.Equals(raw, "0", StringComparison.Ordinal),
            BoolDialect.LooseZeroOrFalse =>
                !string.Equals(raw, "0", StringComparison.Ordinal)
                && !string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase),
            BoolDialect.StrictOptIn =>
                string.Equals(raw, "1", StringComparison.Ordinal),
            BoolDialect.InvertedDisableOne =>
                !string.Equals(raw, "1", StringComparison.Ordinal),
            BoolDialect.InvertedDisableOneOrTrue =>
                !(string.Equals(raw, "1", StringComparison.Ordinal)
                  || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)),
            BoolDialect.OnRequiresExactlyOneWhenSet =>
                string.Equals(raw ?? "1", "1", StringComparison.Ordinal),
            _ => throw new InvalidOperationException($"Unhandled dialect {dialect}"),
        };
    }
}
