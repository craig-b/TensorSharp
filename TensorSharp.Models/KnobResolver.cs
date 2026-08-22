using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace TensorSharp.Models
{
    /// <summary>Resolves the unset knobs of an options record from their
    /// environment variables, once, at model construction. This replaces the
    /// per-site <c>_opts.X ?? envRead()</c> fallbacks: after resolution every
    /// bool knob is non-null and int knobs are non-null whenever their env var
    /// held a valid value (sites keep their non-env defaults behind
    /// <c>??</c>).
    ///
    /// Bool values use the canonical dialect (see <see cref="BoolDialect"/>):
    /// <c>1</c>/<c>true</c>/<c>yes</c>/<c>on</c> and <c>0</c>/<c>false</c>/
    /// <c>no</c>/<c>off</c>, case-insensitive. Unset or empty → the knob's
    /// default; an unrecognized value warns once per knob on stderr and uses
    /// the default.
    ///
    /// Properties already set (by a host, a preset, or the config layer) are
    /// never touched, so explicit values keep precedence over env. The input
    /// record is cloned, never mutated — passing the shared
    /// <see cref="ModelOptions.Default"/> is safe.</summary>
    public static class KnobResolver
    {
        private static readonly ConcurrentDictionary<string, bool> _warned = new();

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
                    prop.SetValue(clone, ResolveBool(knob, raw));
                else if (raw != null && int.TryParse(raw, out int v) && v >= knob.IntMin.Value)
                    prop.SetValue(clone, v);
            }
            return clone;
        }

        /// <summary>The canonical bool tokens. Exposed so the config layer
        /// (<see cref="KnobValue"/>) accepts exactly the same values.</summary>
        public static bool TryParseBoolToken(string raw, out bool value)
        {
            switch (raw?.ToLowerInvariant())
            {
                case "1" or "true" or "yes" or "on": value = true; return true;
                case "0" or "false" or "no" or "off": value = false; return true;
                default: value = false; return false;
            }
        }

        static bool ResolveBool(KnobDef knob, string raw)
        {
            bool defaultValue = knob.Dialect.Value != BoolDialect.DefaultOff;
            if (string.IsNullOrEmpty(raw))
                return defaultValue;
            if (!TryParseBoolToken(raw, out bool token))
            {
                if (_warned.TryAdd(knob.EnvVar, true))
                    Console.Error.WriteLine(
                        $"[knobs] {knob.EnvVar}='{raw}' is not a recognized boolean " +
                        $"(use 1 or 0); using the default ({(defaultValue ? "on" : "off")}).");
                return defaultValue;
            }
            return knob.Dialect.Value == BoolDialect.InvertedDisable ? !token : token;
        }
    }
}
