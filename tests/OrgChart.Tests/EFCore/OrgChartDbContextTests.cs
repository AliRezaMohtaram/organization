using Microsoft.EntityFrameworkCore;

using OrgChart.Core.Model;
using OrgChart.EFCore;

namespace OrgChart.Tests.EFCore;

public sealed class OrgChartDbContextTests : IDisposable
{
    private readonly SqliteOrgChartDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Unit_tree_positions_and_assignments_round_trip()
    {
        DateTime from = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using (OrgChartDbContext db = _db.CreateContext())
        {
            OrgUnit company = Unit(db, "C-001", "Company A");
            OrgUnit finance = Unit(db, "DV-FIN", "Finance", company);
            Position cfo = new() { Key = "POS-CFO", Title = "CFO", OrgUnit = finance, IsManagerial = true };
            db.Positions.Add(cfo);
            db.Assignments.Add(new Assignment { Position = cfo, UserId = "u1", ValidFrom = from });
            db.Assignments.Add(new Assignment { Position = cfo, UserId = "u2", Kind = AssignmentKind.Acting });
            db.SaveChanges();

            finance.ManagerPosition = cfo;
            db.SaveChanges();
        }

        using (OrgChartDbContext db = _db.CreateContext())
        {
            OrgUnit finance = db.OrgUnits
                .Include(u => u.Parent)
                .Include(u => u.ManagerPosition)
                .Include(u => u.Positions).ThenInclude(p => p.Assignments)
                .Single(u => u.Key == "DV-FIN");

            Assert.Equal("C-001", finance.Parent!.Key);
            Assert.Equal("POS-CFO", finance.ManagerPosition!.Key);
            List<Assignment> assignments = finance.Positions.Single().Assignments.OrderBy(a => a.UserId).ToList();
            Assert.Equal(AssignmentKind.Primary, assignments[0].Kind);
            Assert.Equal(DateTimeKind.Utc, assignments[0].ValidFrom!.Value.Kind);
            Assert.Equal(from, assignments[0].ValidFrom);
            Assert.Equal(AssignmentKind.Acting, assignments[1].Kind);
        }
    }

    [Fact]
    public void Keys_are_unique_ignoring_case()
    {
        using OrgChartDbContext db = _db.CreateContext();
        Unit(db, "DV-FIN", "Finance");
        db.SaveChanges();

        Unit(db, "dv-fin", "Finance again");

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void Key_keeps_its_spelling_and_normalizes_for_lookup()
    {
        using (OrgChartDbContext db = _db.CreateContext())
        {
            Unit(db, "Dv-Fin", "Finance");
            db.SaveChanges();
        }

        using (OrgChartDbContext db = _db.CreateContext())
        {
            OrgUnit unit = db.OrgUnits.Single(u => u.NormalizedKey == OrgKey.Normalize("dv-FIN"));
            Assert.Equal("Dv-Fin", unit.Key);
        }
    }

    [Fact]
    public void Key_cannot_change_after_save()
    {
        using OrgChartDbContext db = _db.CreateContext();
        OrgUnit unit = Unit(db, "DV-FIN", "Finance");
        db.SaveChanges();

        unit.Key = "DV-FINANCE";

        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Restructuring_keeps_keys()
    {
        using (OrgChartDbContext db = _db.CreateContext())
        {
            OrgUnit a = Unit(db, "A", "A");
            OrgUnit b = Unit(db, "B", "B");
            Unit(db, "X", "X", a);
            db.SaveChanges();
        }

        using (OrgChartDbContext db = _db.CreateContext())
        {
            OrgUnit x = db.OrgUnits.Single(u => u.Key == "X");
            x.Parent = db.OrgUnits.Single(u => u.Key == "B");
            x.Title = "X renamed";
            x.Code = "X-01";
            db.SaveChanges();
        }

        using (OrgChartDbContext db = _db.CreateContext())
        {
            OrgUnit x = db.OrgUnits.Include(u => u.Parent).Single(u => u.Key == "X");
            Assert.Equal("B", x.Parent!.Key);
            Assert.Equal("X renamed", x.Title);
        }
    }

    [Fact]
    public void Inactive_flag_is_persisted()
    {
        using (OrgChartDbContext db = _db.CreateContext())
        {
            Unit(db, "OLD", "Old unit").IsActive = false;
            db.SaveChanges();
        }

        using (OrgChartDbContext db = _db.CreateContext())
        {
            Assert.False(db.OrgUnits.Single(u => u.Key == "OLD").IsActive);
        }
    }

    [Fact]
    public void Unit_with_children_cannot_be_hard_deleted()
    {
        using OrgChartDbContext db = _db.CreateContext();
        OrgUnit parent = Unit(db, "P", "Parent");
        Unit(db, "C", "Child", parent);
        db.SaveChanges();

        db.ChangeTracker.Clear();
        db.OrgUnits.Remove(db.OrgUnits.Single(u => u.Key == "P"));

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void Validity_range_is_checked()
    {
        using OrgChartDbContext db = _db.CreateContext();
        OrgUnit unit = Unit(db, "U", "Unit");
        Position position = new() { Key = "P", Title = "P", OrgUnit = unit };
        db.Assignments.Add(new Assignment
        {
            Position = position,
            UserId = "u1",
            ValidFrom = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            ValidTo = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void Position_can_head_only_one_unit()
    {
        using OrgChartDbContext db = _db.CreateContext();
        OrgUnit a = Unit(db, "A", "A");
        OrgUnit b = Unit(db, "B", "B");
        Position head = new() { Key = "HEAD", Title = "Head", OrgUnit = a };
        db.Positions.Add(head);
        db.SaveChanges();

        a.ManagerPosition = head;
        b.ManagerPosition = head;

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Theory]
    [InlineData("DV-FIN", true)]
    [InlineData("pos.cfo_1", true)]
    [InlineData("", false)]
    [InlineData("-X", false)]
    [InlineData("has space", false)]
    [InlineData("واحد", false)]
    public void Key_format(string key, bool valid)
    {
        Assert.Equal(valid, OrgKey.IsValid(key));
    }

    private static OrgUnitType DefaultType(OrgChartDbContext db) =>
        db.OrgUnitTypes.Local.FirstOrDefault()
        ?? db.OrgUnitTypes.FirstOrDefault()
        ?? db.OrgUnitTypes.Add(new OrgUnitType { Key = "DEPARTMENT", Title = "Department" }).Entity;

    private static OrgUnit Unit(OrgChartDbContext db, string key, string title, OrgUnit? parent = null) =>
        db.OrgUnits.Add(new OrgUnit { Key = key, Title = title, Type = DefaultType(db), Parent = parent }).Entity;
}
