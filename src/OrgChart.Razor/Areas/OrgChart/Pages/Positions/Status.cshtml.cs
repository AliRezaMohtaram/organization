using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Positions;

[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class StatusModel(IOrgChartReader reader, IOrgChartAdministration admin) : OrgPageModel
{
    public const string FormPath = "/Areas/OrgChart/Pages/Positions/_StatusForm.cshtml";

    [BindProperty(SupportsGet = true)]
    public string? Key { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Active { get; set; }

    public PositionNode Position { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken) ? Form(FormPath) : NotFound();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        try
        {
            await admin.SetPositionActiveAsync(Position.Key, Active, cancellationToken);
            return Done(UnitUrl(Position.UnitKey, IndexModel.TabPositions),
                OrgText.Format(Active ? "Flash_PositionActivated" : "Flash_PositionDeactivated", Position.Title));
        }
        catch (OrgChartAdminException ex)
        {
            AddError(ex);
            return Form(FormPath);
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Position = (await reader.GetSnapshotAsync(cancellationToken)).FindPosition(Key)!;
        return Position is not null;
    }
}
