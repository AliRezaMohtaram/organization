using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Chart;
using OrgChart.Razor.Admin;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages;

[Authorize(Policy = OrgChartPolicies.View)]
public sealed class IndexModel(IOrgChartReader reader, IUserDirectory users) : OrgPageModel
{
    public const string TabPositions = "positions";
    public const string TabPeople = "people";
    public const string TabUnits = "units";

    [BindProperty(SupportsGet = true, Name = "unit")]
    public string? UnitKey { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Tab { get; set; }

    /// <summary>Show ended assignments too.</summary>
    [BindProperty(SupportsGet = true)]
    public bool History { get; set; }

    public OrgChartSnapshot Chart { get; private set; } = OrgChartSnapshot.Empty;
    public UnitNode? Unit { get; private set; }
    public bool CanEdit { get; private set; }
    public DateTime Now { get; private set; }

    /// <summary>Assignments to the unit's positions (current and future; with <see cref="History"/> all).</summary>
    public IReadOnlyList<AssignmentInfo> Assignments { get; private set; } = [];

    /// <summary>Current holders per position key, for the unit's positions and its manager.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<AssignmentInfo>> Holders { get; private set; } =
        new Dictionary<string, IReadOnlyList<AssignmentInfo>>();

    public IReadOnlyDictionary<string, UserInfo> Users { get; private set; } = new Dictionary<string, UserInfo>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Now = UtcNow;
        CanEdit = await CanEditAsync();
        Chart = await reader.GetSnapshotAsync(cancellationToken);
        Unit = Chart.FindUnit(UnitKey) ?? Chart.Roots.FirstOrDefault(u => u.IsActive) ?? Chart.Roots.FirstOrDefault();
        Tab = Tab is TabPeople or TabUnits ? Tab : TabPositions;
        ViewData["Title"] = OrgText.Get("Nav_Chart");
        ViewData["Subtitle"] = OrgText.Format("Chart_Subtitle", OrgFormat.Number(Chart.Units.Count(u => u.IsActive)), OrgFormat.Number(Chart.Positions.Count(p => p.IsActive)));

        if (Unit is null)
        {
            return Page();
        }

        List<string> positionKeys = Chart.GetPositions(Unit.Key).Select(p => p.Key).ToList();
        if (Unit.ManagerPositionKey is { } manager)
        {
            positionKeys.Add(manager);
        }

        Holders = await reader.GetHoldersAsync(positionKeys, Now, cancellationToken);
        Assignments = (await reader.GetUnitAssignmentsAsync(Unit.Key, cancellationToken: cancellationToken))
            .Where(a => History || a.ValidTo is null || a.ValidTo > Now)
            .ToList();

        HashSet<string> userIds = [.. Assignments.Select(a => a.UserId), .. Holders.Values.SelectMany(h => h).Select(h => h.UserId)];
        Users = userIds.Count == 0 ? Users : await users.GetUsersAsync(userIds, cancellationToken);
        return Page();
    }

    public string UserName(string userId) => Users.TryGetValue(userId, out UserInfo? user) ? user.DisplayName : userId;

    public string? UserDetail(string userId) => Users.TryGetValue(userId, out UserInfo? user) ? user.Detail : null;

    public IReadOnlyList<AssignmentInfo> HoldersOf(string positionKey) =>
        Holders.TryGetValue(positionKey, out IReadOnlyList<AssignmentInfo>? holders) ? holders : [];

    /// <summary>"Current", "Upcoming" or "Ended" relative to now.</summary>
    public string State(DateTime? from, DateTime? to) =>
        to is not null && to <= Now ? "Ended" : from is not null && from > Now ? "Upcoming" : "Current";

    public string TypeTitle(string? typeKey, bool unit = true) =>
        (unit ? Chart.FindUnitType(typeKey) : Chart.FindPositionType(typeKey))?.Title ?? typeKey ?? "";

    /// <summary>Icon of a unit: a building for roots (companies), a folder otherwise.</summary>
    public static string UnitIcon(OrgChartSnapshot chart, UnitNode unit) =>
        chart.GetLevel(unit.Key) == 0 ? "oc-building" : "oc-unit";
}
