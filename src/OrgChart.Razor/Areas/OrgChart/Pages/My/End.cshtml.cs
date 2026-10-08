using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.My;

/// <summary>The delegator ends their own delegation (now).</summary>
[Authorize(Policy = OrgChartPolicies.Self)]
public sealed class EndModel(IOrgChartReader reader, IOrgChartAdministration admin) : OrgPageModel
{
    public const string FormPath = "/Areas/OrgChart/Pages/My/_EndForm.cshtml";

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public DelegationInfo Delegation { get; private set; } = null!;
    public string PositionTitle { get; private set; } = "";
    public string BackUrl => Url.Page("/My/Index", new { area = "OrgChart" }) ?? "/OrgChart/My";

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
            DateTime now = UtcNow;
            if (Delegation.ValidFrom is { } from && from >= now)
            {
                // Not started yet: nothing happened under it, so it can go.
                await admin.RemoveDelegationAsync(Delegation.Id, cancellationToken);
            }
            else
            {
                await admin.EndDelegationAsync(Delegation.Id, now, cancellationToken);
            }

            return Done(BackUrl, OrgText.Get("Flash_DelegationEnded"));
        }
        catch (OrgChartAdminException ex)
        {
            AddError(ex);
            return Form(FormPath);
        }
    }

    /// <summary>Only the delegator's own delegations.</summary>
    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        DelegationInfo? delegation = await reader.GetDelegationAsync(Id, cancellationToken);
        if (delegation is null || CurrentUserId is null || delegation.FromUserId != CurrentUserId)
        {
            return false;
        }

        Delegation = delegation;
        PositionTitle = (await reader.GetSnapshotAsync(cancellationToken)).FindPosition(delegation.PositionKey)?.Title ?? delegation.PositionKey;
        return true;
    }
}
