namespace OrgChart.Core.Model;

public class AuditLog
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public string? ActorUserId { get; set; }
    public string Operation { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string? EntityId { get; set; }
    public string? ChangeJson { get; set; }
}
