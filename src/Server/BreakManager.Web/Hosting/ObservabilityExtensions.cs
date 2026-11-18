using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace BreakManager.Web.Hosting;

internal static class ObservabilityExtensions
{
    /// <summary>
    /// Logs (Serilog) y métricas (OpenTelemetry: ASP.NET Core y runtime).
    /// El exportador de métricas se elige con <c>Telemetry:Exporter</c>: <c>none</c> (por defecto), <c>console</c> u <c>otlp</c>.
    /// Con <c>otlp</c>, el destino se configura con las variables estándar <c>OTEL_EXPORTER_OTLP_*</c>.
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.AddSerilogJsonConsole();

        var exporter = builder.Configuration["Telemetry:Exporter"] ?? "none";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("BreakManager.Web"))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation();

                switch (exporter.ToUpperInvariant())
                {
                    case "CONSOLE":
                        metrics.AddConsoleExporter();
                        break;
                    case "OTLP":
                        metrics.AddOtlpExporter();
                        break;
                    case "NONE":
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Valor no soportado para Telemetry:Exporter: \"{exporter}\". Use none, console u otlp.");
                }
            });

        return builder;
    }
}
