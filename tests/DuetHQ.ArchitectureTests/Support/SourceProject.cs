using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DuetHQ.ArchitectureTests.Support;

internal enum ProjectKind
{
    BuildingBlock,
    ModuleHost,
    ModuleContracts,
    Web,
    WebClient,
}

// A project under src/ as DECLARED in its .csproj (ProjectReference / PackageReference / FrameworkReference items).
// The declared graph is what the module-graph rules assert; compiled metadata only lists assemblies the code actually uses.
internal sealed record SourceProject(
    string Name,
    string ProjectFile,
    ProjectKind Kind,
    string? Module,
    IReadOnlySet<string> ProjectReferences,
    IReadOnlySet<string> PackageReferences,
    IReadOnlySet<string> FrameworkReferences,
    bool OverridesRootNamespaceOrAssemblyName)
{
    private static readonly Regex ModuleProjectName = new(
        @"^DuetHQ\.Modules\.(?<module>[A-Za-z]+)(?<contracts>\.Contracts)?$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    public string ProjectDirectory => Path.GetDirectoryName(ProjectFile)!;

    public static IReadOnlyList<SourceProject> LoadAll() =>
        RepoLayout.EnumerateSourceFiles("*.csproj").Select(Load).ToList();

    private static SourceProject Load(string projectFile)
    {
        var name = Path.GetFileNameWithoutExtension(projectFile);
        var document = XDocument.Load(projectFile);
        var (kind, module) = Classify(name);

        return new SourceProject(
            name,
            projectFile,
            kind,
            module,
            IncludesOf(document, "ProjectReference").Select(include => ResolveProjectReference(projectFile, include)).ToHashSet(StringComparer.Ordinal),
            IncludesOf(document, "PackageReference").ToHashSet(StringComparer.Ordinal),
            IncludesOf(document, "FrameworkReference").ToHashSet(StringComparer.Ordinal),
            document.Descendants().Any(e => e.Name.LocalName is "RootNamespace" or "AssemblyName"));
    }

    private static IEnumerable<string> IncludesOf(XDocument document, string item) =>
        document.Descendants().Where(e => e.Name.LocalName == item).Select(e => (string?)e.Attribute("Include") ?? string.Empty).Where(i => i.Length > 0);

    // Throws when a reference does not resolve: silently skipping a project would make every graph rule vacuous.
    private static string ResolveProjectReference(string projectFile, string include)
    {
        var relative = include.Replace('\\', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFile)!, relative));
        if (!File.Exists(full))
        {
            throw new FileNotFoundException($"{Path.GetFileName(projectFile)} references '{include}', which does not resolve to a file ({full}).");
        }

        return Path.GetFileNameWithoutExtension(full);
    }

    private static (ProjectKind Kind, string? Module) Classify(string name)
    {
        if (name is "DuetHQ.SharedKernel" or "DuetHQ.Application.Abstractions" or "DuetHQ.Infrastructure.Common")
        {
            return (ProjectKind.BuildingBlock, null);
        }

        if (name == "DuetHQ.Web")
        {
            return (ProjectKind.Web, null);
        }

        if (name == "DuetHQ.Web.Client")
        {
            return (ProjectKind.WebClient, null);
        }

        var match = ModuleProjectName.Match(name);
        if (match.Success)
        {
            return (match.Groups["contracts"].Success ? ProjectKind.ModuleContracts : ProjectKind.ModuleHost, match.Groups["module"].Value);
        }

        throw new InvalidOperationException($"Unclassified project under src/: {name}. Classify it in SourceProject.Classify and extend the architecture rules deliberately.");
    }
}
