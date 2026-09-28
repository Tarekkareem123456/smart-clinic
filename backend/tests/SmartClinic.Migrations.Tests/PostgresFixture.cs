using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace SmartClinic.Migrations.Tests;

/// <summary>
/// One disposable PostgreSQL container per test class (Testcontainers).
/// Each test gets its own freshly created database with the real T-003
/// bootstrap applied, so tests are isolated and order-independent.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string AdminUser = "postgres";
    private const string AdminPassword = "postgres";
    private const string AdminDatabase = "postgres";

    // Test-only passwords for a throwaway container. T-003 intentionally sets
    // no passwords in the repository; real environments set them from the
    // secret manager.
    public const string MigratorPassword = "test-only-migrator";
    public const string RuntimePassword = "test-only-runtime";
    public const string ReportsPassword = "test-only-reports";

    private static readonly string[] BootstrapFiles =
    {
        "database/bootstrap/001_roles.sql",
        "database/bootstrap/002_extensions.sql",
        "database/bootstrap/003_default_privileges.sql",
    };

    // Test-infrastructure image choice only; the production PostgreSQL major
    // version is not specified by the frozen documents (reported in T-004).
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase(AdminDatabase)
        .WithUsername(AdminUser)
        .WithPassword(AdminPassword)
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Creates a new empty database and applies the T-003 bootstrap files to
    /// it, unchanged and in order, then sets test-only role passwords.
    /// Returns the database name.
    /// </summary>
    public async Task<string> CreateBaselinedDatabaseAsync()
    {
        var databaseName = "t004_" + Guid.NewGuid().ToString("N");

        await RunPsqlAsync(AdminDatabase, $"CREATE DATABASE \"{databaseName}\"");

        foreach (var file in BootstrapFiles)
        {
            await RunSqlFileAsync(databaseName, file);
        }

        await RunPsqlAsync(
            AdminDatabase,
            $"ALTER ROLE app_migrator WITH PASSWORD '{MigratorPassword}'; " +
            $"ALTER ROLE app_runtime WITH PASSWORD '{RuntimePassword}'; " +
            $"ALTER ROLE app_reports WITH PASSWORD '{ReportsPassword}';");

        return databaseName;
    }

    public async Task RunSqlFileAsync(string database, string relativePath)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"SQL file was not copied to the test output: {relativePath}", fullPath);
        }

        await RunPsqlAsync(database, await File.ReadAllTextAsync(fullPath));
    }

    public string ConnectionStringFor(string role, string password, string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Username = role,
            Password = password,
            Database = database,
            Pooling = false,
        };

        return builder.ConnectionString;
    }

    public string AdminConnectionStringFor(string database) =>
        ConnectionStringFor(AdminUser, AdminPassword, database);

    // Runs the SQL with the container's own psql, stopping at the first error
    // (the same way backend/database/README.md documents running these files).
    private async Task RunPsqlAsync(string database, string sql)
    {
        var result = await _container.ExecAsync(new[]
        {
            "psql", "-v", "ON_ERROR_STOP=1", "-U", AdminUser, "-d", database, "-c", sql,
        });

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"psql failed (exit {result.ExitCode}): {result.Stderr}");
        }
    }
}
