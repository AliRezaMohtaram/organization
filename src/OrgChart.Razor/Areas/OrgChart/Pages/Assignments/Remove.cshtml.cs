using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Assignments;

[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class RemoveModel(IOrgChartReader reader, IUserDirectory users, IOrgChartAdministration admin)
    : AssignmentActionModel(reader, users)
{
    public const string FormPath = "/Areas/OrgChart/Pages/Assignments/_RemoveForm.cshtml";

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
            await admin.RemoveAssignmentAsync(Assignment.Id, cancellationToken);
            return Done(BackUrl, OrgText.Format("Flash_AssignmentRemoved", PersonName));
        }
        catch (OrgChartAdminException ex)
        {
            AddError(ex);
            return Form(FormPath);
        }
    }
}
