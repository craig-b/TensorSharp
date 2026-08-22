using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace TensorSharp.Server.Hosting
{
    /// <summary>
    /// Response security headers for every route. The Web UI ships no external
    /// resources (scripts, styles, and media are same-origin files; progressive
    /// image-edit frames arrive as data: URLs), so pages get a strict
    /// same-origin CSP with inline script and style forbidden. Files under
    /// /uploads are client-supplied: they get a no-execute sandbox policy
    /// instead, so a file that opens directly in a browser renders inert.
    /// Embedding an upload via &lt;img&gt;/&lt;video&gt; is governed by the
    /// embedding page's policy, not the upload's own.
    /// </summary>
    internal static class SecurityHeaders
    {
        internal const string PagePolicy =
            "default-src 'self'; script-src 'self'; style-src 'self'; " +
            "img-src 'self' blob: data:; media-src 'self' blob:; connect-src 'self'; " +
            "object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";

        internal const string UploadsPolicy = "default-src 'none'; sandbox";

        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        {
            return app.Use(next => context =>
            {
                Apply(context);
                return next(context);
            });
        }

        /// <summary>
        /// Registers the headers via <see cref="HttpResponse.OnStarting(System.Func{object, Task}, object)"/>
        /// rather than setting them immediately: an error handler downstream may
        /// call <c>Response.Clear()</c> (which drops already-set headers), while
        /// OnStarting callbacks run when the response is actually sent.
        /// </summary>
        internal static void Apply(HttpContext context)
        {
            context.Response.OnStarting(static state =>
            {
                var ctx = (HttpContext)state;
                var headers = ctx.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                headers.XFrameOptions = "DENY";
                // Guarded so a route that needs its own policy can pre-set one.
                if (!headers.ContainsKey("Content-Security-Policy"))
                {
                    headers.ContentSecurityPolicy =
                        ctx.Request.Path.StartsWithSegments("/uploads") ? UploadsPolicy : PagePolicy;
                }
                return Task.CompletedTask;
            }, context);
        }
    }
}
