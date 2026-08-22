using System;
using System.Collections.Generic;
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

    /// <summary>Builds the model-knob configuration tree (env below, CLI
    /// above) and binds it into the typed options record the server passes to
    /// <c>ModelBase.Create</c>.</summary>
    internal static class ModelKnobConfig
    {
        /// <summary>Env vars snapshotted at call time, then CLI flag
        /// overrides on top (a later provider wins in
        /// Microsoft.Extensions.Configuration).</summary>
        public static IConfigurationRoot BuildConfiguration(string[] args)
        {
            return new ConfigurationBuilder()
                .Add(new KnobEnvConfigurationSource())
                .AddInMemoryCollection(CollectCliOverrides(args))
                .Build();
        }

        /// <summary>Registry-driven flag scan: an arg matching a knob's flag
        /// list sets that knob's config key; a <c>--no-</c> prefix means
        /// false. Later flags win, matching the env-writing flag loops.</summary>
        public static Dictionary<string, string> CollectCliOverrides(string[] args)
        {
            var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (args == null)
                return overrides;

            foreach (string arg in args)
            {
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
