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
    /// The user's assignments, past, current and future (acting ones too), with their unit's ancestors.
    /// Assignments in an inactive position or unit are left out. The period is the assignment's period
    /// narrowed to the validity of the position and its unit; assignments where that leaves nothing are left out.
    /// </summary>
    Task<IReadOnlyList<UserPosition>> GetUserPositionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>All assignments (history included) to positions of the unit, or of the unit and its sub-units.</summary>
    Task<IReadOnlyList<AssignmentInfo>> GetUnitAssignmentsAsync(
        string unitKey,
        bool includeSubUnits = false,
        CancellationToken cancellationToken = default);

    /// <summary>One assignment, or null when it does not exist.</summary>
    Task<AssignmentInfo?> GetAssignmentAsync(int assignmentId, CancellationToken cancellationToken = default);

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
public sealed record UserPosition(
    int AssignmentId,
    string PositionKey,
    string PositionTitle,
    string OrgUnitKey,
    string OrgUnitTitle,
    IReadOnlyList<string> OrgUnitAncestorKeys,
    AssignmentKind Kind,
    DateTime? ValidFrom,
    DateTime? ValidTo);

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
