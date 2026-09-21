using DuetHQ.IntegrationTests.Infrastructure;
using Npgsql;

namespace DuetHQ.IntegrationTests;

// Roles are cluster-wide and shared by every test class, so this class only ever creates its own uniquely named roles
// (TestDatabase.UniqueRoleName) and never ALTERs or DROPs any role. Grants are per database, so they stay inside this class's database.
[Collection(PostgresCollectionDefinition.Name)]
public sealed class MultiRoleTests(PostgresFixture postgres)
{
    private const string ReaderPassword = "reader-test-only";
    private const string WriterPassword = "writer-test-only";

    [Fact]
    public async Task Reader_SelectOnGrantedTable_Succeeds()
    {
        var setup = await ArrangeAsync();
        await NpgsqlSql.ExecuteAsync(setup.Database.AdminConnectionString, "INSERT INTO sb.probe (note) VALUES ('seen by reader')");

        var rows = await NpgsqlSql.ScalarAsync<long>(setup.ReaderConnectionString, "SELECT count(*) FROM sb.probe");

        rows.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Reader_InsertWithoutGrant_IsDeniedWithInsufficientPrivilege()
    {
        var setup = await ArrangeAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => NpgsqlSql.ExecuteAsync(setup.ReaderConnectionString, "INSERT INTO sb.probe (note) VALUES ('reader must not write')"));

        exception.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task Writer_InsertOnGrantedTable_Succeeds()
    {
        var setup = await ArrangeAsync();

        await NpgsqlSql.ExecuteAsync(setup.WriterConnectionString, "INSERT INTO sb.probe (note) VALUES ('written by writer')");

        var rows = await NpgsqlSql.ScalarAsync<long>(setup.Database.AdminConnectionString, "SELECT count(*) FROM sb.probe WHERE note = 'written by writer'");
        rows.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Writer_SelectWithoutGrant_IsDeniedWithInsufficientPrivilege()
    {
        var setup = await ArrangeAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => NpgsqlSql.ScalarAsync<long>(setup.WriterConnectionString, "SELECT count(*) FROM sb.probe"));

        exception.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task EnsureRoleAsync_CalledAgainWithAnotherPassword_DoesNotAlterTheExistingRole()
    {
        var database = await postgres.DatabaseFor<MultiRoleTests>();
        var role = database.UniqueRoleName("keeps_first_password");

        var first = await database.EnsureRoleAsync(role, "first-password");
        var second = await database.EnsureRoleAsync(role, "second-password");

        // Still usable with the FIRST password: the second call must not have altered the role.
        (await NpgsqlSql.ScalarAsync<int>(first, "SELECT 1")).ShouldBe(1);
        var exception = await Should.ThrowAsync<PostgresException>(() => NpgsqlSql.ScalarAsync<int>(second, "SELECT 1"));
        exception.SqlState.ShouldBe(PostgresErrorCodes.InvalidPassword);
    }

    // Idempotent on purpose: xUnit builds one class instance per test, and all of them share this database.
    private async Task<Setup> ArrangeAsync()
    {
        var database = await postgres.DatabaseFor<MultiRoleTests>();
        var reader = database.UniqueRoleName("reader");
        var writer = database.UniqueRoleName("writer");
        var readerConnection = await database.EnsureRoleAsync(reader, ReaderPassword);
        var writerConnection = await database.EnsureRoleAsync(writer, WriterPassword);

        var admin = database.AdminConnectionString;
        await NpgsqlSql.ExecuteAsync(admin, "CREATE SCHEMA IF NOT EXISTS sb");
        await NpgsqlSql.ExecuteAsync(admin, "CREATE TABLE IF NOT EXISTS sb.probe (id uuid PRIMARY KEY DEFAULT gen_random_uuid(), note text NOT NULL)");
        await NpgsqlSql.ExecuteAsync(admin, $"GRANT USAGE ON SCHEMA sb TO {NpgsqlSql.QuoteIdentifier(reader)}, {NpgsqlSql.QuoteIdentifier(writer)}");
        await NpgsqlSql.ExecuteAsync(admin, $"GRANT SELECT ON sb.probe TO {NpgsqlSql.QuoteIdentifier(reader)}");
        await NpgsqlSql.ExecuteAsync(admin, $"GRANT INSERT ON sb.probe TO {NpgsqlSql.QuoteIdentifier(writer)}");

        return new Setup(database, readerConnection, writerConnection);
    }

    private sealed record Setup(TestDatabase Database, string ReaderConnectionString, string WriterConnectionString);
}
