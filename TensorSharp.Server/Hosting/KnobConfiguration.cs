using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using TensorSharp.Models;

namespace TensorSharp.Server.Hosting
{
    /// <summary>Feeds the <see cref="KnobRegistry"/> env vars into the
    /// configuration tree under their canonical config keys, normalized per
    /// dialect via <see cref="KnobValue"/>. Values the normalizer cannot map
    /// unambiguously are omitted, leaving the bound options property null so
    /// the knob's own env read decides at the site.</summary>
    internal sealed class KnobEnvConfigurationSource : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) => new KnobEnvConfigurationProvider();

        private sealed class KnobEnvConfigurationProvider : ConfigurationProvider
        {
            public override void Load()
            {
                foreach (KnobDef knob in KnobRegistry.All)
                {
                    string raw = Environment.GetEnvironmentVariable(knob.EnvVar);
                    if (KnobValue.TryNormalize(knob, raw, out string normalized))
                        Data[knob.ConfigKey] = normalized;
                }
            }
        }
    }

    /// <summary>Builds the model-knob configuration tree and binds it into
    /// the typed options record the server passes to
    /// <c>ModelBase.Create</c>. Precedence, lowest to highest: env vars,
    /// per-model preset (from a <c>--config</c> file's <c>"presets"</c>
    /// object), CLI flags / <c>--set</c>.</summary>
    internal static class ModelKnobConfig
    {
        /// <summary>Global tree: env snapshot below, CLI overrides above.</summary>
        public static IConfigurationRoot BuildConfiguration(string[] args)
            => BuildConfiguration(args, configPaths: null, modelFileName: null);

        /// <summary>Per-model tree: env, then any matching
        /// <c>presets.&lt;modelFileName&gt;</c> block from the config files,
        /// then CLI overrides (a later provider wins).</summary>
        public static IConfigurationRoot BuildConfiguration(string[] args, IReadOnlyList<string> configPaths, string modelFileName)
        {
            var builder = new ConfigurationBuilder()
                .Add(new KnobEnvConfigurationSource());
            if (configPaths != null && modelFileName != null)
            {
                foreach (string path in configPaths)
                    builder.AddInMemoryCollection(ReadPreset(path, modelFileName));
            }
            return builder
                .AddInMemoryCollection(CollectCliOverrides(args))
                .Build();
        }

        /// <summary>Registry-driven flag scan. An arg matching a knob's flag
        /// list sets that knob's config key (a <c>--no-</c> prefix means
        /// false); <c>--set NAME=VALUE</c> sets any registry knob by its env
        /// var name. Later args win, matching the env-writing flag loops.</summary>
        public static Dictionary<string, string> CollectCliOverrides(string[] args)
        {
            var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (args == null)
                return overrides;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, "--set", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--set needs an argument of the form NAME=VALUE (e.g. --set TS_PREFILL_CHUNK=512).");
                    ApplySet(args[++i], overrides);
                    continue;
                }

                foreach (KnobDef knob in KnobRegistry.All)
                {
                    foreach (string flag in knob.Flags)
                    {
                        if (!string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (knob.Kind == KnobKind.Bool)
                            overrides[knob.ConfigKey] = flag.StartsWith("--no-", StringComparison.OrdinalIgnoreCase) ? "false" : "true";
                    }
                }
            }
            return overrides;
        }

        private static void ApplySet(string assignment, Dictionary<string, string> overrides)
        {
            int eq = assignment.IndexOf('=');
            if (eq <= 0 || eq == assignment.Length - 1)
                throw new ArgumentException($"Invalid --set '{assignment}': expected NAME=VALUE.");

            string name = assignment.Substring(0, eq).Trim();
            string value = assignment.Substring(eq + 1).Trim();
            KnobDef knob = KnobRegistry.ByEnvVar(name);
            if (knob == null)
                throw new ArgumentException($"Unknown knob in --set: '{name}'. See docs/knobs.md for the full list.");
            if (!KnobValue.TryNormalize(knob, value, out string normalized))
                throw new ArgumentException($"Invalid value for --set {name}: '{value}'. Expected {ExpectedInput(knob)}.");
            overrides[knob.ConfigKey] = normalized;
        }

        /// <summary>Human description of the values <see cref="KnobValue"/>
        /// accepts for a knob, for fail-fast messages.</summary>
        internal static string ExpectedInput(KnobDef knob)
        {
            if (knob.Kind == KnobKind.Int)
                return $"an integer >= {knob.IntMin}";
            return knob.Dialect switch
            {
                BoolDialect.InvertedDisableOne => "1 (disable) or 0 (keep enabled) — inverted opt-out variable",
                BoolDialect.InvertedDisableOneOrTrue => "1 (disable) or 0 (keep enabled) — inverted opt-out variable",
                _ => "1 or 0",
            };
        }

        /// <summary>Reads <c>presets.&lt;modelFileName&gt;</c> from a
        /// <c>--config</c> JSON file, remapped onto the knobs' config keys.
        /// Preset keys are options-record property names; an unknown key or an
        /// out-of-range value is a config error, not a silent no-op — a preset
        /// is explicit operator intent, unlike an ambient env var.</summary>
        private static Dictionary<string, string> ReadPreset(string configPath, string modelFileName)
        {
            var remapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(configPath))
                return remapped;

            IConfigurationSection preset = LoadJson(configPath).GetSection("presets").GetSection(modelFileName);
            foreach (IConfigurationSection entry in preset.GetChildren())
                AddPresetEntry(remapped, entry, modelFileName, configPath);
            return remapped;
        }

        /// <summary>Startup validation: walks EVERY preset block in the given
        /// config files so a broken preset fails the server at startup rather
        /// than at whichever later model load would first read it.</summary>
        public static void ValidatePresets(IReadOnlyList<string> configPaths)
        {
            if (configPaths == null)
                return;
            foreach (string path in configPaths)
            {
                if (!File.Exists(path))
                    continue;
                foreach (IConfigurationSection model in LoadJson(path).GetSection("presets").GetChildren())
                {
                    var sink = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (IConfigurationSection entry in model.GetChildren())
                        AddPresetEntry(sink, entry, model.Key, path);
                }
            }
        }

        private static IConfigurationRoot LoadJson(string configPath)
            => new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(configPath), optional: false).Build();

        private static void AddPresetEntry(
            Dictionary<string, string> remapped, IConfigurationSection entry, string modelFileName, string configPath)
        {
            KnobDef knob = KnobRegistry.ByProperty(entry.Key);
            if (knob == null)
            {
                throw new ArgumentException(
                    $"Unknown knob '{entry.Key}' in preset '{modelFileName}' of {configPath}. "
                    + "Preset keys are options property names — see docs/knobs.md.");
            }
            if (entry.Value == null)
                return;

            string value = entry.Value;
            string normalized;
            if (knob.Kind == KnobKind.Bool)
            {
                // JSON true/false arrive as "True"/"False" from the provider;
                // accept 1/0 too for symmetry with --set.
                if (bool.TryParse(value, out bool b))
                    normalized = b ? "true" : "false";
                else if (value == "1" || value == "0")
                    normalized = value == "1" ? "true" : "false";
                else
                    normalized = null;
            }
            else
            {
                normalized = KnobValue.TryNormalize(knob, value, out string n) ? n : null;
            }

            if (normalized == null)
            {
                throw new ArgumentException(
                    $"Invalid value for knob '{entry.Key}' in preset '{modelFileName}' of {configPath}: "
                    + $"'{value}'. Expected {(knob.Kind == KnobKind.Bool ? "true/false or 1/0" : ExpectedInput(knob))}.");
            }
            remapped[knob.ConfigKey] = normalized;
        }

        /// <summary>Binds as <see cref="Qwen35Options"/> so the Qwen-specific
        /// gates are reachable; other architectures see the
        /// <see cref="ModelOptions"/> base. Keys the config tree does not
        /// carry stay null (pure env-var behaviour at the read site).</summary>
        public static ModelOptions Bind(IConfiguration configuration)
        {
            return configuration.GetSection("Model").Get<Qwen35Options>() ?? new Qwen35Options();
        }
    }
}
