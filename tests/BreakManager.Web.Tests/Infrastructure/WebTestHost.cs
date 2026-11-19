using System.Net;
using System.Text.RegularExpressions;
using BreakManager.Application.Abstractions;
using BreakManager.Application.Identity;
using BreakManager.Infrastructure;
using BreakManager.Infrastructure.Identity;
using BreakManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Group = BreakManager.Domain.Identity.Group;

namespace BreakManager.Web.Tests.Infrastructure;

/// <summary>
/// Host real de BreakManager.Web contra un PostgreSQL de Testcontainers (base nueva por fixture),
/// con migraciones y siembra aplicadas como lo hace el comando <c>migrate</c>.
/// </summary>
public partial class WebTestHost : IAsyncLifetime
{
    public const string BootstrapAdmin = "admin";
    public const string BootstrapPassword = "mate-cocido-con-tostadas-7";
    public const string UserPassword = "otra-frase-bastante-larga-9";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();
    private WebApplicationFactory<Program>? _factory;

    public WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Host sin inicializar.");

    public IServiceProvider Services => Factory.Services;

    protected virtual IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = "web_" + Guid.NewGuid().ToString("N"),
        }.ConnectionString;

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", connectionString);
            builder.UseSetting("RateLimiting:Auth:PermitLimit", "10000");
            foreach (var (key, value) in Settings)
            {
                builder.UseSetting(key, value);
            }
        });

        await Services.MigrateDatabaseAsync(CancellationToken.None);
        var seeded = await Services.SeedDatabaseAsync(new SeedSettings(BootstrapAdmin, BootstrapPassword, IsDevelopment: false), CancellationToken.None);
        if (seeded.IsFailure)
        {
            throw new InvalidOperationException(seeded.Error.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>Cliente HTTPS (las cookies son Secure) que no sigue redirecciones.</summary>
    public HttpClient CreateClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    public async Task<TResult> SendAsync<TCommand, TResult>(TCommand command)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICommandHandler<TCommand, TResult>>()
            .HandleAsync(command, CancellationToken.None);
    }

    public async Task<TResult> QueryAsync<TQuery, TResult>(TQuery query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IQueryHandler<TQuery, TResult>>()
            .HandleAsync(query, CancellationToken.None);
    }

    /// <summary>Usuario web con contraseña definitiva (sin cambio obligatorio).</summary>
    public async Task<(Guid Id, string UserName)> CreateUserAsync(WebRole role, Group? group = null)
    {
        var userName = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}"[..20];
        await using var scope = Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<IUserAccountService>()
            .CreateAsync(new NewUserAccount(userName, role, group, UserPassword), CancellationToken.None);
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByIdAsync(created.Value.ToString());
        user!.MustChangePassword = false;
        await manager.UpdateAsync(user);
        return (created.Value, userName);
    }

    public async Task<long> CountAsync(string sql)
    {
        await using var scope = Services.CreateAsyncScope();
        var connectionString = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
            .GetConnectionString(DependencyInjection.ConnectionStringName);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Envía el formulario de login (SSR estático) con su token antiforgery.</summary>
    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string userName, string password)
    {
        var page = await client.GetStringAsync(new Uri("/login", UriKind.Relative));
        var fields = HiddenInputs(page);
        fields["Input.UserName"] = userName;
        fields["Input.Password"] = password;
        return await client.PostAsync(new Uri("/login", UriKind.Relative), new FormUrlEncodedContent(fields));
    }

    public static Dictionary<string, string> HiddenInputs(string html) =>
        HiddenInputRegex().Matches(html)
            .Select(m => (Name: Attribute(m.Value, "name"), Value: WebUtility.HtmlDecode(Attribute(m.Value, "value"))))
            .Where(f => f.Name.Length > 0)
            .GroupBy(f => f.Name)
            .ToDictionary(g => g.Key, g => g.First().Value);

    private static string Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, $"""{name}="([^"]*)" """.TrimEnd());
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    [GeneratedRegex("""<input[^>]*type="hidden"[^>]*>""")]
    private static partial Regex HiddenInputRegex();
}

/// <summary>Host con revalidación de la sesión web en cada request (para probar la desactivación).</summary>
public sealed class FastRevalidationWebTestHost : WebTestHost
{
    protected override IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["Identity:WebSessionRevalidationInterval"] = "00:00:00.001",
    };
}
