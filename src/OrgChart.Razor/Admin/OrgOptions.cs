using Microsoft.AspNetCore.Mvc.Rendering;

using OrgChart.Core.Chart;
using OrgChart.Core.Model;

namespace OrgChart.Razor.Admin;

/// <summary>Select lists for the forms, in tree order with indented labels.</summary>
public static class OrgOptions
{
    private const string Indent = "   ";

    /// <summary>Active units, optionally without <paramref name="excludeSubtreeOf"/> and everything below it.</summary>
    public static List<SelectListItem> Units(OrgChartSnapshot chart, string? selected, string? excludeSubtreeOf = null) =>
        chart.Units
            .Where(u => u.IsActive && (excludeSubtreeOf is null || !chart.IsSelfOrDescendant(u.Key, excludeSubtreeOf)))
            .Select(u => new SelectListItem(Label(chart, u), u.Key, OrgKey.AreEqual(u.Key, selected)))
            .ToList();

    /// <summary>
    /// Active units a unit of type <paramref name="type"/> may be placed under (level rule), optionally without
    /// <paramref name="excludeSubtreeOf"/> and everything below it.
    /// </summary>
    public static List<SelectListItem> ParentsFor(OrgChartSnapshot chart, OrgTypeNode? type, string? selected, string? excludeSubtreeOf = null) =>
        chart.Units
            .Where(u => u.IsActive
                && (excludeSubtreeOf is null || !chart.IsSelfOrDescendant(u.Key, excludeSubtreeOf))
                && Fits(type, chart.FindUnitType(u.TypeKey)))
            .Select(u => new SelectListItem(Label(chart, u), u.Key, OrgKey.AreEqual(u.Key, selected)))
            .ToList();

    /// <summary>True when a unit of type <paramref name="child"/> may be under a unit of type <paramref name="parent"/> (null parent = root).</summary>
    public static bool Fits(OrgTypeNode? child, OrgTypeNode? parent) =>
        parent is null ? child?.CanBeRoot != false : child is null || child.Level is null || parent.Level is null || child.Level > parent.Level;

    /// <summary>Active positions of the unit a position may report to, indented by depth, without <paramref name="self"/> and its subordinates.</summary>
    public static List<SelectListItem> PositionParents(OrgChartSnapshot chart, string unitKey, string? selected, string? self) =>
        chart.GetPositions(unitKey)
            .Where(p => p.IsActive && (self is null || !chart.IsSelfOrSubordinate(p.Key, self)))
            .Select(p => new SelectListItem(
                string.Concat(Enumerable.Repeat(Indent, chart.GetPositionDepth(p.Key))) + p.Title,
                p.Key,
                OrgKey.AreEqual(p.Key, selected)))
            .ToList();

    /// <summary>Active positions grouped by unit (tree order).</summary>
    public static List<SelectListItem> Positions(OrgChartSnapshot chart, string? selected, string? onlyUnit = null)
    {
        List<SelectListItem> items = [];
        foreach (UnitNode unit in chart.Units.Where(u => u.IsActive && (onlyUnit is null || OrgKey.AreEqual(u.Key, onlyUnit))))
        {
            SelectListGroup group = new() { Name = string.Join(" › ", chart.GetPath(unit.Key).Select(u => u.Title)) };
            items.AddRange(chart.GetPositions(unit.Key)
                .Where(p => p.IsActive)
                .Select(p => new SelectListItem(p.Title, p.Key, OrgKey.AreEqual(p.Key, selected)) { Group = group }));
        }

        return items;
    }

    /// <summary>Active types, plus <paramref name="selected"/> even when inactive (so an edit form keeps it).</summary>
    public static List<SelectListItem> Types(IEnumerable<OrgTypeNode> types, string? selected) =>
        types.Where(t => t.IsActive || OrgKey.AreEqual(t.Key, selected))
            .Select(t => new SelectListItem(t.Title, t.Key, OrgKey.AreEqual(t.Key, selected)))
            .ToList();

    private static string Label(OrgChartSnapshot chart, UnitNode unit) =>
        string.Concat(Enumerable.Repeat(Indent, chart.GetLevel(unit.Key))) + unit.Title;
}
