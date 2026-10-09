using Microsoft.EntityFrameworkCore;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;

namespace OrgChart.EFCore.Admin;

/// <summary>Delegations to people (تفویض) and deputy positions (جانشینی).</summary>
internal sealed partial class EfOrgChartAdministration
{
    public Task<int> DelegateAsync(DelegationInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        return RunAsync(chart: false, async change =>
        {
            Position position = await FindActivePositionAsync(input.PositionKey, cancellationToken);
            string from = RequireUserId(input.FromUserId);
            string to = RequireUserId(input.ToUserId);
            if (from == to)
            {
                throw Error(OrgChartErrors.DelegateIsSelf, "A holder cannot delegate to themselves.");
            }

            (DateTime? validFrom, DateTime? validTo) = RequirePeriod(input.ValidFrom, input.ValidTo);
            Delegation delegation = new()
            {
                Kind = DelegationKind.ToUser,
                Position = position,
                FromUserId = from,
                ToUserId = to,
                ValidFrom = validFrom,
                ValidTo = validTo,
                Note = OptionalNote(input.Note),
            };
            await EnsureDelegatorHoldsAsync(delegation, cancellationToken);
            await EnsureNoOverlappingDelegationAsync(delegation, cancellationToken);
            await SetScopeAsync(delegation, input.AuthorityKeys, cancellationToken);
            db.Delegations.Add(delegation);
            await db.SaveChangesAsync(cancellationToken);

            audit.Write("DelegationCreated", nameof(Delegation), delegation.Id, new { after = DelegationState(delegation) });
            change.Assignments(to);
            return delegation.Id;
        }, cancellationToken);
    }

    public Task<int> AddDeputyAsync(DeputyInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        return RunAsync(chart: false, async change =>
        {
            Position position = await FindActivePositionAsync(input.PositionKey, cancellationToken);
            Position deputy = await FindActivePositionAsync(input.DeputyPositionKey, cancellationToken);
            if (deputy.Id == position.Id)
            {
                throw Error(OrgChartErrors.DelegateIsSelf, "A position cannot be its own deputy.");
            }

            (DateTime? validFrom, DateTime? validTo) = RequirePeriod(input.ValidFrom, input.ValidTo);
            Delegation delegation = new()
            {
                Kind = DelegationKind.ToPosition,
                Position = position,
                ToPosition = deputy,
                Priority = RequirePriority(input.Priority),
                ValidFrom = validFrom,
                ValidTo = validTo,
                Note = OptionalNote(input.Note),
            };
            await EnsureNoOverlappingDelegationAsync(delegation, cancellationToken);
            await SetScopeAsync(delegation, input.AuthorityKeys, cancellationToken);
            db.Delegations.Add(delegation);
            await db.SaveChangesAsync(cancellationToken);

            audit.Write("DeputyAdded", nameof(Delegation), delegation.Id, new { after = DelegationState(delegation) });
            await AddDelegateUsersAsync(change, delegation, cancellationToken);
            return delegation.Id;
        }, cancellationToken);
    }

    public Task UpdateDelegationAsync(int delegationId, DelegationUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        return RunAsync(chart: false, async change =>
        {
            Delegation delegation = await FindDelegationAsync(delegationId, cancellationToken);
            object before = DelegationState(delegation);
            if (delegation.Kind == DelegationKind.ToUser && update.ValidTo is null)
            {
                throw Error(OrgChartErrors.EndDateRequired, "A delegation to a person needs an end date.");
            }

            (delegation.ValidFrom, delegation.ValidTo) = RequirePeriod(update.ValidFrom, update.ValidTo);
            delegation.Note = OptionalNote(update.Note);
            if (delegation.Kind == DelegationKind.ToPosition)
            {
                delegation.Priority = RequirePriority(update.Priority);
            }

            await SetScopeAsync(delegation, update.AuthorityKeys, cancellationToken);
            object after = DelegationState(delegation);
            if (Changed(before, after))
            {
                if (delegation.Kind == DelegationKind.ToUser)
                {
                    await EnsureDelegatorHoldsAsync(delegation, cancellationToken);
                }

                await EnsureNoOverlappingDelegationAsync(delegation, cancellationToken);
                audit.Write("DelegationUpdated", nameof(Delegation), delegation.Id, new { before, after });
                await AddDelegateUsersAsync(change, delegation, cancellationToken);
            }
        }, cancellationToken);
    }

    public Task EndDelegationAsync(int delegationId, DateTime endAtUtc, CancellationToken cancellationToken = default) =>
        RunAsync(chart: false, async change =>
        {
            Delegation delegation = await FindDelegationAsync(delegationId, cancellationToken);
            object before = DelegationState(delegation);
            DateTime end = Utc(endAtUtc);
            if (delegation.ValidTo is { } validTo && validTo <= end)
            {
                throw Error(OrgChartErrors.DelegationAlreadyEnded, $"Delegation {delegation.Id} already ends at {validTo:O}.");
            }

            if (delegation.ValidFrom is { } validFrom && end <= validFrom)
            {
                throw Error(OrgChartErrors.InvalidEndDate, $"Delegation {delegation.Id} starts at {validFrom:O}; remove it instead.");
            }

            delegation.ValidTo = end;
            audit.Write("DelegationEnded", nameof(Delegation), delegation.Id, new { before, after = DelegationState(delegation) });
            await AddDelegateUsersAsync(change, delegation, cancellationToken);
        }, cancellationToken);

    public Task RemoveDelegationAsync(int delegationId, CancellationToken cancellationToken = default) =>
        RunAsync(chart: false, async change =>
        {
            Delegation delegation = await FindDelegationAsync(delegationId, cancellationToken);
            audit.Write("DelegationRemoved", nameof(Delegation), delegation.Id, new { before = DelegationState(delegation) });
            await AddDelegateUsersAsync(change, delegation, cancellationToken);
            db.Delegations.Remove(delegation);
        }, cancellationToken);

    // ---------------------------------------------------------------- helpers

    private async Task<Delegation> FindDelegationAsync(int id, CancellationToken cancellationToken) =>
        await db.Delegations
            .Include(d => d.Position)
            .Include(d => d.ToPosition)
            .Include(d => d.Scopes)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
        ?? throw Error(OrgChartErrors.DelegationNotFound, $"Delegation {id} does not exist.");

    /// <summary>The delegator must hold the position at some time within the delegation's period.</summary>
    private async Task EnsureDelegatorHoldsAsync(Delegation delegation, CancellationToken cancellationToken)
    {
        int positionId = delegation.Position.Id;
        (DateTime? from, DateTime? to) = (delegation.ValidFrom, delegation.ValidTo);
        bool holds = await db.Assignments.AnyAsync(a =>
                a.PositionId == positionId
                && a.UserId == delegation.FromUserId
                && (to == null || a.ValidFrom == null || a.ValidFrom < to)
                && (from == null || a.ValidTo == null || from < a.ValidTo),
            cancellationToken);
        if (!holds)
        {
            throw Error(OrgChartErrors.DelegatorNotHolder,
                $"User '{delegation.FromUserId}' does not hold position '{delegation.Position.Key}' in that period.");
        }
    }

    /// <summary>The same delegation (same position, same delegator and recipient, or same deputy pair) cannot overlap itself.</summary>
    private async Task EnsureNoOverlappingDelegationAsync(Delegation delegation, CancellationToken cancellationToken)
    {
        int positionId = delegation.Position.Id;
        int? toPositionId = delegation.ToPosition?.Id;
        (DateTime? from, DateTime? to) = (delegation.ValidFrom, delegation.ValidTo);
        bool overlaps = await db.Delegations.AnyAsync(d =>
                d.Id != delegation.Id
                && d.Kind == delegation.Kind
                && d.PositionId == positionId
                && d.FromUserId == delegation.FromUserId
                && d.ToUserId == delegation.ToUserId
                && d.ToPositionId == toPositionId
                && (to == null || d.ValidFrom == null || d.ValidFrom < to)
                && (from == null || d.ValidTo == null || from < d.ValidTo),
            cancellationToken);
        if (overlaps)
        {
            throw Error(OrgChartErrors.DelegationOverlap, "The same delegation already exists in an overlapping period.");
        }
    }

    /// <summary>Null = full scope; otherwise a non-empty set of keys the authority catalog offers for the position.</summary>
    private async Task SetScopeAsync(Delegation delegation, IReadOnlyCollection<string>? keys, CancellationToken cancellationToken)
    {
        if (keys is null)
        {
            delegation.IsFullScope = true;
            delegation.Scopes.Clear();
            return;
        }

        List<string> wanted = keys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (wanted.Count == 0)
        {
            throw Error(OrgChartErrors.ScopeEmpty, "Choose at least one authority, or delegate in full.");
        }

        IReadOnlyList<AuthorityInfo> offered = await authorities.GetAuthoritiesAsync(delegation.Position.Key, cancellationToken);
        if (offered.Count == 0)
        {
            throw Error(OrgChartErrors.ScopeNotAvailable, $"No authorities are known for position '{delegation.Position.Key}'; delegate in full.");
        }

        if (wanted.FirstOrDefault(k => !offered.Any(o => o.Key == k)) is { } unknown)
        {
            throw Error(OrgChartErrors.UnknownAuthority, $"Authority '{unknown}' is not offered for position '{delegation.Position.Key}'.");
        }

        delegation.IsFullScope = false;
        foreach (DelegationScope scope in delegation.Scopes.Where(s => !wanted.Contains(s.AuthorityKey)).ToList())
        {
            delegation.Scopes.Remove(scope);
        }

        foreach (string key in wanted.Where(k => !delegation.Scopes.Any(s => s.AuthorityKey == k)))
        {
            delegation.Scopes.Add(new DelegationScope { AuthorityKey = key });
        }
    }

    /// <summary>Whose authority a delegation changes: the recipient, or everyone who ever held the deputy position.</summary>
    private async Task AddDelegateUsersAsync(Change change, Delegation delegation, CancellationToken cancellationToken)
    {
        change.Kind = OrgChartChangeKind.Assignments;
        if (delegation.ToUserId is { } user)
        {
            change.UserIds.Add(user);
        }

        if (delegation.ToPosition?.Id is { } deputyId)
        {
            change.UserIds.UnionWith(await db.Assignments.Where(a => a.PositionId == deputyId).Select(a => a.UserId).Distinct().ToListAsync(cancellationToken));
        }
    }

    /// <summary>
    /// Users whose delegated or deputy authority depends on who holds <paramref name="positionId"/>: recipients of
    /// its delegations and holders of its deputy positions.
    /// </summary>
    private async Task AddDependentUsersAsync(Change change, int positionId, CancellationToken cancellationToken)
    {
        change.UserIds.UnionWith(await db.Delegations
            .Where(d => d.PositionId == positionId && d.ToUserId != null)
            .Select(d => d.ToUserId!)
            .Distinct()
            .ToListAsync(cancellationToken));
        change.UserIds.UnionWith(await db.Assignments
            .Where(a => db.Delegations.Any(d => d.PositionId == positionId && d.ToPositionId == a.PositionId))
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync(cancellationToken));
    }

    private static int RequirePriority(int priority) =>
        priority >= 1 ? priority : throw Error(OrgChartErrors.InvalidPriority, $"Priority {priority} is not valid; use 1 or more.");

    private static object DelegationState(Delegation d) => new
    {
        Kind = d.Kind.ToString(),
        Position = d.Position.Key,
        From = d.FromUserId,
        To = d.ToUserId,
        Deputy = d.ToPosition?.Key,
        d.Priority,
        Scope = d.IsFullScope ? null : string.Join(",", d.Scopes.Select(s => s.AuthorityKey).Order()),
        d.ValidFrom,
        d.ValidTo,
        d.Note,
    };
}
