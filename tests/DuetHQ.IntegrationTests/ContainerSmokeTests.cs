using DuetHQ.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit.Abstractions;

namespace DuetHQ.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class ContainerSmokeTests(PostgresFixture postgres, ITestOutputHelper output)
{
    [Fact]
    public async Task Container_Started_AnswersQueriesOnPostgres16()
    {
        await using var connection = new NpgsqlConnection(postgres.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT current_setting('server_version_num')::int", connection);

        var version = (int)(await command.ExecuteScalarAsync())!;

        output.WriteLine($"postgres container startup: {postgres.StartupTime.TotalSeconds:F1}s (image {PostgresFixture.Image}, server_version_num {version})");
        version.ShouldBeInRange(160000, 169999);
    }
}
