using BreakManager.Domain.Common;

namespace BreakManager.Domain.Identity;

/// <summary>
/// Token de dispositivo de un puesto (solo su hash). Registra cuándo y desde qué IP se usó por última vez (R21).
/// </summary>
public sealed class DeviceCredential : Entity<Guid>
{
    /// <summary>
    /// La fecha de último uso se escribe como máximo una vez por este intervalo, para no escribir en cada request.
    /// Es un detalle técnico, no un parámetro de negocio.
    /// </summary>
    public static readonly TimeSpan LastSeenResolution = TimeSpan.FromMinutes(1);

    internal DeviceCredential(Guid id, string tokenHash, DateTimeOffset issuedAt)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        TokenHash = tokenHash;
        IssuedAt = issuedAt;
    }

    public string TokenHash { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? LastSeenAt { get; private set; }

    public string? LastSeenIp { get; private set; }

    /// <summary>
    /// Registra un uso del token. Informa si cambió la IP respecto del uso anterior y si hay algo que guardar.
    /// </summary>
    public DeviceSeen RecordSeen(DateTimeOffset now, string ip)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);

        var previousIp = LastSeenIp;
        var ipChanged = previousIp is not null && !string.Equals(previousIp, ip, StringComparison.Ordinal);
        var stale = LastSeenAt is null || now - LastSeenAt.Value >= LastSeenResolution;

        if (!stale && !ipChanged)
        {
            return new DeviceSeen(Modified: false, IpChanged: false, previousIp);
        }

        LastSeenAt = now;
        LastSeenIp = ip;
        return new DeviceSeen(Modified: true, ipChanged, previousIp);
    }

    internal void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}

/// <summary>Resultado de <see cref="DeviceCredential.RecordSeen"/>.</summary>
public sealed record DeviceSeen(bool Modified, bool IpChanged, string? PreviousIp);
