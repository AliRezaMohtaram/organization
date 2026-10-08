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

    /// <summary>"now" ends the assignment at once (the position becomes vacant); "date" ends it after <see cref="LastDay"/>.</summary>
    [BindProperty]
    public string When { get; set; } = WhenNow;

    /// <summary>Last day in the position (inclusive), Jalali; used when <see cref="When"/> is "date".</summary>
    [BindProperty]
    public string? LastDay { get; set; }

    public const string WhenNow = "now";
    public const string WhenDate = "date";

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

        DateTime? end = UtcNow;
        if (When == WhenDate
            && (string.IsNullOrWhiteSpace(LastDay) || !Dates.TryParse(LastDay, inclusiveEnd: true, out end) || end is null))
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
