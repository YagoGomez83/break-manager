namespace BreakManager.Application.Common;

/// <summary>
/// Genera secretos aleatorios y sus hashes (ADR-0010). En la base solo se guarda el hash.
/// </summary>
public interface ITokenService
{
    /// <summary>Token opaco de 256 bits en Base64url (dispositivo, sesión, ticket).</summary>
    string GenerateToken();

    /// <summary>Código de enrolamiento legible (sin caracteres ambiguos).</summary>
    string GenerateEnrollmentCode();

    /// <summary>Hash SHA-256 del secreto, en hexadecimal. Los códigos de enrolamiento se normalizan antes.</summary>
    string Hash(string secret);
}
