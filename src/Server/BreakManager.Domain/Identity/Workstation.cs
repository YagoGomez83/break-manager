using BreakManager.Domain.Common;

namespace BreakManager.Domain.Identity;

/// <summary>
/// Puesto físico (PC) de la sala. Se autentica con un token de dispositivo que obtiene al enrolarse (HU-04, ADR-0010).
/// Guarda el historial de credenciales: solo una está vigente a la vez.
/// </summary>
public sealed class Workstation : AggregateRoot<Guid>
{
    public const int MaxHostnameLength = 253;

    private readonly List<DeviceCredential> _credentials = [];

    private Workstation(Guid id, int number, int row, int column, string hostname)
        : base(id)
    {
        Number = number;
        Row = row;
        Column = column;
        Hostname = hostname;
        ServiceStatus = ServiceStatus.InService;
    }

    public int Number { get; }

    /// <summary>Fila de la ubicación física en la sala (desde 1).</summary>
    public int Row { get; private set; }

    /// <summary>Columna de la ubicación física en la sala (desde 1).</summary>
    public int Column { get; private set; }

    public string Hostname { get; private set; }

    /// <summary>En la Fase 2 siempre <see cref="ServiceStatus.InService"/>; HU-06 lo cambia en la Fase 3.</summary>
    public ServiceStatus ServiceStatus { get; private set; }

    public bool Revoked { get; private set; }

    public IReadOnlyList<DeviceCredential> Credentials => _credentials.AsReadOnly();

    public DeviceCredential? ActiveCredential => _credentials.SingleOrDefault(c => c.RevokedAt is null);

    public static Result<Workstation> Create(Guid id, int number, int row, int column, string hostname)
    {
        if (!WorkstationNumber.IsValid(number))
        {
            return WorkstationErrors.NumberOutOfRange;
        }

        var validation = Validate(row, column, hostname);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        return new Workstation(id, number, row, column, hostname.Trim());
    }

    public Result Update(int row, int column, string hostname)
    {
        var validation = Validate(row, column, hostname);
        if (validation.IsFailure)
        {
            return validation;
        }

        Row = row;
        Column = column;
        Hostname = hostname.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Emite una credencial nueva (al canjear un código de enrolamiento). La anterior, si había, queda revocada.
    /// </summary>
    public DeviceCredential Enroll(Guid credentialId, string tokenHash, DateTimeOffset now)
    {
        ActiveCredential?.Revoke(now);

        var credential = new DeviceCredential(credentialId, tokenHash, now);
        _credentials.Add(credential);
        Revoked = false;
        return credential;
    }

    /// <summary>Revocación del Admin: el token vigente deja de valer de inmediato.</summary>
    public void Revoke(DateTimeOffset now)
    {
        ActiveCredential?.Revoke(now);
        Revoked = true;
    }

    private static Result Validate(int row, int column, string hostname)
    {
        if (row < 1 || column < 1)
        {
            return Result.Failure(WorkstationErrors.LocationInvalid);
        }

        if (string.IsNullOrWhiteSpace(hostname) || hostname.Trim().Length > MaxHostnameLength)
        {
            return Result.Failure(WorkstationErrors.HostnameInvalid);
        }

        return Result.Success();
    }
}

public enum ServiceStatus
{
    InService,
    OutOfService,
}

public static class WorkstationErrors
{
    public static readonly Error NumberOutOfRange = new(
        "Workstation.NumberOutOfRange",
        $"El número de puesto debe estar entre {WorkstationNumber.Min} y {WorkstationNumber.Max}.");

    public static readonly Error LocationInvalid = new("Workstation.LocationInvalid", "La fila y la columna deben ser 1 o más.");

    public static readonly Error HostnameInvalid = new(
        "Workstation.HostnameInvalid",
        $"El hostname es obligatorio y no puede superar {Workstation.MaxHostnameLength} caracteres.");
}
