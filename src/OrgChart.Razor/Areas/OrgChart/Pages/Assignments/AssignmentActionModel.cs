using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Chart;

namespace OrgChart.Razor.Admin.Pages.Assignments;

/// <summary>Loads one assignment with its position, unit and person for the End/Transfer/Remove forms.</summary>
public abstract class AssignmentActionModel(IOrgChartReader reader, IUserDirectory users) : OrgPageModel
{
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public OrgChartSnapshot Chart { get; private set; } = OrgChartSnapshot.Empty;
    public AssignmentInfo Assignment { get; private set; } = null!;
    public string PersonName { get; private set; } = "";
    public string PositionTitle => Chart.FindPosition(Assignment.PositionKey)?.Title ?? Assignment.PositionKey;
    public string UnitTitle => Chart.FindUnit(Assignment.OrgUnitKey)?.Title ?? Assignment.OrgUnitKey;
    public string BackUrl => UnitUrl(Assignment.OrgUnitKey, IndexModel.TabPeople);

    protected IOrgChartReader Reader => reader;

    protected async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        AssignmentInfo? assignment = await reader.GetAssignmentAsync(Id, cancellationToken);
        if (assignment is null)
        {
            return false;
        }

        Assignment = assignment;
        Chart = await reader.GetSnapshotAsync(cancellationToken);
        IReadOnlyDictionary<string, UserInfo> found = await users.GetUsersAsync([assignment.UserId], cancellationToken);
        PersonName = found.TryGetValue(assignment.UserId, out UserInfo? user) ? user.DisplayName : assignment.UserId;
        return true;
    }
}
