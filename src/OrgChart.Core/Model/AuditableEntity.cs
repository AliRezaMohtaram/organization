namespace OrgChart.Core.Model;

/// <summary>
/// Base for entities edited from the admin UI. All timestamps are UTC.
/// </summary>
public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
