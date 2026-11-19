using System.Security.Claims;
using BreakManager.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace BreakManager.Web.Security;

/// <summary>
/// Políticas por rol (HU-03). Toda página y endpoint declara la suya; lo que no declara nada
/// cae en la política por defecto (<see cref="AddBreakManagerPolicies"/>): usuario web autenticado.
/// </summary>
internal static class AuthorizationPolicies
{
    public const string Administrator = nameof(WebRole.Administrator);
    public const string Coordinator = nameof(WebRole.Coordinator);
    public const string Quality = nameof(WebRole.Quality);
    public const string Supervisor = nameof(WebRole.Supervisor);

    /// <summary>Cualquier usuario web autenticado (página de inicio, cambio de contraseña).</summary>
    public const string WebUser = "WebUser";

    /// <summary>Agente con token de dispositivo válido (ADR-0010).</summary>
    public const string Agent = "Agent";

    /// <summary>Agente con token de dispositivo y sesión de operador abierta.</summary>
    public const string AgentSession = "AgentSession";

    public static IServiceCollection AddBreakManagerPolicies(this IServiceCollection services)
    {
        var webUser = new AuthorizationPolicyBuilder(IdentityConstants.ApplicationScheme).RequireAuthenticatedUser().Build();

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(webUser)
            .SetFallbackPolicy(webUser)
            .AddPolicy(WebUser, webUser)
            .AddPolicy(Administrator, Role(WebRole.Administrator))
            .AddPolicy(Coordinator, Role(WebRole.Coordinator))
            .AddPolicy(Quality, Role(WebRole.Quality))
            .AddPolicy(Supervisor, Role(WebRole.Supervisor))
            .AddPolicy(Agent, policy => policy
                .AddAuthenticationSchemes(DeviceTokenAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(BreakManagerClaims.WorkstationId))
            .AddPolicy(AgentSession, policy => policy
                .AddAuthenticationSchemes(DeviceTokenAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(BreakManagerClaims.WorkstationId)
                .RequireClaim(BreakManagerClaims.SessionId));

        return services;
    }

    private static AuthorizationPolicy Role(WebRole role) =>
        new AuthorizationPolicyBuilder(IdentityConstants.ApplicationScheme)
            .RequireAuthenticatedUser()
            .RequireRole(role.ToString())
            .Build();
}

internal static class BreakManagerClaims
{
    public const string WorkstationId = "workstation_id";
    public const string DeviceCredentialId = "device_credential_id";
    public const string WorkstationNumber = "workstation_number";
    public const string SessionId = "session_id";
    public const string OperatorId = "operator_id";
    public const string Group = "group";
    public const string MustChangePassword = "must_change_password";

    public static Guid GetGuid(this ClaimsPrincipal principal, string claimType) =>
        Guid.Parse(principal.FindFirstValue(claimType)
            ?? throw new InvalidOperationException($"Falta el claim {claimType}."));

    /// <summary>ID del usuario web autenticado.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) => principal.GetGuid(ClaimTypes.NameIdentifier);
}
