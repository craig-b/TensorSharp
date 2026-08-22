using System;
using System.Linq;
using TensorSharp.Models;
using Xunit;

namespace InferenceWeb.Tests;

/// <summary>
/// Characterization tests for <see cref="KnobResolver"/>: the expected values
/// below are the literal truth tables of the per-site env parses the resolver
/// replaced (taken from the pre-refactor read sites), so a resolver or
/// registry-dialect change that would shift any knob's effective behaviour
/// fails here.
/// </summary>
public class KnobResolverTests : IDisposable
{
    private static readonly string[] TouchedVars =
    {
        "TS_QWEN35_FULL_DECODE",        // LooseZeroOnly
        "TS_GGML_F32_RESIDENT",         // LooseZeroOnly (model scope)
        "TS_QWEN35_BATCHED",            // LooseZeroOrFalse
        "TS_QWEN35_VERIFY_RESIDENT",    // StrictOptIn
        "GDN_DISABLE_CHUNKED_PREFILL",  // InvertedDisableOne
        "TS_DISABLE_FUSED_DENSE_FFN",   // InvertedDisableOneOrTrue
        "TS_MLX_MLOCK_GGUF",            // OnRequiresExactlyOneWhenSet
        "TS_PREFILL_CHUNK",             // int, min 1
        "TS_PREFILL_WARMUP_LEN",        // int, min 2
        "TS_CUDA_PREFILL_GRAPH_MAX_SEQLEN", // int, min 0
        "TS_MLX_EVAL_EVERY_N_LAYERS",   // exempt from central resolution
    };

    private readonly (string Name, string Value)[] _saved =
        Array.ConvertAll(TouchedVars, n => (n, Environment.GetEnvironmentVariable(n)));

    public KnobResolverTests()
    {
        foreach (string n in TouchedVars)
            Environment.SetEnvironmentVariable(n, null);
    }

    public void Dispose()
    {
        foreach ((string name, string value) in _saved)
            Environment.SetEnvironmentVariable(name, value);
    }

    private static Qwen35Options Resolve() =>
        (Qwen35Options)KnobResolver.Resolve(new Qwen35Options());

    // ----- bool dialects: one representative knob per family ---------------

    [Theory] // site was: !string.Equals(env, "0", Ordinal)
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData("false", true)]  // "false" is ON in this family
    [InlineData("False", true)]
    [InlineData("no", true)]
    public void LooseZeroOnly_MatchesLegacySiteParse(string raw, bool expected)
    {
        Environment.SetEnvironmentVariable("TS_QWEN35_FULL_DECODE", raw);
        Environment.SetEnvironmentVariable("TS_GGML_F32_RESIDENT", raw);
        Qwen35Options o = Resolve();
        Assert.Equal(expected, o.FullDecode);
        Assert.Equal(expected, o.GgmlF32Resident);
    }

    [Theory] // site was: raw != "0" && !equals(raw, "false", OrdinalIgnoreCase)
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    public void LooseZeroOrFalse_MatchesLegacySiteParse(string raw, bool expected)
    {
        Environment.SetEnvironmentVariable("TS_QWEN35_BATCHED", raw);
        Assert.Equal(expected, Resolve().Batched);
    }

    [Theory] // site was: string.Equals(env, "1", Ordinal)
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    [InlineData("2", false)]
    public void StrictOptIn_MatchesLegacySiteParse(string raw, bool expected)
    {
        Environment.SetEnvironmentVariable("TS_QWEN35_VERIFY_RESIDENT", raw);
        Assert.Equal(expected, Resolve().VerifyResident);
    }

    [Theory] // site was: disabled iff string.Equals(env, "1", Ordinal); property positive-sense
    [InlineData(null, true)]
    [InlineData("1", false)]
    [InlineData("true", true)]   // word forms do NOT disable this family
    [InlineData("0", true)]
    [InlineData("", true)]
    public void InvertedDisableOne_MatchesLegacySiteParse(string raw, bool expected)
    {
        Environment.SetEnvironmentVariable("GDN_DISABLE_CHUNKED_PREFILL", raw);
        Assert.Equal(expected, Resolve().GdnChunkedPrefill);
    }

    [Theory] // site was: disabled iff env == "1" || equals(env, "true", OrdinalIgnoreCase)
    [InlineData(null, true)]
    [InlineData("1", false)]
    [InlineData("true", false)]
    [InlineData("TRUE", false)]
    [InlineData("0", true)]
    [InlineData("yes", true)]
    public void InvertedDisableOneOrTrue_MatchesLegacySiteParse(string raw, bool expected)
    {
        Environment.SetEnvironmentVariable("TS_DISABLE_FUSED_DENSE_FFN", raw);
        Assert.Equal(expected, Resolve().FusedDenseFfn);
    }

    [Theory] // site was: string.Equals(env ?? "1", "1", Ordinal)
    [InlineData(null, true)]
    [InlineData("1", true)]
    [InlineData("", false)]
    [InlineData("true", false)]
    [InlineData("0", false)]
    public void OnRequiresExactlyOneWhenSet_MatchesLegacySiteParse(string raw, bool expected)
    {
        Environment.SetEnvironmentVariable("TS_MLX_MLOCK_GGUF", raw);
        Assert.Equal(expected, Resolve().MlxMlockGguf);
    }

    // ----- int knobs: fill only on a valid value; sites keep their defaults -

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData("abc", null)]
    [InlineData("0", null)]      // below IntMin 1 → site default decides
    [InlineData("-3", null)]
    [InlineData("768", 768)]
    public void IntKnob_FillsOnlyValidValues(string raw, int? expected)
    {
        Environment.SetEnvironmentVariable("TS_PREFILL_CHUNK", raw);
        Assert.Equal(expected, Resolve().PrefillChunk);
    }

    [Fact]
    public void IntKnob_HonorsPerKnobMinimum()
    {
        Environment.SetEnvironmentVariable("TS_PREFILL_WARMUP_LEN", "1"); // min 2
        Environment.SetEnvironmentVariable("TS_CUDA_PREFILL_GRAPH_MAX_SEQLEN", "0"); // min 0
        Qwen35Options o = Resolve();
        Assert.Null(o.PrefillWarmupLength);
        Assert.Equal(0, o.CudaPrefillGraphMaxSeqLen);
    }

    // ----- resolution mechanics --------------------------------------------

    [Fact]
    public void ExplicitValuesKeepPrecedenceOverEnv()
    {
        Environment.SetEnvironmentVariable("TS_QWEN35_FULL_DECODE", "0");
        var opts = new Qwen35Options { FullDecode = true };
        Assert.True(((Qwen35Options)KnobResolver.Resolve(opts)).FullDecode);
    }

    [Fact]
    public void InputRecordAndSharedDefaultAreNeverMutated()
    {
        Environment.SetEnvironmentVariable("TS_QWEN35_FULL_DECODE", "0");
        var opts = new Qwen35Options();
        KnobResolver.Resolve(opts);
        Assert.Null(opts.FullDecode);
        KnobResolver.Resolve(Qwen35Options.Default);
        Assert.Null(Qwen35Options.Default.FullDecode);
        KnobResolver.Resolve(null); // null → resolves a copy of ModelOptions.Default
        Assert.Null(ModelOptions.Default.GgmlF32Resident);
    }

    [Fact]
    public void PlainModelOptionsResolvesAllModelScopeBools()
    {
        ModelOptions o = KnobResolver.Resolve(new ModelOptions());
        Assert.IsNotType<Qwen35Options>(o);
        foreach (KnobDef k in KnobRegistry.All.Where(k =>
                     k.Scope == KnobScope.Model && k.Kind == KnobKind.Bool && k.EnvResolvedAtCreate))
            Assert.True(typeof(ModelOptions).GetProperty(k.Property).GetValue(o) != null,
                $"{k.Property} not filled");
    }

    [Fact]
    public void EveryResolvableKnobIsFilledOnQwen35Options()
    {
        Qwen35Options o = Resolve();
        foreach (KnobDef k in KnobRegistry.All)
        {
            object v = typeof(Qwen35Options).GetProperty(k.Property).GetValue(o);
            if (k.Kind == KnobKind.Bool && k.EnvResolvedAtCreate)
                Assert.True(v != null, $"{k.Property} not filled");
        }
    }

    [Fact]
    public void ExemptKnobIsNotFilledFromEnv()
    {
        Environment.SetEnvironmentVariable("TS_MLX_EVAL_EVERY_N_LAYERS", "8");
        Assert.Null(Resolve().MlxEvalEveryNLayers);
    }

    [Fact]
    public void ResolutionIsPerCall_EnvChangesArePickedUp()
    {
        Environment.SetEnvironmentVariable("TS_QWEN35_FULL_DECODE", "0");
        Assert.False(Resolve().FullDecode);
        Environment.SetEnvironmentVariable("TS_QWEN35_FULL_DECODE", null);
        Assert.True(Resolve().FullDecode);
    }

    // ----- registry shape: dialect population can't silently drift ---------

    [Fact]
    public void DialectFamilyCountsMatchTheSiteInventory()
    {
        var byDialect = KnobRegistry.All
            .Where(k => k.Kind == KnobKind.Bool)
            .GroupBy(k => k.Dialect.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(28, byDialect[BoolDialect.LooseZeroOnly]);
        Assert.Equal(3, byDialect[BoolDialect.LooseZeroOrFalse]);
        Assert.Equal(14, byDialect[BoolDialect.StrictOptIn]);
        Assert.Equal(2, byDialect[BoolDialect.InvertedDisableOne]);
        Assert.Equal(1, byDialect[BoolDialect.InvertedDisableOneOrTrue]);
        Assert.Equal(1, byDialect[BoolDialect.OnRequiresExactlyOneWhenSet]);
        Assert.Single(KnobRegistry.All.Where(k => !k.EnvResolvedAtCreate));
    }
}
