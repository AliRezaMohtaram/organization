namespace OrgChart.Core.Abstractions;

/// <summary>
/// Told about every committed change to the chart, so other modules can refresh what they cached
/// (e.g. a bridge to Acl bumps access stamps). Register with <c>OrgChartBuilder.AddChangeListener&lt;T&gt;()</c>;
/// listeners are resolved from the scope that made the change.
/// </summary>
/// <remarks>
/// Called after the change is committed. An exception from a listener reaches the caller of the admin
/// operation, but the change itself stays saved.
/// </remarks>
public interface IOrgChartChangeListener
{
    Task OnChangedAsync(OrgChartChange change, CancellationToken cancellationToken = default);
}

/// <param name="Kind">What changed; decides how much a consumer has to refresh.</param>
/// <param name="UserIds">For <see cref="OrgChartChangeKind.Assignments"/>: the users whose positions changed.</param>
public sealed record OrgChartChange(OrgChartChangeKind Kind, IReadOnlyCollection<string> UserIds);

public enum OrgChartChangeKind
{
    /// <summary>
    /// Positions of specific users changed (assigned, ended, moved, removed). Only those users are affected.
    /// </summary>
    Assignments = 1,

    /// <summary>
    /// The shape of the chart changed: units or positions added, moved, re-parented, (de)activated, their
    /// validity changed, or a unit's manager changed. Can affect any user.
    /// </summary>
    Structure = 2,

    /// <summary>
    /// Only descriptive data changed (titles, codes, sort order, types). Keys and tree are unchanged.
    /// </summary>
    Details = 3,
}
