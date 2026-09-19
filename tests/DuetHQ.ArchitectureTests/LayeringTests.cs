using DuetHQ.ArchitectureTests.Support;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace DuetHQ.ArchitectureTests;

// R4 / ADR-0009. Layers are namespaces inside one assembly per module (DuetHQ.Modules.<Module>.<Layer>), so every rule is
// expressed over namespaces; NamespaceIntegrityTests guarantees a file cannot sit outside the namespace its folder implies.
//
//   Domain         -> SharedKernel only
//   Application    -> Domain + Application.Abstractions + SharedKernel + any module's .Contracts (its own included)
//   Infrastructure -> the above + Infrastructure.Common + persistence/infra packages
//   Endpoints      -> Application + Contracts (+ SharedKernel, Application.Abstractions) + ASP.NET Core
//
// Exemptions, and the only ones: the module root namespace (where <Module>Module lives; VisibilityTests keeps it to that one type)
// and the .Endpoints namespace, which is the only layer that may use ASP.NET Core once endpoints exist.
// The banned prefixes apply to .Domain and .Application only. Rules on an empty layer pass by design until code exists;
// each has been proven to bite with a deliberate violation (see the P1-01 notes in docs/IMPLEMENTATION_PLAN.md).
public sealed class LayeringTests
{
    private const string BannedPrefixes = @"^(Microsoft|Npgsql|Serilog)(\..*)?$|^System\.Data(\..*)?$";

    // Domain is pure and synchronous: no async or I/O types either.
    private const string BannedDomainOnly = @"^System\.(Threading\.Tasks|IO|Net)(\..*)?$";

    public static TheoryData<string> Modules => ModuleCatalog.AsTheoryData();

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_Types_DependOnlyOnOwnDomainAndSharedKernel(string module)
    {
        var forbidden = Any(
            DuetHQExcept($@"SharedKernel|Modules\.{module}\.Domain"),
            BannedPrefixes,
            BannedDomainOnly);

        Solution.AssertNoViolations(
            Types().That().ResideInNamespaceMatching(Layer(module, "Domain"))
                .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(forbidden)
                .WithoutRequiringPositiveResults());
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Application_Types_DependOnlyOnDomainAbstractionsSharedKernelAndContracts(string module)
    {
        var forbidden = Any(
            DuetHQExcept($@"SharedKernel|Application\.Abstractions|Modules\.{module}\.(Domain|Application)|Modules\.[A-Za-z]+\.Contracts"),
            BannedPrefixes);

        Solution.AssertNoViolations(
            Types().That().ResideInNamespaceMatching(Layer(module, "Application"))
                .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(forbidden)
                .WithoutRequiringPositiveResults());
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Infrastructure_Types_NeverDependOnEndpointsOrOtherModulesInternals(string module)
    {
        var forbidden = DuetHQExcept($@"SharedKernel|Application\.Abstractions|Infrastructure\.Common|Modules\.{module}\.(Domain|Application|Infrastructure)|Modules\.[A-Za-z]+\.Contracts");

        Solution.AssertNoViolations(
            Types().That().ResideInNamespaceMatching(Layer(module, "Infrastructure"))
                .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(forbidden)
                .WithoutRequiringPositiveResults());
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Endpoints_Types_NeverDependOnDomainInfrastructureOrOtherModulesInternals(string module)
    {
        var forbidden = DuetHQExcept($@"SharedKernel|Application\.Abstractions|Modules\.{module}\.(Application|Endpoints)|Modules\.[A-Za-z]+\.Contracts");

        Solution.AssertNoViolations(
            Types().That().ResideInNamespaceMatching(Layer(module, "Endpoints"))
                .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(forbidden)
                .WithoutRequiringPositiveResults());
    }

    private static string Layer(string module, string layer) => $@"^DuetHQ\.Modules\.{module}\.{layer}(\..*)?$";

    // Every DuetHQ.* namespace except the allowed ones (negative lookahead).
    private static string DuetHQExcept(string allowedAlternation) => $@"^DuetHQ\.(?!({allowedAlternation})(\.|$))";

    private static string Any(params string[] patterns) => string.Join('|', patterns);
}
