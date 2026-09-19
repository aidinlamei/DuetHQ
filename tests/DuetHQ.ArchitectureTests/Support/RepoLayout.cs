namespace DuetHQ.ArchitectureTests.Support;

internal static class RepoLayout
{
    public static string Root { get; } = FindRoot();

    public static string Src { get; } = Path.Combine(Root, "src");

    // bin/ and obj/ hold generated code and copies of the sources; they must never be judged as sources.
    public static bool IsBuildOutput(string path)
    {
        var segments = Path.GetRelativePath(Root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(s => s is "bin" or "obj");
    }

    public static IEnumerable<string> EnumerateSourceFiles(string pattern) =>
        Directory.EnumerateFiles(Src, pattern, SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f))
            .OrderBy(f => f, StringComparer.Ordinal);

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DuetHQ.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException($"DuetHQ.slnx not found in any parent of {AppContext.BaseDirectory}.");
    }
}
