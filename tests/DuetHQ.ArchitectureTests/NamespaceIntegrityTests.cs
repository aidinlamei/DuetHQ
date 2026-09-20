using DuetHQ.ArchitectureTests.Support;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DuetHQ.ArchitectureTests;

// PRE-2. Layers are namespaces inside one assembly per module, so a file in the wrong namespace escapes every layering rule.
// Every .cs file under src/ is parsed once with Roslyn and all file-level rules assert from that syntax tree.
// This class loads no assemblies, so it is independent of (and cannot be masked by) the layering tests.
public sealed class NamespaceIntegrityTests
{
    private static readonly string[] LayerFolders = ["Domain", "Application", "Infrastructure", "Endpoints"];
    private static readonly Lazy<IReadOnlyList<Source>> LazySources = new(LoadSources);

    private static IReadOnlyList<Source> Sources => LazySources.Value;

    [Fact]
    public void SourceFiles_NamespaceMatchesFolderPath_NoViolations()
    {
        var violations = new List<string>();
        foreach (var source in Sources.Where(s => !IsTopLevelProgram(s)))
        {
            var fileScoped = source.Root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().ToList();
            var expected = ExpectedNamespace(source);

            if (source.Root.Members.OfType<NamespaceDeclarationSyntax>().Any())
            {
                violations.Add($"{Display(source.File)}: block-scoped namespace; file-scoped is required (CLAUDE.md section 5).");
            }
            else if (fileScoped.Count != 1)
            {
                violations.Add($"{Display(source.File)}: expected exactly one file-scoped namespace, found {fileScoped.Count}.");
            }
            else if (fileScoped[0].Name.ToString() != expected)
            {
                violations.Add($"{Display(source.File)}: namespace '{fileScoped[0].Name}' should be '{expected}'.");
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void SourceFiles_DeclareAtMostOneTopLevelType_NoViolations()
    {
        var violations = Sources.Where(s => !IsTopLevelProgram(s) && TopLevelTypes(s.Root).Count() > 1)
            .Select(s => $"{Display(s.File)}: declares {TopLevelTypes(s.Root).Count()} top-level types; one type per file (CLAUDE.md section 5).");

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void ModuleEntryPointFiles_Parsed_DeclareExactlyOneType()
    {
        // Non-vacuity guard: on real files the parse must actually find the declaration the rules above count.
        var entryPoints = ModuleCatalog.Modules.Select(m => $"src/Modules/{m}/{ModuleCatalog.HostProject(m)}/{ModuleCatalog.EntryPointName(m)}.cs").ToHashSet(StringComparer.Ordinal);

        var counts = Sources.Where(s => entryPoints.Contains(Display(s.File))).Select(s => TopLevelTypes(s.Root).Count()).ToList();

        counts.Count.ShouldBe(8);
        counts.ShouldAllBe(count => count == 1);
    }

    [Fact]
    public void ModuleHostProjects_Files_LiveInALayerFolderOrAreTheEntryPoint()
    {
        var violations = new List<string>();
        foreach (var source in Sources.Where(s => s.Project.Kind == ProjectKind.ModuleHost))
        {
            var segments = Path.GetRelativePath(source.Project.ProjectDirectory, source.File).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var isEntryPoint = segments.Length == 1 && segments[0] == $"{ModuleCatalog.EntryPointName(source.Project.Module!)}.cs";
            var inLayerFolder = segments.Length > 1 && LayerFolders.Contains(segments[0], StringComparer.Ordinal);
            if (!isEntryPoint && !inLayerFolder)
            {
                violations.Add($"{Display(source.File)}: module files belong in Domain/, Application/, Infrastructure/ or Endpoints/ (the root holds only {ModuleCatalog.EntryPointName(source.Project.Module!)}.cs).");
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void RazorFiles_ExplicitNamespaceDirective_IsNotUsed()
    {
        // Razor derives the namespace from the folder; an @namespace directive would silently override that.
        var violations = RepoLayout.EnumerateSourceFiles("*.razor")
            .Where(f => File.ReadLines(f).Any(l => l.TrimStart().StartsWith("@namespace", StringComparison.Ordinal)))
            .Select(f => $"{Display(f)}: @namespace directive.")
            .ToList();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void SourceWalk_CoversTheKnownFiles()
    {
        // Non-vacuity guard: the walk must see the composition-root files and every module entry point.
        var seen = Sources.Select(s => Display(s.File)).ToHashSet(StringComparer.Ordinal);

        var expected = ModuleCatalog.Modules
            .Select(m => $"src/Modules/{m}/{ModuleCatalog.HostProject(m)}/{ModuleCatalog.EntryPointName(m)}.cs")
            .Concat(["src/DuetHQ.Web/Program.cs", "src/DuetHQ.Web.Client/Program.cs"]);

        expected.Where(e => !seen.Contains(e)).ShouldBeEmpty();
        Solution.Projects.Count.ShouldBe(21);
    }

    [Fact]
    public void SourceFiles_BelongToExactlyOneProject_NoStrayFilesUnderSrc()
    {
        var stray = RepoLayout.EnumerateSourceFiles("*.cs")
            .Where(f => OwningProject(f) is null)
            .Select(Display)
            .ToList();

        stray.ShouldBeEmpty();
    }

    // Types and delegates directly in the file or in its namespace; the descent stops at the first type, so nested types are not counted.
    private static IEnumerable<MemberDeclarationSyntax> TopLevelTypes(CompilationUnitSyntax root) =>
        root.DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .OfType<MemberDeclarationSyntax>()
            .Where(member => member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);

    private static List<Source> LoadSources() =>
        RepoLayout.EnumerateSourceFiles("*.cs")
            .Select(file => (File: file, Project: OwningProject(file)))
            .Where(x => x.Project is not null)
            .Select(x => new Source(x.File, x.Project!, CSharpSyntaxTree.ParseText(File.ReadAllText(x.File)).GetCompilationUnitRoot()))
            .ToList();

    // Nearest ancestor directory that holds a project file.
    private static SourceProject? OwningProject(string file) =>
        Solution.Projects
            .Where(p => file.StartsWith(p.ProjectDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderByDescending(p => p.ProjectDirectory.Length)
            .FirstOrDefault();

    // Web and Web.Client use top-level statements in Program.cs (global namespace); nothing else is exempt.
    private static bool IsTopLevelProgram(Source source) =>
        source.Project.Kind is ProjectKind.Web or ProjectKind.WebClient
        && Path.GetFileName(source.File) == "Program.cs"
        && Path.GetDirectoryName(source.File) == source.Project.ProjectDirectory;

    private static string ExpectedNamespace(Source source)
    {
        var folders = Path.GetRelativePath(source.Project.ProjectDirectory, source.File).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[..^1];
        return folders.Length == 0 ? source.Project.Name : $"{source.Project.Name}.{string.Join('.', folders)}";
    }

    private static string Display(string file) => Path.GetRelativePath(RepoLayout.Root, file).Replace('\\', '/');

    private sealed record Source(string File, SourceProject Project, CompilationUnitSyntax Root);
}
