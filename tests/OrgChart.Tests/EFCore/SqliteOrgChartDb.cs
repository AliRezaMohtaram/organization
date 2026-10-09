using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using OrgChart.EFCore;

namespace OrgChart.Tests.EFCore;

/// <summary>
/// An in-memory SQLite database that lives as long as this object. Create a fresh context per unit of work.
/// </summary>
public sealed class SqliteOrgChartDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<OrgChartDbContext> _options;

    public SqliteOrgChartDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<OrgChartDbContext>().UseSqlite(_connection).Options;

        using OrgChartDbContext db = CreateContext();
        db.Database.EnsureCreated();
    }

    public OrgChartDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
