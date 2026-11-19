namespace BreakManager.Domain.Identity;

/// <summary>
/// Hash del PIN (puerto del dominio; Infrastructure lo implementa con PBKDF2, ADR-0011).
/// </summary>
public interface IPinHasher
{
    string Hash(Pin pin);

    /// <summary>Compara un PIN candidato (sin validar) con un hash guardado.</summary>
    bool Verify(string pinHash, string candidate);
}
