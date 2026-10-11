namespace OrgChart.Core.Admin;

/// <summary>
/// Changes the chart. Units and positions are addressed by their stable keys; keys never change, and nothing
/// is hard-deleted except assignments entered by mistake. Every operation runs in one transaction, writes an
/// audit log row and, once committed, notifies the registered change listeners.
/// Invalid requests throw <see cref="OrgChartAdminException"/>.
/// </summary>
/// <remarks>All dates are UTC; a value with <see cref="DateTimeKind.Unspecified"/> is taken as UTC.</remarks>
public interface IOrgChartAdministration
{
    /// <summary>Returns the key: the given one, or a generated one when it is blank.</summary>
    Task<string> CreateTypeAsync(OrgTypeKind kind, OrgTypeInput input, CancellationToken cancellationToken = default);

    Task UpdateTypeAsync(OrgTypeKind kind, string key, OrgTypeUpdate update, CancellationToken cancellationToken = default);

    /// <summary>An inactive type stays on the units/positions that use it but cannot be chosen any more.</summary>
    Task SetTypeActiveAsync(OrgTypeKind kind, string key, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Returns the key: the given one, or a generated one when it is blank.</summary>
    Task<string> CreateUnitAsync(UnitInput input, CancellationToken cancellationToken = default);

    Task UpdateUnitAsync(string key, UnitUpdate update, CancellationToken cancellationToken = default);

    /// <summary>Re-parents the unit (null makes it a root). Its sub-units and positions move with it.</summary>
    Task MoveUnitAsync(string key, string? newParentKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete / restore. Deactivating needs the unit to have no active sub-units or positions;
    /// reactivating needs an active parent.
    /// </summary>
    Task SetUnitActiveAsync(string key, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Names one active position of the unit as its head, or clears it (null).</summary>
    Task SetUnitManagerAsync(string unitKey, string? positionKey, CancellationToken cancellationToken = default);

    /// <summary>Returns the key: the given one, or a generated one when it is blank.</summary>
    Task<string> CreatePositionAsync(PositionInput input, CancellationToken cancellationToken = default);

    Task UpdatePositionAsync(string key, PositionUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the position, with its holders and every position below it, to the top of another active unit
    /// (where it reports to that unit's head). Keys do not change.
    /// </summary>
    Task MovePositionAsync(string key, string newUnitKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts the position, with its holders and every position below it, under <paramref name="newSuperiorKey"/>;
    /// they move to that position's unit. Keys do not change. A unit head cannot be moved.
    /// </summary>
    Task MovePositionUnderAsync(string key, string newSuperiorKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete / restore. Deactivating needs the position to have no current or future assignments and not
    /// to head a unit; reactivating needs an active unit.
    /// </summary>
    Task SetPositionActiveAsync(string key, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Puts a user in a position; a future <c>ValidFrom</c> plans it ahead. Returns the assignment id.</summary>
    Task<int> AssignAsync(AssignmentInput input, CancellationToken cancellationToken = default);

    Task UpdateAssignmentAsync(int assignmentId, AssignmentUpdate update, CancellationToken cancellationToken = default);

    /// <summary>Ends the assignment at <paramref name="endAtUtc"/> (exclusive); the row stays as history.</summary>
    Task EndAssignmentAsync(int assignmentId, DateTime endAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the holder to another position: ends the assignment at <see cref="TransferInput.EffectiveAt"/> and
    /// starts a new one there, in one transaction. Returns the new assignment id.
    /// </summary>
    Task<int> TransferAsync(int assignmentId, TransferInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes an assignment entered by mistake. To end a real one use <see cref="EndAssignmentAsync"/>.</summary>
    Task RemoveAssignmentAsync(int assignmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a delegation from a holder to a person. The delegator keeps their own authority. Returns the id.
    /// Several delegations of one position may run at once (e.g. different parts to different people).
    /// </summary>
    Task<int> DelegateAsync(DelegationInput input, CancellationToken cancellationToken = default);

    /// <summary>Names a deputy position for a position. Returns the id.</summary>
    Task<int> AddDeputyAsync(DeputyInput input, CancellationToken cancellationToken = default);

    Task UpdateDelegationAsync(int delegationId, DelegationUpdate update, CancellationToken cancellationToken = default);

    /// <summary>Ends a delegation or deputy at <paramref name="endAtUtc"/> (exclusive); the row stays as history.</summary>
    Task EndDelegationAsync(int delegationId, DateTime endAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Deletes a delegation or deputy entered by mistake.</summary>
    Task RemoveDelegationAsync(int delegationId, CancellationToken cancellationToken = default);
}
