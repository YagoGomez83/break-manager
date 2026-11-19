using System.Net;
using BreakManager.Application.Identity;
using BreakManager.Domain.Identity;
using BreakManager.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BreakManager.Web.Tests;

public sealed class AuthorizationTests(WebTestHost host) : IClassFixture<WebTestHost>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Endpoints del framework de Blazor (hub del circuito y descarga de recursos) que no admiten metadata propia;
    /// los cubre la política por defecto (FallbackPolicy: usuario web autenticado).
    /// </summary>
    private static readonly string[] FrameworkRoutePrefixes = ["_blazor", "_framework"];

    /// <summary>
    /// Fallback de archivos que agrega MapStaticAssets sin aplicar convenciones. Lo cubre la FallbackPolicy:
    /// ver <see cref="UnknownFiles_RequireAuthentication"/>.
    /// </summary>
    private const string StaticFileFallbackPattern = "{**path:file}";

    private static readonly string[] AdminPages =
        ["/admin/puestos", "/admin/operadores", "/admin/usuarios", "/admin/avisos", "/admin/sesiones"];

    [Fact]
    public void EveryEndpoint_DeclaresAPolicyOrAllowAnonymous()
    {
        var endpoints = host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        var undeclared = endpoints
            .Where(e => !FrameworkRoutePrefixes.Any(p => (e.RoutePattern.RawText ?? string.Empty).TrimStart('/').StartsWith(p, StringComparison.Ordinal)))
            .Where(e => e.RoutePattern.RawText != StaticFileFallbackPattern)
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null && e.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)
            .Select(e => $"{e.RoutePattern.RawText} ({e.DisplayName})")
            .ToList();

        Assert.True(undeclared.Count == 0, "Sin política ni AllowAnonymous: " + string.Join(", ", undeclared));
    }

    [Fact]
    public async Task UnknownFiles_RequireAuthentication()
    {
        using var client = host.CreateClient();

        var response = await client.GetAsync(new Uri("/no-existe.txt", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location!.PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthChecks_AreAnonymous()
    {
        using var client = host.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/health/live", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/health/ready", UriKind.Relative), Ct)).StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/admin/puestos")]
    [InlineData("/cuenta/cambiar-contrasena")]
    public async Task AnonymousUsers_AreSentToLogin(string path)
    {
        using var client = host.CreateClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location!.PathAndQuery, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(WebRole.Supervisor)]
    [InlineData(WebRole.Coordinator)]
    [InlineData(WebRole.Quality)]
    public async Task NonAdminRoles_GetForbiddenOnAdminPages(WebRole role)
    {
        var (_, userName) = await host.CreateUserAsync(role, role == WebRole.Supervisor ? Group.C : null);
        using var client = host.CreateClient();
        await WebTestHost.LoginAsync(client, userName, WebTestHost.UserPassword);

        foreach (var page in AdminPages)
        {
            var response = await client.GetAsync(new Uri(page, UriKind.Relative), Ct);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/acceso-denegado", response.Headers.Location!.PathAndQuery, StringComparison.Ordinal);

            var denied = await client.GetAsync(response.Headers.Location, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task Administrators_CanOpenEveryAdminPage()
    {
        var (_, userName) = await host.CreateUserAsync(WebRole.Administrator);
        using var client = host.CreateClient();
        await WebTestHost.LoginAsync(client, userName, WebTestHost.UserPassword);

        foreach (var page in AdminPages)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri(page, UriKind.Relative), Ct)).StatusCode);
        }
    }
}

public sealed class WebLoginTests(WebTestHost host) : IClassFixture<WebTestHost>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WrongPassword_ShowsAGenericError_AndRecordsUserLoginFailed()
    {
        var (_, userName) = await host.CreateUserAsync(WebRole.Quality);
        using var client = host.CreateClient();
        var before = await host.CountAsync("SELECT count(*) FROM security_events WHERE type = 'UserLoginFailed'");

        var response = await WebTestHost.LoginAsync(client, userName, "no-es-la-contraseña-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Usuario o contraseña incorrectos.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct)), StringComparison.Ordinal);
        Assert.Equal(before + 1, await host.CountAsync("SELECT count(*) FROM security_events WHERE type = 'UserLoginFailed'"));
    }

    [Fact]
    public async Task RepeatedFailures_LockTheAccount()
    {
        var (_, userName) = await host.CreateUserAsync(WebRole.Quality);
        using var client = host.CreateClient();

        for (var i = 0; i < new IdentityParameters().WebLockoutAttempts; i++)
        {
            await WebTestHost.LoginAsync(client, userName, "no-es-la-contraseña-1");
        }

        var response = await WebTestHost.LoginAsync(client, userName, WebTestHost.UserPassword);

        Assert.Contains("bloqueada", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct)), StringComparison.Ordinal);
        Assert.True(await host.CountAsync("SELECT count(*) FROM security_events WHERE type = 'UserLockedOut'") >= 1);
    }

    [Fact]
    public async Task TemporaryPassword_ForcesThePasswordChange()
    {
        using var client = host.CreateClient();

        var login = await WebTestHost.LoginAsync(client, WebTestHost.BootstrapAdmin, WebTestHost.BootstrapPassword);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var home = await client.GetAsync(new Uri("/admin/puestos", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.Equal("/cuenta/cambiar-contrasena", home.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task DeactivatedUsers_CannotSignIn()
    {
        var (id, userName) = await host.CreateUserAsync(WebRole.Coordinator);
        var admin = await host.CreateUserAsync(WebRole.Administrator);
        await host.SendAsync<BreakManager.Application.Identity.Administration.SetUserActiveCommand, BreakManager.Domain.Common.Result>(
            new(id, false, admin.Id));
        using var client = host.CreateClient();

        var response = await WebTestHost.LoginAsync(client, userName, WebTestHost.UserPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Usuario o contraseña incorrectos.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct)), StringComparison.Ordinal);
    }
}

public sealed class WebSessionRevalidationTests(FastRevalidationWebTestHost host) : IClassFixture<FastRevalidationWebTestHost>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeactivatingAUser_EndsTheirSessionOnTheNextRevalidation()
    {
        var (id, userName) = await host.CreateUserAsync(WebRole.Coordinator);
        var admin = await host.CreateUserAsync(WebRole.Administrator);
        using var client = host.CreateClient();
        await WebTestHost.LoginAsync(client, userName, WebTestHost.UserPassword);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/", UriKind.Relative), Ct)).StatusCode);

        await host.SendAsync<BreakManager.Application.Identity.Administration.SetUserActiveCommand, BreakManager.Domain.Common.Result>(
            new(id, false, admin.Id));
        await Task.Delay(50, Ct);

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location!.PathAndQuery, StringComparison.Ordinal);
    }
}
