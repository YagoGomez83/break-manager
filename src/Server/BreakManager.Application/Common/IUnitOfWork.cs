namespace BreakManager.Application.Common;

/// <summary>
/// Confirma en una sola transacción todos los cambios registrados en los repositorios del request
/// (agregados, eventos de seguridad). Es lo que hace atómicos, por ejemplo, consumir el ticket y abrir la sesión.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
