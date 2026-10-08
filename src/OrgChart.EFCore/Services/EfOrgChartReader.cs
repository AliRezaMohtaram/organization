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

        // 1. Own assignments.
        List<HeldRow> own = await db.Assignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.Position.IsActive && a.Position.OrgUnit.IsActive)
            .OrderBy(a => a.ValidFrom).ThenBy(a => a.Id)
            .Select(a => new HeldRow(a.Id, a.PositionId, a.Kind, a.ValidFrom, a.ValidTo))
            .ToListAsync(cancellationToken);

        // 2. Delegations to the user, valid only while the delegator holds the position.
        List<DelegationRow> delegated = await DelegationRows(db.Delegations.Where(d => d.Kind == DelegationKind.ToUser && d.ToUserId == userId))
            .ToListAsync(cancellationToken);
        List<int> delegatedPositions = delegated.Select(d => d.PositionId).Distinct().ToList();
        List<string> delegators = delegated.Select(d => d.FromUserId!).Distinct().ToList();
        List<HolderRow> delegatorAssignments = delegated.Count == 0 ? [] : await db.Assignments.AsNoTracking()
            .Where(a => delegatedPositions.Contains(a.PositionId) && delegators.Contains(a.UserId))
            .Select(a => new HolderRow(a.Id, a.PositionId, a.UserId, a.ValidFrom, a.ValidTo))
            .ToListAsync(cancellationToken);

        // 3. Deputies of the positions the user holds, valid only while the deputized position has a holder.
        List<int> heldPositions = own.Select(a => a.PositionId).Distinct().ToList();
        List<DelegationRow> deputies = heldPositions.Count == 0 ? [] : await DelegationRows(db.Delegations
                .Where(d => d.Kind == DelegationKind.ToPosition && heldPositions.Contains(d.ToPositionId!.Value)))
            .ToListAsync(cancellationToken);
        List<int> deputized = deputies.Select(d => d.PositionId).Distinct().ToList();
        List<HolderRow> deputizedHolders = deputies.Count == 0 ? [] : await db.Assignments.AsNoTracking()
            .Where(a => deputized.Contains(a.PositionId))
            .Select(a => new HolderRow(a.Id, a.PositionId, a.UserId, a.ValidFrom, a.ValidTo))
            .ToListAsync(cancellationToken);

        if (own.Count == 0 && delegated.Count == 0)
        {
            return [];
        }

        List<int> positionIds = [.. heldPositions, .. delegatedPositions, .. deputized];
        Dictionary<int, PositionRow> positions = await db.Positions.AsNoTracking()
            .Where(p => positionIds.Contains(p.Id) && p.IsActive && p.OrgUnit.IsActive)
            .Select(p => new PositionRow(
                p.Id, p.Key, p.Title, p.OrgUnit.Key, p.OrgUnit.Title,
                Period.MaxFrom(p.ValidFrom, p.OrgUnit.ValidFrom), Period.MinTo(p.ValidTo, p.OrgUnit.ValidTo)))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        OrgChartSnapshot snapshot = await GetSnapshotAsync(cancellationToken);
        List<UserPosition> result = [];
        void Add(int assignmentId, int positionId, AssignmentKind kind, DateTime? from, DateTime? to, DelegationRow? delegation = null)
        {
            if (!positions.TryGetValue(positionId, out PositionRow? position))
            {
                return;
            }

            from = Period.MaxFrom(from, position.From);
            to = Period.MinTo(to, position.To);
            if (!Period.IsEmpty(from, to))
            {
                result.Add(new UserPosition(
                    assignmentId, position.Key, position.Title, position.UnitKey, position.UnitTitle,
                    snapshot.GetAncestorKeys(position.UnitKey), kind, from, to,
                    delegation is { IsFullScope: false } ? delegation.Keys : null, delegation?.Id));
            }
        }

        foreach (HeldRow a in own)
        {
            Add(a.Id, a.PositionId, a.Kind, a.ValidFrom, a.ValidTo);
        }

        foreach (DelegationRow d in delegated)
        {
            foreach (HolderRow a in delegatorAssignments.Where(a => a.PositionId == d.PositionId && a.UserId == d.FromUserId))
            {
                Add(a.Id, d.PositionId, AssignmentKind.Delegated,
                    Period.MaxFrom(d.ValidFrom, a.ValidFrom), Period.MinTo(d.ValidTo, a.ValidTo), d);
            }
        }

        foreach (DelegationRow d in deputies)
        {
            List<(DateTime? From, DateTime? To)> held = MergePeriods(deputizedHolders
                .Where(h => h.PositionId == d.PositionId && h.UserId != userId)
                .Select(h => (h.ValidFrom, h.ValidTo)));
            foreach (HeldRow mine in own.Where(a => a.PositionId == d.ToPositionId))
            {
                foreach ((DateTime? from, DateTime? to) in held)
                {
                    Add(mine.Id, d.PositionId, AssignmentKind.Deputy,
                        Period.MaxFrom(Period.MaxFrom(d.ValidFrom, mine.ValidFrom), from),
                        Period.MinTo(Period.MinTo(d.ValidTo, mine.ValidTo), to), d);
                }
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<DelegationInfo>> GetDelegationsAsync(DelegationQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Delegation> delegations = db.Delegations.AsNoTracking();
        if (query.PositionKeys is { } positionKeys)
        {
            List<string> keys = positionKeys.Select(OrgKey.Normalize).ToList();
            delegations = delegations.Where(d => keys.Contains(d.Position.NormalizedKey));
        }

        if (query.ToPositionKeys is { } toPositionKeys)
        {
            List<string> keys = toPositionKeys.Select(OrgKey.Normalize).ToList();
            delegations = delegations.Where(d => d.ToPosition != null && keys.Contains(d.ToPosition.NormalizedKey));
        }

        if (query.FromUserId is { } from)
        {
            delegations = delegations.Where(d => d.FromUserId == from);
        }

        if (query.ToUserId is { } to)
        {
            delegations = delegations.Where(d => d.ToUserId == to);
        }

        if (query.Kind is { } kind)
        {
            delegations = delegations.Where(d => d.Kind == kind);
        }

        if (query.ActiveAt is { } at)
        {
            delegations = delegations.Where(d => (d.ValidFrom == null || d.ValidFrom <= at) && (d.ValidTo == null || at < d.ValidTo));
        }

        return await ProjectDelegations(delegations).ToListAsync(cancellationToken);
    }

    public Task<DelegationInfo?> GetDelegationAsync(int delegationId, CancellationToken cancellationToken = default) =>
        ProjectDelegations(db.Delegations.AsNoTracking().Where(d => d.Id == delegationId)).SingleOrDefaultAsync(cancellationToken);

    private static IQueryable<DelegationInfo> ProjectDelegations(IQueryable<Delegation> delegations) =>
        delegations
            .OrderBy(d => d.Kind).ThenBy(d => d.Position.Title).ThenBy(d => d.Priority).ThenBy(d => d.ValidFrom).ThenBy(d => d.Id)
            .Select(d => new DelegationInfo(
                d.Id, d.Kind, d.Position.Key, d.FromUserId, d.ToUserId,
                d.ToPosition == null ? null : d.ToPosition.Key,
                d.Priority,
                d.IsFullScope ? null : d.Scopes.Select(s => s.AuthorityKey).OrderBy(k => k).ToList(),
                d.ValidFrom, d.ValidTo, d.Note));

    private IQueryable<DelegationRow> DelegationRows(IQueryable<Delegation> delegations) =>
        delegations.AsNoTracking().Select(d => new DelegationRow(
            d.Id, d.PositionId, d.FromUserId, d.ToPositionId, d.IsFullScope,
            d.Scopes.Select(s => s.AuthorityKey).OrderBy(k => k).ToList(), d.ValidFrom, d.ValidTo));

    /// <summary>The union of [from, to) periods as non-overlapping periods (touching ones joined).</summary>
    internal static List<(DateTime? From, DateTime? To)> MergePeriods(IEnumerable<(DateTime? From, DateTime? To)> periods)
    {
        List<(DateTime? From, DateTime? To)> merged = [];
        foreach ((DateTime? from, DateTime? to) in periods
                     .Where(p => !Period.IsEmpty(p.From, p.To))
                     .OrderBy(p => p.From ?? DateTime.MinValue))
        {
            if (merged.Count > 0 && (merged[^1].To is null || from is null || from <= merged[^1].To))
            {
                (DateTime? lastFrom, DateTime? lastTo) = merged[^1];
                merged[^1] = (lastFrom, lastTo is null || to is null ? null : (to > lastTo ? to : lastTo));
            }
            else
            {
                merged.Add((from, to));
            }
        }

        return merged;
    }

    private sealed record HeldRow(int Id, int PositionId, AssignmentKind Kind, DateTime? ValidFrom, DateTime? ValidTo);

    private sealed record HolderRow(int Id, int PositionId, string UserId, DateTime? ValidFrom, DateTime? ValidTo);

    private sealed record PositionRow(int Id, string Key, string Title, string UnitKey, string UnitTitle, DateTime? From, DateTime? To);

    private sealed record DelegationRow(
        int Id, int PositionId, string? FromUserId, int? ToPositionId, bool IsFullScope, List<string> Keys, DateTime? ValidFrom, DateTime? ValidTo);

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
