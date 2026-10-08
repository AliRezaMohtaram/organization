using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.My;

/// <summary>"My delegations": the signed-in user's positions, the delegations they gave and received.</summary>
[Authorize(Policy = OrgChartPolicies.Self)]
public sealed class IndexModel(IOrgChartReader reader, IUserDirectory users) : OrgPageModel
{
    public IReadOnlyList<UserPosition> Positions { get; private set; } = [];
    public IReadOnlyList<DelegationInfo> Given { get; private set; } = [];
    public IReadOnlyList<DelegationInfo> Received { get; private set; } = [];
    public OrgChartSnapshot Chart { get; private set; } = OrgChartSnapshot.Empty;
    public IReadOnlyDictionary<string, UserInfo> Names { get; private set; } = new Dictionary<string, UserInfo>();
    public DateTime Now { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } me)
        {
            return Forbid();
        }

        ViewData["Title"] = OrgText.Get("Nav_My");
        ViewData["Subtitle"] = OrgText.Get("My_Subtitle");
        Now = UtcNow;
        Chart = await reader.GetSnapshotAsync(cancellationToken);
        Positions = (await reader.GetUserPositionsAsync(me, cancellationToken)).Where(p => p.ValidTo is null || p.ValidTo > Now).ToList();
        Given = (await reader.GetDelegationsAsync(new DelegationQuery(FromUserId: me), cancellationToken))
            .Where(d => d.ValidTo is null || d.ValidTo > Now).ToList();
        Received = (await reader.GetDelegationsAsync(new DelegationQuery(ToUserId: me), cancellationToken))
            .Where(d => d.ValidTo is null || d.ValidTo > Now).ToList();

        HashSet<string> ids = [.. Given.Select(d => d.ToUserId!), .. Received.Select(d => d.FromUserId!)];
        Names = ids.Count == 0 ? Names : await users.GetUsersAsync(ids, cancellationToken);
        return Page();
    }

    public string Name(string? userId) => userId is null ? "" : Names.TryGetValue(userId, out UserInfo? u) ? u.DisplayName : userId;

    public bool IsOwn(UserPosition p) => p.Kind is AssignmentKind.Primary or AssignmentKind.Acting;
}
