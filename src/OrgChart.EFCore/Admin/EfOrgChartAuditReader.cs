using Microsoft.EntityFrameworkCore;

using OrgChart.Core.Admin;
using OrgChart.Core.Model;

namespace OrgChart.EFCore.Admin;

internal sealed class EfOrgChartAuditReader(OrgChartDbContext db) : IOrgChartAuditReader
{
    public const int MaxPageSize = 200;

    public async Task<AuditPage> ReadAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        int page = Math.Max(1, query.Page);

        IQueryable<AuditLog> logs = db.AuditLogs.AsNoTracking();
        if (query.From is { } from)
        {
            logs = logs.Where(l => l.At >= from);
        }

        if (query.To is { } to)
        {
            logs = logs.Where(l => l.At < to);
        }

        if (!string.IsNullOrWhiteSpace(query.ActorUserId))
        {
            string actor = query.ActorUserId.Trim();
            logs = logs.Where(l => l.ActorUserId == actor);
        }

        if (!string.IsNullOrWhiteSpace(query.Operation))
        {
            logs = logs.Where(l => l.Operation == query.Operation);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            logs = logs.Where(l => l.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            string id = query.EntityId.Trim();
            logs = logs.Where(l => l.EntityId == id);
        }

        int total = await logs.CountAsync(cancellationToken);
        List<AuditEntry> items = await logs
            .OrderByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AuditEntry(l.Id, l.At, l.ActorUserId, l.Operation, l.EntityType, l.EntityId, l.ChangeJson))
            .ToListAsync(cancellationToken);

        return new AuditPage(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<string>> GetOperationsAsync(CancellationToken cancellationToken = default) =>
        await db.AuditLogs.AsNoTracking().Select(l => l.Operation).Distinct().OrderBy(o => o).ToListAsync(cancellationToken);
}
