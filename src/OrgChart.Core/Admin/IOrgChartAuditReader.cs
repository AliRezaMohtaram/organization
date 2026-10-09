namespace OrgChart.Core.Admin;

/// <summary>Reads the audit log, newest first.</summary>
public interface IOrgChartAuditReader
{
    Task<AuditPage> ReadAsync(AuditQuery query, CancellationToken cancellationToken = default);

    /// <summary>Operations that occur in the log, for a filter list.</summary>
    Task<IReadOnlyList<string>> GetOperationsAsync(CancellationToken cancellationToken = default);
}

/// <param name="From">Inclusive (UTC).</param>
/// <param name="To">Exclusive (UTC).</param>
/// <param name="Page">1-based.</param>
public sealed record AuditQuery(
    DateTime? From = null,
    DateTime? To = null,
    string? ActorUserId = null,
    string? Operation = null,
    string? EntityType = null,
    string? EntityId = null,
    int Page = 1,
    int PageSize = 50);

public sealed record AuditEntry(
    long Id,
    DateTime At,
    string? ActorUserId,
    string Operation,
    string EntityType,
    string? EntityId,
    string? ChangeJson);

public sealed record AuditPage(IReadOnlyList<AuditEntry> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;
}
