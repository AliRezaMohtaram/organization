using OrgChart.Core.Chart;

namespace OrgChart.Tests.Chart;

public sealed class OrgChartSnapshotTests
{
    private static UnitNode Unit(string key, string? parent = null, int sort = 0, string? manager = null) =>
        new(key, key, null, "T", parent, manager, sort, true, null, null);

    private static PositionNode Position(string key, string unit, bool active = true, string? parent = null, int sort = 0) =>
        new(key, key, null, unit, null, false, sort, active, null, null, parent);

    private static OrgChartSnapshot Snapshot(IEnumerable<UnitNode> units, IEnumerable<PositionNode>? positions = null) =>
        new(units, positions ?? [], [], []);

    [Fact]
    public void Tree_queries()
    {
        OrgChartSnapshot chart = Snapshot([
            Unit("C"),
            Unit("FIN", "C", sort: 2),
            Unit("HR", "C", sort: 1),
            Unit("ACC", "FIN"),
            Unit("TRE", "FIN"),
        ]);

        Assert.Equal(["C"], chart.Roots.Select(u => u.Key));
        Assert.Equal(["HR", "FIN"], chart.GetChildren("C").Select(u => u.Key));
        Assert.Equal(["FIN", "C"], chart.GetAncestorKeys("ACC"));
        Assert.Equal(["HR", "FIN", "ACC", "TRE"], chart.GetDescendantKeys("C"));
        Assert.Equal(["ACC", "TRE"], chart.GetDescendantKeys("fin"));
        Assert.Empty(chart.GetDescendantKeys("ACC"));
        Assert.Equal(2, chart.GetLevel("ACC"));
        Assert.Equal(["C", "FIN", "ACC"], chart.GetPath("acc").Select(u => u.Key));
        Assert.Equal(["C", "HR", "FIN", "ACC", "TRE"], chart.Units.Select(u => u.Key));
        Assert.True(chart.IsSelfOrDescendant("TRE", "c"));
        Assert.True(chart.IsSelfOrDescendant("FIN", "FIN"));
        Assert.False(chart.IsSelfOrDescendant("HR", "FIN"));
    }

    [Fact]
    public void Unknown_keys_give_empty_results()
    {
        OrgChartSnapshot chart = Snapshot([Unit("C")]);

        Assert.Null(chart.FindUnit("X"));
        Assert.Null(chart.FindUnit(null));
        Assert.Empty(chart.GetChildren("X"));
        Assert.Empty(chart.GetAncestorKeys("X"));
        Assert.Empty(chart.GetDescendantKeys("X"));
        Assert.Empty(chart.GetPath("X"));
        Assert.Empty(chart.GetManagerChain("X"));
    }

    [Fact]
    public void Missing_parent_makes_a_root()
    {
        OrgChartSnapshot chart = Snapshot([Unit("A", "GONE"), Unit("B", "A")]);

        Assert.Equal(["A"], chart.Roots.Select(u => u.Key));
        Assert.Equal(["A"], chart.GetAncestorKeys("B"));
    }

    [Fact]
    public void Cycle_is_cut_and_nothing_is_lost()
    {
        // A → B → C → A, plus D under B.
        OrgChartSnapshot chart = Snapshot([Unit("A", "C"), Unit("B", "A"), Unit("C", "B"), Unit("D", "B")]);

        // Cut at the smallest key: A becomes a root.
        Assert.Equal(["A"], chart.Roots.Select(u => u.Key));
        Assert.Equal(["B", "C", "D"], chart.GetDescendantKeys("A").Order());
        Assert.Equal(["B", "A"], chart.GetAncestorKeys("C"));
        Assert.Equal(4, chart.Units.Count);
    }

    [Fact]
    public void Self_parent_is_a_root()
    {
        OrgChartSnapshot chart = Snapshot([Unit("A", "A")]);

        Assert.Equal(["A"], chart.Roots.Select(u => u.Key));
        Assert.Empty(chart.GetDescendantKeys("A"));
    }

    [Fact]
    public void Positions_follow_tree_order()
    {
        OrgChartSnapshot chart = Snapshot(
            [Unit("C"), Unit("B", "C", sort: 2), Unit("A", "C", sort: 1)],
            [Position("P-B", "B"), Position("P-A", "A"), Position("P-C", "C"), Position("P-X", "NOWHERE")]);

        Assert.Equal(["P-C", "P-A", "P-B", "P-X"], chart.Positions.Select(p => p.Key));
        Assert.Equal(["P-A"], chart.GetPositions("a").Select(p => p.Key));
    }

    [Fact]
    public void Manager_chain_skips_self_and_units_without_manager()
    {
        OrgChartSnapshot chart = Snapshot(
            [Unit("C", manager: "CEO"), Unit("FIN", "C", manager: "CFO"), Unit("ACC", "FIN"), Unit("TAX", "ACC", manager: "OLD")],
            [Position("CEO", "C"), Position("CFO", "FIN"), Position("CLERK", "ACC"), Position("OLD", "TAX", active: false), Position("T1", "TAX")]);

        Assert.Equal(["CFO", "CEO"], chart.GetManagerChain("CLERK"));
        Assert.Equal(["CEO"], chart.GetManagerChain("CFO"));
        Assert.Empty(chart.GetManagerChain("CEO"));

        // An inactive manager position is skipped.
        Assert.Equal(["CFO", "CEO"], chart.GetManagerChain("T1"));
    }

    [Fact]
    public void Positions_form_a_tree_inside_their_unit()
    {
        // The user's example: head of accounting → oil accounting lead → oil accounting expert.
        OrgChartSnapshot chart = Snapshot(
            [Unit("FIN", manager: "FIN-MGR"), Unit("ACC", "FIN", manager: "ACC-HEAD"), Unit("STORE", "FIN")],
            [
                Position("FIN-MGR", "FIN"),
                Position("ACC-HEAD", "ACC", sort: 1),
                Position("OIL-LEAD", "ACC", parent: "ACC-HEAD"),
                Position("OIL-EXP", "ACC", parent: "OIL-LEAD"),
                Position("ACC-CLERK", "ACC", sort: 2),
                Position("STORE-CLERK", "STORE"),
                Position("WRONG", "ACC", parent: "STORE-CLERK", sort: 3), // parent in another unit: ignored
            ]);

        Assert.Equal(["ACC-HEAD", "OIL-LEAD", "OIL-EXP", "ACC-CLERK", "WRONG"], chart.GetPositions("ACC").Select(p => p.Key));
        Assert.Equal(["OIL-LEAD"], chart.GetChildPositions("ACC-HEAD").Select(p => p.Key));
        Assert.Equal(2, chart.GetPositionDepth("OIL-EXP"));
        Assert.Null(chart.GetParentPositionKey("WRONG"));
        Assert.True(chart.IsSelfOrSubordinate("OIL-EXP", "ACC-HEAD"));
        Assert.False(chart.IsSelfOrSubordinate("ACC-CLERK", "OIL-LEAD"));

        // Reporting line: parent position, else the unit's head, else the head of the nearest ancestor unit.
        Assert.Equal(["OIL-LEAD", "ACC-HEAD", "FIN-MGR"], chart.GetManagerChain("OIL-EXP"));
        Assert.Equal("ACC-HEAD", chart.GetSuperiorKey("ACC-CLERK"));
        Assert.Equal("FIN-MGR", chart.GetSuperiorKey("ACC-HEAD"));
        Assert.Equal("FIN-MGR", chart.GetSuperiorKey("STORE-CLERK")); // STORE has no head
        Assert.Null(chart.GetSuperiorKey("FIN-MGR"));
    }

    [Fact]
    public void Position_cycle_is_cut()
    {
        OrgChartSnapshot chart = Snapshot([Unit("U")], [Position("A", "U", parent: "B"), Position("B", "U", parent: "A")]);

        Assert.Equal(["A", "B"], chart.GetPositions("U").Select(p => p.Key));
        Assert.Equal(["A"], chart.GetManagerChain("B"));
        Assert.Empty(chart.GetManagerChain("A"));
    }

    [Fact]
    public void Top_positions_are_shown_under_the_unit_head()
    {
        OrgChartSnapshot chart = Snapshot(
            [Unit("DIV", manager: "VP"), Unit("FREE")],
            [
                Position("VP", "DIV", sort: 1),
                Position("MOVED-IN", "DIV", sort: 2),   // e.g. moved here: no parent, reports to the head
                Position("ASSIST", "DIV", sort: 3, parent: "MOVED-IN"),
                Position("A", "FREE"), Position("B", "FREE"),
            ]);

        Assert.Equal(["VP"], chart.GetUnitTopPositions("DIV").Select(p => p.Key));
        Assert.Equal(["MOVED-IN"], chart.GetReportingChildren("VP").Select(p => p.Key));
        Assert.Equal([("VP", 0), ("MOVED-IN", 1), ("ASSIST", 2)], chart.GetPositionOutline("DIV").Select(o => (o.Position.Key, o.Depth)));

        // Without a head the top positions stay at the top.
        Assert.Equal(["A", "B"], chart.GetUnitTopPositions("FREE").Select(p => p.Key));
    }
}
