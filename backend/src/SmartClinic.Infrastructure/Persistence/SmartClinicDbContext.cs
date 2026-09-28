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
    public SmartClinicDbContext(DbContextOptions<SmartClinicDbContext> options)
        : base(options)
    {
    }
}
