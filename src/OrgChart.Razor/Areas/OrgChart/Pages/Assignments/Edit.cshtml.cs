using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Assignments;

[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class EditModel(IOrgChartReader reader, IOrgChartAdministration admin, IUserDirectory users) : OrgPageModel
{
    public const string FormPath = "/Areas/OrgChart/Pages/Assignments/_AssignmentForm.cshtml";
    public const int MaxSearchResults = 10;

    /// <summary>The assignment to edit; empty to create one.</summary>
    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    /// <summary>Fixed position of a new assignment.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Position { get; set; }

    /// <summary>Unit whose positions are offered for a new assignment.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Unit { get; set; }

    [BindProperty]
    public AssignmentForm Input { get; set; } = new();

    public OrgChartSnapshot Chart { get; private set; } = OrgChartSnapshot.Empty;
    public AssignmentInfo? Existing { get; private set; }
    public PositionNode? FixedPosition { get; private set; }
    public UnitNode? OwnerUnit { get; private set; }
    public UserInfo? Person { get; private set; }
    public bool IsEdit => Existing is not null;
    public bool HasDirectory => users is not NullUserDirectory;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        if (Existing is { } a)
        {
            Input = new AssignmentForm
            {
                UserId = a.UserId,
                Kind = a.Kind,
                ValidFrom = Dates.Input(a.ValidFrom),
                ValidTo = Dates.Input(a.ValidTo, isEnd: true),
                Note = a.Note,
            };
        }
        else
        {
            Input.PositionKey = FixedPosition?.Key;
            Input.ValidFrom = Dates.Input(Dates.StartOfDay(UtcNow));
        }

        return Form(FormPath);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        if (!TryReadPeriod(Input.ValidFrom, Input.ValidTo, out DateTime? from, out DateTime? to))
        {
            return Form(FormPath);
        }

        try
        {
            string positionKey;
            if (Existing is { } a)
            {
                positionKey = a.PositionKey;
                await admin.UpdateAssignmentAsync(a.Id, new AssignmentUpdate(Input.Kind, from, to, Input.Note), cancellationToken);
            }
            else
            {
                positionKey = FixedPosition?.Key ?? Input.PositionKey ?? "";
                await admin.AssignAsync(new AssignmentInput(positionKey, Input.UserId ?? "", Input.Kind, from, to, Input.Note), cancellationToken);
            }

            return Done(UnitUrl(Chart.FindPosition(positionKey)?.UnitKey, IndexModel.TabPeople),
                OrgText.Get(IsEdit ? "Flash_AssignmentSaved" : "Flash_AssignmentCreated"));
        }
        catch (OrgChartAdminException ex)
        {
            AddError(ex);
            await LoadUserAsync(Input.UserId, cancellationToken);
            return Form(FormPath);
        }
    }

    /// <summary>JSON user search for the form: <c>[{ id, name, detail }]</c>.</summary>
    public async Task<IActionResult> OnGetUsersAsync(string? q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return new JsonResult(Array.Empty<object>());
        }

        IReadOnlyList<UserInfo> found = await users.SearchAsync(q.Trim(), MaxSearchResults, cancellationToken);
        return new JsonResult(found.Select(u => new { id = u.UserId, name = u.DisplayName, detail = u.Detail }));
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Chart = await reader.GetSnapshotAsync(cancellationToken);
        if (Id is { } id)
        {
            Existing = await reader.GetAssignmentAsync(id, cancellationToken);
            if (Existing is null)
            {
                return false;
            }

            FixedPosition = Chart.FindPosition(Existing.PositionKey);
            OwnerUnit = Chart.FindUnit(Existing.OrgUnitKey);
            await LoadUserAsync(Existing.UserId, cancellationToken);
            return true;
        }

        if (!string.IsNullOrEmpty(Position))
        {
            FixedPosition = Chart.FindPosition(Position);
            OwnerUnit = Chart.FindUnit(FixedPosition?.UnitKey);
            return FixedPosition is not null;
        }

        OwnerUnit = Chart.FindUnit(Unit);
        return OwnerUnit is not null;
    }

    private async Task LoadUserAsync(string? userId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            IReadOnlyDictionary<string, UserInfo> found = await users.GetUsersAsync([userId.Trim()], cancellationToken);
            Person = found.Values.FirstOrDefault();
        }
    }
}

public sealed class AssignmentForm
{
    public string? PositionKey { get; set; }
    public string? UserId { get; set; }
    public AssignmentKind Kind { get; set; } = AssignmentKind.Primary;
    public string? ValidFrom { get; set; }
    public string? ValidTo { get; set; }
    public string? Note { get; set; }
}
