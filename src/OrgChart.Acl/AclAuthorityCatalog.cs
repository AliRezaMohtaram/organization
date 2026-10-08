using System.Globalization;

using Acl.Core.Admin;

using OrgChart.Core.Abstractions;

namespace OrgChart.Acl;

/// <summary>
/// What a position can delegate: the Acl roles bound to it. The authority key is the role id, so a partial
/// delegation reaches Acl as <c>PositionAssignment.RoleIds</c>.
/// </summary>
public sealed class AclAuthorityCatalog(IAssignmentAdministration assignments) : IAuthorityCatalog
{
    public async Task<IReadOnlyList<AuthorityInfo>> GetAuthoritiesAsync(string positionKey, CancellationToken cancellationToken = default) =>
        (await assignments.GetPositionRolesAsync(positionKey, cancellationToken))
            .Select(r => new AuthorityInfo(r.RoleId.ToString(CultureInfo.InvariantCulture), r.RoleName, r.Description))
            .ToList();

    /// <summary>Role ids from authority keys; keys that are not role ids (no longer offered) grant nothing.</summary>
    public static IReadOnlyCollection<int> ParseRoleIds(IEnumerable<string> authorityKeys) =>
        authorityKeys
            .Select(k => int.TryParse(k, NumberStyles.None, CultureInfo.InvariantCulture, out int id) ? id : (int?)null)
            .OfType<int>()
            .ToHashSet();
}
