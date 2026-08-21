namespace DjVisualizer.Api.Middleware;

/// <summary>
/// Applies response security headers, with a content-negotiated Content-Security-Policy.
/// </summary>
/// <remarks>
/// The policy has to differ by response kind because this process serves two very different
/// things. Its JSON/video responses are never a browsing context, so they get the most
/// restrictive policy that exists (<c>default-src 'none'</c>) - nothing should ever load a
/// subresource from an API response. But in single-container hosting the same process also serves
/// the built SPA, and <c>default-src 'none'</c> on an HTML document blocks its own scripts and
/// stylesheets, i.e. it would ship a blank page. Switching on the negotiated content type keeps
/// the strict policy everywhere it is meaningful without breaking the document that needs a real
/// one.
/// </remarks>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>Policy for API responses: they are data, never a browsing context.</summary>
    internal const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

    /// <summary>
    /// Policy for the SPA document and its assets. Everything is same-origin: the bundle, the
    /// styles and the API it calls are all served by this process, so no host allowlist is
    /// needed. Two relaxations are load-bearing rather than habitual:
    /// <c>'unsafe-inline'</c> for styles, because the progress bar sets its width with a React
    /// inline style attribute; and <c>blob:</c> for images, because artwork is previewed before
    /// upload via <c>URL.createObjectURL</c>. Scripts get no such relaxation.
    /// </summary>
    internal const string AppContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'none'; " +
        "form-action 'none'; " +
        "frame-ancestors 'none'";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";

            var isHtmlDocument = context.Response.ContentType?
                .StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ?? false;
            headers["Content-Security-Policy"] = isHtmlDocument ? AppContentSecurityPolicy : ApiContentSecurityPolicy;

            return Task.CompletedTask;
        });

        await next(context);
    }
}
