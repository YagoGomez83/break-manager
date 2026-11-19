using BreakManager.Application.Abstractions;
using BreakManager.Application.Common;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;

namespace BreakManager.Application.Identity.Workstations;

public sealed record CreateWorkstationCommand(int Number, int Row, int Column, string Hostname);

internal sealed class CreateWorkstationHandler(IWorkstationRepository workstations, IUnitOfWork unitOfWork, TimeProvider time)
    : ICommandHandler<CreateWorkstationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(CreateWorkstationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await workstations.NumberExistsAsync(command.Number, cancellationToken).ConfigureAwait(false))
        {
            return IdentityErrors.WorkstationNumberTaken;
        }

        var created = Workstation.Create(
            Guid.CreateVersion7(time.GetUtcNow()), command.Number, command.Row, command.Column, command.Hostname);
        if (created.IsFailure)
        {
            return created.Error;
        }

        workstations.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return created.Value.Id;
    }
}

public sealed record UpdateWorkstationCommand(Guid WorkstationId, int Row, int Column, string Hostname);

internal sealed class UpdateWorkstationHandler(IWorkstationRepository workstations, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateWorkstationCommand, Result>
{
    public async Task<Result> HandleAsync(UpdateWorkstationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var workstation = await workstations.GetByIdAsync(command.WorkstationId, cancellationToken).ConfigureAwait(false);
        if (workstation is null)
        {
            return Result.Failure(IdentityErrors.WorkstationNotFound);
        }

        var updated = workstation.Update(command.Row, command.Column, command.Hostname);
        if (updated.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return updated;
    }
}
