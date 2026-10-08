namespace OrgChart.Core.Abstractions;

/// <summary>
/// Lets the admin UI find users and show their names. OrgChart itself only knows user ids; the host implements
/// this over its own user store. Same shape as Acl's IUserDirectory, so one host class can serve both.
/// </summary>
public interface IUserDirectory
{
    /// <summary>Users whose name, user name or e-mail contains <paramref name="text"/>.</summary>
    Task<IReadOnlyList<UserInfo>> SearchAsync(string text, int maxResults, CancellationToken cancellationToken = default);

    /// <summary>The users that exist among <paramref name="userIds"/>; unknown ids are omitted.</summary>
    Task<IReadOnlyDictionary<string, UserInfo>> GetUsersAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);
}

/// <param name="UserId">The id OrgChart stores (e.g. AspNetUsers.Id).</param>
/// <param name="DisplayName">Shown to admins.</param>
/// <param name="Detail">Helps tell users apart, e.g. user name, e-mail or personnel number.</param>
public sealed record UserInfo(string UserId, string DisplayName, string? Detail);

/// <summary>Default when the host has no user directory: nothing to search, no names.</summary>
public sealed class NullUserDirectory : IUserDirectory
{
    public Task<IReadOnlyList<UserInfo>> SearchAsync(string text, int maxResults, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserInfo>>([]);

    public Task<IReadOnlyDictionary<string, UserInfo>> GetUsersAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, UserInfo>>(new Dictionary<string, UserInfo>());
}
