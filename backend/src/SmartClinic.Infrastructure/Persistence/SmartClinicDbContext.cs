using Microsoft.EntityFrameworkCore;

namespace SmartClinic.Infrastructure.Persistence;

/// <summary>
/// T-004 — the EF Core DbContext that owns the versioned migrations
/// mechanism.
///
/// Intentionally defines NO entities. Domain/application schema (Patient,
/// Appointment, AuditLogEntry, ...) is owned by the tasks that introduce
/// those models; T-004 only provides the pipeline they will use. Visit
/// remains a shell/boundary and is not modeled here.
/// </summary>
public sealed class SmartClinicDbContext : DbContext
{
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public SmartClinicDbContext(DbContextOptions<SmartClinicDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("AuditLogEntry");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Id).UseIdentityByDefaultColumn();
            entity.Property(entry => entry.EntityType).IsRequired();
            entity.Property(entry => entry.EntityId).IsRequired();
            entity.Property(entry => entry.OldValue);
            entity.Property(entry => entry.NewValue);
            entity.Property(entry => entry.CreatedAtUtc).IsRequired();
            // entityType + entityId are intentionally polymorphic: no FK.
        });
    }
}
