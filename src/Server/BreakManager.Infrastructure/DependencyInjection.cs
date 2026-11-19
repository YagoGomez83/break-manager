using BreakManager.Application.Abstractions;
using BreakManager.Application.Common;
using BreakManager.Application.Common.Security;
using BreakManager.Application.Identity;
using BreakManager.Application.Identity.Queries;
using BreakManager.Application.Presence;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;
using BreakManager.Infrastructure.Identity;
using BreakManager.Infrastructure.Persistence;
using BreakManager.Infrastructure.Queries;
using BreakManager.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BreakManager.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "BreakManager";

    /// <summary>
    /// Registra la persistencia (EF Core + PostgreSQL con snake_case), los adaptadores de los puertos de Application,
    /// ASP.NET Core Identity (stores, políticas de contraseña y bloqueo desde <see cref="IdentityParameters"/>),
    /// las claves de DataProtection en la base y el reloj del sistema.
    /// La cadena de conexión se lee de <c>ConnectionStrings:BreakManager</c>.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión 'ConnectionStrings:{ConnectionStringName}'.");

        services.AddDbContext<BreakManagerDbContext>(options => Configure(options, connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<IdentityParameters>();

        AddPersistenceAdapters(services);
        AddWebIdentity(services);
        AddQueries(services);

        services.AddDataProtection()
            .SetApplicationName("BreakManager")
            .PersistKeysToDbContext<BreakManagerDbContext>();

        return services;
    }

    /// <summary>
    /// Agrega el health check de la base de datos, con la etiqueta <c>ready</c>.
    /// </summary>
    public static IHealthChecksBuilder AddInfrastructureHealthChecks(this IHealthChecksBuilder builder, params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddDbContextCheck<BreakManagerDbContext>("database", tags: tags);
    }

    /// <summary>
    /// Aplica las migraciones pendientes. Se invoca solo con un comando explícito (nunca al arrancar).
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BreakManagerDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Siembra de arranque (la llama el comando <c>migrate</c>, después de las migraciones): roles web,
    /// admin inicial si no existe ningún Administrador, y el aviso BORRADOR solo en Development.
    /// </summary>
    public static Task<Result<SeedReport>> SeedDatabaseAsync(
        this IServiceProvider services,
        SeedSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);
        return DatabaseSeeder.SeedAsync(services, settings, cancellationToken);
    }

    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention();

    private static void AddPersistenceAdapters(IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ISecurityEventLog, EfSecurityEventLog>();
        services.AddScoped<IOperatorRepository, EfOperatorRepository>();
        services.AddScoped<IWorkstationRepository, EfWorkstationRepository>();
        services.AddScoped<IEnrollmentCodeRepository, EfEnrollmentCodeRepository>();
        services.AddScoped<ILoginTicketRepository, EfLoginTicketRepository>();
        services.AddScoped<IPrivacyNoticeRepository, EfPrivacyNoticeRepository>();
        services.AddScoped<IWorkstationSessionRepository, EfWorkstationSessionRepository>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPinHasher, Pbkdf2PinHasher>();
    }

    private static void AddWebIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<BreakManagerDbContext>()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>()
            .AddPasswordValidator<CommonPasswordValidator>();

        services.AddOptions<IdentityOptions>()
            .Configure<IOptions<IdentityParameters>>((options, parameters) =>
            {
                var p = parameters.Value;

                // ASVS nivel 1: largo mínimo y lista de contraseñas comunes, sin reglas de composición.
                options.Password.RequiredLength = p.WebPasswordMinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = p.WebLockoutAttempts;
                options.Lockout.DefaultLockoutTimeSpan = p.WebLockoutDuration;

                options.User.RequireUniqueEmail = false;
            });

        services.AddScoped<IUserAccountService, UserAccountService>();
    }

    private static void AddQueries(IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<ListWorkstationsQuery, IReadOnlyList<WorkstationListItem>>, ListWorkstationsHandler>();
        services.AddScoped<IQueryHandler<ListOperatorsQuery, IReadOnlyList<OperatorListItem>>, ListOperatorsHandler>();
        services.AddScoped<IQueryHandler<ListPrivacyNoticesQuery, IReadOnlyList<PrivacyNoticeListItem>>, ListPrivacyNoticesHandler>();
        services.AddScoped<IQueryHandler<ListOpenSessionsQuery, IReadOnlyList<OpenSessionListItem>>, ListOpenSessionsHandler>();
    }
}
