using BreakManager.Infrastructure;

namespace BreakManager.Web.Hosting;

/// <summary>
/// Comando explícito para aplicar migraciones: <c>dotnet BreakManager.Web.dll migrate</c>.
/// Las migraciones nunca se aplican automáticamente al arrancar la aplicación.
/// </summary>
internal static partial class MigrateCommand
{
    private const string CommandName = "migrate";

    public static bool IsRequested(string[] args) =>
        args.Length > 0 && string.Equals(args[0], CommandName, StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MigrateCommand));

        LogApplying(logger);
        await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        LogApplied(logger);

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Aplicando migraciones de la base de datos")]
    private static partial void LogApplying(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migraciones aplicadas")]
    private static partial void LogApplied(ILogger logger);
}
