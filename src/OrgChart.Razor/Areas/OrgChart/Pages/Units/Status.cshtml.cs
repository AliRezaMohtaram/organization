using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Units;

[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class StatusModel(IOrgChartReader reader, IOrgChartAdministration admin) : OrgPageModel
{
    public const string FormPath = "/Areas/OrgChart/Pages/Units/_StatusForm.cshtml";

    [BindProperty(SupportsGet = true)]
    public string? Key { get; set; }

    /// <summary>The state to set.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Active { get; set; }

    public UnitNode Unit { get; private set; } = null!;

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
            await admin.SetUnitActiveAsync(Unit.Key, Active, cancellationToken);
            return Done(UnitUrl(Unit.Key), OrgText.Format(Active ? "Flash_UnitActivated" : "Flash_UnitDeactivated", Unit.Title));
        }
        catch (OrgChartAdminException ex)
        {
            AddError(ex);
            return Form(FormPath);
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Unit = (await reader.GetSnapshotAsync(cancellationToken)).FindUnit(Key)!;
        return Unit is not null;
    }
}
