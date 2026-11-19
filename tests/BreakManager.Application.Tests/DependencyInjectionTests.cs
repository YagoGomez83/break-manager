using BreakManager.Application;
using BreakManager.Application.Abstractions;
using BreakManager.Domain.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BreakManager.Application.Tests;

public sealed class DependencyInjectionTests
{
    public sealed record PingCommand(string Text);

    public sealed record PingQuery(int Number);

    public sealed class PingCommandHandler : ICommandHandler<PingCommand, Result<string>>
    {
        public Task<Result<string>> HandleAsync(PingCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(command.Text));
    }

    public sealed class PingQueryHandler : IQueryHandler<PingQuery, int>
    {
        public Task<int> HandleAsync(PingQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(query.Number * 2);
    }

    public sealed class PingCommandValidator : AbstractValidator<PingCommand>
    {
        public PingCommandValidator() => RuleFor(c => c.Text).NotEmpty();
    }

    public abstract class AbstractHandler : ICommandHandler<PingCommand, Result<string>>
    {
        public abstract Task<Result<string>> HandleAsync(PingCommand command, CancellationToken cancellationToken);
    }

    // Sin ValidateOnBuild: Application registra servicios que dependen de puertos que implementa Infrastructure.
    // Que todo el grafo se resuelva lo verifican los tests de Web, con el contenedor real.
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection()
            .AddApplication(typeof(DependencyInjectionTests).Assembly)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

    [Fact]
    public async Task AddApplication_RegistersCommandHandlersFromScannedAssemblies()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<PingCommand, Result<string>>>();
        var result = await handler.HandleAsync(new PingCommand("hola"), TestContext.Current.CancellationToken);

        Assert.IsType<PingCommandHandler>(handler);
        Assert.Equal("hola", result.Value);
    }

    [Fact]
    public async Task AddApplication_RegistersQueryHandlersFromScannedAssemblies()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<PingQuery, int>>();

        Assert.Equal(42, await handler.HandleAsync(new PingQuery(21), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AddApplication_RegistersHandlersAsScoped()
    {
        var services = new ServiceCollection().AddApplication(typeof(DependencyInjectionTests).Assembly);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IQueryHandler<PingQuery, int>));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddApplication_IgnoresAbstractHandlers()
    {
        var services = new ServiceCollection().AddApplication(typeof(DependencyInjectionTests).Assembly);

        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(AbstractHandler));
    }

    [Fact]
    public void AddApplication_RegistersFluentValidationValidators()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var validator = scope.ServiceProvider.GetRequiredService<IValidator<PingCommand>>();

        Assert.False(validator.Validate(new PingCommand(string.Empty)).IsValid);
    }

    [Fact]
    public void AddApplication_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection()
            .AddApplication(typeof(DependencyInjectionTests).Assembly)
            .AddApplication(typeof(DependencyInjectionTests).Assembly);

        Assert.Single(services, d => d.ServiceType == typeof(IQueryHandler<PingQuery, int>));
    }
}
