using System.IO.Compression;
using Microsoft.AspNetCore.Identity;

namespace BreakManager.Infrastructure.Identity;

/// <summary>
/// Rechaza contraseñas que aparecen en la lista embebida de contraseñas comunes (ADR-0011).
/// No usa internet; la comparación no distingue mayúsculas de minúsculas.
/// </summary>
internal sealed class CommonPasswordValidator : IPasswordValidator<ApplicationUser>
{
    private const string ResourceName = "BreakManager.Infrastructure.Security.Resources.common-passwords.txt.gz";

    private static readonly Lazy<HashSet<string>> CommonPasswords = new(Load);

    public static bool IsCommon(string password) => CommonPasswords.Value.Contains(password);

    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        if (password is not null && IsCommon(password))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "CommonPassword",
                Description = "La contraseña es demasiado común. Elegí otra.",
            }));
        }

        return Task.FromResult(IdentityResult.Success);
    }

    private static HashSet<string> Load()
    {
        using var resource = typeof(CommonPasswordValidator).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Falta el recurso embebido {ResourceName}.");
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);

        var passwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                passwords.Add(line);
            }
        }

        return passwords;
    }
}
