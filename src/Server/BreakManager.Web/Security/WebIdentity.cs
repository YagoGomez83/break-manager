using System.Security.Claims;
using BreakManager.Application.Identity;
using BreakManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BreakManager.Web.Security;

/// <summary>Agrega a la cookie el grupo del supervisor y la marca de contraseña temporal.</summary>
internal sealed class BreakManagerClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identity = await base.GenerateClaimsAsync(user).ConfigureAwait(false);
        if (user.Group is { } group)
        {
            identity.AddClaim(new Claim(BreakManagerClaims.Group, group.ToString()));
        }

        if (user.MustChangePassword)
        {
            identity.AddClaim(new Claim(BreakManagerClaims.MustChangePassword, "true"));
        }

        return identity;
    }
}

/// <summary>
/// Solo los usuarios activos pueden ingresar (se usa con <c>SignIn.RequireConfirmedAccount</c>).
/// </summary>
internal sealed class ActiveUserConfirmation : IUserConfirmation<ApplicationUser>
{
    public Task<bool> IsConfirmedAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.Active);
    }
}

/// <summary>
/// Revalida los circuitos de Blazor Server cada <c>WebSessionRevalidationInterval</c>: si el usuario
/// se desactivó o cambió su security stamp, la sesión se corta (HU-03, ADR-0011).
/// </summary>
internal sealed class IdentityRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> identityOptions,
    IOptions<IdentityParameters> parameters)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => parameters.Value.WebSessionRevalidationInterval;

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authenticationState);

        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(authenticationState.User).ConfigureAwait(false);
        if (user is null || !user.Active)
        {
            return false;
        }

        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        var principalStamp = authenticationState.User.FindFirstValue(identityOptions.Value.ClaimsIdentity.SecurityStampClaimType);
        var userStamp = await userManager.GetSecurityStampAsync(user).ConfigureAwait(false);
        return principalStamp == userStamp;
    }
}
