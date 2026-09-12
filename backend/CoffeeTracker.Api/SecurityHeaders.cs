namespace CoffeeTracker.Api;

/// <summary>
/// The response headers that defend the app in the browser rather than on the server.
///
/// They matter here more than they would in a session-cookie app: the session lives in
/// <c>localStorage</c> (a refresh token valid for days), which no <c>HttpOnly</c> flag
/// protects. Script injection is therefore the shortest path to a stolen session, and a
/// content security policy is what closes it.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// Applies the headers to every response, static files and the SPA shell included —
    /// so it must be registered before the static-file middleware short-circuits.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this WebApplication app, string? oidcAuthority)
    {
        var policy = BuildContentSecurityPolicy(app.Environment.IsDevelopment(), oidcAuthority);

        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            // Stop a stored photo (or any response) being MIME-sniffed into active content.
            headers["X-Content-Type-Options"] = "nosniff";
            // Nothing about a URL here is useful to a third party, and photo URLs carry
            // a signature — never send them in a Referer.
            headers["Referrer-Policy"] = "no-referrer";
            // frame-ancestors below is the modern spelling; this one is for browsers and
            // embedded webviews that still only read the legacy header.
            headers["X-Frame-Options"] = "DENY";
            headers["Content-Security-Policy"] = policy;
            await next();
        });
    }

    /// <summary>
    /// The policy the Angular build actually needs, and nothing more.
    ///
    /// <c>style-src</c> keeps <c>'unsafe-inline'</c> because Angular injects component
    /// styles as inline &lt;style&gt; elements at runtime; scripts need no such
    /// exception, which is the half that matters. In Development the Swagger UI served
    /// at the root does run inline script, so the exception is widened there — and only
    /// there, where the app is not exposed.
    /// </summary>
    internal static string BuildContentSecurityPolicy(bool isDevelopment, string? oidcAuthority)
    {
        // The provider is contacted from the browser for discovery and the PKCE code
        // exchange, so its origin has to be reachable by fetch. Only the origin is
        // granted, never the configured path.
        var provider = OriginOf(oidcAuthority);
        var connect = provider is null ? "'self'" : $"'self' {provider}";

        var script = isDevelopment ? "'self' 'unsafe-inline' 'unsafe-eval'" : "'self'";

        return string.Join("; ",
        [
            "default-src 'self'",
            $"script-src {script}",
            "style-src 'self' 'unsafe-inline'",
            // data: for inline previews of a photo the user just picked, blob: for the
            // same image read back from the file input.
            "img-src 'self' data: blob:",
            "font-src 'self' data:",
            $"connect-src {connect}",
            // The PWA's service worker, registered from the app's own origin.
            "worker-src 'self' blob:",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'",
        ]);
    }

    /// <summary>
    /// The scheme-host-port of a configured authority, or null when none is configured
    /// or the value is not an absolute URL — options validation already refuses the
    /// latter at startup, so this only has to avoid throwing before it gets there.
    /// </summary>
    private static string? OriginOf(string? authority) =>
        Uri.TryCreate(authority, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;
}
