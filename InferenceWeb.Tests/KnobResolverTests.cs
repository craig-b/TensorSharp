using System;
using System.Linq;
using TensorSharp.Models;
using Xunit;

namespace InferenceWeb.Tests;

/// <summary>
/// Tests for <see cref="KnobResolver"/>: the canonical bool dialect's literal
/// truth tables (one theory per family), the int fill rules, and the
/// resolution mechanics (precedence, cloning, scope, the exempt knob).
/// </summary>
public class KnobResolverTests : IDisposable
{
    private static readonly string[] TouchedVars =
    {
        "TS_QWEN35_FULL_DECODE",        // DefaultOn
        "TS_GGML_F32_RESIDENT",         // DefaultOn (model scope)
        "TS_QWEN35_BATCHED",            // DefaultOn
        "TS_QWEN35_VERIFY_RESIDENT",    // DefaultOff
        "GDN_DISABLE_CHUNKED_PREFILL",  // InvertedDisable
        "TS_DISABLE_FUSED_DENSE_FFN",   // InvertedDisable
        "TS_MLX_MLOCK_GGUF",            // DefaultOn
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

    // ----- canonical bool dialect ------------------------------------------

    [Theory] // canonical tokens, i-case; unset/empty/unrecognized -> default (on)
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("no", false)]
    [InlineData("off", false)]
    [InlineData("yes", true)]
    [InlineData("ON", true)]
    [InlineData("banana", true)]  // unrecognized -> warn once + default
    public void DefaultOn_UsesCanonicalTokens(string raw, bool expected)
    {
        Environment.SetEnvironmentVariable("TS_QWEN35_FULL_DECODE", raw);
        Environment.SetEnvironmentVariable("TS_GGML_F32_RESIDENT", raw);
        Environment.SetEnvironmentVariable("TS_QWEN35_BATCHED", raw);
        Environment.SetEnvironmentVariable("TS_MLX_MLOCK_GGUF", raw);
        Qwen35Options o = Resolve();
        Assert.Equal(expected, o.FullDecode);
        Assert.Equal(expected, o.GgmlF32Resident);
        Assert.Equal(expected, o.Batched);
        Assert.Equal(expected, o.MlxMlockGguf);
    }

    [Theory] // opt-in knob: unset/empty/unrecognized -> default (off)
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("yes", true)]
    [InlineData("0", false)]
    [InlineData("2", false)]      // unrecognized -> warn once + default
    public void DefaultOff_UsesCanonicalTokens(string raw, bool expected)
    {
        Environment.SetEnvironmentVariable("TS_QWEN35_VERIFY_RESIDENT", raw);
        Assert.Equal(expected, Resolve().VerifyResident);
    }

    [Theory] // DISABLE_* var: a true token disables; property positive-sense
    [InlineData(null, true)]
    [InlineData("1", false)]
    [InlineData("true", false)]
    [InlineData("YES", false)]
    [InlineData("0", true)]
    [InlineData("off", true)]
    [InlineData("banana", true)]  // unrecognized -> warn once + default (on)
    public void InvertedDisable_UsesCanonicalTokens(string raw, bool expected)
    {
        Environment.SetEnvironmentVariable("GDN_DISABLE_CHUNKED_PREFILL", raw);
        Environment.SetEnvironmentVariable("TS_DISABLE_FUSED_DENSE_FFN", raw);
        Qwen35Options o = Resolve();
        Assert.Equal(expected, o.GdnChunkedPrefill);
        Assert.Equal(expected, o.FusedDenseFfn);
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
        Assert.Equal(32, byDialect[BoolDialect.DefaultOn]);
        Assert.Equal(14, byDialect[BoolDialect.DefaultOff]);
        Assert.Equal(3, byDialect[BoolDialect.InvertedDisable]);
        Assert.Single(KnobRegistry.All.Where(k => !k.EnvResolvedAtCreate));
    }
}
