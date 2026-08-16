using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using TensorSharp.Server.Hosting;

namespace InferenceWeb.Tests;

/// <summary>
/// Every response carries the fixed security headers; pages get the strict
/// same-origin CSP while /uploads responses get the inert sandbox policy.
/// </summary>
public class SecurityHeadersTests
{
    [Fact]
    public async Task PageResponse_GetsStrictSameOriginPolicy()
    {
        var ctx = ContextFor("/");
        SecurityHeaders.Apply(ctx);
        await FireOnStarting(ctx);

        Assert.Equal("nosniff", ctx.Response.Headers.XContentTypeOptions);
        Assert.Equal("no-referrer", ctx.Response.Headers["Referrer-Policy"]);
        Assert.Equal("DENY", ctx.Response.Headers.XFrameOptions);
        Assert.Equal(SecurityHeaders.PagePolicy, ctx.Response.Headers.ContentSecurityPolicy);
    }

    [Theory]
    [InlineData("/api/models")]
    [InlineData("/v1/chat/completions")]
    [InlineData("/styles.css")]
    [InlineData("/js/app.js")]
    public async Task NonUploadPaths_GetThePagePolicy(string path)
    {
        var ctx = ContextFor(path);
        SecurityHeaders.Apply(ctx);
        await FireOnStarting(ctx);

        Assert.Equal(SecurityHeaders.PagePolicy, ctx.Response.Headers.ContentSecurityPolicy);
    }

    [Theory]
    [InlineData("/uploads/evil.html")]
    [InlineData("/uploads/photo.png")]
    [InlineData("/Uploads/evil.html")]
    public async Task UploadResponses_GetTheSandboxPolicy(string path)
    {
        var ctx = ContextFor(path);
        SecurityHeaders.Apply(ctx);
        await FireOnStarting(ctx);

        Assert.Equal(SecurityHeaders.UploadsPolicy, ctx.Response.Headers.ContentSecurityPolicy);
        Assert.Equal("nosniff", ctx.Response.Headers.XContentTypeOptions);
    }

    [Fact]
    public async Task PreSetPolicy_IsNotOverwritten()
    {
        var ctx = ContextFor("/api/models");
        SecurityHeaders.Apply(ctx);
        ctx.Response.Headers.ContentSecurityPolicy = "default-src 'none'";
        await FireOnStarting(ctx);

        Assert.Equal("default-src 'none'", ctx.Response.Headers.ContentSecurityPolicy);
    }

    [Fact]
    public void PagePolicy_ForbidsInlineScriptAndStyle()
    {
        Assert.DoesNotContain("unsafe-inline", SecurityHeaders.PagePolicy);
        Assert.DoesNotContain("unsafe-eval", SecurityHeaders.PagePolicy);
    }

    /// <summary>
    /// DefaultHttpContext's response feature ignores OnStarting registrations,
    /// so capture and fire them the way a server would just before sending.
    /// </summary>
    private static HttpContext ContextFor(string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Features.Set<IHttpResponseFeature>(new OnStartingCapturingResponseFeature());
        ctx.Request.Path = path;
        return ctx;
    }

    private static async Task FireOnStarting(HttpContext ctx)
    {
        var feature = (OnStartingCapturingResponseFeature)ctx.Features.Get<IHttpResponseFeature>();
        foreach (var (callback, state) in feature.OnStartingCallbacks)
            await callback(state);
    }

    private sealed class OnStartingCapturingResponseFeature : HttpResponseFeature
    {
        public List<(Func<object, Task> Callback, object State)> OnStartingCallbacks { get; } = new();

        public override void OnStarting(Func<object, Task> callback, object state)
            => OnStartingCallbacks.Add((callback, state));
    }
}
