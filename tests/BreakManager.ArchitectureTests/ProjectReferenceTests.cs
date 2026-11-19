using static BreakManager.ArchitectureTests.SolutionLayout;

namespace BreakManager.ArchitectureTests;

/// <summary>
/// Reglas sobre los .csproj: detectan una referencia prohibida aunque todavía no se use en el código.
/// </summary>
public sealed class ProjectReferenceTests
{
    private static readonly string[] InfrastructurePackagePrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "EFCore.",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.Identity",
        "Microsoft.Extensions.Diagnostics.HealthChecks",
        "ClosedXML",
        "Serilog",
        "OpenTelemetry",
    ];

    [Fact]
    public void Domain_DoesNotReferenceProjectsOrPackages()
    {
        var project = Project(DomainProject);

        Assert.Empty(project.ProjectReferences);
        Assert.Empty(project.PackageReferences);
    }

    [Fact]
    public void Application_OnlyReferencesDomain()
    {
        Assert.Equal([DomainProject], Project(ApplicationProject).ProjectReferences);
    }

    [Fact]
    public void Application_DoesNotReferenceInfrastructurePackages()
    {
        var forbidden = Project(ApplicationProject).PackageReferences
            .Where(package => InfrastructurePackagePrefixes.Any(
                prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceWebOrAgent()
    {
        var references = Project(InfrastructureProject).ProjectReferences;

        Assert.DoesNotContain(WebProject, references);
        Assert.DoesNotContain(AgentCoreProject, references);
        Assert.DoesNotContain(AgentWindowsProject, references);
    }

    [Fact]
    public void Contracts_DoesNotReferenceProjects()
    {
        Assert.Empty(Project(ContractsProject).ProjectReferences);
    }

    [Fact]
    public void AgentCore_OnlyReferencesContracts()
    {
        Assert.Equal([ContractsProject], Project(AgentCoreProject).ProjectReferences);
    }

    [Fact]
    public void AgentCore_DoesNotReferenceDpapi()
    {
        // DPAPI es Windows: el almacén de credenciales vive en Agent.Windows (ADR-0010).
        Assert.DoesNotContain(
            Project(AgentCoreProject).PackageReferences,
            package => package.StartsWith("System.Security.Cryptography.ProtectedData", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AgentCore_DoesNotUseWpfOrWindowsTargets()
    {
        var project = Project(AgentCoreProject);

        Assert.NotEqual("true", project.Property("UseWPF"), StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual("true", project.Property("UseWindowsForms"), StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "windows",
            project.Property("TargetFramework") ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }
}
