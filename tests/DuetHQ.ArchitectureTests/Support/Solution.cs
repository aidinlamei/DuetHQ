using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;

namespace DuetHQ.ArchitectureTests.Support;

internal static class Solution
{
    private static readonly Lazy<IReadOnlyList<SourceProject>> LazyProjects = new(SourceProject.LoadAll);
    private static readonly Lazy<Architecture> LazyArchitecture = new(BuildArchitecture);

    public static IReadOnlyList<SourceProject> Projects => LazyProjects.Value;

    public static IEnumerable<SourceProject> Hosts => Projects.Where(p => p.Kind == ProjectKind.ModuleHost);

    public static IEnumerable<SourceProject> Contracts => Projects.Where(p => p.Kind == ProjectKind.ModuleContracts);

    // Every project ArchitectureTests references explicitly (Web.Client is only reachable through Web and is not analysed as an assembly).
    public static IEnumerable<SourceProject> AnalysedAssemblies => Projects.Where(p => p.Kind != ProjectKind.WebClient);

    public static Architecture Architecture => LazyArchitecture.Value;

    public static SourceProject Project(string name) => Projects.Single(p => p.Name == name);

    public static System.Reflection.Assembly LoadAssembly(SourceProject project) => System.Reflection.Assembly.Load(new AssemblyName(project.Name));

    public static void AssertNoViolations(IArchRule rule)
    {
        var violations = rule.Evaluate(Architecture).Where(r => !r.Passed).Select(r => r.Description).ToList();
        violations.ShouldBeEmpty($"{rule.Description}{Environment.NewLine}");
    }

    private static Architecture BuildArchitecture() =>
        new ArchLoader().LoadAssemblies(AnalysedAssemblies.Select(LoadAssembly).ToArray()).Build();
}
