using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using DuetHQ.ArchitectureTests.Support;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace DuetHQ.ArchitectureTests;

// Two layers of evidence, deliberately:
//  * DECLARED graph (.csproj ProjectReference items): the declaration-level guard. It is exact today because
//    the modules contain no code yet, and it stays as the guard afterwards.
//  * COMPILED graph (assembly metadata): Roslyn only records assemblies the code actually uses, so on the empty
//    skeleton it proves little. It becomes the authoritative check from Phase 2 onward, once assemblies are
//    non-empty, and it catches usage that leaks in through a TRANSITIVE reference (used but never declared directly).
public sealed class ModuleDependencyTests
{
    private static readonly Regex ModuleProjectReference = new(
        @"^DuetHQ\.Modules\.(?<module>[A-Za-z]+)(?<contracts>\.Contracts)?$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    [Fact]
    public void SourceProjects_UnderSrc_AreExactlyTheExpectedSet()
    {
        // Silently skipping a project is the one failure mode that would make every graph rule vacuous.
        var expected = ModuleCatalog.Modules.Select(ModuleCatalog.HostProject)
            .Concat(ModuleCatalog.Modules.Select(ModuleCatalog.ContractsProject))
            .Concat(["DuetHQ.SharedKernel", "DuetHQ.Application.Abstractions", "DuetHQ.Infrastructure.Common", "DuetHQ.Web", "DuetHQ.Web.Client"])
            .Order(StringComparer.Ordinal)
            .ToList();

        var actual = Solution.Projects.Select(p => p.Name).Order(StringComparer.Ordinal).ToList();
        var diff = actual.Except(expected).Select(name => $"+ {name}  (unexpected project under src/)")
            .Concat(expected.Except(actual).Select(name => $"- {name}  (expected project not found under src/)"))
            .ToList();

        diff.ShouldBeEmpty("The projects under src/ must be exactly 8 module hosts, 8 .Contracts, 3 building blocks, Web and Web.Client:");
        Solution.Hosts.Count().ShouldBe(8);
        Solution.Contracts.Count().ShouldBe(8);
    }

    [Fact]
    public void SourceProjects_RootNamespaceAndAssemblyName_AreNotOverridden()
    {
        // Namespace/assembly names are derived from the project name by every other rule.
        Solution.Projects.Where(p => p.OverridesRootNamespaceOrAssemblyName).Select(p => p.Name).ShouldBeEmpty();
    }

    [Fact]
    public void ModuleGraph_DeclaredProjectReferences_EqualSection5TableExactly()
    {
        var actualEdges = Solution.Hosts
            .SelectMany(host => ModulesReferencedBy(host).Select(other => $"{host.Module} -> {other}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        var expectedEdges = ModuleCatalog.AllowedDependencies
            .SelectMany(entry => entry.Value.Select(other => $"{entry.Key} -> {other}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        var diff = actualEdges.Except(expectedEdges).Select(edge => $"+ {edge}  (not in the section 5 table)")
            .Concat(expectedEdges.Except(actualEdges).Select(edge => $"- {edge}  (in the section 5 table, missing from the projects)"))
            .ToList();

        diff.ShouldBeEmpty("Declared module graph differs from ARCHITECTURE.md section 5; update the table, this fixture and the docs together:");
    }

    [Fact]
    public void ModuleHosts_ReferencingOtherModules_OnlyThroughContracts()
    {
        var violations = new List<string>();
        foreach (var host in Solution.Hosts)
        {
            foreach (var reference in host.ProjectReferences)
            {
                if (reference is "DuetHQ.Web" or "DuetHQ.Web.Client")
                {
                    violations.Add($"{host.Name} references the composition root {reference}.");
                    continue;
                }

                var match = ModuleProjectReference.Match(reference);
                if (match.Success && match.Groups["module"].Value != host.Module && !match.Groups["contracts"].Success)
                {
                    violations.Add($"{host.Name} references {reference}; other modules are reachable only through their .Contracts project.");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void ContractsProjects_DeclaredDependencies_AreSharedKernelOnly()
    {
        var violations = new List<string>();
        foreach (var contracts in Solution.Contracts)
        {
            violations.AddRange(contracts.ProjectReferences.Where(r => r != "DuetHQ.SharedKernel").Select(r => $"{contracts.Name} references project {r}."));
            violations.AddRange(contracts.PackageReferences.Select(r => $"{contracts.Name} references package {r}."));
            violations.AddRange(contracts.FrameworkReferences.Select(r => $"{contracts.Name} references framework {r}."));
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void ContractsAssemblies_CompiledReferences_AreSharedKernelAndBaseLibraryOnly()
    {
        var violations = new List<string>();
        foreach (var contracts in Solution.Contracts)
        {
            var references = Solution.LoadAssembly(contracts).GetReferencedAssemblies().Select(a => a.Name!);
            violations.AddRange(references
                .Where(name => name != "DuetHQ.SharedKernel" && !name.StartsWith("System.", StringComparison.Ordinal) && name != "System")
                .Select(name => $"{contracts.Name} is compiled against {name}."));
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void CompiledAssemblies_DuetHQReferences_AreAllDeclaredDirectly()
    {
        // No transitive inheritance: a compiled reference to a DuetHQ.* assembly must be a direct ProjectReference.
        var violations = new List<string>();
        foreach (var project in Solution.AnalysedAssemblies)
        {
            var compiled = Solution.LoadAssembly(project).GetReferencedAssemblies()
                .Select(a => a.Name!)
                .Where(name => name.StartsWith("DuetHQ.", StringComparison.Ordinal));

            violations.AddRange(compiled
                .Where(name => !project.ProjectReferences.Contains(name))
                .Select(name => $"{project.Name} is compiled against {name} without declaring it as a ProjectReference."));
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Insights_ProjectAndAssemblyReferences_NeverIncludePersonal()
    {
        // INV-05: Insights must have no path to Personal (safety evaluation and vault live there).
        var insights = new[] { ModuleCatalog.HostProject("Insights"), ModuleCatalog.ContractsProject("Insights") }
            .Select(name => Solution.Projects.Single(p => p.Name == name))
            .ToList();

        var violations = new List<string>();
        foreach (var project in insights)
        {
            violations.AddRange(project.ProjectReferences
                .Where(r => r.StartsWith("DuetHQ.Modules.Personal", StringComparison.Ordinal))
                .Select(r => $"{project.Name} declares a reference to {r}."));

            violations.AddRange(Solution.LoadAssembly(project).GetReferencedAssemblies()
                .Select(a => a.Name!)
                .Where(n => n.StartsWith("DuetHQ.Modules.Personal", StringComparison.Ordinal))
                .Select(n => $"{project.Name} is compiled against {n}."));
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Insights_Types_NeverDependOnPersonalTypes()
    {
        var rule = Types().That().ResideInNamespaceMatching(@"^DuetHQ\.Modules\.Insights(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^DuetHQ\.Modules\.Personal(\..*)?$")
            // TODO(P2-01): remove WithoutRequiringPositiveResults() (see the P2-01 sub-task in docs/IMPLEMENTATION_PLAN.md); until then this rule cannot fail on real code.
            .WithoutRequiringPositiveResults();

        Solution.AssertNoViolations(rule);
    }

    private static IEnumerable<string> ModulesReferencedBy(SourceProject host) =>
        host.ProjectReferences
            .Select(reference => ModuleProjectReference.Match(reference))
            .Where(match => match.Success && match.Groups["module"].Value != host.Module)
            .Select(match => match.Groups["module"].Value)
            .Distinct(StringComparer.Ordinal);
}
