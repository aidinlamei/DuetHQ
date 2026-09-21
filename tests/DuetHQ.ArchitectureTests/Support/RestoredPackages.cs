using System.Text.Json;

namespace DuetHQ.ArchitectureTests.Support;

// The EFFECTIVE, restored package set of a project, read from obj/project.assets.json. Unlike the csproj it includes packages
// injected by Directory.Build.props and transitive packages, and it saves us from re-implementing MSBuild evaluation.
internal static class RestoredPackages
{
    // Package ids (no versions). Throws when the assets file is missing: tests run after restore/build, so a missing file is a
    // broken setup, and skipping it would silently make the rule pass.
    public static IReadOnlySet<string> Read(string projectDirectory)
    {
        var assetsFile = Path.Combine(projectDirectory, "obj", "project.assets.json");
        if (!File.Exists(assetsFile))
        {
            throw new FileNotFoundException($"{assetsFile} does not exist. Restore the solution before running the architecture tests; the rule must never be skipped.");
        }

        using var stream = File.OpenRead(assetsFile);
        using var document = JsonDocument.Parse(stream);

        var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var library in document.RootElement.GetProperty("libraries").EnumerateObject())
        {
            if (library.Value.GetProperty("type").GetString() != "package")
            {
                continue;
            }

            // Keys look like "Serilog/4.4.0".
            packages.Add(library.Name[..library.Name.IndexOf('/', StringComparison.Ordinal)]);
        }

        return packages;
    }
}
