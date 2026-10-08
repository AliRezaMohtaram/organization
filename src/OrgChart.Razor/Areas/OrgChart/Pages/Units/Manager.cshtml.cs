using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Units;

[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class ManagerModel(IOrgChartReader reader, IOrgChartAdministration admin) : OrgPageModel
{
    public const string FormPath = "/Areas/OrgChart/Pages/Units/_ManagerForm.cshtml";

    [BindProperty(SupportsGet = true)]
    public string? Key { get; set; }

    /// <summary>The unit's head position; empty clears it.</summary>
    [BindProperty]
    public string? PositionKey { get; set; }

    public OrgChartSnapshot Chart { get; private set; } = OrgChartSnapshot.Empty;
    public UnitNode Unit { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        PositionKey = Unit.ManagerPositionKey;
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
            await admin.SetUnitManagerAsync(Unit.Key, string.IsNullOrWhiteSpace(PositionKey) ? null : PositionKey, cancellationToken);
            return Done(UnitUrl(Unit.Key), OrgText.Format("Flash_ManagerSet", Unit.Title));
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
        Unit = Chart.FindUnit(Key)!;
        return Unit is not null;
    }
}
