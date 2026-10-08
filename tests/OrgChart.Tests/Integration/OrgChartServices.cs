using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using OrgChart.Core;
using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.EFCore;

namespace OrgChart.Tests.Integration;

/// <summary>
/// Real DI + SQLite in memory, with a manual clock, a settable current user and a recording change listener.
/// <see cref="CreateServer"/> gives a second "server" (own container and caches) on the same database.
/// </summary>
public sealed class OrgChartServices : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly List<ServiceProvider> _providers = [];

    public OrgChartServices()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Provider = Build();

        using IServiceScope scope = Provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<OrgChartDbContext>().Database.EnsureCreated();
    }

    public ServiceProvider Provider { get; }
    public ManualTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero));
    public TestCurrentUser CurrentUser { get; } = new();
    public ChangeLog Changes { get; } = new();

    public DateTime Now => Clock.GetUtcNow().UtcDateTime;

    /// <summary>Another "server": its own container and caches, the same database.</summary>
    public ServiceProvider CreateServer(Action<OrgChartBuilder>? configure = null) => Build(configure);

    /// <summary>Runs <paramref name="action"/> in a new scope, like one request.</summary>
    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action, ServiceProvider? server = null)
    {
        await using AsyncServiceScope scope = (server ?? Provider).CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public Task RunAsync(Func<IServiceProvider, Task> action, ServiceProvider? server = null) =>
        RunAsync<object?>(async services =>
        {
            await action(services);
            return null;
        }, server);

    public Task AdminAsync(Func<IOrgChartAdministration, Task> action) =>
        RunAsync(services => action(services.GetRequiredService<IOrgChartAdministration>()));

    public Task<T> AdminAsync<T>(Func<IOrgChartAdministration, Task<T>> action) =>
        RunAsync(services => action(services.GetRequiredService<IOrgChartAdministration>()));

    public Task<T> ReadAsync<T>(Func<IOrgChartReader, Task<T>> action, ServiceProvider? server = null) =>
        RunAsync(services => action(services.GetRequiredService<IOrgChartReader>()), server);

    public Task<T> DbAsync<T>(Func<OrgChartDbContext, Task<T>> action) =>
        RunAsync(services => action(services.GetRequiredService<OrgChartDbContext>()));

    /// <summary>A small chart: company C → finance FIN (head POS-CFO, POS-ACC) → accounting ACC (POS-CLERK).</summary>
    public async Task SeedAsync()
    {
        await AdminAsync(async admin =>
        {
            await admin.CreateTypeAsync(OrgTypeKind.Unit, new OrgTypeInput("COMPANY", "شرکت", 1));
            await admin.CreateTypeAsync(OrgTypeKind.Unit, new OrgTypeInput("DEPARTMENT", "اداره", 2));
            await admin.CreateTypeAsync(OrgTypeKind.Position, new OrgTypeInput("MANAGER", "مدیریتی"));
            await admin.CreateUnitAsync(new UnitInput("C", "Company", "COMPANY"));
            await admin.CreateUnitAsync(new UnitInput("FIN", "Finance", "DEPARTMENT", "C"));
            await admin.CreateUnitAsync(new UnitInput("ACC", "Accounting", "DEPARTMENT", "FIN"));
            await admin.CreatePositionAsync(new PositionInput("POS-CEO", "CEO", "C", "MANAGER", IsManagerial: true));
            await admin.CreatePositionAsync(new PositionInput("POS-CFO", "CFO", "FIN", "MANAGER", IsManagerial: true));
            await admin.CreatePositionAsync(new PositionInput("POS-ACC", "Accountant", "FIN"));
            await admin.CreatePositionAsync(new PositionInput("POS-CLERK", "Clerk", "ACC"));
            await admin.SetUnitManagerAsync("C", "POS-CEO");
            await admin.SetUnitManagerAsync("FIN", "POS-CFO");
        });
        Changes.Clear();
    }

    public void Dispose()
    {
        foreach (ServiceProvider provider in _providers)
        {
            provider.Dispose();
        }

        _connection.Dispose();
    }

    private ServiceProvider Build(Action<OrgChartBuilder>? configure = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton(CurrentUser);
        services.AddSingleton(Changes);
        OrgChartBuilder builder = services.AddOrgChart()
            .AddEntityFrameworkStore(options => options.UseSqlite(_connection))
            .AddCurrentUser<TestCurrentUserAccessor>()
            .AddChangeListener<RecordingListener>();
        configure?.Invoke(builder);

        ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _providers.Add(provider);
        return provider;
    }
}

public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

public sealed class TestCurrentUser
{
    public string? UserId { get; set; } = "admin";
}

internal sealed class TestCurrentUserAccessor(TestCurrentUser user) : ICurrentUser
{
    public string? UserId => user.UserId;
}

public sealed class ChangeLog
{
    private readonly List<OrgChartChange> _changes = [];

    public IReadOnlyList<OrgChartChange> All
    {
        get
        {
            lock (_changes)
            {
                return _changes.ToList();
            }
        }
    }

    public void Add(OrgChartChange change)
    {
        lock (_changes)
        {
            _changes.Add(change);
        }
    }

    public void Clear()
    {
        lock (_changes)
        {
            _changes.Clear();
        }
    }
}

internal sealed class RecordingListener(ChangeLog log) : IOrgChartChangeListener
{
    public Task OnChangedAsync(OrgChartChange change, CancellationToken cancellationToken = default)
    {
        log.Add(change);
        return Task.CompletedTask;
    }
}
