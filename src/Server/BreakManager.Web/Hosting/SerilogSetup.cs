using Serilog;
using Serilog.Formatting.Compact;

namespace BreakManager.Web.Hosting;

internal static class SerilogSetup
{
    /// <summary>
    /// Logger mínimo para registrar fallos durante el arranque, antes de leer la configuración.
    /// </summary>
    public static Serilog.Extensions.Hosting.ReloadableLogger CreateBootstrapLogger() =>
        new LoggerConfiguration()
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .CreateBootstrapLogger();

    /// <summary>
    /// Logs estructurados en JSON a la consola. El formato compacto incluye el TraceId (<c>@tr</c>) y el
    /// SpanId (<c>@sp</c>) de la <see cref="System.Diagnostics.Activity"/> actual para correlacionar.
    /// Regla del proyecto: solo IDs en los logs, nunca nombres de personas.
    /// </summary>
    public static void AddSerilogJsonConsole(this WebApplicationBuilder builder) =>
        builder.Services.AddSerilog((services, configuration) => configuration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "BreakManager.Web")
            .WriteTo.Console(new RenderedCompactJsonFormatter()));
}
