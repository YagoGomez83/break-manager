using BreakManager.Application.Abstractions;
using BreakManager.Application.Common;
using BreakManager.Application.Common.Security;
using BreakManager.Domain.Common;
using BreakManager.Domain.Identity;

namespace BreakManager.Application.Identity.Administration;

/// <summary>Alta de operador con un PIN temporal (HU-05). El operador lo cambia en el primer login.</summary>
public sealed record CreateOperatorCommand(string EmployeeNumber, string FullName, Group Group, int? HomeWorkstationNumber, string TemporaryPin)
{
    public override string ToString() => $"CreateOperatorCommand {{ Group = {Group}, HomeWorkstationNumber = {HomeWorkstationNumber} }}";
}

internal sealed class CreateOperatorHandler(IOperatorRepository operators, IPinHasher pinHasher, IUnitOfWork unitOfWork, TimeProvider time)
    : ICommandHandler<CreateOperatorCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(CreateOperatorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employeeNumber = EmployeeNumber.Create(command.EmployeeNumber);
        if (employeeNumber.IsFailure)
        {
            return employeeNumber.Error;
        }

        var pin = Pin.Create(command.TemporaryPin, employeeNumber.Value);
        if (pin.IsFailure)
        {
            return pin.Error;
        }

        if (await operators.ExistsAsync(employeeNumber.Value, cancellationToken).ConfigureAwait(false))
        {
            return IdentityErrors.EmployeeNumberTaken;
        }

        var created = Operator.Create(
            Guid.CreateVersion7(time.GetUtcNow()),
            employeeNumber.Value,
            command.FullName,
            command.Group,
            command.HomeWorkstationNumber,
            pinHasher.Hash(pin.Value));
        if (created.IsFailure)
        {
            return created.Error;
        }

        operators.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return created.Value.Id;
    }
}

public sealed record UpdateOperatorCommand(Guid OperatorId, string FullName, Group Group, int? HomeWorkstationNumber);

internal sealed class UpdateOperatorHandler(IOperatorRepository operators, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateOperatorCommand, Result>
{
    public async Task<Result> HandleAsync(UpdateOperatorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var op = await operators.GetByIdAsync(command.OperatorId, cancellationToken).ConfigureAwait(false);
        if (op is null)
        {
            return Result.Failure(IdentityErrors.OperatorNotFound);
        }

        var updated = op.Update(command.FullName, command.Group, command.HomeWorkstationNumber);
        if (updated.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return updated;
    }
}

/// <summary>Desactivar o reactivar un operador. Nunca se borra.</summary>
public sealed record SetOperatorActiveCommand(Guid OperatorId, bool Active);

internal sealed class SetOperatorActiveHandler(IOperatorRepository operators, IUnitOfWork unitOfWork)
    : ICommandHandler<SetOperatorActiveCommand, Result>
{
    public async Task<Result> HandleAsync(SetOperatorActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var op = await operators.GetByIdAsync(command.OperatorId, cancellationToken).ConfigureAwait(false);
        if (op is null)
        {
            return Result.Failure(IdentityErrors.OperatorNotFound);
        }

        if (command.Active)
        {
            op.Activate();
        }
        else
        {
            op.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}

public sealed record AssignTemporaryPinCommand(Guid OperatorId, string TemporaryPin, Guid ActorUserId)
{
    public override string ToString() => $"AssignTemporaryPinCommand {{ OperatorId = {OperatorId}, ActorUserId = {ActorUserId} }}";
}

internal sealed class AssignTemporaryPinHandler(
    IOperatorRepository operators,
    IPinHasher pinHasher,
    ISecurityEventLog securityEvents,
    IUnitOfWork unitOfWork,
    TimeProvider time) : ICommandHandler<AssignTemporaryPinCommand, Result>
{
    public async Task<Result> HandleAsync(AssignTemporaryPinCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var op = await operators.GetByIdAsync(command.OperatorId, cancellationToken).ConfigureAwait(false);
        if (op is null)
        {
            return Result.Failure(IdentityErrors.OperatorNotFound);
        }

        var pin = Pin.Create(command.TemporaryPin, op.EmployeeNumber);
        if (pin.IsFailure)
        {
            return Result.Failure(pin.Error);
        }

        op.AssignTemporaryPin(pinHasher.Hash(pin.Value));

        var now = time.GetUtcNow();
        securityEvents.Record(OperatorEvent(SecurityEventType.OperatorPinReset, command.ActorUserId, op.Id, now));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    internal static SecurityEvent OperatorEvent(SecurityEventType type, Guid actorUserId, Guid operatorId, DateTimeOffset now) =>
        new(
            Guid.CreateVersion7(now),
            type,
            now,
            SecurityActorType.User,
            actorUserId,
            WorkstationId: null,
            new Dictionary<string, string> { ["operatorId"] = operatorId.ToString() });
}

/// <summary>Desbloqueo manual del legajo por el Admin (R20).</summary>
public sealed record UnlockOperatorCommand(Guid OperatorId, Guid ActorUserId);

internal sealed class UnlockOperatorHandler(
    IOperatorRepository operators,
    ISecurityEventLog securityEvents,
    IUnitOfWork unitOfWork,
    TimeProvider time) : ICommandHandler<UnlockOperatorCommand, Result>
{
    public async Task<Result> HandleAsync(UnlockOperatorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var op = await operators.GetByIdAsync(command.OperatorId, cancellationToken).ConfigureAwait(false);
        if (op is null)
        {
            return Result.Failure(IdentityErrors.OperatorNotFound);
        }

        op.Unlock();

        var now = time.GetUtcNow();
        securityEvents.Record(
            AssignTemporaryPinHandler.OperatorEvent(SecurityEventType.OperatorUnlocked, command.ActorUserId, op.Id, now));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
