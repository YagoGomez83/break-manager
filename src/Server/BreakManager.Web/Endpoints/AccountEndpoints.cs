using BreakManager.Infrastructure.Identity;
using BreakManager.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BreakManager.Web.Endpoints;

internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // El parámetro de formulario hace que se valide el token antiforgery del POST.
        endpoints.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager, [FromForm] string? origin) =>
        {
            _ = origin;
            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect("/login");
        }).RequireAuthorization(AuthorizationPolicies.WebUser);

        return endpoints;
    }
}
