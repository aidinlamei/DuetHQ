using Npgsql;

namespace DuetHQ.IntegrationTests.Infrastructure;

/// <summary>
/// One database inside the shared container, owned by one test class (see <see cref="PostgresFixture.DatabaseFor(Type)"/>).
/// Tables, schemas and grants live in this database only, so classes cannot see each other's data.
/// </summary>
/// <remarks>
/// ROLES ARE NOT ISOLATED. A PostgreSQL role is cluster-wide and every test class shares one container, so a role that is altered or
/// dropped in one class is altered or dropped for all of them. Hygiene rules (P3 depends on them):
/// the real <c>duethq_*</c> roles are created once per container by the fixture and never ALTERed or DROPped by tests;
/// a test that needs different role attributes creates its own uniquely named role (<see cref="UniqueRoleName"/>).
/// </remarks>
public sealed class TestDatabase
{
    private const int MaxRolePrefixLength = 40;

    private readonly string _suffix;

    internal TestDatabase(string name, string suffix, string adminConnectionString)
    {
        Name = name;
        _suffix = suffix;
        AdminConnectionString = adminConnectionString;
    }

    public string Name { get; }

    /// <summary>The container's superuser, connected to this database.</summary>
    public string AdminConnectionString { get; }

    /// <summary>A role name that no other test class can collide with: <c>{prefix}_{random part of this database's name}</c>.</summary>
    public string UniqueRoleName(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return $"{PostgresFixture.Sanitize(prefix, MaxRolePrefixLength)}_{_suffix}";
    }

    /// <summary>
    /// Creates the login role if it does not exist yet and returns a connection string for it on this database.
    /// Idempotent and NEVER alters: when the role already exists it is left exactly as it is, including its password,
    /// so pass the same password on every call for one role name. Roles are cluster-wide, see the type remarks.
    /// </summary>
    public async Task<string> EnsureRoleAsync(string roleName, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var exists = await ScalarRoleExistsAsync(roleName, cancellationToken);
        if (!exists)
        {
            try
            {
                await NpgsqlSql.ExecuteAsync(
                    AdminConnectionString,
                    $"CREATE ROLE {NpgsqlSql.QuoteIdentifier(roleName)} LOGIN PASSWORD {NpgsqlSql.QuoteLiteral(password)}",
                    cancellationToken);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.DuplicateObject)
            {
                // Created between the check and the CREATE: the outcome is the wanted one.
            }
        }

        return ConnectionStringFor(roleName, password);
    }

    public string ConnectionStringFor(string roleName, string password) =>
        new NpgsqlConnectionStringBuilder(AdminConnectionString) { Username = roleName, Password = password }.ConnectionString;

    private async Task<bool> ScalarRoleExistsAsync(string roleName, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @name)", connection);
        command.Parameters.AddWithValue("name", roleName);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
