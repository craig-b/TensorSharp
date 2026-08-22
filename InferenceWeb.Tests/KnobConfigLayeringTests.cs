using System;
using TensorSharp.Models;
using TensorSharp.Server.Hosting;
using Xunit;

namespace InferenceWeb.Tests;

/// <summary>
/// The byte-compatibility contract of the knob config layer: a bound options
/// property is either null (the read site's own env fallback decides — the
/// pre-config behaviour by construction) or exactly the value every read site
/// in the knob's dialect family would derive from the same env value. Plus
/// precedence: CLI flags beat env vars.
/// </summary>
public class KnobConfigLayeringTests : IDisposable
{
    private static readonly string[] TouchedVars =
    {
        "TS_QWEN35_BATCHED",            // LooseZeroOrFalse (with flags)
        "TS_GGML_F32_RESIDENT",         // LooseZeroOnly
        "TS_QWEN35_VERIFY_RESIDENT",    // StrictOptIn
        "TS_DISABLE_FUSED_DENSE_FFN",   // InvertedDisableOneOrTrue
        "TS_MLX_MLOCK_GGUF",            // OnRequiresExactlyOneWhenSet
        "TS_PREFILL_CHUNK",             // Int, min 1
        "TS_MLX_EVAL_EVERY_N_LAYERS",   // Int, min 0
    };

    private readonly EnvVarScope _env = new(TouchedVars);

    public void Dispose() => _env.Dispose();

    private sealed class EnvVarScope : IDisposable
    {
        private readonly (string Name, string Value)[] _saved;

        public EnvVarScope(string[] names)
        {
            _saved = Array.ConvertAll(names, n => (n, Environment.GetEnvironmentVariable(n)));
            foreach (string n in names)
                Environment.SetEnvironmentVariable(n, null);
        }

        public void Dispose()
        {
            foreach ((string name, string value) in _saved)
                Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static Qwen35Options Bind(params string[] args) =>
        (Qwen35Options)ServerOptionsBuilder.BuildModelOptions(args ?? Array.Empty<string>());

    [Theory]
    // Canonical bool tokens map on any dialect; unset or unrecognized values
    // stay null here (KnobResolver applies the default at model construction).
    [InlineData(null, null)]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("banana", null)]
    public void DefaultOn_NormalizesCanonicalTokens(string raw, bool? expected)
    {
        Environment.SetEnvironmentVariable("TS_QWEN35_BATCHED", raw);
        Assert.Equal(expected, Bind().Batched);
        Environment.SetEnvironmentVariable("TS_GGML_F32_RESIDENT", raw);
        Assert.Equal(expected, Bind().GgmlF32Resident);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", true)]
    [InlineData("yes", true)]
    [InlineData("2", null)]
    public void DefaultOff_NormalizesCanonicalTokens(string raw, bool? expected)
    {
        Environment.SetEnvironmentVariable("TS_QWEN35_VERIFY_RESIDENT", raw);
        Assert.Equal(expected, Bind().VerifyResident);
    }

    [Theory]
    // InvertedDisable ("DISABLE_*" var, positive-sense property): a true
    // token disables, so the normalized value comes out negated.
    [InlineData(null, null)]
    [InlineData("1", false)]
    [InlineData("0", true)]
    [InlineData("true", false)]
    [InlineData("off", true)]
    public void InvertedDisable_NormalizesCanonicalTokensNegated(string raw, bool? expected)
    {
        Environment.SetEnvironmentVariable("TS_DISABLE_FUSED_DENSE_FFN", raw);
        Assert.Equal(expected, Bind().FusedDenseFfn);
    }

    [Theory]
    // Int: valid values at or above the knob's minimum map; anything else is
    // deferred to the site (which ignores it and uses its default).
    [InlineData("TS_PREFILL_CHUNK", "512", 512)]
    [InlineData("TS_PREFILL_CHUNK", "1", 1)]
    [InlineData("TS_PREFILL_CHUNK", "0", null)]
    [InlineData("TS_PREFILL_CHUNK", "abc", null)]
    [InlineData("TS_MLX_EVAL_EVERY_N_LAYERS", "0", 0)]
    [InlineData("TS_MLX_EVAL_EVERY_N_LAYERS", "-1", null)]
    public void Int_MapsOnlyValidValues(string envVar, string raw, int? expected)
    {
        Environment.SetEnvironmentVariable(envVar, raw);
        var bound = Bind();
        int? actual = envVar == "TS_PREFILL_CHUNK" ? bound.PrefillChunk : bound.MlxEvalEveryNLayers;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CliFlagBeatsEnvVar()
    {
        Environment.SetEnvironmentVariable("TS_QWEN35_BATCHED", "1");
        Assert.False(Bind("--no-continuous-batching").Batched);
        Environment.SetEnvironmentVariable("TS_QWEN35_BATCHED", "0");
        Assert.True(Bind("--paged-batching").Batched);
    }

    [Fact]
    public void EnvAloneBindsWithoutFlags()
    {
        Environment.SetEnvironmentVariable("TS_QWEN35_BATCHED", "0");
        Assert.False(Bind().Batched);
    }

    [Fact]
    public void UnrelatedPropertiesStayNull()
    {
        Environment.SetEnvironmentVariable("TS_QWEN35_BATCHED", "1");
        var bound = Bind();
        Assert.Null(bound.FullDecode);
        Assert.Null(bound.PrefillChunk);
        Assert.Null(bound.MtpFusedDraft);
    }
}
