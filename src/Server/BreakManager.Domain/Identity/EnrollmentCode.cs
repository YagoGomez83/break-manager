using BreakManager.Domain.Common;

namespace BreakManager.Domain.Identity;

/// <summary>
/// Código de enrolamiento de un solo uso para un puesto (HU-04, ADR-0010). Solo se guarda su hash.
/// </summary>
public sealed class EnrollmentCode : AggregateRoot<Guid>
{
    private EnrollmentCode(Guid id, Guid workstationId, string codeHash, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
        : base(id)
    {
        WorkstationId = workstationId;
        CodeHash = codeHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    public Guid WorkstationId { get; }

    public string CodeHash { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? RedeemedAt { get; private set; }

    /// <summary>Se invalida cuando el Admin genera otro código para el mismo puesto.</summary>
    public DateTimeOffset? InvalidatedAt { get; private set; }

    public static EnrollmentCode Issue(Guid id, Guid workstationId, string codeHash, DateTimeOffset now, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        return new EnrollmentCode(id, workstationId, codeHash, now, now + lifetime);
    }

    public bool IsUsable(DateTimeOffset now) => RedeemedAt is null && InvalidatedAt is null && now < ExpiresAt;

    public Result Redeem(DateTimeOffset now)
    {
        if (RedeemedAt is not null)
        {
            return Result.Failure(EnrollmentCodeErrors.AlreadyUsed);
        }

        if (InvalidatedAt is not null)
        {
            return Result.Failure(EnrollmentCodeErrors.Invalidated);
        }

        if (now >= ExpiresAt)
        {
            return Result.Failure(EnrollmentCodeErrors.Expired);
        }

        RedeemedAt = now;
        return Result.Success();
    }

    public void Invalidate(DateTimeOffset now)
    {
        if (RedeemedAt is null)
        {
            InvalidatedAt ??= now;
        }
    }
}

/// <summary>
/// Causas internas (para auditoría y tests). Al agente se le responde siempre con un mensaje genérico.
/// </summary>
public static class EnrollmentCodeErrors
{
    public static readonly Error AlreadyUsed = new("EnrollmentCode.AlreadyUsed", "El código de enrolamiento ya se usó.");

    public static readonly Error Expired = new("EnrollmentCode.Expired", "El código de enrolamiento venció.");

    public static readonly Error Invalidated = new(
        "EnrollmentCode.Invalidated",
        "El código de enrolamiento fue reemplazado por otro.");
}
