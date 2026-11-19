using BreakManager.Application.Abstractions;
using BreakManager.Application.Common;
using BreakManager.Application.Common.Security;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;

namespace BreakManager.Application.Identity.Administration;

/// <summary>Publica una versión nueva del aviso de privacidad (HU-02). Devuelve el número de versión.</summary>
public sealed record PublishPrivacyNoticeCommand(string Text, Guid ActorUserId);

internal sealed class PublishPrivacyNoticeHandler(
    IPrivacyNoticeRepository notices,
    ISecurityEventLog securityEvents,
    IUnitOfWork unitOfWork,
    TimeProvider time) : ICommandHandler<PublishPrivacyNoticeCommand, Result<int>>
{
    public async Task<Result<int>> HandleAsync(PublishPrivacyNoticeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = time.GetUtcNow();
        var current = await notices.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var published = PrivacyNotice.Publish(Guid.CreateVersion7(now), current?.Version, command.Text, now, command.ActorUserId);
        if (published.IsFailure)
        {
            return published.Error;
        }

        notices.Add(published.Value);
        securityEvents.Record(new SecurityEvent(
            Guid.CreateVersion7(now),
            SecurityEventType.PrivacyNoticePublished,
            now,
            SecurityActorType.User,
            command.ActorUserId,
            WorkstationId: null,
            new Dictionary<string, string> { ["version"] = published.Value.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) }));

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return published.Value.Version;
    }
}
