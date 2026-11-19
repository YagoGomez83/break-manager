using BreakManager.Infrastructure;
using BreakManager.Infrastructure.Persistence;

namespace BreakManager.Web.Hosting;

/// <summary>
/// Comando explícito para aplicar migraciones y la siembra de arranque: <c>dotnet BreakManager.Web.dll migrate</c>.
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
        var cancellationToken = app.Lifetime.ApplicationStopping;

        LogApplying(logger);
        await app.Services.MigrateDatabaseAsync(cancellationToken).ConfigureAwait(false);
        LogApplied(logger);

        var settings = new SeedSettings(
            app.Configuration["BOOTSTRAP_ADMIN_USERNAME"],
            app.Configuration["BOOTSTRAP_ADMIN_PASSWORD"],
            app.Environment.IsDevelopment());

        var seeded = await app.Services.SeedDatabaseAsync(settings, cancellationToken).ConfigureAwait(false);
        if (seeded.IsFailure)
        {
            LogSeedFailed(logger, seeded.Error.Code, seeded.Error.Message);
            return 1;
        }

        LogSeeded(logger, seeded.Value.AdminCreated, seeded.Value.AdminId, seeded.Value.DraftNoticePublished);
        if (seeded.Value.AdminCreated)
        {
            LogRemoveBootstrapPassword(logger);
        }

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Aplicando migraciones de la base de datos")]
    private static partial void LogApplying(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migraciones aplicadas")]
    private static partial void LogApplied(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Siembra aplicada: admin creado {AdminCreated} ({AdminId}), aviso BORRADOR publicado {DraftNoticePublished}")]
    private static partial void LogSeeded(ILogger logger, bool adminCreated, Guid? adminId, bool draftNoticePublished);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Se creó el admin inicial: después del primer ingreso, borrá BOOTSTRAP_ADMIN_PASSWORD del .env")]
    private static partial void LogRemoveBootstrapPassword(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo aplicar la siembra de arranque: {Code} {Detail}")]
    private static partial void LogSeedFailed(ILogger logger, string code, string detail);
}
