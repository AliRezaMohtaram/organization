namespace OrgChart.Core.Model;

/// <summary>
/// A user holding a position for a period. A user may hold several positions at once, and
/// future-dated rows describe planned moves. Ending an assignment sets <see cref="ValidTo"/>;
/// rows are kept as history.
/// </summary>
public class Assignment : AuditableEntity
{
    public int Id { get; set; }
    public int PositionId { get; set; }

    /// <summary>The host's user id (e.g. AspNetUsers.Id). No foreign key to the host.</summary>
    public string UserId { get; set; } = null!;

    public AssignmentKind Kind { get; set; } = AssignmentKind.Primary;

    /// <summary>Inclusive start (UTC), or null for unbounded.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Exclusive end (UTC), or null for unbounded.</summary>
    public DateTime? ValidTo { get; set; }

    public string? Note { get; set; }

    public Position Position { get; set; } = null!;
}
