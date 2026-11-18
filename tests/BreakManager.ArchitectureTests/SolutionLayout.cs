using System.Reflection;
using System.Xml.Linq;

namespace BreakManager.ArchitectureTests;

/// <summary>
/// Ensamblados y archivos de proyecto bajo prueba.
/// </summary>
internal static class SolutionLayout
{
    public const string DomainProject = "BreakManager.Domain";
    public const string ApplicationProject = "BreakManager.Application";
    public const string InfrastructureProject = "BreakManager.Infrastructure";
    public const string WebProject = "BreakManager.Web";
    public const string ContractsProject = "BreakManager.Contracts";
    public const string AgentCoreProject = "BreakManager.Agent.Core";
    public const string AgentWindowsProject = "BreakManager.Agent.Windows";

    public static readonly Assembly DomainAssembly = typeof(BreakManager.Domain.Common.Result).Assembly;
    public static readonly Assembly ApplicationAssembly = typeof(BreakManager.Application.DependencyInjection).Assembly;
    public static readonly Assembly InfrastructureAssembly = typeof(BreakManager.Infrastructure.DependencyInjection).Assembly;
    public static readonly Assembly AgentCoreAssembly = typeof(BreakManager.Agent.Core.Activity.IActivitySource).Assembly;

    private static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

    public static ProjectFile Project(string name)
    {
        var path = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot.Value, "src"), $"{name}.csproj", SearchOption.AllDirectories)
            .Single();
        return new ProjectFile(XDocument.Load(path));
    }

    private static string FindRepositoryRoot()
    {
        var root = typeof(SolutionLayout).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == "RepositoryRoot")?.Value;

        return root is not null && File.Exists(Path.Combine(root, "BreakManager.sln"))
            ? root
            : throw new InvalidOperationException("No se encontró BreakManager.sln para ubicar los proyectos.");
    }
}

internal sealed record ProjectFile(XDocument Document)
{
    public IReadOnlyList<string> ProjectReferences =>
        [.. Items("ProjectReference").Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))];

    public IReadOnlyList<string> PackageReferences => [.. Items("PackageReference")];

    public string? Property(string name) =>
        Document.Descendants(name).Select(e => e.Value).FirstOrDefault();

    private IEnumerable<string> Items(string itemType) =>
        Document.Descendants(itemType)
            .Select(e => (string?)e.Attribute("Include"))
            .OfType<string>();
}
