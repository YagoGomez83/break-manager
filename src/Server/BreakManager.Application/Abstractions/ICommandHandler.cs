namespace BreakManager.Application.Abstractions;

/// <summary>
/// Caso de uso que modifica estado. Se registra explícitamente en DI (sin MediatR).
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
