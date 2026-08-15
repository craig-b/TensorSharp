using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TensorSharp.Server.Hosting;

namespace InferenceWeb.Tests;

/// <summary>
/// Tests for the opt-in API-key check: option resolution in
/// <see cref="ServerOptionsBuilder"/> and enforcement in
/// <see cref="ApiKeyMiddleware"/>. A server started without a key must behave
/// exactly as before the feature existed.
/// </summary>
public class ApiKeyAuthenticationTests : IDisposable
{
    private readonly string _baseDir;

    public ApiKeyAuthenticationTests()
    {
        _baseDir = Directory.CreateTempSubdirectory("ts-apikey-test").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_baseDir, recursive: true);
    }

    // ---- Option resolution ------------------------------------------------

    [Fact]
    public void Build_NoKeyAnywhere_LeavesApiKeyNull()
    {
        using var env = new EnvScope();
        env.Set("TS_API_KEY", null);

        var options = ServerOptionsBuilder.Build(Array.Empty<string>(), _baseDir);

        Assert.Null(options.ApiKey);
        Assert.Equal(ApiKeyScope.All, options.ApiKeyScope);
    }

    [Fact]
    public void Build_ApiKeyFlag_SetsKeyWithScopeAllDefault()
    {
        using var env = new EnvScope();
        env.Set("TS_API_KEY", null);

        var options = ServerOptionsBuilder.Build(new[] { "--api-key", "sk-secret" }, _baseDir);

        Assert.Equal("sk-secret", options.ApiKey);
        Assert.Equal(ApiKeyScope.All, options.ApiKeyScope);
    }

    [Fact]
    public void Build_ApiKeyFile_ReadsTrimmedContent()
    {
        using var env = new EnvScope();
        env.Set("TS_API_KEY", null);
        string keyFile = Path.Combine(_baseDir, "key.txt");
        File.WriteAllText(keyFile, "  sk-from-file\n");

        var options = ServerOptionsBuilder.Build(new[] { "--api-key-file", keyFile }, _baseDir);

        Assert.Equal("sk-from-file", options.ApiKey);
    }

    [Fact]
    public void Build_EnvVar_IsTheFallbackKeySource()
    {
        using var env = new EnvScope();
        env.Set("TS_API_KEY", "sk-from-env");

        var options = ServerOptionsBuilder.Build(Array.Empty<string>(), _baseDir);

        Assert.Equal("sk-from-env", options.ApiKey);
    }

    [Fact]
    public void Build_ApiKeyFlag_BeatsFileAndEnv()
    {
        using var env = new EnvScope();
        env.Set("TS_API_KEY", "sk-from-env");
        string keyFile = Path.Combine(_baseDir, "key.txt");
        File.WriteAllText(keyFile, "sk-from-file");

        var options = ServerOptionsBuilder.Build(
            new[] { "--api-key", "sk-flag", "--api-key-file", keyFile }, _baseDir);

        Assert.Equal("sk-flag", options.ApiKey);
    }

    [Fact]
    public void Build_ScopeExternal_Parses()
    {
        using var env = new EnvScope();
        env.Set("TS_API_KEY", null);

        var options = ServerOptionsBuilder.Build(
            new[] { "--api-key", "sk-secret", "--api-key-scope", "external" }, _baseDir);

        Assert.Equal(ApiKeyScope.External, options.ApiKeyScope);
    }

    [Fact]
    public void Build_InvalidScope_Throws()
    {
        using var env = new EnvScope();
        env.Set("TS_API_KEY", null);

        var ex = Assert.Throws<ArgumentException>(() => ServerOptionsBuilder.Build(
            new[] { "--api-key", "sk-secret", "--api-key-scope", "public" }, _baseDir));
        Assert.Contains("--api-key-scope", ex.Message);
    }

    [Fact]
    public void Build_ScopeWithoutAnyKey_Throws()
    {
        using var env = new EnvScope();
        env.Set("TS_API_KEY", null);

        var ex = Assert.Throws<ArgumentException>(() => ServerOptionsBuilder.Build(
            new[] { "--api-key-scope", "external" }, _baseDir));
        Assert.Contains("requires an API key", ex.Message);
    }

    [Fact]
    public void Build_MissingKeyFile_Throws()
    {
        using var env = new EnvScope();
        env.Set("TS_API_KEY", null);

        Assert.Throws<FileNotFoundException>(() => ServerOptionsBuilder.Build(
            new[] { "--api-key-file", Path.Combine(_baseDir, "absent.txt") }, _baseDir));
    }

    [Fact]
    public void Build_EmptyKeyFile_Throws()
    {
        using var env = new EnvScope();
        env.Set("TS_API_KEY", null);
        string keyFile = Path.Combine(_baseDir, "empty.txt");
        File.WriteAllText(keyFile, "   \n");

        var ex = Assert.Throws<ArgumentException>(() => ServerOptionsBuilder.Build(
            new[] { "--api-key-file", keyFile }, _baseDir));
        Assert.Contains("empty", ex.Message);
    }

    // ---- Middleware enforcement -------------------------------------------

    private static ApiKeyMiddleware MakeMiddleware(
        string key, ApiKeyScope scope, RequestDelegate next)
    {
        var options = new ServerHostingOptions(
            startupModelPath: null,
            startupMmProjPath: null,
            defaultBackend: "ggml_cpu",
            supportedBackends: null,
            defaultMaxTokens: 100,
            maxTokensPinned: false,
            defaultWanVideoFrames: 0,
            defaultWanVideoFps: 0,
            uploadDirectory: null,
            logDirectory: null,
            fileLoggingEnabled: false,
            samplingDefaults: null,
            apiKey: key,
            apiKeyScope: scope);
        return new ApiKeyMiddleware(next, options, NullLoggerFactory.Instance);
    }

    private static DefaultHttpContext MakeContext(
        string path, string remoteAddress = "203.0.113.9")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Response.Body = new MemoryStream();
        if (remoteAddress != null)
            ctx.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        return ctx;
    }

    [Fact]
    public async Task Middleware_MissingKey_Returns401WithChallenge()
    {
        bool nextCalled = false;
        var middleware = MakeMiddleware("sk-secret", ApiKeyScope.All,
            _ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = MakeContext("/api/chat");

        await middleware.InvokeAsync(ctx);

        Assert.False(nextCalled);
        Assert.Equal(401, ctx.Response.StatusCode);
        Assert.Equal("Bearer", ctx.Response.Headers.WWWAuthenticate);
    }

    [Theory]
    [InlineData("Authorization", "Bearer sk-secret")]
    [InlineData("X-Api-Key", "sk-secret")]
    public async Task Middleware_CorrectKey_PassesThrough(string header, string value)
    {
        bool nextCalled = false;
        var middleware = MakeMiddleware("sk-secret", ApiKeyScope.All,
            _ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = MakeContext("/api/chat");
        ctx.Request.Headers[header] = value;

        await middleware.InvokeAsync(ctx);

        Assert.True(nextCalled);
        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Theory]
    [InlineData("Authorization", "Bearer sk-wrong")]
    [InlineData("Authorization", "Basic sk-secret")]
    [InlineData("X-Api-Key", "sk-wrong")]
    [InlineData("X-Api-Key", "sk-secret-longer")]
    public async Task Middleware_WrongKey_Returns401(string header, string value)
    {
        bool nextCalled = false;
        var middleware = MakeMiddleware("sk-secret", ApiKeyScope.All,
            _ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = MakeContext("/v1/chat/completions");
        ctx.Request.Headers[header] = value;

        await middleware.InvokeAsync(ctx);

        Assert.False(nextCalled);
        Assert.Equal(401, ctx.Response.StatusCode);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/")]
    public async Task Middleware_HealthAndUiShell_AreAlwaysExempt(string path)
    {
        bool nextCalled = false;
        var middleware = MakeMiddleware("sk-secret", ApiKeyScope.All,
            _ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = MakeContext(path);

        await middleware.InvokeAsync(ctx);

        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    public async Task Middleware_ExternalScope_LoopbackClientStaysAnonymous(string remote)
    {
        bool nextCalled = false;
        var middleware = MakeMiddleware("sk-secret", ApiKeyScope.External,
            _ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = MakeContext("/api/chat", remote);

        await middleware.InvokeAsync(ctx);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Middleware_ExternalScope_RemoteClientIsKeyed()
    {
        bool nextCalled = false;
        var middleware = MakeMiddleware("sk-secret", ApiKeyScope.External,
            _ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = MakeContext("/api/chat", "203.0.113.9");

        await middleware.InvokeAsync(ctx);

        Assert.False(nextCalled);
        Assert.Equal(401, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_ExternalScope_UnknownRemoteAddressIsKeyed()
    {
        bool nextCalled = false;
        var middleware = MakeMiddleware("sk-secret", ApiKeyScope.External,
            _ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = MakeContext("/api/chat", remoteAddress: null);

        await middleware.InvokeAsync(ctx);

        Assert.False(nextCalled);
        Assert.Equal(401, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_AllScope_LoopbackClientIsKeyed()
    {
        bool nextCalled = false;
        var middleware = MakeMiddleware("sk-secret", ApiKeyScope.All,
            _ => { nextCalled = true; return Task.CompletedTask; });
        var ctx = MakeContext("/api/chat", "127.0.0.1");

        await middleware.InvokeAsync(ctx);

        Assert.False(nextCalled);
        Assert.Equal(401, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_OpenAiPath_UsesOpenAiErrorShape()
    {
        var middleware = MakeMiddleware("sk-secret", ApiKeyScope.All, _ => Task.CompletedTask);
        var ctx = MakeContext("/v1/chat/completions");

        await middleware.InvokeAsync(ctx);

        ctx.Response.Body.Position = 0;
        string body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        Assert.Contains("authentication_error", body);
    }

    [Fact]
    public async Task Middleware_NonOpenAiPath_UsesFlatErrorShape()
    {
        var middleware = MakeMiddleware("sk-secret", ApiKeyScope.All, _ => Task.CompletedTask);
        var ctx = MakeContext("/api/chat");

        await middleware.InvokeAsync(ctx);

        ctx.Response.Body.Position = 0;
        string body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        Assert.Contains("\"error\"", body);
        Assert.DoesNotContain("authentication_error", body);
    }

    [Theory]
    [InlineData("all", false)]
    [InlineData("ALL", false)]
    [InlineData("external", true)]
    [InlineData("External", true)]
    public void TryParseScope_AcceptsKnownValuesCaseInsensitively(string raw, bool expectExternal)
    {
        Assert.True(ApiKeyAuthentication.TryParseScope(raw, out var scope));
        Assert.Equal(expectExternal ? ApiKeyScope.External : ApiKeyScope.All, scope);
    }

    [Theory]
    [InlineData("public")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseScope_RejectsUnknownValues(string raw)
    {
        Assert.False(ApiKeyAuthentication.TryParseScope(raw, out _));
    }
}
