using Microsoft.EntityFrameworkCore;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;

namespace OrgChart.Tests.Integration;

public sealed class DelegationTests : IDisposable
{
    private static readonly DateTime s_jan = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly OrgChartServices _s = new();

    public void Dispose() => _s.Dispose();

    private async Task<string> ExpectErrorAsync(Func<IOrgChartAdministration, Task> action) =>
        (await Assert.ThrowsAsync<OrgChartAdminException>(() => _s.AdminAsync(action))).Code;

    private Task<IReadOnlyList<UserPosition>> PositionsOf(string userId) => _s.ReadAsync(r => r.GetUserPositionsAsync(userId));

    /// <summary>u1 is CFO from January.</summary>
    private async Task<int> SeedCfoAsync()
    {
        await _s.SeedAsync();
        int id = await _s.AdminAsync(a => a.AssignAsync(new AssignmentInput("POS-CFO", "u1", ValidFrom: s_jan)));
        _s.Changes.Clear();
        return id;
    }

    [Fact]
    public async Task Delegation_gives_the_recipient_the_position_for_the_period_and_the_delegator_keeps_it()
    {
        await SeedCfoAsync();

        int id = await _s.AdminAsync(a => a.DelegateAsync(new DelegationInput("POS-CFO", "u1", "u2", s_jan.AddMonths(1), s_jan.AddMonths(2), Note: "leave")));

        UserPosition delegated = Assert.Single(await PositionsOf("u2"));
        Assert.Equal((AssignmentKind.Delegated, "POS-CFO", "FIN"), (delegated.Kind, delegated.PositionKey, delegated.OrgUnitKey));
        Assert.Equal((s_jan.AddMonths(1), s_jan.AddMonths(2)), (delegated.ValidFrom!.Value, delegated.ValidTo!.Value));
        Assert.Null(delegated.AuthorityKeys);
        Assert.Equal(id, delegated.DelegationId);
        Assert.Equal(AssignmentKind.Primary, Assert.Single(await PositionsOf("u1")).Kind);

        OrgChartChange change = Assert.Single(_s.Changes.All);
        Assert.Equal((OrgChartChangeKind.Assignments, "u2"), (change.Kind, Assert.Single(change.UserIds)));
        Assert.Equal("DelegationCreated", (await _s.DbAsync(db => db.AuditLogs.OrderBy(l => l.Id).LastAsync())).Operation);
    }

    [Fact]
    public async Task Delegation_ends_with_the_delegators_assignment_and_the_recipient_is_notified()
    {
        int assignment = await SeedCfoAsync();
        await _s.AdminAsync(a => a.DelegateAsync(new DelegationInput("POS-CFO", "u1", "u2", s_jan.AddMonths(1), s_jan.AddMonths(3))));
        _s.Changes.Clear();

        await _s.AdminAsync(a => a.EndAssignmentAsync(assignment, s_jan.AddMonths(2)));

        Assert.Equal(s_jan.AddMonths(2), Assert.Single(await PositionsOf("u2")).ValidTo);
        Assert.Equal(["u1", "u2"], Assert.Single(_s.Changes.All).UserIds.Order());
    }

    [Fact]
    public async Task Partial_delegation_carries_its_authority_keys()
    {
        await SeedCfoAsync();

        await _s.AdminAsync(a => a.DelegateAsync(new DelegationInput("POS-CFO", "u1", "u2", null, s_jan.AddMonths(2), ["R2", "R1", "R1"])));

        Assert.Equal(["R1", "R2"], Assert.Single(await PositionsOf("u2")).AuthorityKeys);
        Assert.Equal(OrgChartErrors.UnknownAuthority,
            await ExpectErrorAsync(a => a.DelegateAsync(new DelegationInput("POS-CFO", "u1", "u3", null, s_jan.AddMonths(2), ["NOPE"]))));
        Assert.Equal(OrgChartErrors.ScopeEmpty,
            await ExpectErrorAsync(a => a.DelegateAsync(new DelegationInput("POS-CFO", "u1", "u3", null, s_jan.AddMonths(2), []))));
    }

    [Fact]
    public async Task Without_a_catalog_only_full_delegations_are_possible()
    {
        await SeedCfoAsync();
        Microsoft.Extensions.DependencyInjection.ServiceProvider plain = _s.CreateServer(b => b.AddAuthorityCatalog<EmptyCatalog>());

        OrgChartAdminException error = await Assert.ThrowsAsync<OrgChartAdminException>(() => _s.RunAsync(sp =>
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IOrgChartAdministration>(sp)
                .DelegateAsync(new DelegationInput("POS-CFO", "u1", "u2", null, s_jan.AddMonths(2), ["R1"])), plain));

        Assert.Equal(OrgChartErrors.ScopeNotAvailable, error.Code);
    }

    [Fact]
    public async Task Delegation_rules()
    {
        await SeedCfoAsync();
        await _s.AdminAsync(a => a.DelegateAsync(new DelegationInput("POS-CFO", "u1", "u2", s_jan, s_jan.AddMonths(1))));

        Assert.Equal(OrgChartErrors.DelegateIsSelf,
            await ExpectErrorAsync(a => a.DelegateAsync(new DelegationInput("POS-CFO", "u1", "u1", s_jan, s_jan.AddMonths(1)))));
        Assert.Equal(OrgChartErrors.DelegatorNotHolder,
            await ExpectErrorAsync(a => a.DelegateAsync(new DelegationInput("POS-CFO", "u9", "u2", s_jan, s_jan.AddMonths(1)))));
        Assert.Equal(OrgChartErrors.DelegatorNotHolder,
            await ExpectErrorAsync(a => a.DelegateAsync(new DelegationInput("POS-CFO", "u1", "u2", s_jan.AddYears(-2), s_jan.AddYears(-1)))));
        Assert.Equal(OrgChartErrors.DelegationOverlap,
            await ExpectErrorAsync(a => a.DelegateAsync(new DelegationInput("POS-CFO", "u1", "u2", s_jan.AddDays(10), s_jan.AddMonths(2)))));

        // No chains: the recipient does not hold the position, so cannot pass it on.
        Assert.Equal(OrgChartErrors.DelegatorNotHolder,
            await ExpectErrorAsync(a => a.DelegateAsync(new DelegationInput("POS-CFO", "u2", "u3", s_jan, s_jan.AddDays(5)))));

        // Different parts to different people at the same time are fine.
        await _s.AdminAsync(a => a.DelegateAsync(new DelegationInput("POS-CFO", "u1", "u3", s_jan, s_jan.AddMonths(1), ["R3"])));
    }

    [Fact]
    public async Task Deputy_has_authority_only_while_the_position_has_a_holder()
    {
        await _s.SeedAsync();
        await _s.AdminAsync(async a =>
        {
            await a.AssignAsync(new AssignmentInput("POS-ACC", "deputy", ValidFrom: s_jan));
            await a.AssignAsync(new AssignmentInput("POS-CFO", "u1", ValidFrom: s_jan, ValidTo: s_jan.AddMonths(2)));
            await a.AssignAsync(new AssignmentInput("POS-CFO", "u2", AssignmentKind.Acting, s_jan.AddMonths(2), s_jan.AddMonths(3)));
            await a.AssignAsync(new AssignmentInput("POS-CFO", "u3", ValidFrom: s_jan.AddMonths(5)));
        });
        _s.Changes.Clear();

        await _s.AdminAsync(a => a.AddDeputyAsync(new DeputyInput("POS-CFO", "POS-ACC", AuthorityKeys: ["R1"])));

        List<UserPosition> deputy = (await PositionsOf("deputy")).Where(p => p.Kind == AssignmentKind.Deputy).ToList();
        Assert.Equal(
            [(s_jan, (DateTime?)s_jan.AddMonths(3)), (s_jan.AddMonths(5), null)],
            deputy.Select(p => (p.ValidFrom!.Value, p.ValidTo)));
        Assert.All(deputy, p => Assert.Equal(["R1"], p.AuthorityKeys));
        Assert.Equal(["deputy"], Assert.Single(_s.Changes.All).UserIds);

        Assert.Equal(OrgChartErrors.DelegateIsSelf, await ExpectErrorAsync(a => a.AddDeputyAsync(new DeputyInput("POS-CFO", "pos-cfo"))));
        Assert.Equal(OrgChartErrors.DelegationOverlap, await ExpectErrorAsync(a => a.AddDeputyAsync(new DeputyInput("POS-CFO", "POS-ACC"))));
        Assert.Equal(OrgChartErrors.InvalidPriority, await ExpectErrorAsync(a => a.AddDeputyAsync(new DeputyInput("POS-CFO", "POS-CLERK", 0))));

        // A change of the deputized position's holders tells the deputy.
        _s.Changes.Clear();
        await _s.AdminAsync(a => a.AssignAsync(new AssignmentInput("POS-CFO", "u4", AssignmentKind.Acting, s_jan.AddMonths(3), s_jan.AddMonths(4))));
        Assert.Contains("deputy", Assert.Single(_s.Changes.All).UserIds);
    }

    [Fact]
    public async Task Update_end_and_remove()
    {
        await SeedCfoAsync();
        int id = await _s.AdminAsync(a => a.DelegateAsync(new DelegationInput("POS-CFO", "u1", "u2", s_jan, s_jan.AddMonths(3))));

        await _s.AdminAsync(a => a.UpdateDelegationAsync(id, new DelegationUpdate(s_jan, s_jan.AddMonths(2), ["R2"], "shorter")));
        DelegationInfo info = (await _s.ReadAsync(r => r.GetDelegationAsync(id)))!;
        Assert.Equal((s_jan.AddMonths(2), "shorter", "R2"), (info.ValidTo!.Value, info.Note, Assert.Single(info.AuthorityKeys!)));
        Assert.Equal(OrgChartErrors.EndDateRequired, await ExpectErrorAsync(a => a.UpdateDelegationAsync(id, new DelegationUpdate(s_jan, null, null, null))));

        await _s.AdminAsync(a => a.EndDelegationAsync(id, s_jan.AddMonths(1)));
        Assert.Equal(s_jan.AddMonths(1), Assert.Single(await PositionsOf("u2")).ValidTo);
        Assert.Equal(OrgChartErrors.DelegationAlreadyEnded, await ExpectErrorAsync(a => a.EndDelegationAsync(id, s_jan.AddMonths(2))));

        await _s.AdminAsync(a => a.RemoveDelegationAsync(id));
        Assert.Empty(await PositionsOf("u2"));
        Assert.Null(await _s.ReadAsync(r => r.GetDelegationAsync(id)));
        Assert.Equal(OrgChartErrors.DelegationNotFound, await ExpectErrorAsync(a => a.RemoveDelegationAsync(id)));
    }

    [Fact]
    public async Task Delegations_can_be_queried()
    {
        await SeedCfoAsync();
        await _s.AdminAsync(async a =>
        {
            await a.DelegateAsync(new DelegationInput("POS-CFO", "u1", "u2", s_jan, s_jan.AddMonths(1)));
            await a.AddDeputyAsync(new DeputyInput("POS-CFO", "POS-ACC", 2));
            await a.AddDeputyAsync(new DeputyInput("POS-CFO", "POS-CLERK", 1));
        });

        IReadOnlyList<DelegationInfo> all = await _s.ReadAsync(r => r.GetDelegationsAsync(new DelegationQuery(PositionKeys: ["pos-cfo"])));
        Assert.Equal([DelegationKind.ToUser, DelegationKind.ToPosition, DelegationKind.ToPosition], all.Select(d => d.Kind));
        Assert.Equal(["POS-CLERK", "POS-ACC"], all.Where(d => d.Kind == DelegationKind.ToPosition).Select(d => d.ToPositionKey));
        Assert.Single(await _s.ReadAsync(r => r.GetDelegationsAsync(new DelegationQuery(ToUserId: "u2"))));
        Assert.Single(await _s.ReadAsync(r => r.GetDelegationsAsync(new DelegationQuery(ToPositionKeys: ["POS-ACC"]))));
        Assert.Empty(await _s.ReadAsync(r => r.GetDelegationsAsync(new DelegationQuery(Kind: DelegationKind.ToUser, ActiveAt: s_jan.AddMonths(2)))));
    }

    private sealed class EmptyCatalog : IAuthorityCatalog
    {
        public Task<IReadOnlyList<AuthorityInfo>> GetAuthoritiesAsync(string positionKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuthorityInfo>>([]);
    }
}
