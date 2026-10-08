using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Assignments;

[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class EndModel(IOrgChartReader reader, IUserDirectory users, IOrgChartAdministration admin)
    : AssignmentActionModel(reader, users)
{
    public const string FormPath = "/Areas/OrgChart/Pages/Assignments/_EndForm.cshtml";

    /// <summary>Last day in the position (inclusive), Jalali.</summary>
    [BindProperty]
    public string? LastDay { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        LastDay = Dates.Input(Dates.StartOfDay(UtcNow));
        return Form(FormPath);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(LastDay) || !Dates.TryParse(LastDay, inclusiveEnd: true, out DateTime? end) || end is null)
        {
            AddError("Error_InvalidDate");
            return Form(FormPath);
        }

        try
        {
            await admin.EndAssignmentAsync(Assignment.Id, end.Value, cancellationToken);
            return Done(BackUrl, OrgText.Format("Flash_AssignmentEnded", PersonName));
        }
        catch (OrgChartAdminException ex)
        {
            AddError(ex);
            return Form(FormPath);
        }
    }
}
