using Npgsql;

namespace DuetHQ.IntegrationTests.Infrastructure;

// Small helpers for test setup and assertions. Identifiers and literals cannot be bound as parameters in utility statements
// (CREATE DATABASE / CREATE ROLE), so they are quoted here; every value that CAN be a parameter still is one.
internal static class NpgsqlSql
{
    public static async Task ExecuteAsync(string connectionString, string sql, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<T> ScalarAsync<T>(string connectionString, string sql, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public static string QuoteIdentifier(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    public static string QuoteLiteral(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
