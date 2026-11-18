using System.Reflection;
using BreakManager.Application.Abstractions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BreakManager.Application;

public static class DependencyInjection
{
    private static readonly Type[] HandlerInterfaces =
    [
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
    ];

    /// <summary>
    /// Registra los handlers de comandos y consultas (scoped) y los validadores de FluentValidation
    /// del ensamblado de Application y de los ensamblados adicionales indicados.
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        params Assembly[] additionalAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(additionalAssemblies);

        Assembly[] assemblies = [typeof(DependencyInjection).Assembly, .. additionalAssemblies.Distinct()];

        foreach (var assembly in assemblies.Distinct())
        {
            RegisterHandlers(services, assembly);
            services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped, includeInternalTypes: true);
        }

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly assembly)
    {
        var implementations = assembly.DefinedTypes
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var implementation in implementations)
        {
            var handlerInterfaces = implementation.ImplementedInterfaces
                .Where(i => i.IsGenericType && HandlerInterfaces.Contains(i.GetGenericTypeDefinition()));

            foreach (var serviceType in handlerInterfaces)
            {
                services.TryAddEnumerable(ServiceDescriptor.Scoped(serviceType, implementation));
            }
        }
    }
}
