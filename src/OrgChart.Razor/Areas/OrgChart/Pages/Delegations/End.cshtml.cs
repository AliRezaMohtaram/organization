using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Delegations;

/// <summary>Ends (?mode=end) or deletes (?mode=remove) a delegation or deputy.</summary>
[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class EndModel(IOrgChartReader reader, IOrgChartAdministration admin, IUserDirectory users) : OrgPageModel
{
    public const string FormPath = "/Areas/OrgChart/Pages/Delegations/_EndForm.cshtml";
    public const string ModeRemove = "remove";

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Mode { get; set; }

    [BindProperty] public string When { get; set; } = "now";
    [BindProperty] public string? LastDay { get; set; }

    public DelegationInfo Delegation { get; private set; } = null!;
    public OrgChartSnapshot Chart { get; private set; } = OrgChartSnapshot.Empty;
    public string Summary { get; private set; } = "";
    public bool IsRemove => Mode == ModeRemove;
    public string BackUrl => UnitUrl(Chart.FindPosition(Delegation.PositionKey)?.UnitKey, IndexModel.TabDelegations);

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
            if (IsRemove)
            {
                await admin.RemoveDelegationAsync(Delegation.Id, cancellationToken);
            }
            else
            {
                DateTime? end = UtcNow;
                if (When == "date" && (string.IsNullOrWhiteSpace(LastDay) || !Dates.TryParse(LastDay, inclusiveEnd: true, out end) || end is null))
                {
                    AddError("Error_InvalidDate");
                    return Form(FormPath);
                }

                await admin.EndDelegationAsync(Delegation.Id, end!.Value, cancellationToken);
            }

            return Done(BackUrl, OrgText.Get(IsRemove ? "Flash_DelegationRemoved" : "Flash_DelegationEnded"));
        }
        catch (OrgChartAdminException ex)
        {
            AddError(ex);
            return Form(FormPath);
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        DelegationInfo? delegation = await reader.GetDelegationAsync(Id, cancellationToken);
        if (delegation is null)
        {
            return false;
        }

        Delegation = delegation;
        Chart = await reader.GetSnapshotAsync(cancellationToken);
        string title = Chart.FindPosition(delegation.PositionKey)?.Title ?? delegation.PositionKey;
        if (delegation.Kind == DelegationKind.ToUser)
        {
            IReadOnlyDictionary<string, UserInfo> names = await users.GetUsersAsync([delegation.FromUserId!, delegation.ToUserId!], cancellationToken);
            string Name(string id) => names.TryGetValue(id, out UserInfo? u) ? u.DisplayName : id;
            Summary = OrgText.Format("Delegation_Summary", title, Name(delegation.FromUserId!), Name(delegation.ToUserId!));
        }
        else
        {
            Summary = OrgText.Format("Deputy_Summary", title, Chart.FindPosition(delegation.ToPositionKey)?.Title ?? delegation.ToPositionKey);
        }

        return true;
    }
}
