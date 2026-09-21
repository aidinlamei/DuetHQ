using System.Text.RegularExpressions;

namespace DuetHQ.ArchitectureTests.Support;

// Packages that only tests may use. Anchored, case-insensitive: the base package "Testcontainers" is pulled in by
// Testcontainers.PostgreSql and must be caught too, so the pattern is ^Testcontainers(\..*)?$ and not a literal "Testcontainers.*".
internal static class TestOnlyPackages
{
    private static readonly Regex[] Patterns =
    [
        Create(@"^Microsoft\.Extensions\.TimeProvider\.Testing$"),
        Create(@"^Testcontainers(\..*)?$"),
        Create(@"^xunit"),
        Create(@"^Shouldly$"),
        Create(@"^Microsoft\.NET\.Test\.Sdk$"),
    ];

    public static bool IsTestOnly(string packageId) => Patterns.Any(pattern => pattern.IsMatch(packageId));

    private static Regex Create(string pattern) =>
        new(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
}
