using Acl.Core.Abstractions;

using OrgChart.Core.Chart;
using OrgChart.Core.Model;

namespace OrgChart.Acl;

/// <summary>Acl's view of the chart, read through <see cref="IOrgChartReader"/> (the snapshot is cached there).</summary>
public sealed class OrgChartOrgStructure(IOrgChartReader reader) : IOrgStructure
{
    public async Task<IReadOnlyList<PositionAssignment>> GetUserPositionsAsync(string userId, CancellationToken cancellationToken = default) =>
        (await reader.GetUserPositionsAsync(userId, cancellationToken)).Select(ToAcl).ToList();

    public async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetDescendantUnitKeysAsync(
        IReadOnlyCollection<string> orgUnitKeys,
        CancellationToken cancellationToken = default)
    {
        OrgChartSnapshot chart = await reader.GetSnapshotAsync(cancellationToken);
        Dictionary<string, IReadOnlyCollection<string>> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (string key in orgUnitKeys)
        {
            if (chart.FindUnit(key) is { } unit && !result.ContainsKey(unit.Key)
                && chart.GetDescendantKeys(unit.Key) is { Count: > 0 } descendants)
            {
                result[unit.Key] = descendants;
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<OrgUnitInfo>> GetUnitsAsync(CancellationToken cancellationToken = default) =>
        (await reader.GetSnapshotAsync(cancellationToken)).Units
            .Where(u => u.IsActive)
            .Select(u => new OrgUnitInfo(u.Key, u.Title, u.ParentKey))
            .ToList();

    public async Task<IReadOnlyList<PositionInfo>> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        OrgChartSnapshot chart = await reader.GetSnapshotAsync(cancellationToken);
        return chart.Units
            .Where(u => u.IsActive)
            .SelectMany(u => chart.GetPositions(u.Key))
            .Where(p => p.IsActive)
            .Select(p => new PositionInfo(p.Key, p.Title, p.UnitKey))
            .ToList();
    }

    /// <summary>Delegations and deputies become "acting for" entries limited to the delegated roles.</summary>
    public static PositionAssignment ToAcl(UserPosition position) =>
        new(position.PositionKey, position.OrgUnitKey, position.OrgUnitAncestorKeys, position.ValidFrom, position.ValidTo)
        {
            Kind = position.Kind switch
            {
                AssignmentKind.Delegated => PositionAssignmentKind.Delegated,
                AssignmentKind.Deputy => PositionAssignmentKind.Deputy,
                _ => PositionAssignmentKind.Holder,
            },
            RoleIds = position.AuthorityKeys is null ? null : AclAuthorityCatalog.ParseRoleIds(position.AuthorityKeys),
        };
}
