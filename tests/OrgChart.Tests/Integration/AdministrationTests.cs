using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;

namespace OrgChart.Tests.Integration;

public sealed class AdministrationTests : IDisposable
{
    private readonly OrgChartServices _s = new();

    public void Dispose() => _s.Dispose();

    private async Task<string> ExpectErrorAsync(Func<IOrgChartAdministration, Task> action)
    {
        OrgChartAdminException error = await Assert.ThrowsAsync<OrgChartAdminException>(() => _s.AdminAsync(action));
        return error.Code;
    }

    private Task<OrgChartSnapshot> SnapshotAsync() => _s.ReadAsync(r => r.GetSnapshotAsync());

    // ---------------------------------------------------------------- units

    [Fact]
    public async Task Create_unit_records_audit_and_notifies_structure_change()
    {
        await _s.SeedAsync();

        await _s.AdminAsync(a => a.CreateUnitAsync(new UnitInput("TRE", " خزانه ", "department", "fin", Code: "D-7", SortOrder: 3)));

        UnitNode unit = (await SnapshotAsync()).FindUnit("TRE")!;
        Assert.Equal("خزانه", unit.Title);
        Assert.Equal("DEPARTMENT", unit.TypeKey);
        Assert.Equal("FIN", unit.ParentKey);
        Assert.Equal("D-7", unit.Code);

        OrgChartChange change = Assert.Single(_s.Changes.All);
        Assert.Equal(OrgChartChangeKind.Structure, change.Kind);

        AuditLog log = await _s.DbAsync(db => db.AuditLogs.OrderBy(l => l.Id).LastAsync());
        Assert.Equal("UnitCreated", log.Operation);
        Assert.Equal("TRE", log.EntityId);
        Assert.Equal("admin", log.ActorUserId);
        Assert.Contains("خزانه", log.ChangeJson);

        OrgUnit row = await _s.DbAsync(db => db.OrgUnits.SingleAsync(u => u.Key == "TRE"));
        Assert.Equal("admin", row.CreatedBy);
        Assert.Equal(_s.Now, row.CreatedAt);
    }

    [Theory]
    [InlineData("bad key", OrgChartErrors.KeyInvalid)]
    [InlineData("fin", OrgChartErrors.KeyTaken)]
    public async Task Create_unit_validates_key(string key, string code)
    {
        await _s.SeedAsync();

        Assert.Equal(code, await ExpectErrorAsync(a => a.CreateUnitAsync(new UnitInput(key, "X", "DEPARTMENT", "C"))));
    }

    [Fact]
    public async Task Create_unit_validates_references_and_values()
    {
        await _s.SeedAsync();

        Assert.Equal(OrgChartErrors.TitleRequired, await ExpectErrorAsync(a => a.CreateUnitAsync(new UnitInput("X", "  ", "DEPARTMENT"))));
        Assert.Equal(OrgChartErrors.TitleTooLong, await ExpectErrorAsync(a => a.CreateUnitAsync(new UnitInput("X", new string('x', 257), "DEPARTMENT"))));
        Assert.Equal(OrgChartErrors.TypeNotFound, await ExpectErrorAsync(a => a.CreateUnitAsync(new UnitInput("X", "X", "NOPE"))));
        Assert.Equal(OrgChartErrors.ParentNotFound, await ExpectErrorAsync(a => a.CreateUnitAsync(new UnitInput("X", "X", "DEPARTMENT", "NOPE"))));
        await _s.AdminAsync(a => a.CreateUnitAsync(new UnitInput("X", "X", "DEPARTMENT", Code: "A")));
        Assert.Equal(OrgChartErrors.CodeTaken, await ExpectErrorAsync(a => a.CreateUnitAsync(new UnitInput("Y", "Y", "DEPARTMENT", Code: "A"))));
        Assert.Equal(OrgChartErrors.CodeTaken, await ExpectErrorAsync(a => a.UpdateUnitAsync("FIN", new UnitUpdate("Finance", "DEPARTMENT", " A ", 0, null, null))));
        Assert.Equal(OrgChartErrors.InvalidPeriod, await ExpectErrorAsync(a => a.CreateUnitAsync(new UnitInput("Z", "Z", "DEPARTMENT",
            ValidFrom: new DateTime(2026, 2, 1), ValidTo: new DateTime(2026, 2, 1)))));

        await _s.AdminAsync(a => a.SetTypeActiveAsync(OrgTypeKind.Unit, "DEPARTMENT", false));
        Assert.Equal(OrgChartErrors.TypeInactive, await ExpectErrorAsync(a => a.CreateUnitAsync(new UnitInput("W", "W", "DEPARTMENT"))));
    }

    [Fact]
    public async Task Failed_operation_leaves_nothing_behind()
    {
        await _s.SeedAsync();

        await _s.AdminAsync(async a =>
        {
            // Title is applied before the type check fails; the same scope then does a valid change.
            await Assert.ThrowsAsync<OrgChartAdminException>(() =>
                a.UpdateUnitAsync("FIN", new UnitUpdate("Changed", "NOPE", null, 0, null, null)));
            await a.UpdateUnitAsync("ACC", new UnitUpdate("Accounting 2", "DEPARTMENT", null, 0, null, null));
        });

        OrgChartSnapshot chart = await SnapshotAsync();
        Assert.Equal("Finance", chart.FindUnit("FIN")!.Title);
        Assert.Equal("Accounting 2", chart.FindUnit("ACC")!.Title);
    }

    [Fact]
    public async Task Update_unit_title_is_a_details_change_and_validity_a_structure_change()
    {
        await _s.SeedAsync();

        await _s.AdminAsync(a => a.UpdateUnitAsync("FIN", new UnitUpdate("Finance & Treasury", "DEPARTMENT", "F", 5, null, null)));
        await _s.AdminAsync(a => a.UpdateUnitAsync("FIN", new UnitUpdate("Finance & Treasury", "DEPARTMENT", "F", 5, null, new DateTime(2027, 1, 1))));
        await _s.AdminAsync(a => a.UpdateUnitAsync("FIN", new UnitUpdate("Finance & Treasury", "DEPARTMENT", "F", 5, null, new DateTime(2027, 1, 1))));

        Assert.Equal([OrgChartChangeKind.Details, OrgChartChangeKind.Structure], _s.Changes.All.Select(c => c.Kind));
        UnitNode fin = (await SnapshotAsync()).FindUnit("FIN")!;
        Assert.Equal("Finance & Treasury", fin.Title);
        Assert.Equal(DateTimeKind.Utc, fin.ValidTo!.Value.Kind);
        Assert.Equal(2, await _s.DbAsync(db => db.AuditLogs.CountAsync(l => l.Operation == "UnitUpdated")));
    }

    [Fact]
    public async Task Move_unit_keeps_keys_and_subtree()
    {
        await _s.SeedAsync();
        await _s.AdminAsync(a => a.CreateUnitAsync(new UnitInput("OPS", "Operations", "DEPARTMENT", "C")));

        await _s.AdminAsync(a => a.MoveUnitAsync("FIN", "OPS"));

        OrgChartSnapshot chart = await SnapshotAsync();
        Assert.Equal(["FIN", "OPS", "C"], chart.GetAncestorKeys("ACC"));
        Assert.Equal("ACC", chart.FindUnit("acc")!.Key);
        AuditLog log = await _s.DbAsync(db => db.AuditLogs.OrderBy(l => l.Id).LastAsync());
        Assert.Equal("UnitMoved", log.Operation);
        Assert.Contains("\"OPS\"", log.ChangeJson);

        await _s.AdminAsync(a => a.MoveUnitAsync("FIN", null));
        Assert.Null((await SnapshotAsync()).FindUnit("FIN")!.ParentKey);
    }

    [Fact]
    public async Task Move_unit_rejects_cycles()
    {
        await _s.SeedAsync();

        Assert.Equal(OrgChartErrors.ParentCycle, await ExpectErrorAsync(a => a.MoveUnitAsync("FIN", "FIN")));
        Assert.Equal(OrgChartErrors.ParentCycle, await ExpectErrorAsync(a => a.MoveUnitAsync("C", "ACC")));
        Assert.Equal(OrgChartErrors.ParentNotFound, await ExpectErrorAsync(a => a.MoveUnitAsync("ACC", "NOPE")));
    }

    [Fact]
    public async Task Moving_to_the_same_parent_changes_nothing()
    {
        await _s.SeedAsync();

        await _s.AdminAsync(a => a.MoveUnitAsync("ACC", "fin"));

        Assert.Empty(_s.Changes.All);
    }

    [Fact]
    public async Task Deactivate_unit_requires_empty_unit_and_reactivate_requires_active_parent()
    {
        await _s.SeedAsync();

        Assert.Equal(OrgChartErrors.UnitHasActiveChildren, await ExpectErrorAsync(a => a.SetUnitActiveAsync("FIN", false)));
        Assert.Equal(OrgChartErrors.UnitHasActivePositions, await ExpectErrorAsync(a => a.SetUnitActiveAsync("ACC", false)));

        await _s.AdminAsync(async a =>
        {
            await a.SetPositionActiveAsync("POS-CLERK", false);
            await a.SetUnitActiveAsync("ACC", false);
        });
        Assert.False((await SnapshotAsync()).FindUnit("ACC")!.IsActive);

        Assert.Equal(OrgChartErrors.UnitInactive, await ExpectErrorAsync(a => a.CreatePositionAsync(new PositionInput("P", "P", "ACC"))));
        Assert.Equal(OrgChartErrors.ParentInactive, await ExpectErrorAsync(a => a.CreateUnitAsync(new UnitInput("SUB", "Sub", "DEPARTMENT", "ACC"))));
        Assert.Equal(OrgChartErrors.ParentInactive, await ExpectErrorAsync(a => a.MoveUnitAsync("FIN", "ACC")));

        await _s.AdminAsync(a => a.SetUnitActiveAsync("ACC", true));
        Assert.True((await SnapshotAsync()).FindUnit("ACC")!.IsActive);
    }

    [Fact]
    public async Task Unit_manager_must_be_an_active_position_of_the_unit()
    {
        await _s.SeedAsync();

        Assert.Equal(OrgChartErrors.ManagerPositionNotInUnit, await ExpectErrorAsync(a => a.SetUnitManagerAsync("ACC", "POS-CFO")));
        Assert.Equal(OrgChartErrors.PositionNotFound, await ExpectErrorAsync(a => a.SetUnitManagerAsync("ACC", "NOPE")));

        await _s.AdminAsync(a => a.SetUnitManagerAsync("ACC", "POS-CLERK"));
        Assert.Equal("POS-CLERK", (await SnapshotAsync()).FindUnit("ACC")!.ManagerPositionKey);

        await _s.AdminAsync(a => a.SetUnitManagerAsync("ACC", null));
        Assert.Null((await SnapshotAsync()).FindUnit("ACC")!.ManagerPositionKey);
    }

    // ---------------------------------------------------------------- positions

    [Fact]
    public async Task Position_create_update_move()
    {
        await _s.SeedAsync();

        await _s.AdminAsync(a => a.CreatePositionAsync(new PositionInput("POS-AUD", "Auditor", "fin", Code: "P-9")));
        await _s.AdminAsync(a => a.UpdatePositionAsync("pos-aud", new PositionUpdate("Senior auditor", "MANAGER", true, "P-9", 1, null, null)));
        await _s.AdminAsync(a => a.MovePositionAsync("POS-AUD", "ACC"));

        PositionNode position = (await SnapshotAsync()).FindPosition("POS-AUD")!;
        Assert.Equal(("Senior auditor", "MANAGER", true, "ACC"), (position.Title, position.TypeKey, position.IsManagerial, position.UnitKey));
        Assert.Equal(
            [OrgChartChangeKind.Structure, OrgChartChangeKind.Details, OrgChartChangeKind.Structure],
            _s.Changes.All.Select(c => c.Kind));

        Assert.Equal(OrgChartErrors.KeyTaken, await ExpectErrorAsync(a => a.CreatePositionAsync(new PositionInput("pos-aud", "X", "FIN"))));
        Assert.Equal(OrgChartErrors.CodeTaken, await ExpectErrorAsync(a => a.CreatePositionAsync(new PositionInput("POS-X", "X", "FIN", Code: "P-9"))));
        Assert.Equal(OrgChartErrors.UnitNotFound, await ExpectErrorAsync(a => a.CreatePositionAsync(new PositionInput("POS-Y", "Y", "NOPE"))));
    }

    [Fact]
    public async Task Unit_head_cannot_move_or_be_deactivated()
    {
        await _s.SeedAsync();

        Assert.Equal(OrgChartErrors.PositionIsUnitManager, await ExpectErrorAsync(a => a.MovePositionAsync("POS-CFO", "ACC")));
        Assert.Equal(OrgChartErrors.PositionIsUnitManager, await ExpectErrorAsync(a => a.SetPositionActiveAsync("POS-CFO", false)));
    }

    [Fact]
    public async Task Position_with_current_or_future_holder_cannot_be_deactivated()
    {
        await _s.SeedAsync();
        int id = await _s.AdminAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", "u1", ValidFrom: _s.Now.AddDays(10))));

        Assert.Equal(OrgChartErrors.PositionHasAssignments, await ExpectErrorAsync(a => a.SetPositionActiveAsync("POS-ACC", false)));

        await _s.AdminAsync(a => a.EndAssignmentAsync(id, _s.Now.AddDays(20)));
        _s.Clock.Advance(TimeSpan.FromDays(21));
        await _s.AdminAsync(a => a.SetPositionActiveAsync("POS-ACC", false));

        Assert.False((await SnapshotAsync()).FindPosition("POS-ACC")!.IsActive);
        Assert.Equal(OrgChartErrors.PositionInactive, await ExpectErrorAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", "u2"))));
    }

    // ---------------------------------------------------------------- types

    [Fact]
    public async Task Types_are_separate_per_kind()
    {
        await _s.AdminAsync(async a =>
        {
            await a.CreateTypeAsync(OrgTypeKind.Unit, new OrgTypeInput("TEAM", "تیم"));
            await a.CreateTypeAsync(OrgTypeKind.Position, new OrgTypeInput("team", "Team lead"));
            await a.UpdateTypeAsync(OrgTypeKind.Unit, "Team", new OrgTypeUpdate("تیم کاری", 4));
        });

        OrgChartSnapshot chart = await SnapshotAsync();
        Assert.Equal(("تیم کاری", 4), (chart.FindUnitType("TEAM")!.Title, chart.FindUnitType("TEAM")!.SortOrder));
        Assert.Equal("Team lead", chart.FindPositionType("TEAM")!.Title);
        Assert.Equal(OrgChartErrors.KeyTaken, await ExpectErrorAsync(a => a.CreateTypeAsync(OrgTypeKind.Unit, new OrgTypeInput("team", "x"))));
        Assert.Equal(OrgChartErrors.TypeNotFound, await ExpectErrorAsync(a => a.UpdateTypeAsync(OrgTypeKind.Position, "NOPE", new OrgTypeUpdate("x", 0))));
        Assert.All(_s.Changes.All, c => Assert.Equal(OrgChartChangeKind.Details, c.Kind));
    }

    // ---------------------------------------------------------------- assignments

    [Fact]
    public async Task Assign_notifies_only_that_user()
    {
        await _s.SeedAsync();

        int id = await _s.AdminAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", " u1 ", Note: " first ")));

        OrgChartChange change = Assert.Single(_s.Changes.All);
        Assert.Equal(OrgChartChangeKind.Assignments, change.Kind);
        Assert.Equal(["u1"], change.UserIds);

        Assignment row = await _s.DbAsync(db => db.Assignments.SingleAsync(a => a.Id == id));
        Assert.Equal(("u1", "first", AssignmentKind.Primary), (row.UserId, row.Note, row.Kind));

        AuditLog log = await _s.DbAsync(db => db.AuditLogs.OrderBy(l => l.Id).LastAsync());
        Assert.Equal(("AssignmentCreated", id.ToString()), (log.Operation, log.EntityId));
        Assert.Equal("POS-ACC", JsonDocument.Parse(log.ChangeJson!).RootElement.GetProperty("after").GetProperty("Position").GetString());
    }

    [Fact]
    public async Task Same_user_cannot_hold_a_position_twice_in_overlapping_periods()
    {
        await _s.SeedAsync();
        DateTime jan = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime feb = jan.AddMonths(1);
        await _s.AdminAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", "u1", ValidFrom: jan, ValidTo: feb)));

        Assert.Equal(OrgChartErrors.AssignmentOverlap,
            await ExpectErrorAsync(a => a.AssignAsync(new AssignmentInput("pos-acc", "u1", ValidFrom: jan.AddDays(10)))));

        // Back to back is fine, and so is another user or another position.
        await _s.AdminAsync(async a =>
        {
            await a.AssignAsync(new AssignmentInput("POS-ACC", "u1", ValidFrom: feb));
            await a.AssignAsync(new AssignmentInput("POS-ACC", "u2", ValidFrom: jan));
            await a.AssignAsync(new AssignmentInput("POS-CLERK", "u1", AssignmentKind.Acting, jan));
        });
    }

    [Fact]
    public async Task Assignment_input_is_validated()
    {
        await _s.SeedAsync();

        Assert.Equal(OrgChartErrors.UserIdRequired, await ExpectErrorAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", " "))));
        Assert.Equal(OrgChartErrors.PositionNotFound, await ExpectErrorAsync(a => a.AssignAsync(new AssignmentInput("NOPE", "u1"))));
        Assert.Equal(OrgChartErrors.InvalidKind, await ExpectErrorAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", "u1", (AssignmentKind)9))));
        Assert.Equal(OrgChartErrors.NoteTooLong, await ExpectErrorAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", "u1", Note: new string('n', 1001)))));
        Assert.Equal(OrgChartErrors.AssignmentNotFound, await ExpectErrorAsync(a => a.EndAssignmentAsync(999, _s.Now)));
    }

    [Fact]
    public async Task End_assignment()
    {
        await _s.SeedAsync();
        int id = await _s.AdminAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", "u1", ValidFrom: _s.Now)));

        Assert.Equal(OrgChartErrors.InvalidEndDate, await ExpectErrorAsync(a => a.EndAssignmentAsync(id, _s.Now)));

        await _s.AdminAsync(a => a.EndAssignmentAsync(id, _s.Now.AddDays(5)));
        Assert.Equal(_s.Now.AddDays(5), (await _s.DbAsync(db => db.Assignments.SingleAsync(a => a.Id == id))).ValidTo);

        Assert.Equal(OrgChartErrors.AssignmentAlreadyEnded, await ExpectErrorAsync(a => a.EndAssignmentAsync(id, _s.Now.AddDays(6))));

        // Shortening an end date is still possible.
        await _s.AdminAsync(a => a.EndAssignmentAsync(id, _s.Now.AddDays(3)));
    }

    [Fact]
    public async Task Transfer_ends_the_old_assignment_and_starts_the_new_one()
    {
        await _s.SeedAsync();
        int oldId = await _s.AdminAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", "u1", ValidFrom: _s.Now.AddDays(-30))));
        DateTime nextMonth = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        _s.Changes.Clear();

        int newId = await _s.AdminAsync(a => a.TransferAsync(oldId, new TransferInput("POS-CLERK", nextMonth)));

        Assignment old = await _s.DbAsync(db => db.Assignments.SingleAsync(a => a.Id == oldId));
        Assignment next = await _s.DbAsync(db => db.Assignments.Include(a => a.Position).SingleAsync(a => a.Id == newId));
        Assert.Equal(nextMonth, old.ValidTo);
        Assert.Equal((nextMonth, (DateTime?)null, "POS-CLERK", "u1"), (next.ValidFrom!.Value, next.ValidTo, next.Position.Key, next.UserId));
        Assert.Equal(["u1"], Assert.Single(_s.Changes.All).UserIds);
    }

    [Fact]
    public async Task Transfer_within_the_same_position_changes_the_kind()
    {
        await _s.SeedAsync();
        int oldId = await _s.AdminAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", "u1", AssignmentKind.Acting, _s.Now.AddDays(-30))));

        int newId = await _s.AdminAsync(a => a.TransferAsync(oldId, new TransferInput("POS-ACC", _s.Now)));

        Assert.Equal(AssignmentKind.Primary, (await _s.DbAsync(db => db.Assignments.SingleAsync(a => a.Id == newId))).Kind);
    }

    [Fact]
    public async Task Update_and_remove_assignment()
    {
        await _s.SeedAsync();
        int id = await _s.AdminAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", "u1")));

        await _s.AdminAsync(a => a.UpdateAssignmentAsync(id, new AssignmentUpdate(AssignmentKind.Acting, null, _s.Now.AddDays(30), "temp")));
        Assignment row = await _s.DbAsync(db => db.Assignments.SingleAsync(a => a.Id == id));
        Assert.Equal((AssignmentKind.Acting, "temp"), (row.Kind, row.Note));

        await _s.AdminAsync(a => a.RemoveAssignmentAsync(id));
        Assert.False(await _s.DbAsync(db => db.Assignments.AnyAsync()));
        Assert.Equal("AssignmentRemoved", (await _s.DbAsync(db => db.AuditLogs.OrderBy(l => l.Id).LastAsync())).Operation);
        Assert.Equal(3, _s.Changes.All.Count(c => c.Kind == OrgChartChangeKind.Assignments));
    }

    [Fact]
    public async Task Listener_failure_reaches_caller_but_change_is_saved()
    {
        await _s.SeedAsync();
        ServiceProvider server = _s.CreateServer(b => b.AddChangeListener<ThrowingListener>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => _s.RunAsync(
            sp => sp.GetRequiredService<IOrgChartAdministration>().AssignAsync(new AssignmentInput("POS-ACC", "u9")),
            server));

        Assert.True(await _s.DbAsync(db => db.Assignments.AnyAsync(a => a.UserId == "u9")));
    }

    private sealed class ThrowingListener : IOrgChartChangeListener
    {
        public Task OnChangedAsync(OrgChartChange change, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("listener failed");
    }
}
