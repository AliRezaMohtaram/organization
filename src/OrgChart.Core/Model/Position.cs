namespace OrgChart.Core.Model;

/// <summary>
/// A post in a unit. Access and reporting attach to positions, not to people.
/// </summary>
public class Position : KeyedEntity
{
    /// <summary>Optional human-facing code; unlike <see cref="KeyedEntity.Key"/> it may change.</summary>
    public string? Code { get; set; }

    public int OrgUnitId { get; set; }

    /// <summary>
    /// The position this one reports to, always in the same unit; null for a top position of the unit.
    /// Top positions report to the unit's head, and the head to the head of the nearest ancestor unit that has one.
    /// </summary>
    public int? ParentPositionId { get; set; }

    public int? TypeId { get; set; }
    public bool IsManagerial { get; set; }

    /// <summary>Inclusive start (UTC), or null for unbounded.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Exclusive end (UTC), or null for unbounded.</summary>
    public DateTime? ValidTo { get; set; }

    public OrgUnit OrgUnit { get; set; } = null!;
    public Position? ParentPosition { get; set; }
    public PositionType? Type { get; set; }
    public ICollection<Assignment> Assignments { get; set; } = [];
}
