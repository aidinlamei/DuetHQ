using System.Reflection;
using System.Runtime.CompilerServices;
using DuetHQ.ArchitectureTests.Support;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace DuetHQ.ArchitectureTests;

// R7 (§4): module types are internal by default. The only public types in a module host project are its <Module>Module
// entry point; everything in .Contracts is public by design and is not judged here.
public sealed class VisibilityTests
{
    public static TheoryData<string> Modules => ModuleCatalog.AsTheoryData();

    [Theory]
    [MemberData(nameof(Modules))]
    public void ModuleHostAssembly_Types_AreInternalExceptTheEntryPoint(string module)
    {
        var host = Solution.LoadAssembly(Solution.Projects.Single(p => p.Name == ModuleCatalog.HostProject(module)));
        var entryPoint = $"{ModuleCatalog.HostProject(module)}.{ModuleCatalog.EntryPointName(module)}";

        Solution.AssertNoViolations(
            Types().That().ResideInAssembly(host.FullName!).And().DoNotHaveFullName(entryPoint)
                .Should().NotBePublic()
                // TODO(P2-01): remove WithoutRequiringPositiveResults() (see the P2-01 sub-task in docs/IMPLEMENTATION_PLAN.md); until then this rule cannot fail on real code.
                .WithoutRequiringPositiveResults());
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void ModuleEntryPoint_Exists_AsPublicStaticClassInTheRootNamespace(string module)
    {
        var host = Solution.LoadAssembly(Solution.Projects.Single(p => p.Name == ModuleCatalog.HostProject(module)));

        // Non-vacuity guard: the host assembly must actually contain the anchor the other rules rely on.
        var entryPoint = host.GetType($"{ModuleCatalog.HostProject(module)}.{ModuleCatalog.EntryPointName(module)}");

        entryPoint.ShouldNotBeNull();
        (entryPoint.IsPublic && entryPoint.IsAbstract && entryPoint.IsSealed).ShouldBeTrue("the entry point must be a public static class.");
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void ModuleRootNamespace_Types_AreOnlyTheEntryPoint(string module)
    {
        // The root namespace is exempt from the layering rules, so nothing but the entry point may live there.
        var host = Solution.LoadAssembly(Solution.Projects.Single(p => p.Name == ModuleCatalog.HostProject(module)));

        var rootTypes = host.GetTypes()
            .Where(t => t.Namespace == ModuleCatalog.HostProject(module) && t.DeclaringType is null && t.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .Select(t => t.Name);

        rootTypes.ShouldBe([ModuleCatalog.EntryPointName(module)]);
    }
}
