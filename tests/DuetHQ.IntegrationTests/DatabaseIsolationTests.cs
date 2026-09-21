using DuetHQ.IntegrationTests.Infrastructure;

namespace DuetHQ.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class DatabaseIsolationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task DatabaseFor_SameTestClassTwice_ReturnsTheSameDatabase()
    {
        var first = await postgres.DatabaseFor<DatabaseIsolationTests>();
        var second = await postgres.DatabaseFor(GetType());

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public async Task DatabaseFor_TwoTestClasses_GetDifferentDatabases()
    {
        var mine = await postgres.DatabaseFor<DatabaseIsolationTests>();
        var other = await postgres.DatabaseFor<OtherClassMarker>();

        other.Name.ShouldNotBe(mine.Name);
        mine.Name.ShouldStartWith("databaseisolationtests_");
        other.Name.Length.ShouldBeLessThanOrEqualTo(63);
    }

    [Fact]
    public async Task DatabaseFor_TableCreatedInOneClassDatabase_IsInvisibleInAnother()
    {
        var mine = await postgres.DatabaseFor<DatabaseIsolationTests>();
        var other = await postgres.DatabaseFor<OtherClassMarker>();
        await NpgsqlSql.ExecuteAsync(mine.AdminConnectionString, "CREATE TABLE IF NOT EXISTS public.isolation_probe (id int)");

        var inMine = await NpgsqlSql.ScalarAsync<bool>(mine.AdminConnectionString, "SELECT to_regclass('public.isolation_probe') IS NOT NULL");
        var inOther = await NpgsqlSql.ScalarAsync<bool>(other.AdminConnectionString, "SELECT to_regclass('public.isolation_probe') IS NOT NULL");

        inMine.ShouldBeTrue();
        inOther.ShouldBeFalse();
    }

    [Fact]
    public async Task DatabaseFor_ConcurrentFirstUse_CreatesExactlyOneDatabase()
    {
        var databases = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => postgres.DatabaseFor<ConcurrencyMarker>()));

        databases.Select(d => d.Name).Distinct().Count().ShouldBe(1);
    }

    // Stand-ins for "another test class"; they are never instantiated.
    private sealed class OtherClassMarker;

    private sealed class ConcurrencyMarker;
}
