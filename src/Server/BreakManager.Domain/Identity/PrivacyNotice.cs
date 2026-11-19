using BreakManager.Domain.Common;

namespace BreakManager.Domain.Identity;

/// <summary>
/// Versión publicada del aviso de privacidad (HU-02, ADR-0006). Inmutable: publicar crea una versión nueva.
/// </summary>
public sealed class PrivacyNotice : AggregateRoot<Guid>
{
    public const int MaxTextLength = 20_000;

    private PrivacyNotice(Guid id, int version, string text, DateTimeOffset publishedAt, Guid publishedByUserId)
        : base(id)
    {
        Version = version;
        Text = text;
        PublishedAt = publishedAt;
        PublishedByUserId = publishedByUserId;
    }

    public int Version { get; }

    public string Text { get; }

    public DateTimeOffset PublishedAt { get; }

    public Guid PublishedByUserId { get; }

    /// <param name="currentVersion">Versión vigente, o <c>null</c> si todavía no hay ninguna.</param>
    public static Result<PrivacyNotice> Publish(
        Guid id,
        int? currentVersion,
        string text,
        DateTimeOffset now,
        Guid publishedByUserId)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return PrivacyNoticeErrors.TextRequired;
        }

        var trimmed = text.Trim();
        if (trimmed.Length > MaxTextLength)
        {
            return PrivacyNoticeErrors.TextTooLong;
        }

        return new PrivacyNotice(id, (currentVersion ?? 0) + 1, trimmed, now, publishedByUserId);
    }

    /// <summary>Registra que el operador aceptó esta versión, en el puesto y a la hora del servidor.</summary>
    public NoticeAcceptance Accept(Guid acceptanceId, Guid operatorId, Guid workstationId, DateTimeOffset now) =>
        new(acceptanceId, operatorId, Version, workstationId, now);
}

/// <summary>Aceptación de una versión del aviso. Solo se agrega; nunca se modifica.</summary>
public sealed class NoticeAcceptance : Entity<Guid>
{
    internal NoticeAcceptance(Guid id, Guid operatorId, int noticeVersion, Guid workstationId, DateTimeOffset acceptedAt)
        : base(id)
    {
        OperatorId = operatorId;
        NoticeVersion = noticeVersion;
        WorkstationId = workstationId;
        AcceptedAt = acceptedAt;
    }

    public Guid OperatorId { get; }

    public int NoticeVersion { get; }

    public Guid WorkstationId { get; }

    public DateTimeOffset AcceptedAt { get; }
}

public static class PrivacyNoticeErrors
{
    public static readonly Error TextRequired = new("PrivacyNotice.TextRequired", "El texto del aviso es obligatorio.");

    public static readonly Error TextTooLong = new(
        "PrivacyNotice.TextTooLong",
        $"El texto del aviso no puede superar {PrivacyNotice.MaxTextLength} caracteres.");

    public static readonly Error NonePublished = new(
        "PrivacyNotice.NonePublished",
        "El sistema no está habilitado: falta publicar el aviso de privacidad.");
}
