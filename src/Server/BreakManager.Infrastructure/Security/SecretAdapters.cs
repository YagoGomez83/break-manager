using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using BreakManager.Application.Common;
using BreakManager.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace BreakManager.Infrastructure.Security;

/// <summary>Tokens opacos y su hash SHA-256 (ADR-0010).</summary>
internal sealed class TokenService : ITokenService
{
    // Sin caracteres ambiguos (0/O, 1/I/L). RandomNumberGenerator.GetString elige sin sesgo.
    private const string EnrollmentAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int EnrollmentCodeLength = 10;
    private const int TokenBytes = 32;

    public string GenerateToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

    public string GenerateEnrollmentCode() => RandomNumberGenerator.GetString(EnrollmentAlphabet, EnrollmentCodeLength);

    public string Hash(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }
}

/// <summary>PIN con PBKDF2, usando el hasher de contraseñas de ASP.NET Core Identity (ADR-0011).</summary>
internal sealed class Pbkdf2PinHasher : IPinHasher
{
    private readonly PasswordHasher<Operator> _hasher = new();

    public string Hash(Pin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        return _hasher.HashPassword(null!, pin.Value);
    }

    public bool Verify(string pinHash, string candidate) =>
        !string.IsNullOrEmpty(pinHash)
        && !string.IsNullOrEmpty(candidate)
        && _hasher.VerifyHashedPassword(null!, pinHash, candidate) != PasswordVerificationResult.Failed;
}
