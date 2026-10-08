namespace OrgChart.Core.Abstractions;

/// <summary>
/// The user the current operation runs as; recorded in Created/Updated fields and the audit log.
/// Supplied by the host (e.g. from the authenticated principal).
/// </summary>
public interface ICurrentUser
{
    /// <summary>Null for anonymous or system work.</summary>
    string? UserId { get; }
}

/// <summary>Default when the host supplies no user: changes are recorded without an actor.</summary>
public sealed class NullCurrentUser : ICurrentUser
{
    public string? UserId => null;
}
