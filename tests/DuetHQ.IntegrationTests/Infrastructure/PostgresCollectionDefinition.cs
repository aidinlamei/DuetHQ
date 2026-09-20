namespace DuetHQ.IntegrationTests.Infrastructure;

// xUnit 2 has no assembly-level fixture: every integration test class joins this collection, so they share one container
// and run serially. Isolate by database (unique name per test class) and idempotent roles, never by starting more containers.
[CollectionDefinition(Name)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
