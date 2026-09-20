using System.Text.RegularExpressions;
using DuetHQ.IntegrationTests.Infrastructure;

namespace DuetHQ.IntegrationTests;

// Needs no Docker. A drifting image makes the suite fail for reasons unrelated to our code, so the fixture and the compose file must agree.
public sealed class DockerComposeImageTests
{
    [Fact]
    public void PostgresImage_FixtureAndDockerCompose_AreTheSameExactMinorTag()
    {
        var images = File.ReadLines(Path.Combine(FindRepoRoot(), "docker-compose.yml"))
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("image:", StringComparison.Ordinal))
            .Select(line => line["image:".Length..].Trim())
            .Where(image => image.StartsWith("postgres:", StringComparison.Ordinal))
            .ToList();

        images.ShouldBe([PostgresFixture.Image]);
        Regex.IsMatch(PostgresFixture.Image, @"^postgres:\d+\.\d+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            .ShouldBeTrue("pin the exact minor (postgres:16.N), not a floating major, 'latest' or a digest.");
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DuetHQ.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException($"DuetHQ.slnx not found above {AppContext.BaseDirectory}.");
    }
}
