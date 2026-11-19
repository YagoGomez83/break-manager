namespace BreakManager.Web.Hosting;

internal static class SecurityHeadersExtensions
{
    /// <summary>
    /// Cabeceras de seguridad básicas en todas las respuestas. El reverse proxy (Fase 7) puede agregar más.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            await next(context).ConfigureAwait(false);
        });
}
