namespace BreakManager.Web.Hosting;

internal static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    /// <summary>
    /// <c>/health/live</c>: el proceso responde (sin checks).
    /// <c>/health/ready</c>: además, las dependencias (PostgreSQL) están disponibles.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Anónimos a propósito: los consulta el orquestador (Docker) sin credenciales.
        endpoints.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains(ReadyTag) }).AllowAnonymous();
        return endpoints;
    }
}
