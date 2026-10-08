using Microsoft.EntityFrameworkCore;

using OrgChart.Core;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.EFCore;
using OrgChart.EFCore.Admin;
using OrgChart.EFCore.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class OrgChartBuilderEntityFrameworkExtensions
{
    /// <summary>Stores the chart in SQL Server, schema "org".</summary>
    public static OrgChartBuilder AddSqlServerStore(this OrgChartBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        return builder.AddEntityFrameworkStore(options => options.UseSqlServer(
            connectionString,
            sql => sql.MigrationsHistoryTable(OrgChartDbContext.MigrationsHistoryTable, OrgChartDbContext.Schema)));
    }

    /// <summary>Stores the chart through EF Core with a provider of your choice (e.g. SQLite in tests).</summary>
    public static OrgChartBuilder AddEntityFrameworkStore(this OrgChartBuilder builder, Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddScoped<AuditableEntityInterceptor>();
        builder.Services.AddDbContext<OrgChartDbContext>((services, options) =>
        {
            configure(options);
            options.AddInterceptors(services.GetRequiredService<AuditableEntityInterceptor>());
        });
        builder.Services.AddSingleton<SnapshotCache>();
        builder.Services.AddScoped<IOrgChartReader, EfOrgChartReader>();
        builder.Services.AddScoped<AuditWriter>();
        builder.Services.AddScoped<IOrgChartAdministration, EfOrgChartAdministration>();
        return builder;
    }
}
