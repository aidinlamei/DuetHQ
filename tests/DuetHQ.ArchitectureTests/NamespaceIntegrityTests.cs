using System.Text.RegularExpressions;
using DuetHQ.ArchitectureTests.Support;

namespace DuetHQ.ArchitectureTests;

// PRE-2. Layers are namespaces inside one assembly per module, so a file in the wrong namespace escapes every layering rule.
// This class reads source files only and loads no assemblies, so it is independent of (and cannot be masked by) the layering tests.
public sealed class NamespaceIntegrityTests
{
    private static readonly string[] LayerFolders = ["Domain", "Application", "Infrastructure", "Endpoints"];

    private static readonly Regex FileScopedNamespace = new(
        @"^[ \t]*namespace[ \t]+(?<name>[A-Za-z_][A-Za-z0-9_.]*)[ \t]*;",
        RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly Regex BlockNamespace = new(
        @"^[ \t]*namespace[ \t]+[A-Za-z_][A-Za-z0-9_.]*[ \t]*(\r?\n[ \t]*)?\{",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    [Fact]
    public void SourceFiles_NamespaceMatchesFolderPath_NoViolations()
    {
        var violations = new List<string>();
        foreach (var (file, project) in SourceFiles())
        {
            if (IsTopLevelProgram(file, project))
            {
                continue;
            }

            var relative = Path.GetRelativePath(project.ProjectDirectory, file);
            var expected = ExpectedNamespace(project, relative);
            var text = File.ReadAllText(file);

            var fileScoped = FileScopedNamespace.Matches(text);
            if (BlockNamespace.IsMatch(text))
            {
                violations.Add($"{Display(file)}: block-scoped namespace; file-scoped is required (CLAUDE.md section 5).");
            }
            else if (fileScoped.Count != 1)
            {
                violations.Add($"{Display(file)}: expected exactly one file-scoped namespace, found {fileScoped.Count}.");
            }
            else if (fileScoped[0].Groups["name"].Value != expected)
            {
                violations.Add($"{Display(file)}: namespace '{fileScoped[0].Groups["name"].Value}' should be '{expected}'.");
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void ModuleHostProjects_Files_LiveInALayerFolderOrAreTheEntryPoint()
    {
        var violations = new List<string>();
        foreach (var (file, project) in SourceFiles().Where(f => f.Project.Kind == ProjectKind.ModuleHost))
        {
            var relative = Path.GetRelativePath(project.ProjectDirectory, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var isEntryPoint = segments.Length == 1 && segments[0] == $"{ModuleCatalog.EntryPointName(project.Module!)}.cs";
            var inLayerFolder = segments.Length > 1 && LayerFolders.Contains(segments[0], StringComparer.Ordinal);
            if (!isEntryPoint && !inLayerFolder)
            {
                violations.Add($"{Display(file)}: module files belong in Domain/, Application/, Infrastructure/ or Endpoints/ (the root holds only {ModuleCatalog.EntryPointName(project.Module!)}.cs).");
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
        var seen = SourceFiles().Select(f => Display(f.File)).ToHashSet(StringComparer.Ordinal);

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

    private static IEnumerable<(string File, SourceProject Project)> SourceFiles() =>
        RepoLayout.EnumerateSourceFiles("*.cs").Select(f => (File: f, Project: OwningProject(f))).Where(x => x.Project is not null).Select(x => (x.File, x.Project!));

    // Nearest ancestor directory that holds a project file.
    private static SourceProject? OwningProject(string file) =>
        Solution.Projects
            .Where(p => file.StartsWith(p.ProjectDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderByDescending(p => p.ProjectDirectory.Length)
            .FirstOrDefault();

    // Web and Web.Client use top-level statements in Program.cs (global namespace); nothing else is exempt.
    private static bool IsTopLevelProgram(string file, SourceProject project) =>
        project.Kind is ProjectKind.Web or ProjectKind.WebClient
        && Path.GetFileName(file) == "Program.cs"
        && Path.GetDirectoryName(file) == project.ProjectDirectory;

    private static string ExpectedNamespace(SourceProject project, string relativePath)
    {
        var folders = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[..^1];
        return folders.Length == 0 ? project.Name : $"{project.Name}.{string.Join('.', folders)}";
    }

    private static string Display(string file) => Path.GetRelativePath(RepoLayout.Root, file).Replace('\\', '/');
}
