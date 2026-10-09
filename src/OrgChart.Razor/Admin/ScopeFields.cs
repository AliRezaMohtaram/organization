using OrgChart.Core.Abstractions;

namespace OrgChart.Razor.Admin;

/// <summary>View model of <c>_ScopeFields</c>: the authorities a position offers and what is chosen.</summary>
public sealed record ScopeFields(IReadOnlyList<AuthorityInfo> Offered, bool Full, IReadOnlyCollection<string> Selected);
