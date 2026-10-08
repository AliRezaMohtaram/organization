using Microsoft.EntityFrameworkCore;

using OrgChart.Core.Model;
using OrgChart.EFCore.Configurations;

namespace OrgChart.EFCore;

public class OrgChartDbContext(DbContextOptions<OrgChartDbContext> options) : DbContext(options)
{
    public const string Schema = "org";
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";

    public DbSet<OrgUnitType> OrgUnitTypes => Set<OrgUnitType>();
    public DbSet<PositionType> PositionTypes => Set<PositionType>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrgChartDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All timestamps are stored as UTC; make sure they come back with Kind = Utc.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }
}
