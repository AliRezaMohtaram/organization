using OrgChart.Core.Chart;

namespace OrgChart.Tests.Chart;

public sealed class OrgChartSnapshotTests
{
    private static UnitNode Unit(string key, string? parent = null, int sort = 0, string? manager = null) =>
        new(key, key, null, "T", parent, manager, sort, true, null, null);

    private static PositionNode Position(string key, string unit, bool active = true) =>
        new(key, key, null, unit, null, false, 0, active, null, null);

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
}
