namespace DuetHQ.ArchitectureTests.Support;

internal static class ModuleCatalog
{
    public static IReadOnlyList<string> Modules { get; } =
    [
        "Organization", "Content", "CheckIn", "Personal", "Insights", "Actions", "Notifications", "Pilot",
    ];

    // Frozen copy of docs/ARCHITECTURE.md §5, column "Depends on (Contracts)": module -> modules it may depend on.
    // The declared graph must equal this EXACTLY, so any new edge, in either direction, fails the build.
    //
    // CheckIn <-> Insights is a deliberate mutual dependency at Contracts level
    // (§9.3 step 6 hands drafts to Insights; Insights consumes ParticipationRecorded from CheckIn). It is encoded, not "fixed".
    //
    // Expected future red build: P7-02 (prompt scheduler) and P11-02 (team broadcast) will need edges
    // that are not in §5 yet (e.g. Notifications -> CheckIn, Notifications -> Actions). Each of those tasks
    // adds its edge here AND to §5 in the same commit; the red build before that is intended, not a regression.
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedDependencies { get; } =
        new Dictionary<string, IReadOnlySet<string>>
        {
            ["Organization"] = Set(),
            ["Content"] = Set(),
            ["CheckIn"] = Set("Organization", "Content", "Personal", "Insights"),
            ["Personal"] = Set("Content"),
            ["Insights"] = Set("Organization", "Content", "CheckIn"),
            ["Actions"] = Set("Organization", "Insights"),
            ["Notifications"] = Set("Organization"),
            ["Pilot"] = Set("Organization"),
        };

    public static TheoryData<string> AsTheoryData()
    {
        var data = new TheoryData<string>();
        foreach (var module in Modules)
        {
            data.Add(module);
        }

        return data;
    }

    public static string HostProject(string module) => $"DuetHQ.Modules.{module}";

    public static string ContractsProject(string module) => $"DuetHQ.Modules.{module}.Contracts";

    public static string EntryPointName(string module) => $"{module}Module";

    private static HashSet<string> Set(params string[] items) => [.. items];
}
