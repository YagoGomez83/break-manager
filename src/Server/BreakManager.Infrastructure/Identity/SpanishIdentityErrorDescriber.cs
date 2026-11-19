using System.Globalization;
using Microsoft.AspNetCore.Identity;

namespace BreakManager.Infrastructure.Identity;

/// <summary>
/// Mensajes de ASP.NET Core Identity en español (los textos para el usuario van en español, ADR-0007).
/// </summary>
internal sealed class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(nameof(DefaultError), "Ocurrió un error inesperado.");

    public override IdentityError ConcurrencyFailure() =>
        Error(nameof(ConcurrencyFailure), "Los datos cambiaron mientras se guardaban. Volvé a intentar.");

    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "La contraseña es incorrecta.");

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), "El nombre de usuario solo puede tener letras, números y . _ -");

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), "Ya existe un usuario con ese nombre.");

    public override IdentityError UserAlreadyHasPassword() =>
        Error(nameof(UserAlreadyHasPassword), "El usuario ya tiene contraseña.");

    public override IdentityError UserLockoutNotEnabled() =>
        Error(nameof(UserLockoutNotEnabled), "El bloqueo no está habilitado para este usuario.");

    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole), "El usuario ya tiene ese rol.");

    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole), "El usuario no tiene ese rol.");

    public override IdentityError PasswordTooShort(int length) =>
        Error(
            nameof(PasswordTooShort),
            string.Create(CultureInfo.InvariantCulture, $"La contraseña tiene que tener al menos {length} caracteres."));

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(
            nameof(PasswordRequiresUniqueChars),
            string.Create(CultureInfo.InvariantCulture, $"La contraseña tiene que tener al menos {uniqueChars} caracteres distintos."));

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
