using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BreakManager.Agent.Core.Identity;

namespace BreakManager.Agent.Windows.Identity;

/// <summary>
/// Guarda la credencial del puesto cifrada con DPAPI, alcance <see cref="DataProtectionScope.LocalMachine"/>
/// (hay una instancia del agente por PC; ADR-0010). El token de sesión nunca se guarda: vive en memoria.
/// </summary>
internal sealed class DpapiCredentialStore : IDeviceCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BreakManager.Agent.DeviceCredential.v1");

    private readonly string _path;

    public DpapiCredentialStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BreakManager", "Agent", "credential.bin"))
    {
    }

    public DpapiCredentialStore(string path)
    {
        _path = path;
    }

    public async Task<DeviceCredential?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            var encrypted = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
            var json = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.LocalMachine);
            return JsonSerializer.Deserialize<DeviceCredential>(json);
        }
        catch (CryptographicException)
        {
            // Archivo copiado de otra PC o dañado: se trata como no enrolado.
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(DeviceCredential credential, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.SerializeToUtf8Bytes(credential);
        var encrypted = ProtectedData.Protect(json, Entropy, DataProtectionScope.LocalMachine);
        await File.WriteAllBytesAsync(_path, encrypted, cancellationToken).ConfigureAwait(false);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }

        return Task.CompletedTask;
    }
}
