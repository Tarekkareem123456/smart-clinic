using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartClinic.Infrastructure.Persistence;

/// <summary>
/// T-004 — used ONLY by the EF Core design-time tools
/// (`dotnet ef migrations add/remove/script`).
///
/// It deliberately points at a non-existent placeholder database and reads
/// no environment variable, so that running `dotnet ef database update`
/// through the tools cannot silently touch a real database. Applying
/// migrations is done only through <see cref="MigrationApplier"/> (run by
/// the SmartClinic.Migrator executable from CI/CD as app_migrator).
/// </summary>
public sealed class SmartClinicDbContextFactory : IDesignTimeDbContextFactory<SmartClinicDbContext>
{
    private const string DesignTimeOnlyConnectionString =
        "Host=localhost;Database=design_time_only;Username=design_time_only";

    public SmartClinicDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SmartClinicDbContext>()
            .UseNpgsql(DesignTimeOnlyConnectionString)
            .Options;

        return new SmartClinicDbContext(options);
    }
}
