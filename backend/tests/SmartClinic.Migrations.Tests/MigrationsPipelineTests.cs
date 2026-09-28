using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartClinic.Infrastructure.Persistence;
using Xunit;

namespace SmartClinic.Migrations.Tests;

/// <summary>
/// T-004 acceptance evidence: migrations are applied to a disposable
/// PostgreSQL container (Testcontainers), through the project-controlled
/// MigrationApplier, on top of the real T-003 baseline.
/// A missing Docker daemon makes these tests FAIL (never skip), so a
/// pipeline cannot succeed without exercising the migrations.
/// </summary>
public sealed class MigrationsPipelineTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;

    public MigrationsPipelineTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Migrations_apply_as_app_migrator_and_are_recorded_in_history()
    {
        var database = await _pg.CreateBaselinedDatabaseAsync();
        var migrator = _pg.ConnectionStringFor("app_migrator", PostgresFixture.MigratorPassword, database);

        await MigrationApplier.ApplyAsync(migrator);

        var declared = DeclaredMigrations(migrator);
        Assert.NotEmpty(declared);

        var applied = await QueryStringsAsync(
            migrator, "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"");
        Assert.Equal(declared, applied);

        var owner = await QueryStringsAsync(
            migrator,
            "SELECT tableowner FROM pg_tables WHERE schemaname = 'public' AND tablename = '__EFMigrationsHistory'");
        Assert.Equal("app_migrator", Assert.Single(owner));
    }

    [Fact]
    public async Task Applying_twice_is_idempotent()
    {
        var database = await _pg.CreateBaselinedDatabaseAsync();
        var migrator = _pg.ConnectionStringFor("app_migrator", PostgresFixture.MigratorPassword, database);

        await MigrationApplier.ApplyAsync(migrator);
        await MigrationApplier.ApplyAsync(migrator);

        var applied = await QueryStringsAsync(
            migrator, "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"");
        Assert.Equal(DeclaredMigrations(migrator), applied);
    }

    [Fact]
    public async Task Applier_refuses_any_identity_other_than_app_migrator()
    {
        var database = await _pg.CreateBaselinedDatabaseAsync();

        var superuser = _pg.AdminConnectionStringFor(database);
        var runtime = _pg.ConnectionStringFor("app_runtime", PostgresFixture.RuntimePassword, database);
        var reports = _pg.ConnectionStringFor("app_reports", PostgresFixture.ReportsPassword, database);

        await Assert.ThrowsAsync<InvalidOperationException>(() => MigrationApplier.ApplyAsync(superuser));
        await Assert.ThrowsAsync<InvalidOperationException>(() => MigrationApplier.ApplyAsync(runtime));
        await Assert.ThrowsAsync<InvalidOperationException>(() => MigrationApplier.ApplyAsync(reports));

        // Nothing was created by the refused attempts.
        var historyTables = await QueryStringsAsync(
            superuser,
            "SELECT count(*)::text FROM pg_tables WHERE tablename = '__EFMigrationsHistory'");
        Assert.Equal("0", Assert.Single(historyTables));
    }

    [Fact]
    public async Task Runtime_and_reports_roles_have_no_schema_change_path()
    {
        var database = await _pg.CreateBaselinedDatabaseAsync();
        var migrator = _pg.ConnectionStringFor("app_migrator", PostgresFixture.MigratorPassword, database);
        await MigrationApplier.ApplyAsync(migrator);

        var runtime = _pg.ConnectionStringFor("app_runtime", PostgresFixture.RuntimePassword, database);
        var reports = _pg.ConnectionStringFor("app_reports", PostgresFixture.ReportsPassword, database);

        foreach (var connectionString in new[] { runtime, reports })
        {
            await AssertPermissionDeniedAsync(connectionString, "CREATE TABLE t004_should_fail (id int)");
            await AssertPermissionDeniedAsync(connectionString, "DROP TABLE \"__EFMigrationsHistory\"");
            await AssertPermissionDeniedAsync(connectionString, "ALTER TABLE \"__EFMigrationsHistory\" ADD COLUMN x int");
        }
    }

    [Fact]
    public async Task Runtime_can_read_but_not_modify_migration_history()
    {
        var database = await _pg.CreateBaselinedDatabaseAsync();
        var migrator = _pg.ConnectionStringFor("app_migrator", PostgresFixture.MigratorPassword, database);
        await MigrationApplier.ApplyAsync(migrator);

        var runtime = _pg.ConnectionStringFor("app_runtime", PostgresFixture.RuntimePassword, database);
        await AssertHistoryReadOnlyAsync(runtime);
    }

    [Fact]
    public async Task Reports_can_read_but_not_modify_migration_history()
    {
        var database = await _pg.CreateBaselinedDatabaseAsync();
        var migrator = _pg.ConnectionStringFor("app_migrator", PostgresFixture.MigratorPassword, database);
        await MigrationApplier.ApplyAsync(migrator);

        var reports = _pg.ConnectionStringFor("app_reports", PostgresFixture.ReportsPassword, database);
        await AssertHistoryReadOnlyAsync(reports);
    }

    [Fact]
    public async Task History_protection_survives_reapplying_and_migrator_can_still_apply()
    {
        var database = await _pg.CreateBaselinedDatabaseAsync();
        var migrator = _pg.ConnectionStringFor("app_migrator", PostgresFixture.MigratorPassword, database);

        await MigrationApplier.ApplyAsync(migrator);
        await MigrationApplier.ApplyAsync(migrator); // idempotent, still works as app_migrator

        var applied = await QueryStringsAsync(
            migrator, "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"");
        Assert.Equal(DeclaredMigrations(migrator), applied);

        await AssertHistoryReadOnlyAsync(_pg.ConnectionStringFor("app_runtime", PostgresFixture.RuntimePassword, database));
        await AssertHistoryReadOnlyAsync(_pg.ConnectionStringFor("app_reports", PostgresFixture.ReportsPassword, database));
    }

    [Fact]
    public async Task History_restriction_is_scoped_to_that_table_and_leaves_T003_defaults_intact()
    {
        var database = await _pg.CreateBaselinedDatabaseAsync();
        var migrator = _pg.ConnectionStringFor("app_migrator", PostgresFixture.MigratorPassword, database);
        await MigrationApplier.ApplyAsync(migrator);

        // A throwaway table created by app_migrator AFTER the pipeline ran
        // (a stand-in for any future table; not a domain model).
        await ExecuteAsync(migrator, "CREATE TABLE t004_scope_probe (id int)");

        var runtime = _pg.ConnectionStringFor("app_runtime", PostgresFixture.RuntimePassword, database);
        var reports = _pg.ConnectionStringFor("app_reports", PostgresFixture.ReportsPassword, database);

        // T-003 defaults unchanged: runtime keeps CRUD, reports keeps SELECT only.
        await ExecuteAsync(runtime, "INSERT INTO t004_scope_probe (id) VALUES (1)");
        await ExecuteAsync(runtime, "UPDATE t004_scope_probe SET id = 2");
        await ExecuteAsync(runtime, "DELETE FROM t004_scope_probe");
        await QueryStringsAsync(reports, "SELECT count(*)::text FROM t004_scope_probe");
        await AssertPermissionDeniedAsync(reports, "INSERT INTO t004_scope_probe (id) VALUES (1)");
    }

    [Fact]
    public async Task T003_verify_script_passes_on_the_baselined_database()
    {
        var database = await _pg.CreateBaselinedDatabaseAsync();

        // Runs backend/database/verify/verify_t003.sql unchanged; any failed
        // assertion inside it raises an exception and fails this test.
        await _pg.RunSqlFileAsync(database, "database/verify/verify_t003.sql");
    }

    private static async Task AssertHistoryReadOnlyAsync(string connectionString)
    {
        // Reading is allowed (T-003 SELECT default is unchanged)...
        var count = await QueryStringsAsync(connectionString, "SELECT count(*)::text FROM \"__EFMigrationsHistory\"");
        Assert.NotEmpty(count);

        // ...modification is not.
        await AssertPermissionDeniedAsync(
            connectionString,
            "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('x_Forged', '0')");
        await AssertPermissionDeniedAsync(connectionString, "UPDATE \"__EFMigrationsHistory\" SET \"ProductVersion\" = '0'");
        await AssertPermissionDeniedAsync(connectionString, "DELETE FROM \"__EFMigrationsHistory\"");
        await AssertPermissionDeniedAsync(connectionString, "TRUNCATE TABLE \"__EFMigrationsHistory\"");
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static IReadOnlyList<string> DeclaredMigrations(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SmartClinicDbContext>().UseNpgsql(connectionString).Options;
        using var context = new SmartClinicDbContext(options);
        return context.Database.GetMigrations().ToList();
    }

    private static async Task<List<string>> QueryStringsAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static async Task AssertPermissionDeniedAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("42501", exception.SqlState); // insufficient_privilege
    }
}
