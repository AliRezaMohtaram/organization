using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrgChart.EFCore;

/// <summary>Used only by <c>dotnet ef</c> to build migrations; never at runtime.</summary>
internal sealed class OrgChartDbContextDesignTimeFactory : IDesignTimeDbContextFactory<OrgChartDbContext>
{
    public OrgChartDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<OrgChartDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=OrgChartDesign;Trusted_Connection=True",
                sql => sql.MigrationsHistoryTable(OrgChartDbContext.MigrationsHistoryTable, OrgChartDbContext.Schema))
            .Options);
}
