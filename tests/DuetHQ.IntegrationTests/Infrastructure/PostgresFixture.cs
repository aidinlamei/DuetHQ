using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DuetHQ.IntegrationTests.Infrastructure;

// One PostgreSQL container for the whole test assembly (see PostgresCollectionDefinition).
public sealed class PostgresFixture : IAsyncLifetime
{
    // Exact minor, identical to docker-compose.yml (DockerComposeImageTests enforces it). Upgrade deliberately, in its own commit.
    public const string Image = "postgres:16.15";

    // 63 bytes is the PostgreSQL identifier limit; name = sanitised class name + '_' + 12 hex characters.
    private const int MaxClassNameLength = 40;

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();
    private readonly ConcurrentDictionary<Type, Lazy<Task<TestDatabase>>> _databases = new();

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

    /// <summary>
    /// The database of one test class: created on first use, then the same instance for every later call with that type.
    /// Never dropped; the container is disposed at the end of the run. Roles are cluster-wide and NOT isolated, see <see cref="TestDatabase"/>.
    /// </summary>
    public Task<TestDatabase> DatabaseFor<TTestClass>(CancellationToken cancellationToken = default) => DatabaseFor(typeof(TTestClass), cancellationToken);

    public Task<TestDatabase> DatabaseFor(Type testClass, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(testClass);

        // The creation is shared by every caller, so it must not be tied to the first caller's token: each caller only stops waiting.
        return _databases.GetOrAdd(testClass, type => new Lazy<Task<TestDatabase>>(() => CreateDatabaseAsync(type))).Value.WaitAsync(cancellationToken);
    }

    // Lower-case ASCII letters, digits and '_' only, so the result is a safe unquoted-style identifier and a readable database name.
    internal static string Sanitize(string value, int maxLength)
    {
        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var character in value)
        {
            if (builder.Length == maxLength)
            {
                break;
            }

            builder.Append(char.IsAsciiLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_');
        }

        return builder.ToString();
    }

    private async Task<TestDatabase> CreateDatabaseAsync(Type testClass)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var name = $"{Sanitize(testClass.Name, MaxClassNameLength)}_{suffix}";

        await NpgsqlSql.ExecuteAsync(AdminConnectionString, $"CREATE DATABASE {NpgsqlSql.QuoteIdentifier(name)}");

        var adminOnNewDatabase = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = name }.ConnectionString;
        return new TestDatabase(name, suffix, adminOnNewDatabase);
    }
}
