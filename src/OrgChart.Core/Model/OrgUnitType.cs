namespace OrgChart.Core.Model;

/// <summary>
/// Kind of org unit (company, branch, division, department, …). Editable by admins so each host can
/// define its own levels.
/// </summary>
public class OrgUnitType : KeyedEntity
{
    /// <summary>
    /// Place in the hierarchy (1 = top, e.g. company). A unit's type must have a greater level than its parent's.
    /// Null = no level rule for units of this type.
    /// </summary>
    public int? Level { get; set; }

    /// <summary>Units of this type may be roots of the tree (e.g. a company).</summary>
    public bool CanBeRoot { get; set; } = true;

    /// <summary>True when a unit of type <paramref name="child"/> may be placed under one of type <paramref name="parent"/>.</summary>
    public static bool AllowsUnder(int? child, int? parent) => child is null || parent is null || child > parent;
}
