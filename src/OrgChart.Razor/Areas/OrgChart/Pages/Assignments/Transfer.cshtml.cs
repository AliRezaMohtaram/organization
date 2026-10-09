using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Assignments;

[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class TransferModel(IOrgChartReader reader, IUserDirectory users, IOrgChartAdministration admin)
    : AssignmentActionModel(reader, users)
{
    public const string FormPath = "/Areas/OrgChart/Pages/Assignments/_TransferForm.cshtml";

    [BindProperty]
    public string? ToPositionKey { get; set; }

    /// <summary>First day in the new position, Jalali.</summary>
    [BindProperty]
    public string? FirstDay { get; set; }

    [BindProperty]
    public AssignmentKind Kind { get; set; } = AssignmentKind.Primary;

    [BindProperty]
    public string? Note { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        FirstDay = Dates.Input(Dates.StartOfDay(UtcNow));
        return Form(FormPath);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(FirstDay) || !Dates.TryParse(FirstDay, inclusiveEnd: false, out DateTime? at) || at is null)
        {
            AddError("Error_InvalidDate");
            return Form(FormPath);
        }

        try
        {
            await admin.TransferAsync(Assignment.Id, new TransferInput(ToPositionKey ?? "", at.Value, Kind, Note), cancellationToken);
            string? unit = Chart.FindPosition(ToPositionKey)?.UnitKey;
            return Done(UnitUrl(unit, IndexModel.TabPeople), OrgText.Format("Flash_AssignmentTransferred", PersonName));
        }
        catch (OrgChartAdminException ex)
        {
            AddError(ex);
            return Form(FormPath);
        }
    }
}
