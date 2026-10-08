using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Positions;

[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class MoveModel(IOrgChartReader reader, IOrgChartAdministration admin) : OrgPageModel
{
    public const string FormPath = "/Areas/OrgChart/Pages/Positions/_MoveForm.cshtml";

    [BindProperty(SupportsGet = true)]
    public string? Key { get; set; }

    [BindProperty]
    public string? UnitKey { get; set; }

    public OrgChartSnapshot Chart { get; private set; } = OrgChartSnapshot.Empty;
    public PositionNode Position { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        UnitKey = Position.UnitKey;
        return Form(FormPath);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        try
        {
            await admin.MovePositionAsync(Position.Key, UnitKey ?? "", cancellationToken);
            return Done(UnitUrl(UnitKey, IndexModel.TabPositions), OrgText.Format("Flash_PositionMoved", Position.Title));
        }
        catch (OrgChartAdminException ex)
        {
            AddError(ex);
            return Form(FormPath);
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Chart = await reader.GetSnapshotAsync(cancellationToken);
        Position = Chart.FindPosition(Key)!;
        return Position is not null;
    }
}
