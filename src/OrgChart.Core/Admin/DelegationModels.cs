namespace OrgChart.Core.Admin;

/// <summary>A holder delegates (part of) a position's authority to a person for a period (تفویض اختیار).</summary>
/// <param name="FromUserId">The delegating holder; the delegation applies only while they hold the position.</param>
/// <param name="ValidTo">Exclusive end (UTC); required.</param>
/// <param name="AuthorityKeys">Null = all of the position's authority; else keys from the <c>IAuthorityCatalog</c>.</param>
public sealed record DelegationInput(
    string PositionKey,
    string FromUserId,
    string ToUserId,
    DateTime? ValidFrom,
    DateTime ValidTo,
    IReadOnlyCollection<string>? AuthorityKeys = null,
    string? Note = null);

/// <summary>A standing deputy position for a position (جانشینی); whoever holds the deputy position has the authority.</summary>
/// <param name="Priority">1 = first deputy.</param>
/// <param name="AuthorityKeys">Null = all of the position's authority; else keys from the <c>IAuthorityCatalog</c>.</param>
public sealed record DeputyInput(
    string PositionKey,
    string DeputyPositionKey,
    int Priority = 1,
    DateTime? ValidFrom = null,
    DateTime? ValidTo = null,
    IReadOnlyCollection<string>? AuthorityKeys = null,
    string? Note = null);

/// <summary>Changes to a delegation or deputy. <paramref name="ValidTo"/> is required for a delegation to a person.</summary>
public sealed record DelegationUpdate(
    DateTime? ValidFrom,
    DateTime? ValidTo,
    IReadOnlyCollection<string>? AuthorityKeys,
    string? Note,
    int Priority = 1);
