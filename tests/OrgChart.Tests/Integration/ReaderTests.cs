using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;

namespace OrgChart.Tests.Integration;

public sealed class ReaderTests : IDisposable
{
    private static readonly DateTime s_jan = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly OrgChartServices _s = new();

    public void Dispose() => _s.Dispose();

    [Fact]
    public async Task User_positions_include_past_current_and_future_with_ancestors()
    {
        await _s.SeedAsync();
        await _s.AdminAsync(async a =>
        {
            await a.AssignAsync(new AssignmentInput("POS-ACC", "u1", ValidFrom: s_jan, ValidTo: s_jan.AddMonths(1)));
            await a.AssignAsync(new AssignmentInput("POS-CLERK", "u1", ValidFrom: s_jan.AddMonths(1)));
            await a.AssignAsync(new AssignmentInput("POS-CFO", "u1", AssignmentKind.Acting, s_jan.AddMonths(6), s_jan.AddMonths(7)));
            await a.AssignAsync(new AssignmentInput("POS-ACC", "u2"));
        });

        IReadOnlyList<UserPosition> positions = await _s.ReadAsync(r => r.GetUserPositionsAsync("u1"));

        Assert.Equal(["POS-ACC", "POS-CLERK", "POS-CFO"], positions.Select(p => p.PositionKey));
        UserPosition clerk = positions[1];
        Assert.Equal(("ACC", "Accounting"), (clerk.OrgUnitKey, clerk.OrgUnitTitle));
        Assert.Equal(["FIN", "C"], clerk.OrgUnitAncestorKeys);
        Assert.Equal((s_jan.AddMonths(1), (DateTime?)null), (clerk.ValidFrom!.Value, clerk.ValidTo));
        Assert.Equal(AssignmentKind.Acting, positions[2].Kind);
        Assert.Empty(await _s.ReadAsync(r => r.GetUserPositionsAsync("nobody")));
    }

    [Fact]
    public async Task User_position_period_is_narrowed_to_position_and_unit_validity()
    {
        await _s.SeedAsync();
        await _s.AdminAsync(async a =>
        {
            await a.AssignAsync(new AssignmentInput("POS-CLERK", "u1", ValidFrom: s_jan));
            await a.AssignAsync(new AssignmentInput("POS-ACC", "u1", ValidFrom: s_jan));
            await a.UpdateUnitAsync("ACC", new UnitUpdate("Accounting", "DEPARTMENT", null, 0, s_jan.AddMonths(2), s_jan.AddMonths(12)));
            await a.UpdatePositionAsync("POS-CLERK", new PositionUpdate("Clerk", null, false, null, 0, null, s_jan.AddMonths(6)));

            // The position ended before the assignment starts: nothing is left of it.
            await a.UpdatePositionAsync("POS-ACC", new PositionUpdate("Accountant", null, false, null, 0, null, s_jan));
        });

        UserPosition clerk = Assert.Single(await _s.ReadAsync(r => r.GetUserPositionsAsync("u1")));
        Assert.Equal((s_jan.AddMonths(2), s_jan.AddMonths(6)), (clerk.ValidFrom!.Value, clerk.ValidTo!.Value));
    }

    [Fact]
    public async Task Unit_assignments_and_holders()
    {
        await _s.SeedAsync();
        await _s.AdminAsync(async a =>
        {
            await a.AssignAsync(new AssignmentInput("POS-ACC", "u1", ValidFrom: s_jan, ValidTo: s_jan.AddMonths(1)));
            await a.AssignAsync(new AssignmentInput("POS-ACC", "u2", ValidFrom: s_jan.AddMonths(1)));
            await a.AssignAsync(new AssignmentInput("POS-CLERK", "u3"));
        });

        IReadOnlyList<AssignmentInfo> fin = await _s.ReadAsync(r => r.GetUnitAssignmentsAsync("fin"));
        Assert.Equal(["u1", "u2"], fin.Select(a => a.UserId));
        IReadOnlyList<AssignmentInfo> finTree = await _s.ReadAsync(r => r.GetUnitAssignmentsAsync("FIN", includeSubUnits: true));
        Assert.Equal(["u1", "u2", "u3"], finTree.Select(a => a.UserId).Order());

        IReadOnlyDictionary<string, IReadOnlyList<AssignmentInfo>> holders =
            await _s.ReadAsync(r => r.GetHoldersAsync(["pos-acc", "POS-CLERK", "POS-CFO"], s_jan.AddDays(40)));
        Assert.Equal("u2", Assert.Single(holders["POS-ACC"]).UserId);
        Assert.Equal("u3", Assert.Single(holders["pos-clerk"]).UserId);
        Assert.False(holders.ContainsKey("POS-CFO"));
    }

    [Fact]
    public async Task Managers_walk_up_to_the_nearest_held_manager_position()
    {
        await _s.SeedAsync();
        DateTime now = _s.Now;
        await _s.AdminAsync(async a =>
        {
            await a.AssignAsync(new AssignmentInput("POS-CLERK", "clerk"));
            await a.AssignAsync(new AssignmentInput("POS-CEO", "ceo"));
        });

        // FIN's head (CFO) is vacant, so the clerk reports to the CEO.
        ManagerInfo manager = Assert.Single(await _s.ReadAsync(r => r.GetManagersAsync("clerk", now)));
        Assert.Equal(("POS-CLERK", "POS-CEO", "ceo"), (manager.ForPositionKey, manager.ManagerPositionKey, Assert.Single(manager.Holders).UserId));

        await _s.AdminAsync(a => a.AssignAsync(new AssignmentInput("POS-CFO", "cfo", AssignmentKind.Acting)));
        manager = Assert.Single(await _s.ReadAsync(r => r.GetManagersAsync("clerk", now)));
        Assert.Equal("POS-CFO", manager.ManagerPositionKey);

        // The CFO reports to the CEO; the CEO to nobody.
        Assert.Equal("POS-CEO", Assert.Single(await _s.ReadAsync(r => r.GetManagersAsync("cfo", now))).ManagerPositionKey);
        Assert.Empty(await _s.ReadAsync(r => r.GetManagersAsync("ceo", now)));
    }

    [Fact]
    public async Task Snapshot_is_cached_until_the_chart_changes_even_on_another_server()
    {
        await _s.SeedAsync();
        ServiceProvider other = _s.CreateServer();

        OrgChartSnapshot first = await _s.ReadAsync(r => r.GetSnapshotAsync(), other);
        Assert.Same(first, await _s.ReadAsync(r => r.GetSnapshotAsync(), other));

        // An assignment does not touch the snapshot.
        await _s.AdminAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", "u1")));
        Assert.Same(first, await _s.ReadAsync(r => r.GetSnapshotAsync(), other));

        // A change made on the first server is seen on the other one.
        await _s.AdminAsync(a => a.UpdateUnitAsync("FIN", new UnitUpdate("Finance 2", "DEPARTMENT", null, 0, null, null)));
        OrgChartSnapshot second = await _s.ReadAsync(r => r.GetSnapshotAsync(), other);
        Assert.NotSame(first, second);
        Assert.Equal("Finance 2", second.FindUnit("FIN")!.Title);
    }

    [Fact]
    public async Task Rejected_change_does_not_move_the_stamp_on_other_servers()
    {
        await _s.SeedAsync();
        ServiceProvider other = _s.CreateServer();
        OrgChartSnapshot first = await _s.ReadAsync(r => r.GetSnapshotAsync(), other);

        await Assert.ThrowsAsync<OrgChartAdminException>(() => _s.AdminAsync(a => a.MoveUnitAsync("C", "ACC")));

        Assert.Same(first, await _s.ReadAsync(r => r.GetSnapshotAsync(), other));
    }

    [Fact]
    public async Task Inactive_positions_are_left_out_of_user_positions()
    {
        await _s.SeedAsync();
        int id = await _s.AdminAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", "u1", ValidFrom: s_jan, ValidTo: s_jan.AddMonths(1))));
        await _s.AdminAsync(a => a.SetPositionActiveAsync("POS-ACC", false));

        Assert.Empty(await _s.ReadAsync(r => r.GetUserPositionsAsync("u1")));
        Assert.True(await _s.DbAsync(db => db.Assignments.AnyAsync(a => a.Id == id)));
    }
}
