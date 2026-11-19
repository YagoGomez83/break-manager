using NetArchTest.Rules;
using static BreakManager.ArchitectureTests.SolutionLayout;

namespace BreakManager.ArchitectureTests;

/// <summary>
/// Reglas sobre los ensamblados compilados: lo que el código realmente usa.
/// </summary>
public sealed class AssemblyDependencyTests
{
    [Fact]
    public void Domain_OnlyReferencesTheBaseClassLibrary()
    {
        var nonBcl = DomainAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => !IsBaseClassLibrary(name));

        Assert.Empty(nonBcl);
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureWebOrEfCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                InfrastructureProject,
                WebProject,
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions.Identity")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void AgentCore_DoesNotDependOnWpfWin32OrWeb()
    {
        var result = Types.InAssembly(AgentCoreAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "System.Windows",
                "Microsoft.Win32",
                "H.NotifyIcon",
                "System.Security.Cryptography.ProtectedData",
                AgentWindowsProject,
                WebProject)
            .GetResult();

        AssertSuccessful(result);
        Assert.DoesNotContain(
            AgentCoreAssembly.GetReferencedAssemblies(),
            reference => reference.Name is "PresentationFramework" or "PresentationCore" or "WindowsBase"
                or "System.Security.Cryptography.ProtectedData");
    }

    [Fact]
    public void Infrastructure_PublicTypes_AreOnlyDependencyInjectionExtensions()
    {
        // Regla opcional de la spec de la Fase 1: se reporta como advertencia (test omitido), no rompe el build.
        var unexpected = InfrastructureAssembly.GetExportedTypes()
            .Where(type => !(type is { IsAbstract: true, IsSealed: true } && type.Name == "DependencyInjection"))
            .Where(type => type.Namespace?.EndsWith(".Migrations", StringComparison.Ordinal) != true)
            .Select(type => type.FullName)
            .ToList();

        if (unexpected.Count > 0)
        {
            Assert.Skip(
                "Advertencia: tipos públicos en Infrastructure que no son extensiones de DI: "
                + string.Join(", ", unexpected));
        }
    }

    private static bool IsBaseClassLibrary(string assemblyName) =>
        assemblyName is "netstandard" or "mscorlib"
        || assemblyName.StartsWith("System", StringComparison.Ordinal);

    private static void AssertSuccessful(NetArchTest.Rules.TestResult result) =>
        Assert.True(
            result.IsSuccessful,
            "Tipos que violan la regla: " + string.Join(", ", result.FailingTypeNames ?? []));
}
