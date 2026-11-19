using BreakManager.Domain.Common;

namespace BreakManager.Domain.Identity;

/// <summary>
/// Operador de monitoreo. Guarda el hash del PIN y el estado de bloqueo por intentos fallidos
/// (HU-01, con bloqueo progresivo: R20 en 05-preguntas-abiertas.md).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Identificador del glosario (02-dominio.md); la solución es solo C#.")]
public sealed class Operator : AggregateRoot<Guid>
{
    public const int MaxFullNameLength = 200;

    private readonly List<DateTimeOffset> _recentLockouts = [];

    private Operator(Guid id, EmployeeNumber employeeNumber, string fullName, Group group, int? homeWorkstationNumber, string pinHash)
        : base(id)
    {
        EmployeeNumber = employeeNumber;
        FullName = fullName;
        Group = group;
        HomeWorkstationNumber = homeWorkstationNumber;
        PinHash = pinHash;
        MustChangePin = true;
        Active = true;
    }

    public EmployeeNumber EmployeeNumber { get; }

    public string FullName { get; private set; }

    public Group Group { get; private set; }

    public int? HomeWorkstationNumber { get; private set; }

    public bool Active { get; private set; }

    public string PinHash { get; private set; }

    /// <summary>El PIN actual es temporal (asignado por el Admin) y debe cambiarse antes de abrir sesión.</summary>
    public bool MustChangePin { get; private set; }

    /// <summary>Intentos fallidos seguidos desde el último login exitoso o el último bloqueo.</summary>
    public int FailedAttempts { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public bool RequiresManualUnlock { get; private set; }

    /// <summary>Instantes de los bloqueos que siguen dentro de la ventana de la política.</summary>
    public IReadOnlyList<DateTimeOffset> RecentLockouts => _recentLockouts.AsReadOnly();

    public static Result<Operator> Create(
        Guid id,
        EmployeeNumber employeeNumber,
        string fullName,
        Group group,
        int? homeWorkstationNumber,
        string temporaryPinHash)
    {
        ArgumentNullException.ThrowIfNull(employeeNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPinHash);

        var validation = Validate(fullName, homeWorkstationNumber);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        return new Operator(id, employeeNumber, fullName.Trim(), group, homeWorkstationNumber, temporaryPinHash);
    }

    public Result Update(string fullName, Group group, int? homeWorkstationNumber)
    {
        var validation = Validate(fullName, homeWorkstationNumber);
        if (validation.IsFailure)
        {
            return validation;
        }

        FullName = fullName.Trim();
        Group = group;
        HomeWorkstationNumber = homeWorkstationNumber;
        return Result.Success();
    }

    public void Deactivate() => Active = false;

    public void Activate() => Active = true;

    public bool IsLockedOut(DateTimeOffset now) => RequiresManualUnlock || LockedUntil > now;

    /// <summary>
    /// Intento de login con un PIN candidato. Un operador inactivo responde como credenciales inválidas
    /// (no se revela su estado). Los intentos durante un bloqueo no cuentan.
    /// </summary>
    public LoginAttemptResult AttemptLogin(string candidatePin, DateTimeOffset now, LockoutPolicy policy, IPinHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(hasher);

        if (!Active)
        {
            return LoginAttemptResult.InvalidCredentials;
        }

        if (IsLockedOut(now))
        {
            return LoginAttemptResult.LockedOut;
        }

        if (candidatePin is not null && hasher.Verify(PinHash, candidatePin))
        {
            FailedAttempts = 0;
            return LoginAttemptResult.Succeeded;
        }

        return RegisterFailure(now, policy);
    }

    /// <summary>Desbloqueo manual del Admin: limpia el bloqueo, los intentos y el historial de bloqueos.</summary>
    public void Unlock()
    {
        FailedAttempts = 0;
        LockedUntil = null;
        RequiresManualUnlock = false;
        _recentLockouts.Clear();
    }

    /// <summary>El Admin asigna un PIN temporal: el operador debe cambiarlo en el próximo login.</summary>
    public void AssignTemporaryPin(string pinHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pinHash);

        PinHash = pinHash;
        MustChangePin = true;
    }

    /// <summary>El operador elige un PIN nuevo, que no puede ser igual al actual.</summary>
    public Result ChangePin(Pin newPin, IPinHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(newPin);
        ArgumentNullException.ThrowIfNull(hasher);

        if (hasher.Verify(PinHash, newPin.Value))
        {
            return Result.Failure(PinErrors.SameAsCurrent);
        }

        PinHash = hasher.Hash(newPin);
        MustChangePin = false;
        return Result.Success();
    }

    private LoginAttemptResult RegisterFailure(DateTimeOffset now, LockoutPolicy policy)
    {
        FailedAttempts++;
        if (FailedAttempts < policy.Attempts)
        {
            return LoginAttemptResult.InvalidCredentials;
        }

        FailedAttempts = 0;
        LockedUntil = now + policy.Duration;
        _recentLockouts.RemoveAll(lockout => lockout <= now - policy.Window);
        _recentLockouts.Add(now);

        if (_recentLockouts.Count >= policy.LockoutsBeforeManualUnlock)
        {
            RequiresManualUnlock = true;
            return LoginAttemptResult.LockedOutUntilManualUnlock;
        }

        return LoginAttemptResult.LockedOut;
    }

    private static Result Validate(string fullName, int? homeWorkstationNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure(OperatorErrors.FullNameRequired);
        }

        if (fullName.Trim().Length > MaxFullNameLength)
        {
            return Result.Failure(OperatorErrors.FullNameTooLong);
        }

        if (homeWorkstationNumber is { } number && !WorkstationNumber.IsValid(number))
        {
            return Result.Failure(OperatorErrors.HomeWorkstationOutOfRange);
        }

        return Result.Success();
    }
}

/// <summary>Resultado de un intento de login. Sin detalle para credenciales inválidas (no se revela si el legajo existe).</summary>
public enum LoginAttemptResult
{
    Succeeded,
    InvalidCredentials,

    /// <summary>Bloqueado: por este intento o por un bloqueo vigente.</summary>
    LockedOut,

    /// <summary>Este intento provocó un bloqueo que solo levanta el Admin.</summary>
    LockedOutUntilManualUnlock,
}

public static class OperatorErrors
{
    public static readonly Error FullNameRequired = new("Operator.FullNameRequired", "El nombre del operador es obligatorio.");

    public static readonly Error FullNameTooLong = new(
        "Operator.FullNameTooLong",
        $"El nombre del operador no puede superar {Operator.MaxFullNameLength} caracteres.");

    public static readonly Error HomeWorkstationOutOfRange = new(
        "Operator.HomeWorkstationOutOfRange",
        $"El puesto habitual debe estar entre {WorkstationNumber.Min} y {WorkstationNumber.Max}.");
}
