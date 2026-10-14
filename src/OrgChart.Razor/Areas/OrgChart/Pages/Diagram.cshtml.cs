using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Chart;
using OrgChart.Razor.Admin;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages;

/// <summary>
/// The organization as a graphical chart: unit boxes top-down with their head (position and holder), counts and,
/// on request, their positions; connector lines, collapse/expand, search, zoom, pan and print (script). The whole
/// chart, or one unit's branch (<see cref="UnitKey"/>); inactive units only on request.
/// </summary>
[Authorize(Policy = OrgChartPolicies.View)]
public sealed class DiagramModel(IOrgChartReader reader, IUserDirectory users) : OrgPageModel
{
    /// <summary>The branch to show (its unit at the top); null = every root.</summary>
    [BindProperty(SupportsGet = true, Name = "unit")]
    public string? UnitKey { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Inactive { get; set; }

    public OrgChartSnapshot Chart { get; private set; } = OrgChartSnapshot.Empty;

    /// <summary>The branch's top unit; null = the whole chart.</summary>
    public UnitNode? Top { get; private set; }

    /// <summary>The units at the top of the drawing.</summary>
    public IReadOnlyList<UnitNode> Roots { get; private set; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<AssignmentInfo>> Holders { get; private set; } =
        new Dictionary<string, IReadOnlyList<AssignmentInfo>>();

    public IReadOnlyDictionary<string, UserInfo> Users { get; private set; } = new Dictionary<string, UserInfo>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Chart = await reader.GetSnapshotAsync(cancellationToken);
        Top = Chart.FindUnit(UnitKey);
        Roots = Top is not null ? [Top] : Chart.Roots.Where(Shown).ToList();
        ViewData["Title"] = OrgText.Get("Nav_Diagram");
        ViewData["Subtitle"] = Top is null
            ? OrgText.Format("Chart_Subtitle", OrgFormat.Number(Chart.Units.Count(u => u.IsActive)), OrgFormat.Number(Chart.Positions.Count(p => p.IsActive)))
            : string.Join(" › ", Chart.GetPath(Top.Key).Select(u => u.Title));

        Holders = await reader.GetHoldersAsync(Chart.Positions.Where(p => p.IsActive).Select(p => p.Key).ToList(), UtcNow, cancellationToken);
        HashSet<string> userIds = [.. Holders.Values.SelectMany(h => h).Select(h => h.UserId)];
        Users = userIds.Count == 0 ? Users : await users.GetUsersAsync(userIds, cancellationToken);
        return Page();
    }

    public bool Shown(UnitNode unit) => Inactive || unit.IsActive;

    public IReadOnlyList<UnitNode> ChildrenOf(UnitNode unit) => Chart.GetChildren(unit.Key).Where(Shown).ToList();

    public IReadOnlyList<PositionNode> PositionsOf(UnitNode unit) =>
        Chart.GetPositionOutline(unit.Key).Select(p => p.Position).Where(p => Inactive || p.IsActive).ToList();

    public IReadOnlyList<AssignmentInfo> HoldersOf(string? positionKey) =>
        positionKey is not null && Holders.TryGetValue(positionKey, out IReadOnlyList<AssignmentInfo>? holders) ? holders : [];

    public string UserName(string userId) => Users.TryGetValue(userId, out UserInfo? user) ? user.DisplayName : userId;

    public string Names(string? positionKey) => string.Join("، ", HoldersOf(positionKey).Select(h => UserName(h.UserId)).Distinct());

    /// <summary>People holding the unit's positions now (each once).</summary>
    public int PeopleOf(UnitNode unit) =>
        Chart.GetPositions(unit.Key).SelectMany(p => HoldersOf(p.Key)).Select(h => h.UserId).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    /// <summary>Units in the branch below, at any depth (for the collapse button).</summary>
    public int DescendantsOf(UnitNode unit) => Chart.GetDescendantKeys(unit.Key).Count(k => Chart.FindUnit(k) is { } u && Shown(u));
}
