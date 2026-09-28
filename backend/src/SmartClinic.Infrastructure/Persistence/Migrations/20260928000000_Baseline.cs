using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartClinic.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// T-004 baseline: intentionally empty. Proves the versioned-migrations
    /// pipeline end to end (EF creates and populates __EFMigrationsHistory)
    /// without inventing any application/domain schema. The first real
    /// schema migration is added by the task that introduces that model.
    /// </summary>
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
