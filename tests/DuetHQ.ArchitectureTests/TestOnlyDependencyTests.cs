using DuetHQ.ArchitectureTests.Support;

namespace DuetHQ.ArchitectureTests;

// R8: production code (src/) must not reach test-only code or test-only packages by ANY route.
//  * projects: no ProjectReference leaves src/, and DuetHQ.TestSupport is unreachable through the whole reference closure;
//  * packages: no src/ project restores a test-only package. The authority is obj/project.assets.json (effective restored set,
//    including packages injected by Directory.Build.props and transitive ones); the csproj-level check only gives clearer messages
//    for direct references.
// Each rule has a positive control (a test project that DOES reach TestSupport / restore xunit) so it cannot pass by looking at nothing.
public sealed class TestOnlyDependencyTests
{
    private const string TestSupport = "DuetHQ.TestSupport";

    private static string TestSupportProject => Path.Combine(RepoLayout.Root, "tests", TestSupport, $"{TestSupport}.csproj");

    private static string TestSupportTestsProject => Path.Combine(RepoLayout.Root, "tests", $"{TestSupport}.Tests", $"{TestSupport}.Tests.csproj");

    [Fact]
    public void SourceProjects_ProjectReferences_NeverLeaveSrc()
    {
        var src = RepoLayout.Src + Path.DirectorySeparatorChar;
        var violations = Solution.Projects
            .SelectMany(project => ProjectGraph.ReferencedProjectFiles(project.ProjectFile)
                .Where(reference => !reference.StartsWith(src, StringComparison.OrdinalIgnoreCase))
                .Select(reference => $"{project.Name} references {Path.GetRelativePath(RepoLayout.Root, reference)}, which is outside src/."))
            .ToList();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void SourceProjects_ReferenceClosure_NeverReachesTestSupport()
    {
        var reached = ProjectGraph.ReachableFrom(Solution.Projects.Select(p => p.ProjectFile));

        var violations = reached
            .Where(entry => ProjectGraph.NameOf(entry.Key) == TestSupport)
            .Select(entry => $"src/ reaches {TestSupport}: {string.Join(" -> ", entry.Value)}")
            .ToList();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void ReferenceWalker_StartingAtTheTestSupportTestsProject_ReachesTestSupport()
    {
        // Positive control: proves the walker can see TestSupport at all.
        File.Exists(TestSupportProject).ShouldBeTrue($"{TestSupportProject} must exist; renaming the project would make the R8 rules vacuous.");

        var reached = ProjectGraph.ReachableFrom([TestSupportTestsProject]);

        reached.ContainsKey(Path.GetFullPath(TestSupportProject)).ShouldBeTrue();
    }

    [Fact]
    public void SourceAssemblies_CompiledReferences_NeverIncludeTestSupport()
    {
        var violations = Solution.AnalysedAssemblies
            .SelectMany(project => Solution.LoadAssembly(project).GetReferencedAssemblies()
                .Where(reference => reference.Name == TestSupport)
                .Select(_ => $"{project.Name} is compiled against {TestSupport}."))
            .ToList();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void SourceProjects_DeclaredPackageReferences_NeverIncludeTestOnlyPackages()
    {
        var violations = Solution.Projects
            .SelectMany(project => project.PackageReferences
                .Where(TestOnlyPackages.IsTestOnly)
                .Select(package => $"{project.Name} declares the test-only package {package}."))
            .ToList();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void SourceProjects_RestoredPackages_NeverIncludeTestOnlyPackages()
    {
        var violations = Solution.Projects
            .SelectMany(project => RestoredPackages.Read(project.ProjectDirectory)
                .Where(TestOnlyPackages.IsTestOnly)
                .Select(package => $"{project.Name} restores the test-only package {package} (direct, transitive or injected by a props file)."))
            .ToList();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void RestoredPackages_OfEverySourceProject_SeeThePropsInjectedAnalyzer()
    {
        // Positive control for "injected by Directory.Build.props": BannedApiAnalyzers is added to every project there, never in a csproj.
        var missing = Solution.Projects
            .Where(project => !RestoredPackages.Read(project.ProjectDirectory).Contains("Microsoft.CodeAnalysis.BannedApiAnalyzers"))
            .Select(project => project.Name)
            .ToList();

        missing.ShouldBeEmpty("the restored set must include packages injected by props files, otherwise it cannot police them:");
    }

    [Fact]
    public void RestoredPackages_OfTestProjects_AreFlaggedAsTestOnly()
    {
        // Positive control: the pipeline (assets.json reader + matcher) does flag what tests legitimately use.
        var testSupportTests = RestoredPackages.Read(Path.GetDirectoryName(TestSupportTestsProject)!);
        var testSupport = RestoredPackages.Read(Path.GetDirectoryName(TestSupportProject)!);

        testSupportTests.Where(TestOnlyPackages.IsTestOnly).Contains("xunit", StringComparer.OrdinalIgnoreCase).ShouldBeTrue();
        testSupport.Where(TestOnlyPackages.IsTestOnly).Contains("Microsoft.Extensions.TimeProvider.Testing", StringComparer.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Microsoft.Extensions.TimeProvider.Testing")]
    [InlineData("Testcontainers")]
    [InlineData("Testcontainers.PostgreSql")]
    [InlineData("xunit")]
    [InlineData("xunit.core")]
    [InlineData("XUnit.Assert")]
    [InlineData("Shouldly")]
    [InlineData("Microsoft.NET.Test.Sdk")]
    public void IsTestOnly_TestPackageNames_AreFlagged(string packageId) => TestOnlyPackages.IsTestOnly(packageId).ShouldBeTrue();

    [Theory]
    [InlineData("Npgsql")]
    [InlineData("Serilog")]
    [InlineData("Microsoft.Extensions.Configuration.Abstractions")]
    [InlineData("Microsoft.CodeAnalysis.BannedApiAnalyzers")]
    [InlineData("Microsoft.AspNetCore.Components.WebAssembly.Server")]
    public void IsTestOnly_ProductionPackageNames_AreNotFlagged(string packageId) => TestOnlyPackages.IsTestOnly(packageId).ShouldBeFalse();
}
