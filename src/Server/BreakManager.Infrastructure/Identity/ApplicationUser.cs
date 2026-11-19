using BreakManager.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace BreakManager.Infrastructure.Identity;

/// <summary>
/// Usuario web de ASP.NET Core Identity (ADR-0011). Es público porque Web lo usa en SignInManager.
/// El rol se guarda como rol de Identity; el grupo solo lo tiene el supervisor.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Group? Group { get; set; }

    /// <summary>La contraseña actual es temporal (alta, reseteo, admin inicial): hay que cambiarla al ingresar.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Un usuario desactivado no puede ingresar y su sesión se corta en la próxima revalidación.</summary>
    public bool Active { get; set; } = true;
}
