using System;
using System.IO;
using TensorSharp.Models;
using TensorSharp.Server.Hosting;
using Xunit;

namespace InferenceWeb.Tests;

/// <summary>
/// Rung-6 fail-fast paths: preset value validation and the startup-time
/// ValidatePresets sweep that catches broken blocks for models other than the
/// startup one.
/// </summary>
public class KnobValidationTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("knob-validation-tests").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string WriteConfig(string json)
    {
        string path = Path.Combine(_dir, "server.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Preset_BadIntValue_FailsWithExpectedInput()
    {
        string config = WriteConfig("""
            { "presets": { "m.gguf": { "PrefillChunk": "lots" } } }
            """);
        var resolve = ServerOptionsBuilder.CreateModelOptionsResolver(Array.Empty<string>(), new[] { config });
        var ex = Assert.Throws<ArgumentException>(() => resolve("m.gguf"));
        Assert.Contains("PrefillChunk", ex.Message);
        Assert.Contains("integer", ex.Message);
    }

    [Fact]
    public void Preset_JsonBoolsAndBits_AllAccepted()
    {
        string config = WriteConfig("""
            { "presets": { "m.gguf": { "FullDecode": false, "PrefillWarmup": "1" } } }
            """);
        var qwen = (Qwen35Options)ServerOptionsBuilder.CreateModelOptionsResolver(
            Array.Empty<string>(), new[] { config })("m.gguf");
        Assert.False(qwen.FullDecode);
        Assert.True(qwen.PrefillWarmup);
    }

    [Fact]
    public void ValidatePresets_CatchesBrokenBlockForAnyModel()
    {
        // The broken block belongs to a model that is NOT being loaded; the
        // startup sweep must still reject it.
        string config = WriteConfig("""
            {
              "presets": {
                "good.gguf": { "PrefillChunk": 256 },
                "broken.gguf": { "NotAKnob": true }
              }
            }
            """);
        var ex = Assert.Throws<ArgumentException>(() => ModelKnobConfig.ValidatePresets(new[] { config }));
        Assert.Contains("NotAKnob", ex.Message);
        Assert.Contains("broken.gguf", ex.Message);
    }

    [Fact]
    public void ValidatePresets_NoPresetsOrMissingFile_IsQuiet()
    {
        string config = WriteConfig("""{ "backend": "ggml_cpu" }""");
        ModelKnobConfig.ValidatePresets(new[] { config });
        ModelKnobConfig.ValidatePresets(new[] { Path.Combine(_dir, "absent.json") });
        ModelKnobConfig.ValidatePresets(null);
    }
}
