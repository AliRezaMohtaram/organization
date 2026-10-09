namespace OrgChart.Core.Model;

/// <summary>
/// A node of the unit tree. A company is simply a root unit whose type is "company".
/// </summary>
public class OrgUnit : KeyedEntity
{
    /// <summary>Optional human-facing code; unlike <see cref="KeyedEntity.Key"/> it may change.</summary>
    public string? Code { get; set; }

    public int TypeId { get; set; }
    public int? ParentId { get; set; }

    /// <summary>The position (in this unit) that heads it; used for reporting lines.</summary>
    public int? ManagerPositionId { get; set; }

    /// <summary>Inclusive start (UTC), or null for unbounded.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Exclusive end (UTC), or null for unbounded.</summary>
    public DateTime? ValidTo { get; set; }

    public OrgUnitType Type { get; set; } = null!;
    public OrgUnit? Parent { get; set; }
    public Position? ManagerPosition { get; set; }
    public ICollection<OrgUnit> Children { get; set; } = [];
    public ICollection<Position> Positions { get; set; } = [];
}
