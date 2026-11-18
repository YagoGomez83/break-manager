namespace BreakManager.Application.Abstractions;

/// <summary>
/// Caso de uso de solo lectura. Se registra explícitamente en DI (sin MediatR).
/// </summary>
public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
