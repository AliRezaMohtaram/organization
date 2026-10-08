using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.My;

/// <summary>The signed-in user delegates a position they hold.</summary>
[Authorize(Policy = OrgChartPolicies.Self)]
public sealed class DelegateModel(IOrgChartReader reader, IOrgChartAdministration admin, IUserDirectory users, IAuthorityCatalog authorities) : OrgPageModel
{
    public const string FormPath = "/Areas/OrgChart/Pages/My/_DelegateForm.cshtml";

    [BindProperty(SupportsGet = true)] public string? Position { get; set; }
    [BindProperty] public string? ToUserId { get; set; }
    [BindProperty] public string? ValidFrom { get; set; }
    [BindProperty] public string? ValidTo { get; set; }
    [BindProperty] public string? Note { get; set; }
    [BindProperty] public bool FullScope { get; set; } = true;
    [BindProperty] public List<string> AuthorityKeys { get; set; } = [];

    public PositionNode Subject { get; private set; } = null!;
    public IReadOnlyList<AuthorityInfo> Offered { get; private set; } = [];
    public bool HasDirectory => users is not NullUserDirectory;
    public ScopeFields Scope => new(Offered, FullScope, AuthorityKeys);
    public string BackUrl => Url.Page("/My/Index", new { area = "OrgChart" }) ?? "/OrgChart/My";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (await LoadAsync(cancellationToken) is { } fail)
        {
            return fail;
        }

        ValidFrom = Dates.Input(Dates.StartOfDay(UtcNow));
        return Form(FormPath);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (await LoadAsync(cancellationToken) is { } fail)
        {
            return fail;
        }

        if (!TryReadPeriod(ValidFrom, ValidTo, out DateTime? from, out DateTime? to))
        {
            return Form(FormPath);
        }

        try
        {
            if (to is null)
            {
                throw new OrgChartAdminException(OrgChartErrors.EndDateRequired, "End date required.");
            }

            await admin.DelegateAsync(new DelegationInput(Subject.Key, CurrentUserId!, ToUserId ?? "", from, to.Value, ReadScope(FullScope, AuthorityKeys), Note), cancellationToken);
            return Done(BackUrl, OrgText.Format("Flash_DelegationSaved", Subject.Title));
        }
        catch (OrgChartAdminException ex)
        {
            AddError(ex);
            return Form(FormPath);
        }
    }

    public Task<IActionResult> OnGetUsersAsync(string? q, CancellationToken cancellationToken) => SearchUsersAsync(q, cancellationToken);

    /// <summary>Null when the user holds the position now or later; else the result to return.</summary>
    private async Task<IActionResult?> LoadAsync(CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } me)
        {
            return Forbid();
        }

        DateTime now = UtcNow;
        bool holds = (await reader.GetUserPositionsAsync(me, cancellationToken)).Any(p =>
            p.Kind is AssignmentKind.Primary or AssignmentKind.Acting
            && OrgKey.AreEqual(p.PositionKey, Position)
            && (p.ValidTo is null || p.ValidTo > now));
        if (!holds || (await reader.GetSnapshotAsync(cancellationToken)).FindPosition(Position) is not { } subject)
        {
            return NotFound();
        }

        Subject = subject;
        Offered = await authorities.GetAuthoritiesAsync(subject.Key, cancellationToken);
        return null;
    }
}
