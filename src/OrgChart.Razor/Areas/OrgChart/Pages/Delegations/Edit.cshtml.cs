using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Delegations;

/// <summary>Admin form for a delegation to a person or a deputy position: create (?position=&amp;kind=) or edit (?id=).</summary>
[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class EditModel(IOrgChartReader reader, IOrgChartAdministration admin, IUserDirectory users, IAuthorityCatalog authorities) : OrgPageModel
{
    public const string FormPath = "/Areas/OrgChart/Pages/Delegations/_DelegationForm.cshtml";

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Position { get; set; }

    [BindProperty(SupportsGet = true)]
    public DelegationKind Kind { get; set; } = DelegationKind.ToUser;

    [BindProperty] public string? FromUserId { get; set; }
    [BindProperty] public string? ToUserId { get; set; }
    [BindProperty] public string? DeputyPositionKey { get; set; }
    [BindProperty] public int Priority { get; set; } = 1;
    [BindProperty] public string? ValidFrom { get; set; }
    [BindProperty] public string? ValidTo { get; set; }
    [BindProperty] public string? Note { get; set; }
    [BindProperty] public bool FullScope { get; set; } = true;
    [BindProperty] public List<string> AuthorityKeys { get; set; } = [];

    public OrgChartSnapshot Chart { get; private set; } = OrgChartSnapshot.Empty;
    public PositionNode Subject { get; private set; } = null!;
    public DelegationInfo? Existing { get; private set; }
    public IReadOnlyList<AuthorityInfo> Offered { get; private set; } = [];

    /// <summary>Current and future holders of the position, for "from" (delegations to a person).</summary>
    public IReadOnlyList<(string UserId, string Name)> Holders { get; private set; } = [];

    public IReadOnlyDictionary<string, UserInfo> Names { get; private set; } = new Dictionary<string, UserInfo>();
    public bool IsEdit => Existing is not null;
    public bool HasDirectory => users is not NullUserDirectory;
    public ScopeFields Scope => new(Offered, FullScope, AuthorityKeys);
    public string BackUrl => UnitUrl(Subject.UnitKey, IndexModel.TabDelegations);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        if (Existing is { } d)
        {
            Priority = d.Priority;
            ValidFrom = Dates.Input(d.ValidFrom);
            ValidTo = Dates.Input(d.ValidTo, isEnd: true);
            Note = d.Note;
            FullScope = d.AuthorityKeys is null;
            AuthorityKeys = d.AuthorityKeys?.ToList() ?? [];
        }
        else
        {
            FromUserId = Holders.FirstOrDefault().UserId;
            ValidFrom = Dates.Input(Dates.StartOfDay(UtcNow));
        }

        return Form(FormPath);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        if (!TryReadPeriod(ValidFrom, ValidTo, out DateTime? from, out DateTime? to))
        {
            return Form(FormPath);
        }

        IReadOnlyCollection<string>? scope = ReadScope(FullScope, AuthorityKeys);
        try
        {
            if (Existing is { } d)
            {
                await admin.UpdateDelegationAsync(d.Id, new DelegationUpdate(from, to, scope, Note, Priority), cancellationToken);
            }
            else if (Kind == DelegationKind.ToUser)
            {
                if (to is null)
                {
                    throw new OrgChartAdminException(OrgChartErrors.EndDateRequired, "End date required.");
                }

                await admin.DelegateAsync(new DelegationInput(Subject.Key, FromUserId ?? "", ToUserId ?? "", from, to.Value, scope, Note), cancellationToken);
            }
            else
            {
                await admin.AddDeputyAsync(new DeputyInput(Subject.Key, DeputyPositionKey ?? "", Priority, from, to, scope, Note), cancellationToken);
            }

            return Done(BackUrl, OrgText.Format(Kind == DelegationKind.ToUser ? "Flash_DelegationSaved" : "Flash_DeputySaved", Subject.Title));
        }
        catch (OrgChartAdminException ex)
        {
            AddError(ex);
            return Form(FormPath);
        }
    }

    public Task<IActionResult> OnGetUsersAsync(string? q, CancellationToken cancellationToken) => SearchUsersAsync(q, cancellationToken);

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Chart = await reader.GetSnapshotAsync(cancellationToken);
        if (Id is { } id)
        {
            Existing = await reader.GetDelegationAsync(id, cancellationToken);
            if (Existing is null)
            {
                return false;
            }

            Kind = Existing.Kind;
            Position = Existing.PositionKey;
            FromUserId = Existing.FromUserId;
            ToUserId = Existing.ToUserId;
            DeputyPositionKey = Existing.ToPositionKey;
        }

        if (Chart.FindPosition(Position) is not { } subject || !Enum.IsDefined(Kind))
        {
            return false;
        }

        Subject = subject;
        Offered = await authorities.GetAuthoritiesAsync(subject.Key, cancellationToken);

        DateTime now = UtcNow;
        List<string> holderIds = (await reader.GetUnitAssignmentsAsync(subject.UnitKey, cancellationToken: cancellationToken))
            .Where(a => OrgKey.AreEqual(a.PositionKey, subject.Key) && (a.ValidTo is null || a.ValidTo > now))
            .Select(a => a.UserId)
            .Distinct()
            .ToList();
        HashSet<string> ids = [.. holderIds];
        if (FromUserId is not null) ids.Add(FromUserId);
        if (ToUserId is not null) ids.Add(ToUserId);
        Names = ids.Count == 0 ? Names : await users.GetUsersAsync(ids, cancellationToken);
        Holders = holderIds.Select(u => (u, NameOf(u))).ToList();
        return true;
    }

    public string NameOf(string? userId) =>
        userId is null ? "" : Names.TryGetValue(userId, out UserInfo? user) ? user.DisplayName : userId;
}
