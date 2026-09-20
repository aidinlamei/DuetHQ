using System.Diagnostics;
using Testcontainers.PostgreSql;

namespace DuetHQ.IntegrationTests.Infrastructure;

// One PostgreSQL container for the whole test assembly (see PostgresCollectionDefinition).
public sealed class PostgresFixture : IAsyncLifetime
{
    // Exact minor, identical to docker-compose.yml (DockerComposeImageTests enforces it). Upgrade deliberately, in its own commit.
    public const string Image = "postgres:16.15";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    public string AdminConnectionString => _container.GetConnectionString();

    // Includes the image pull when the image is not cached; CI pulls it in a separate step first.
    public TimeSpan StartupTime { get; private set; }

    public async Task InitializeAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        await _container.StartAsync();
        StartupTime = stopwatch.Elapsed;
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
