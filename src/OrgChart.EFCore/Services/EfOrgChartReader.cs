using Microsoft.EntityFrameworkCore;

using OrgChart.Core.Chart;
using OrgChart.Core.Model;

namespace OrgChart.EFCore.Services;

internal sealed class EfOrgChartReader(OrgChartDbContext db, SnapshotCache cache) : IOrgChartReader
{
    public async Task<OrgChartSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        // Read the stamp before the data: a change in between makes the stamp stale, never the data.
        Guid stamp = await db.ChartStamps.AsNoTracking()
            .Where(s => s.Id == ChartStamp.SingletonId)
            .Select(s => s.Stamp)
            .SingleAsync(cancellationToken);

        if (cache.Get(stamp) is { } cached)
        {
            return cached;
        }

        OrgChartSnapshot snapshot = await LoadAsync(cancellationToken);
        cache.Set(stamp, snapshot);
        return snapshot;
    }

    public async Task<IReadOnlyList<UserPosition>> GetUserPositionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var rows = await db.Assignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.Position.IsActive && a.Position.OrgUnit.IsActive)
            .OrderBy(a => a.ValidFrom).ThenBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.Kind,
                a.ValidFrom,
                a.ValidTo,
                PositionKey = a.Position.Key,
                PositionTitle = a.Position.Title,
                PositionFrom = a.Position.ValidFrom,
                PositionTo = a.Position.ValidTo,
                UnitKey = a.Position.OrgUnit.Key,
                UnitTitle = a.Position.OrgUnit.Title,
                UnitFrom = a.Position.OrgUnit.ValidFrom,
                UnitTo = a.Position.OrgUnit.ValidTo,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        OrgChartSnapshot snapshot = await GetSnapshotAsync(cancellationToken);
        List<UserPosition> positions = [];
        foreach (var row in rows)
        {
            DateTime? from = Period.MaxFrom(Period.MaxFrom(row.ValidFrom, row.PositionFrom), row.UnitFrom);
            DateTime? to = Period.MinTo(Period.MinTo(row.ValidTo, row.PositionTo), row.UnitTo);
            if (!Period.IsEmpty(from, to))
            {
                positions.Add(new UserPosition(
                    row.Id, row.PositionKey, row.PositionTitle, row.UnitKey, row.UnitTitle,
                    snapshot.GetAncestorKeys(row.UnitKey), row.Kind, from, to));
            }
        }

        return positions;
    }

    public async Task<IReadOnlyList<AssignmentInfo>> GetUnitAssignmentsAsync(
        string unitKey,
        bool includeSubUnits = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unitKey);

        List<string> unitKeys = [OrgKey.Normalize(unitKey)];
        if (includeSubUnits)
        {
            OrgChartSnapshot snapshot = await GetSnapshotAsync(cancellationToken);
            unitKeys.AddRange(snapshot.GetDescendantKeys(unitKey).Select(OrgKey.Normalize));
        }

        return await Project(db.Assignments.Where(a => unitKeys.Contains(a.Position.OrgUnit.NormalizedKey)))
            .ToListAsync(cancellationToken);
    }

    public Task<AssignmentInfo?> GetAssignmentAsync(int assignmentId, CancellationToken cancellationToken = default) =>
        Project(db.Assignments.Where(a => a.Id == assignmentId)).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<AssignmentInfo>>> GetHoldersAsync(
        IReadOnlyCollection<string> positionKeys,
        DateTime atUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(positionKeys);

        List<string> keys = positionKeys.Select(OrgKey.Normalize).Distinct().ToList();
        if (keys.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<AssignmentInfo>>(OrgKey.Comparer);
        }

        List<AssignmentInfo> holders = await Project(db.Assignments.Where(a =>
                keys.Contains(a.Position.NormalizedKey)
                && (a.ValidFrom == null || a.ValidFrom <= atUtc)
                && (a.ValidTo == null || atUtc < a.ValidTo)))
            .ToListAsync(cancellationToken);

        return holders
            .GroupBy(h => h.PositionKey, OrgKey.Comparer)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AssignmentInfo>)g.ToList(), OrgKey.Comparer);
    }

    public async Task<IReadOnlyList<ManagerInfo>> GetManagersAsync(string userId, DateTime atUtc, CancellationToken cancellationToken = default)
    {
        List<UserPosition> current = (await GetUserPositionsAsync(userId, cancellationToken))
            .Where(p => Period.Contains(p.ValidFrom, p.ValidTo, atUtc))
            .ToList();
        if (current.Count == 0)
        {
            return [];
        }

        OrgChartSnapshot snapshot = await GetSnapshotAsync(cancellationToken);
        Dictionary<string, IReadOnlyList<string>> chains = current
            .Select(p => p.PositionKey)
            .Distinct(OrgKey.Comparer)
            .ToDictionary(k => k, snapshot.GetManagerChain, OrgKey.Comparer);
        IReadOnlyDictionary<string, IReadOnlyList<AssignmentInfo>> holders = await GetHoldersAsync(
            chains.Values.SelectMany(c => c).Distinct(OrgKey.Comparer).ToList(), atUtc, cancellationToken);

        List<ManagerInfo> managers = [];
        foreach ((string positionKey, IReadOnlyList<string> chain) in chains)
        {
            foreach (string managerKey in chain)
            {
                List<AssignmentInfo> others = holders.GetValueOrDefault(managerKey, [])
                    .Where(h => h.UserId != userId)
                    .ToList();
                if (others.Count > 0)
                {
                    managers.Add(new ManagerInfo(positionKey, managerKey, others));
                    break;
                }
            }
        }

        return managers;
    }

    private static IQueryable<AssignmentInfo> Project(IQueryable<Assignment> assignments) =>
        assignments.AsNoTracking()
            .OrderBy(a => a.Position.SortOrder).ThenBy(a => a.Position.Title).ThenBy(a => a.ValidFrom).ThenBy(a => a.Id)
            .Select(a => new AssignmentInfo(
                a.Id, a.UserId, a.Position.Key, a.Position.OrgUnit.Key, a.Kind, a.ValidFrom, a.ValidTo, a.Note));

    private async Task<OrgChartSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        List<UnitNode> units = await db.OrgUnits.AsNoTracking()
            .Select(u => new UnitNode(
                u.Key, u.Title, u.Code, u.Type.Key,
                u.Parent == null ? null : u.Parent.Key,
                u.ManagerPosition == null ? null : u.ManagerPosition.Key,
                u.SortOrder, u.IsActive, u.ValidFrom, u.ValidTo))
            .ToListAsync(cancellationToken);

        List<PositionNode> positions = await db.Positions.AsNoTracking()
            .Select(p => new PositionNode(
                p.Key, p.Title, p.Code, p.OrgUnit.Key,
                p.Type == null ? null : p.Type.Key,
                p.IsManagerial, p.SortOrder, p.IsActive, p.ValidFrom, p.ValidTo,
                p.ParentPosition == null ? null : p.ParentPosition.Key))
            .ToListAsync(cancellationToken);

        List<OrgTypeNode> unitTypes = await db.OrgUnitTypes.AsNoTracking()
            .Select(t => new OrgTypeNode(t.Key, t.Title, t.SortOrder, t.IsActive, t.Level, t.CanBeRoot))
            .ToListAsync(cancellationToken);

        List<OrgTypeNode> positionTypes = await db.PositionTypes.AsNoTracking()
            .Select(t => new OrgTypeNode(t.Key, t.Title, t.SortOrder, t.IsActive))
            .ToListAsync(cancellationToken);

        return new OrgChartSnapshot(units, positions, unitTypes, positionTypes);
    }
}
