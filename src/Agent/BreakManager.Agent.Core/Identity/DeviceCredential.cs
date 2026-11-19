namespace BreakManager.Agent.Core.Identity;

/// <summary>
/// Credencial del puesto obtenida al enrolarse (ADR-0010). En Windows se guarda cifrada con DPAPI.
/// </summary>
public sealed record DeviceCredential(Uri ServerUrl, string DeviceToken, int WorkstationNumber)
{
    public override string ToString() => $"DeviceCredential {{ ServerUrl = {ServerUrl}, WorkstationNumber = {WorkstationNumber} }}";
}

/// <summary>Almacén de la credencial del puesto (Agent.Windows lo implementa con DPAPI).</summary>
public interface IDeviceCredentialStore
{
    Task<DeviceCredential?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(DeviceCredential credential, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
