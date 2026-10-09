using Acl.Core.Abstractions;
using Acl.Core.Model;
using Acl.EFCore;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.EFCore;

using static Acl.Core.Model.WellKnownActions;

namespace OrgChart.Acl.Tests;

/// <summary>OrgChart and Acl in one container, each on its own SQLite database, connected by <c>AddAcl()</c>.</summary>
public sealed class BridgeTests : IAsyncLifetime
{
    private readonly SqliteConnection _chartDb = new("DataSource=:memory:");
    private readonly SqliteConnection _aclDb = new("DataSource=:memory:");
    private ServiceProvider _services = null!;
    private int _approver;
    private int _budgeter;

    public async Task InitializeAsync()
    {
        _chartDb.Open();
        _aclDb.Open();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddOrgChart()
            .AddEntityFrameworkStore(o => o.UseSqlite(_chartDb))
            .AddAcl();
        services.AddAccessControl(o => o.ApplicationKey = "app")
            .AddEntityFrameworkStore(o => o.UseSqlite(_aclDb));
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        await RunAsync(async sp =>
        {
            await sp.GetRequiredService<OrgChartDbContext>().Database.EnsureCreatedAsync();
            AclDbContext acl = sp.GetRequiredService<AclDbContext>();
            await acl.Database.EnsureCreatedAsync();

            AclApplication app = new() { Key = "app", Name = "app" };
            acl.Applications.Add(app);
            await acl.SaveChangesAsync();
            Resource approve = new() { ApplicationId = app.Id, Key = "Approve", Title = "Approve", Type = ResourceType.Page };
            Resource budget = new() { ApplicationId = app.Id, Key = "Budget", Title = "Budget", Type = ResourceType.Page };
            Resource reports = new() { ApplicationId = app.Id, Key = "Reports", Title = "Reports", Type = ResourceType.Page };
            acl.Resources.AddRange(approve, budget, reports);
            Role approver = new() { Name = "Approver", ApplicationId = app.Id };
            Role budgeter = new() { Name = "Budgeter", ApplicationId = app.Id };
            Role staff = new() { Name = "Staff", ApplicationId = app.Id };
            acl.Roles.AddRange(approver, budgeter, staff);
            await acl.SaveChangesAsync();
            int viewId = await acl.Actions.Where(a => a.Key == View).Select(a => a.Id).SingleAsync();
            foreach ((Role role, Resource resource) in new[] { (approver, approve), (budgeter, budget), (staff, reports) })
            {
                Permission permission = new() { ResourceId = resource.Id, ActionId = viewId };
                acl.Permissions.Add(permission);
                await acl.SaveChangesAsync();
                acl.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id, Effect = PermissionEffect.Allow });
            }

            acl.PositionRoles.AddRange(
                new PositionRole { PositionKey = "MGR", RoleId = approver.Id },
                new PositionRole { PositionKey = "MGR", RoleId = budgeter.Id });
            acl.OrgUnitRoles.Add(new OrgUnitRole { OrgUnitKey = "FIN", RoleId = staff.Id });
            await acl.SaveChangesAsync();
            (_approver, _budgeter) = (approver.Id, budgeter.Id);

            IOrgChartAdministration chart = sp.GetRequiredService<IOrgChartAdministration>();
            await chart.CreateTypeAsync(OrgTypeKind.Unit, new OrgTypeInput("DEP", "اداره"));
            await chart.CreateUnitAsync(new UnitInput("HQ", "ستاد", "DEP"));
            await chart.CreateUnitAsync(new UnitInput("FIN", "مالی", "DEP", "HQ"));
            await chart.CreateUnitAsync(new UnitInput("PAY", "حقوق", "DEP", "FIN"));
            await chart.CreatePositionAsync(new PositionInput("MGR", "مدیر مالی", "FIN"));
            await chart.CreatePositionAsync(new PositionInput("DEPUTY", "معاون", "FIN"));
            await chart.AssignAsync(new AssignmentInput("MGR", "boss", ValidFrom: DateTime.UtcNow.AddDays(-10)));
        });
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        _chartDb.Dispose();
        _aclDb.Dispose();
    }

    private async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private Task RunAsync(Func<IServiceProvider, Task> action) => RunAsync<object?>(async sp =>
    {
        await action(sp);
        return null;
    });

    private Task<bool> CanAsync(string userId, string resource) =>
        RunAsync(async sp => (await sp.GetRequiredService<IAccessService>().GetAccessAsync(userId)).IsAllowed(resource, View));

    private Task ChartAsync(Func<IOrgChartAdministration, Task> action) =>
        RunAsync(sp => action(sp.GetRequiredService<IOrgChartAdministration>()));

    [Fact]
    public async Task Holder_gets_position_and_unit_roles()
    {
        Assert.True(await CanAsync("boss", "Approve"));
        Assert.True(await CanAsync("boss", "Budget"));
        Assert.True(await CanAsync("boss", "Reports"));
    }

    [Fact]
    public async Task Partial_delegation_passes_only_the_chosen_role_and_ending_it_takes_effect_at_once()
    {
        Assert.False(await CanAsync("helper", "Approve"));

        await ChartAsync(a => a.DelegateAsync(new DelegationInput("MGR", "boss", "helper", null, DateTime.UtcNow.AddDays(7), [_approver.ToString()])));

        Assert.True(await CanAsync("helper", "Approve"));
        Assert.False(await CanAsync("helper", "Budget"));
        Assert.False(await CanAsync("helper", "Reports"));
        Assert.True(await CanAsync("boss", "Budget"));

        int id = (await RunAsync(sp => sp.GetRequiredService<OrgChart.Core.Chart.IOrgChartReader>()
            .GetDelegationsAsync(new OrgChart.Core.Chart.DelegationQuery(ToUserId: "helper"))))[0].Id;
        await ChartAsync(a => a.EndDelegationAsync(id, DateTime.UtcNow));

        Assert.False(await CanAsync("helper", "Approve"));
    }

    [Fact]
    public async Task Deputy_position_holder_gets_the_scoped_roles_only_while_the_position_has_a_holder()
    {
        await ChartAsync(async a =>
        {
            await a.AssignAsync(new AssignmentInput("DEPUTY", "second"));
            await a.AddDeputyAsync(new DeputyInput("MGR", "DEPUTY", AuthorityKeys: [_budgeter.ToString()]));
        });

        Assert.True(await CanAsync("second", "Budget"));
        Assert.False(await CanAsync("second", "Approve"));

        int assignment = (await RunAsync(sp => sp.GetRequiredService<OrgChart.Core.Chart.IOrgChartReader>().GetUnitAssignmentsAsync("FIN")))
            .Single(x => x.UserId == "boss").Id;
        await ChartAsync(a => a.EndAssignmentAsync(assignment, DateTime.UtcNow));

        Assert.False(await CanAsync("second", "Budget"));
    }

    [Fact]
    public async Task Catalog_offers_the_position_roles_and_rejects_others()
    {
        IReadOnlyList<AuthorityInfo> offered = await RunAsync(sp => sp.GetRequiredService<IAuthorityCatalog>().GetAuthoritiesAsync("mgr"));

        Assert.Equal(["Approver", "Budgeter"], offered.Select(a => a.Title));
        Assert.Equal([_approver.ToString(), _budgeter.ToString()], offered.Select(a => a.Key));

        OrgChartAdminException ex = await Assert.ThrowsAsync<OrgChartAdminException>(() =>
            ChartAsync(a => a.DelegateAsync(new DelegationInput("MGR", "boss", "helper", null, DateTime.UtcNow.AddDays(1), ["999"]))));
        Assert.Equal(OrgChartErrors.UnknownAuthority, ex.Code);
    }

    [Fact]
    public async Task Structure_changes_bump_the_global_stamp_and_details_do_not()
    {
        Task<Guid> GlobalAsync() => RunAsync(async sp => (await sp.GetRequiredService<IAccessStampStore>().GetAsync("x")).Global);
        Guid before = await GlobalAsync();

        await ChartAsync(a => a.UpdateUnitAsync("PAY", new UnitUpdate("حقوق و دستمزد", "DEP", null, 0, null, null)));
        Assert.Equal(before, await GlobalAsync());

        await ChartAsync(a => a.MoveUnitAsync("PAY", "HQ"));
        Assert.NotEqual(before, await GlobalAsync());
    }

    [Fact]
    public async Task Org_structure_lists_units_positions_and_descendants()
    {
        await RunAsync(async sp =>
        {
            IOrgStructure org = sp.GetRequiredService<IOrgStructure>();
            Assert.IsType<OrgChartOrgStructure>(org);

            Assert.Equal(["HQ", "FIN", "PAY"], (await org.GetUnitsAsync()).Select(u => u.Key));
            Assert.Equal(["MGR", "DEPUTY"], (await org.GetPositionsAsync()).Select(p => p.Key).Order().Reverse());
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> descendants = await org.GetDescendantUnitKeysAsync(["hq", "PAY"]);
            Assert.Equal(["FIN", "PAY"], descendants["HQ"].Order());
            Assert.False(descendants.ContainsKey("PAY"));

            PositionAssignment held = Assert.Single(await org.GetUserPositionsAsync("boss"));
            Assert.Equal(("MGR", "FIN", PositionAssignmentKind.Holder), (held.PositionKey, held.OrgUnitKey, held.Kind));
            Assert.Equal(["HQ"], held.OrgUnitAncestorKeys);
        });
    }
}
