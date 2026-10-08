using OrgChart.Core.Model;

namespace OrgChart.Core.Chart;

/// <summary>
/// Read side of the chart, for other modules and the UI. The snapshot is cached and reloaded only when the
/// chart has changed (checked with one small query per call), so calling it often is cheap.
/// </summary>
public interface IOrgChartReader
{
    /// <summary>The whole chart, active and inactive.</summary>
    Task<OrgChartSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Every position the user holds or has authority in, past, current and future, with the unit's ancestors:
    /// own assignments (<see cref="AssignmentKind.Primary"/>, <see cref="AssignmentKind.Acting"/>), delegations to the
    /// user (<see cref="AssignmentKind.Delegated"/>: only while the delegator holds the position) and deputy positions
    /// the user holds (<see cref="AssignmentKind.Deputy"/>: only while the position has a holder). Delegated and deputy
    /// entries may be limited to <see cref="UserPosition.AuthorityKeys"/>. Periods are narrowed to the validity of the
    /// positions and units involved; inactive positions/units and empty periods are left out.
    /// </summary>
    Task<IReadOnlyList<UserPosition>> GetUserPositionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>All assignments (history included) to positions of the unit, or of the unit and its sub-units.</summary>
    Task<IReadOnlyList<AssignmentInfo>> GetUnitAssignmentsAsync(
        string unitKey,
        bool includeSubUnits = false,
        CancellationToken cancellationToken = default);

    /// <summary>One assignment, or null when it does not exist.</summary>
    Task<AssignmentInfo?> GetAssignmentAsync(int assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Delegations and deputies matching the query, history included.</summary>
    Task<IReadOnlyList<DelegationInfo>> GetDelegationsAsync(DelegationQuery query, CancellationToken cancellationToken = default);

    Task<DelegationInfo?> GetDelegationAsync(int delegationId, CancellationToken cancellationToken = default);

    /// <summary>Assignments valid at <paramref name="atUtc"/> for each of the positions; positions nobody holds are omitted.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<AssignmentInfo>>> GetHoldersAsync(
        IReadOnlyCollection<string> positionKeys,
        DateTime atUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The user's managers at <paramref name="atUtc"/>: for each position the user holds then, the nearest
    /// manager position up the chart (see <see cref="OrgChartSnapshot.GetManagerChain"/>) that someone other
    /// than the user holds, with its holders.
    /// </summary>
    Task<IReadOnlyList<ManagerInfo>> GetManagersAsync(string userId, DateTime atUtc, CancellationToken cancellationToken = default);
}

/// <param name="ValidFrom">Inclusive start (UTC), or null for unbounded.</param>
/// <param name="ValidTo">Exclusive end (UTC), or null for unbounded.</param>
/// <param name="AssignmentId">The user's own assignment; for a delegation the delegator's assignment.</param>
/// <param name="AuthorityKeys">Delegated/deputy entries: the authority keys passed on; null = all of the position's authority.</param>
/// <param name="DelegationId">Delegated/deputy entries: the delegation they come from.</param>
public sealed record UserPosition(
    int AssignmentId,
    string PositionKey,
    string PositionTitle,
    string OrgUnitKey,
    string OrgUnitTitle,
    IReadOnlyList<string> OrgUnitAncestorKeys,
    AssignmentKind Kind,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    IReadOnlyList<string>? AuthorityKeys = null,
    int? DelegationId = null);

/// <param name="PositionKeys">Delegations from these positions (null = any).</param>
/// <param name="ToPositionKeys">Deputies: delegations to these positions (null = any).</param>
/// <param name="FromUserId">Delegations made by this user.</param>
/// <param name="ToUserId">Delegations to this user.</param>
/// <param name="ActiveAt">Only those valid at this time (null = all, history included).</param>
public sealed record DelegationQuery(
    IReadOnlyCollection<string>? PositionKeys = null,
    IReadOnlyCollection<string>? ToPositionKeys = null,
    string? FromUserId = null,
    string? ToUserId = null,
    DelegationKind? Kind = null,
    DateTime? ActiveAt = null);

/// <param name="AuthorityKeys">Null = full scope.</param>
public sealed record DelegationInfo(
    int Id,
    DelegationKind Kind,
    string PositionKey,
    string? FromUserId,
    string? ToUserId,
    string? ToPositionKey,
    int Priority,
    IReadOnlyList<string>? AuthorityKeys,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    string? Note);

/// <param name="ValidFrom">Inclusive start (UTC), or null for unbounded.</param>
/// <param name="ValidTo">Exclusive end (UTC), or null for unbounded.</param>
public sealed record AssignmentInfo(
    int Id,
    string UserId,
    string PositionKey,
    string OrgUnitKey,
    AssignmentKind Kind,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    string? Note);

/// <param name="ForPositionKey">The user's position this manager is for.</param>
/// <param name="ManagerPositionKey">The manager position.</param>
/// <param name="Holders">Who holds the manager position at the given time.</param>
public sealed record ManagerInfo(string ForPositionKey, string ManagerPositionKey, IReadOnlyList<AssignmentInfo> Holders);
