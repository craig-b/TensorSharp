using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TensorSharp.Runtime.Logging;

namespace TensorSharp.Server.Hosting
{
    /// <summary>Which clients must present the configured API key.</summary>
    internal enum ApiKeyScope
    {
        /// <summary>Every client, loopback included.</summary>
        All,

        /// <summary>
        /// Only clients connecting from non-loopback addresses; loopback stays
        /// anonymous, so local tools and the bundled Web UI keep working while
        /// a LAN/internet-facing bind is protected. Judged by the connection's
        /// remote address — behind a reverse proxy every request looks
        /// loopback, so proxied deployments need <see cref="All"/>.
        /// </summary>
        External,
    }

    /// <summary>
    /// Opt-in API-key gate. With no key configured the pipeline is untouched
    /// and the server behaves exactly as it did before the feature existed.
    /// With a key, every route except <c>/health</c> (and the wwwroot static
    /// files, which are served by earlier middleware) requires it via
    /// <c>Authorization: Bearer</c> or <c>X-Api-Key</c>.
    /// </summary>
    internal static class ApiKeyAuthentication
    {
        internal static bool TryParseScope(string value, out ApiKeyScope scope)
        {
            if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
            {
                scope = ApiKeyScope.All;
                return true;
            }
            if (string.Equals(value, "external", StringComparison.OrdinalIgnoreCase))
            {
                scope = ApiKeyScope.External;
                return true;
            }
            scope = ApiKeyScope.All;
            return false;
        }

        public static IApplicationBuilder UseApiKeyAuthentication(
            this IApplicationBuilder app, ServerHostingOptions options)
        {
            if (string.IsNullOrEmpty(options.ApiKey))
                return app;

            app.ApplicationServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("TensorSharp.Server.ApiKey")
                .LogInformation("API key authentication enabled (scope: {Scope})",
                    options.ApiKeyScope == ApiKeyScope.External ? "external" : "all");
            return app.UseMiddleware<ApiKeyMiddleware>();
        }
    }

    /// <summary>
    /// Rejects requests that do not carry the configured API key with 401.
    /// Only added to the pipeline when a key is configured — see
    /// <see cref="ApiKeyAuthentication.UseApiKeyAuthentication"/>.
    /// </summary>
    internal sealed class ApiKeyMiddleware
    {
        private const string BearerPrefix = "Bearer ";

        private readonly RequestDelegate _next;
        private readonly ServerHostingOptions _options;
        private readonly ILogger _logger;

        public ApiKeyMiddleware(
            RequestDelegate next, ServerHostingOptions options, ILoggerFactory loggerFactory)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = loggerFactory.CreateLogger("TensorSharp.Server.ApiKey");
        }

        public Task InvokeAsync(HttpContext context)
        {
            // /health stays open so probes and load balancers work unkeyed.
            // "/" stays open because it serves the Web UI shell via the
            // HealthEndpoints route (not the static-files middleware, which
            // runs before this gate) — the shell is public application code,
            // and every API call it makes is still keyed.
            PathString path = context.Request.Path;
            if (path.Equals("/health", StringComparison.OrdinalIgnoreCase)
                || !path.HasValue || path.Equals("/", StringComparison.Ordinal))
                return _next(context);

            if (_options.ApiKeyScope == ApiKeyScope.External && IsLoopbackClient(context))
                return _next(context);

            string presented = ExtractPresentedKey(context.Request);
            if (presented != null && KeyMatches(presented, _options.ApiKey))
                return _next(context);

            return RejectAsync(context, missing: presented == null);
        }

        /// <summary>
        /// True when the connection provably comes from this machine. A
        /// connection with no remote address (unix sockets, some test hosts)
        /// is not provably local, so it stays keyed.
        /// </summary>
        internal static bool IsLoopbackClient(HttpContext context)
        {
            IPAddress remote = context.Connection.RemoteIpAddress;
            return remote != null && IPAddress.IsLoopback(remote);
        }

        /// <summary>
        /// Key offered by the request: <c>Authorization: Bearer &lt;key&gt;</c>
        /// (what OpenAI SDKs send) or <c>X-Api-Key: &lt;key&gt;</c>. Null when
        /// neither header carries one.
        /// </summary>
        internal static string ExtractPresentedKey(HttpRequest request)
        {
            string auth = request.Headers.Authorization.ToString();
            if (auth.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
                return auth.Substring(BearerPrefix.Length).Trim();

            string headerKey = request.Headers["X-Api-Key"].ToString();
            return headerKey.Length == 0 ? null : headerKey.Trim();
        }

        /// <summary>Constant-time comparison so the check leaks no timing signal.</summary>
        internal static bool KeyMatches(string presented, string expected)
        {
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(expected));
        }

        private async Task RejectAsync(HttpContext context, bool missing)
        {
            _logger.LogWarning(LogEventIds.HttpRequestRejected,
                "Request rejected ({Reason}): {Method} {Path} from {Remote}",
                missing ? "missing API key" : "invalid API key",
                context.Request.Method, context.Request.Path,
                context.Connection.RemoteIpAddress?.ToString() ?? "(unknown)");

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";

            string message = missing
                ? "Missing API key. Pass it as 'Authorization: Bearer <key>' or 'X-Api-Key: <key>'."
                : "Invalid API key.";

            // OpenAI clients surface error.message from this nested shape; every
            // other surface of this server uses a flat { error } object.
            if (context.Request.Path.StartsWithSegments("/v1"))
            {
                await context.Response.WriteAsJsonAsync(new
                {
                    error = new
                    {
                        message,
                        type = "authentication_error",
                        code = missing ? "missing_api_key" : "invalid_api_key",
                    },
                });
            }
            else
            {
                await context.Response.WriteAsJsonAsync(new { error = message });
            }
        }
    }
}
