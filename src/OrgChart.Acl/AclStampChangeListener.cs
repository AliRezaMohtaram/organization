using Acl.Core.Abstractions;

using OrgChart.Core.Abstractions;

namespace OrgChart.Acl;

/// <summary>
/// Invalidates Acl's cached access after chart changes: assignment, delegation and deputy changes refresh the
/// users concerned; structural changes refresh everyone; titles, codes and types change no access.
/// </summary>
public sealed class AclStampChangeListener(IAccessStampStore stamps) : IOrgChartChangeListener
{
    public Task OnChangedAsync(OrgChartChange change, CancellationToken cancellationToken = default) => change.Kind switch
    {
        OrgChartChangeKind.Assignments when change.UserIds.Count > 0 => stamps.BumpUsersAsync(change.UserIds, cancellationToken),
        OrgChartChangeKind.Structure => stamps.BumpGlobalAsync(cancellationToken),
        _ => Task.CompletedTask,
    };
}
