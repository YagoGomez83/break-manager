using BreakManager.Application.Abstractions;

namespace BreakManager.Web.Hosting;

/// <summary>
/// Ejecuta un handler en un scope de DI propio. Lo usan las páginas interactivas de Blazor Server:
/// el circuito vive mucho, pero cada operación usa su propio DbContext.
/// </summary>
internal sealed class HandlerRunner(IServiceScopeFactory scopeFactory)
{
    public async Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<TCommand, TResult>>();
        return await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<TQuery, TResult>>();
        return await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
    }
}
