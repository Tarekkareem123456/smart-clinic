using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SmartClinic.Infrastructure.Persistence;

/// <summary>
/// T-004 — the single controlled way to apply EF Core migrations.
///
/// Used by the SmartClinic.Migrator executable (run from CI/CD) and by the
/// Testcontainers integration tests. It must NOT be called from the
/// running API (scripts/check-migration-guards.mjs enforces that).
///
/// Safety guard: it refuses to run unless the connection authenticates as
/// the T-003 `app_migrator` role. That keeps schema changes on the
/// least-privileged migration identity (never a superuser, never
/// app_runtime / app_reports) and makes the T-003 role split a real
/// precondition of the pipeline instead of a convention.
///
/// History protection: T-003's default privileges give app_runtime CRUD on
/// every table app_migrator creates, which would include EF's
/// __EFMigrationsHistory. After migrating, this class revokes
/// INSERT/UPDATE/DELETE on that one table from app_runtime and app_reports
/// (SELECT is left as granted by T-003). T-003 itself is unchanged.
/// </summary>
public static class MigrationApplier
{
    /// <summary>Database role created by T-003 (backend/database/bootstrap).</summary>
    public const string RequiredDatabaseRole = "app_migrator";

    // Static SQL, no interpolation. EF's default history table in the
    // single `public` schema (Backend Stack: one schema). REVOKE is
    // idempotent, so this is safe on every run.
    private const string RestrictHistoryWritesSql =
        "REVOKE INSERT, UPDATE, DELETE ON TABLE \"public\".\"__EFMigrationsHistory\" " +
        "FROM app_runtime, app_reports";

    public static async Task ApplyAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A connection string is required.", nameof(connectionString));
        }

        await EnsureConnectedAsMigratorAsync(connectionString, cancellationToken);

        var options = new DbContextOptionsBuilder<SmartClinicDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var context = new SmartClinicDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);

        await RestrictHistoryWritesAsync(connectionString, cancellationToken);
    }

    private static async Task RestrictHistoryWritesAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(RestrictHistoryWritesSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureConnectedAsMigratorAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand("SELECT current_user", connection);
        var currentUser = (string?)await command.ExecuteScalarAsync(cancellationToken);

        if (!string.Equals(currentUser, RequiredDatabaseRole, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Migrations must be applied as database role '{RequiredDatabaseRole}' " +
                $"(connected as '{currentUser}'). See backend/database/MIGRATIONS.md.");
        }
    }
}
