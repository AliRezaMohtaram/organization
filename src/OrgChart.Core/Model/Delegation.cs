namespace OrgChart.Core.Model;

/// <summary>
/// Authority of a position passed on, in full or in part (<see cref="Scopes"/>), without taking it from its holder:
/// to a person for a period (<see cref="DelegationKind.ToUser"/>) or to a deputy position (<see cref="DelegationKind.ToPosition"/>).
/// It never applies while the position is vacant; vacant positions are covered by acting assignments.
/// Ending one sets <see cref="ValidTo"/>; rows are history.
/// </summary>
public class Delegation : AuditableEntity
{
    public int Id { get; set; }
    public DelegationKind Kind { get; set; }

    /// <summary>The position whose authority is passed on.</summary>
    public int PositionId { get; set; }

    /// <summary><see cref="DelegationKind.ToUser"/>: the holder who delegates; applies only while they hold the position.</summary>
    public string? FromUserId { get; set; }

    /// <summary><see cref="DelegationKind.ToUser"/>: who receives the authority.</summary>
    public string? ToUserId { get; set; }

    /// <summary><see cref="DelegationKind.ToPosition"/>: the deputy position.</summary>
    public int? ToPositionId { get; set; }

    /// <summary><see cref="DelegationKind.ToPosition"/>: order among the position's deputies (1 = first).</summary>
    public int Priority { get; set; } = 1;

    /// <summary>True: all of the position's authority; false: only the authority keys in <see cref="Scopes"/>.</summary>
    public bool IsFullScope { get; set; } = true;

    /// <summary>Inclusive start (UTC), or null for unbounded.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Exclusive end (UTC), or null for unbounded (deputies only).</summary>
    public DateTime? ValidTo { get; set; }

    public string? Note { get; set; }

    public Position Position { get; set; } = null!;
    public Position? ToPosition { get; set; }
    public ICollection<DelegationScope> Scopes { get; set; } = [];
}

/// <summary>
/// One authority key in a partial delegation. Keys come from the host's <c>IAuthorityCatalog</c> (with Acl: role ids);
/// OrgChart stores and returns them without interpreting them.
/// </summary>
public class DelegationScope
{
    public int DelegationId { get; set; }
    public string AuthorityKey { get; set; } = null!;
}
