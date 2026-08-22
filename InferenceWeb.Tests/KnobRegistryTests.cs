using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TensorSharp.Models;
using Xunit;

namespace InferenceWeb.Tests;

/// <summary>
/// Keeps <see cref="KnobRegistry"/> honest: every settable property of the
/// options records has exactly one registry entry of the matching kind, env
/// var names are unique, and the committed knob-reference doc matches the
/// generator output.
/// </summary>
public class KnobRegistryTests
{
    private static IReadOnlyList<PropertyInfo> OptionProperties() =>
        typeof(Qwen35Options)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetSetMethod() != null || p.SetMethod != null)
            .ToList();

    [Fact]
    public void EveryOptionsPropertyHasExactlyOneRegistryEntry()
    {
        var props = OptionProperties();
        Assert.NotEmpty(props);
        foreach (var p in props)
        {
            var matches = KnobRegistry.All.Where(k => k.Property == p.Name).ToList();
            Assert.True(matches.Count == 1,
                $"{p.Name}: expected exactly 1 registry entry, found {matches.Count}.");
        }
    }

    [Fact]
    public void EveryRegistryEntryHasABackingProperty()
    {
        var names = OptionProperties().Select(p => p.Name).ToHashSet();
        foreach (var k in KnobRegistry.All)
            Assert.True(names.Contains(k.Property), $"Registry entry '{k.Property}' has no options property.");
    }

    [Fact]
    public void KindAndScopeMatchThePropertyDeclaration()
    {
        foreach (var p in OptionProperties())
        {
            var k = KnobRegistry.ByProperty(p.Name);
            Assert.NotNull(k);

            Type t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            KnobKind expectedKind = t == typeof(bool) ? KnobKind.Bool : KnobKind.Int;
            Assert.Equal(expectedKind, k.Kind);
            if (k.Kind == KnobKind.Bool)
            {
                Assert.NotNull(k.Dialect);
                Assert.Null(k.IntMin);
            }
            else
            {
                Assert.Null(k.Dialect);
                Assert.NotNull(k.IntMin);
            }

            KnobScope expectedScope = p.DeclaringType == typeof(ModelOptions) ? KnobScope.Model : KnobScope.Qwen35;
            Assert.Equal(expectedScope, k.Scope);
        }
    }

    [Fact]
    public void EnvVarsAndFlagsAreUniqueAcrossTheRegistry()
    {
        var dupEnv = KnobRegistry.All.GroupBy(k => k.EnvVar).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(dupEnv);
        var dupFlags = KnobRegistry.All.SelectMany(k => k.Flags).GroupBy(f => f).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(dupFlags);
        Assert.All(KnobRegistry.All, k => Assert.False(string.IsNullOrWhiteSpace(k.EnvVar)));
        Assert.All(KnobRegistry.All, k => Assert.False(string.IsNullOrWhiteSpace(k.Summary)));
    }

    [Fact]
    public void LookupsResolveByPropertyAndEnvVar()
    {
        var k = KnobRegistry.ByEnvVar("TS_QWEN35_BATCHED");
        Assert.NotNull(k);
        Assert.Equal(nameof(Qwen35Options.Batched), k.Property);
        Assert.Contains("--continuous-batching", k.Flags);
        Assert.Equal("Model:Batched", k.ConfigKey);
        Assert.Same(k, KnobRegistry.ByProperty(nameof(Qwen35Options.Batched)));
    }

    [Fact]
    public void CommittedKnobDocIsInSync()
    {
        // Walk up from the test bin directory to the repo root.
        string dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "TensorSharp.slnx")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);

        string docPath = Path.Combine(dir, "docs", "knobs.md");
        Assert.True(File.Exists(docPath), $"docs/knobs.md missing — write KnobRegistry.ToMarkdown() to {docPath}.");
        string committed = File.ReadAllText(docPath).ReplaceLineEndings("\n");
        string generated = KnobRegistry.ToMarkdown().ReplaceLineEndings("\n");
        Assert.True(committed == generated,
            "docs/knobs.md is stale — regenerate it from KnobRegistry.ToMarkdown().");
    }
}
