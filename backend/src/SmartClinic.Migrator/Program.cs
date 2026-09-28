using SmartClinic.Infrastructure.Persistence;

// T-004 — applies EF Core migrations. Intended to be run by CI/CD only
// (Technical Plan: "Test -> build image -> apply migrations -> deploy").
//
// The connection string comes ONLY from an environment variable supplied by
// the pipeline's secret store (Backend Stack Q34). Nothing is read from,
// or committed to, the repository. MigrationApplier additionally refuses to
// run unless the connection authenticates as the T-003 `app_migrator` role.
const string ConnectionStringVariable = "SMARTCLINIC_MIGRATOR_CONNECTION_STRING";

var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine($"{ConnectionStringVariable} is not set. Refusing to run.");
    return 2;
}

try
{
    await MigrationApplier.ApplyAsync(connectionString);
    Console.WriteLine("Migrations applied.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Migration failed: {ex.Message}");
    return 1;
}
