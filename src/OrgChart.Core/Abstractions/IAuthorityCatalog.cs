namespace OrgChart.Core.Abstractions;

/// <summary>
/// What can be delegated from a position, for partial delegations and deputies. OrgChart does not know what an
/// "authority" is; the host (e.g. the Acl bridge: the roles bound to the position) supplies the list and interprets
/// the keys. Without a catalog only full delegations are possible.
/// </summary>
public interface IAuthorityCatalog
{
    Task<IReadOnlyList<AuthorityInfo>> GetAuthoritiesAsync(string positionKey, CancellationToken cancellationToken = default);
}

/// <param name="Key">Stable key stored in the delegation (e.g. an Acl role id).</param>
public sealed record AuthorityInfo(string Key, string Title, string? Description = null);

public sealed class NullAuthorityCatalog : IAuthorityCatalog
{
    public Task<IReadOnlyList<AuthorityInfo>> GetAuthoritiesAsync(string positionKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AuthorityInfo>>([]);
}
