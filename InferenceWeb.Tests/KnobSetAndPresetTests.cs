using System;
using System.Collections.Generic;
using System.IO;
using TensorSharp.Models;
using TensorSharp.Runtime;
using TensorSharp.Server.Hosting;
using Xunit;

namespace InferenceWeb.Tests;

/// <summary>
/// The rung-5 surface: the <c>--set NAME=VALUE</c> escape hatch and per-model
/// <c>"presets"</c> blocks in <c>--config</c> files, including their
/// precedence (env &lt; preset &lt; CLI/--set) and fail-fast validation.
/// </summary>
public class KnobSetAndPresetTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("knob-preset-tests").FullName;
    private readonly List<(string Name, string Value)> _savedEnv = new();

    public void Dispose()
    {
        foreach ((string name, string value) in _savedEnv)
            Environment.SetEnvironmentVariable(name, value);
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private void SetEnv(string name, string value)
    {
        _savedEnv.Add((name, Environment.GetEnvironmentVariable(name)));
        Environment.SetEnvironmentVariable(name, value);
    }

    private string WriteConfig(string json)
    {
        string path = Path.Combine(_dir, "server.json");
        File.WriteAllText(path, json);
        return path;
    }

    // ----- --set -----

    [Fact]
    public void Set_MapsIntAndBoolKnobs()
    {
        var qwen = (Qwen35Options)ServerOptionsBuilder.BuildModelOptions(
            new[] { "--set", "TS_PREFILL_CHUNK=512", "--set", "TS_QWEN35_FULL_DECODE=0" });
        Assert.Equal(512, qwen.PrefillChunk);
        Assert.False(qwen.FullDecode);
    }

    [Fact]
    public void Set_BeatsEnvVar()
    {
        SetEnv("TS_PREFILL_CHUNK", "128");
        var qwen = (Qwen35Options)ServerOptionsBuilder.BuildModelOptions(
            new[] { "--set", "TS_PREFILL_CHUNK=512" });
        Assert.Equal(512, qwen.PrefillChunk);
    }

    [Fact]
    public void Set_UnknownNameOrBadValue_FailsFast()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ServerOptionsBuilder.BuildModelOptions(new[] { "--set", "TS_NOT_A_KNOB=1" }));
        Assert.Contains("TS_NOT_A_KNOB", ex.Message);

        ex = Assert.Throws<ArgumentException>(() =>
            ServerOptionsBuilder.BuildModelOptions(new[] { "--set", "TS_PREFILL_CHUNK=zero" }));
        Assert.Contains("integer", ex.Message);

        // Canonical word forms are accepted; anything outside the token set
        // still fails fast.
        Assert.False(((Qwen35Options)ServerOptionsBuilder.BuildModelOptions(
            new[] { "--set", "TS_QWEN35_FULL_DECODE=false" })).FullDecode);
        ex = Assert.Throws<ArgumentException>(() =>
            ServerOptionsBuilder.BuildModelOptions(new[] { "--set", "TS_QWEN35_FULL_DECODE=banana" }));
        Assert.Contains("1 or 0", ex.Message);

        Assert.Throws<ArgumentException>(() =>
            ServerOptionsBuilder.BuildModelOptions(new[] { "--set", "TS_PREFILL_CHUNK" }));
        Assert.Throws<ArgumentException>(() =>
            ServerOptionsBuilder.BuildModelOptions(new[] { "--set" }));
    }

    [Fact]
    public void Set_DoesNotTripParseArgsUnknownArgTrap()
    {
        var options = ServerOptionsBuilder.Build(new[] { "--set", "TS_PREFILL_CHUNK=512" }, _dir);
        Assert.NotNull(options);
    }

    // ----- per-model presets -----

    [Fact]
    public void Preset_AppliesToMatchingModelOnly()
    {
        string config = WriteConfig("""
            {
              "presets": {
                "modelA.gguf": { "PrefillChunk": 256, "FullDecode": false },
                "modelB.gguf": { "PrefillChunk": 999 }
              }
            }
            """);
        var resolve = ServerOptionsBuilder.CreateModelOptionsResolver(
            Array.Empty<string>(), new[] { config });

        var a = (Qwen35Options)resolve("/models/modelA.gguf");
        Assert.Equal(256, a.PrefillChunk);
        Assert.False(a.FullDecode);

        var b = (Qwen35Options)resolve("/models/modelB.gguf");
        Assert.Equal(999, b.PrefillChunk);
        Assert.Null(b.FullDecode);

        var other = (Qwen35Options)resolve("/models/other.gguf");
        Assert.Null(other.PrefillChunk);
    }

    [Fact]
    public void Preset_BeatsEnv_ButLosesToCli()
    {
        SetEnv("TS_PREFILL_CHUNK", "128");
        string config = WriteConfig("""
            { "presets": { "m.gguf": { "PrefillChunk": 256 } } }
            """);

        var fromPreset = (Qwen35Options)ServerOptionsBuilder.CreateModelOptionsResolver(
            Array.Empty<string>(), new[] { config })("m.gguf");
        Assert.Equal(256, fromPreset.PrefillChunk);

        var fromCli = (Qwen35Options)ServerOptionsBuilder.CreateModelOptionsResolver(
            new[] { "--set", "TS_PREFILL_CHUNK=512" }, new[] { config })("m.gguf");
        Assert.Equal(512, fromCli.PrefillChunk);
    }

    [Fact]
    public void Preset_UnknownKnobKey_FailsFast()
    {
        string config = WriteConfig("""
            { "presets": { "m.gguf": { "NotAKnob": 1 } } }
            """);
        var resolve = ServerOptionsBuilder.CreateModelOptionsResolver(
            Array.Empty<string>(), new[] { config });
        var ex = Assert.Throws<ArgumentException>(() => resolve("m.gguf"));
        Assert.Contains("NotAKnob", ex.Message);
    }

    [Fact]
    public void ConfigFileArgs_TreatsPresetsAsReserved()
    {
        string config = WriteConfig("""
            { "backend": "ggml_cpu", "presets": { "m.gguf": { "PrefillChunk": 256 } } }
            """);
        string[] expanded = ConfigFileArgs.Expand(new[] { "--config", config });
        Assert.Contains("--backend", expanded);
        Assert.DoesNotContain("--presets", expanded);
    }
}
