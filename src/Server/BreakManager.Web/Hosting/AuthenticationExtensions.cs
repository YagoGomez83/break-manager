using System.Threading.RateLimiting;
using BreakManager.Application.Identity;
using BreakManager.Infrastructure.Identity;
using BreakManager.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BreakManager.Web.Hosting;

/// <summary>
/// Autenticación web (cookie de Identity), del agente (DeviceToken), políticas y rate limiting (HU-03, ADR-0010, ADR-0011).
/// </summary>
internal static class AuthenticationExtensions
{
    public const string AuthRateLimitPolicy = "auth";
    public const string LoginPath = "/login";
    public const string ChangePasswordPath = "/cuenta/cambiar-contrasena";

    public static WebApplicationBuilder AddBreakManagerAuthentication(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddOptions<IdentityParameters>()
            .BindConfiguration(IdentityParameters.SectionName)
            .ValidateOnStart();

        new IdentityBuilder(typeof(ApplicationUser), typeof(IdentityRole<Guid>), services)
            .AddSignInManager()
            .AddClaimsPrincipalFactory<BreakManagerClaimsPrincipalFactory>();
        services.AddScoped<IUserConfirmation<ApplicationUser>, ActiveUserConfirmation>();
        services.Configure<IdentityOptions>(options => options.SignIn.RequireConfirmedAccount = true);

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, DeviceTokenAuthenticationHandler>(DeviceTokenAuthenticationHandler.SchemeName, null);

        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<IdentityParameters>>((cookie, parameters) =>
            {
                cookie.Cookie.Name = "BreakManager.Auth";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.Cookie.SameSite = SameSiteMode.Strict;
                cookie.ExpireTimeSpan = parameters.Value.WebSessionLifetime;
                cookie.SlidingExpiration = true;
                cookie.LoginPath = LoginPath;
                cookie.LogoutPath = "/logout";
                cookie.AccessDeniedPath = "/acceso-denegado";
            });

        services.AddOptions<SecurityStampValidatorOptions>()
            .Configure<IOptions<IdentityParameters>>((validator, parameters) =>
                validator.ValidationInterval = parameters.Value.WebSessionRevalidationInterval);

        services.AddBreakManagerPolicies();
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        AddRateLimiting(builder);
        return builder;
    }

    /// <summary>
    /// Mientras la contraseña sea temporal, el usuario web solo puede cambiarla o salir.
    /// </summary>
    public static IApplicationBuilder UseMustChangePassword(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            var mustChange = context.User.HasClaim(BreakManagerClaims.MustChangePassword, "true");
            var allowed = path.StartsWithSegments(ChangePasswordPath, StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/logout", StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/api/agent", StringComparison.OrdinalIgnoreCase)
                || Path.HasExtension(path.Value);

            if (mustChange && !allowed)
            {
                context.Response.Redirect(ChangePasswordPath);
                return;
            }

            await next(context).ConfigureAwait(false);
        });

    private static void AddRateLimiting(WebApplicationBuilder builder)
    {
        // Límite técnico contra fuerza bruta (no es un umbral de negocio); configurable en RateLimiting:Auth.
        var permitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 20);
        var window = builder.Configuration.GetValue("RateLimiting:Auth:Window", TimeSpan.FromMinutes(1));

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0 }));
        });
    }
}
