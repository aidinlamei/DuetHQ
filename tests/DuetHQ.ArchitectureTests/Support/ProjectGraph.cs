using System.Xml.Linq;

namespace DuetHQ.ArchitectureTests.Support;

// The ProjectReference closure, walked through ANY csproj in the repository. SourceProject only knows src/, which cannot see a
// src/ project that reaches test-only code through a project outside src/.
internal static class ProjectGraph
{
    // Full paths of the projects a csproj references. Throws when one does not resolve: a skipped reference would make every rule vacuous.
    public static IReadOnlyList<string> ReferencedProjectFiles(string projectFile)
    {
        var directory = Path.GetDirectoryName(projectFile)!;
        return XDocument.Load(projectFile).Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(include => !string.IsNullOrEmpty(include))
            .Select(include => Resolve(projectFile, directory, include!))
            .ToList();
    }

    // Breadth-first from the roots. Value = the chain of project names from a root to that project, for readable failures.
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ReachableFrom(IEnumerable<string> roots)
    {
        var reached = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        foreach (var root in roots)
        {
            if (reached.TryAdd(root, [NameOf(root)]))
            {
                queue.Enqueue(root);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in ReferencedProjectFiles(current))
            {
                if (reached.TryAdd(next, [.. reached[current], NameOf(next)]))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return reached;
    }

    public static string NameOf(string projectFile) => Path.GetFileNameWithoutExtension(projectFile);

    private static string Resolve(string projectFile, string directory, string include)
    {
        var full = Path.GetFullPath(Path.Combine(directory, include.Replace('\\', Path.DirectorySeparatorChar)));
        if (!File.Exists(full))
        {
            throw new FileNotFoundException($"{Path.GetFileName(projectFile)} references '{include}', which does not resolve to a file ({full}).");
        }

        return full;
    }
}
