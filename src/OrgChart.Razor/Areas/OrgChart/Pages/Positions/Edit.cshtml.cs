using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Positions;

[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class EditModel(IOrgChartReader reader, IOrgChartAdministration admin) : OrgPageModel
{
    public const string FormPath = "/Areas/OrgChart/Pages/Positions/_PositionForm.cshtml";

    /// <summary>The position to edit; empty to create one.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Key { get; set; }

    /// <summary>Unit of a new position.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Unit { get; set; }

    [BindProperty]
    public PositionForm Input { get; set; } = new();

    public OrgChartSnapshot Chart { get; private set; } = OrgChartSnapshot.Empty;
    public PositionNode? Existing { get; private set; }
    public UnitNode? OwnerUnit { get; private set; }
    public bool IsEdit => Existing is not null;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        if (Existing is { } position)
        {
            Input = new PositionForm
            {
                Key = position.Key,
                Title = position.Title,
                TypeKey = position.TypeKey,
                IsManagerial = position.IsManagerial,
                Code = position.Code,
                SortOrder = position.SortOrder,
                ValidFrom = Dates.Input(position.ValidFrom),
                ValidTo = Dates.Input(position.ValidTo, isEnd: true),
            };
        }
        else
        {
            IReadOnlyList<PositionNode> siblings = Chart.GetPositions(OwnerUnit!.Key);
            Input.SortOrder = siblings.Count == 0 ? 0 : siblings.Max(p => p.SortOrder) + 1;
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

        string? type = string.IsNullOrWhiteSpace(Input.TypeKey) ? null : Input.TypeKey;
        try
        {
            if (Existing is { } position)
            {
                await admin.UpdatePositionAsync(position.Key,
                    new PositionUpdate(Input.Title ?? "", type, Input.IsManagerial, Input.Code, Input.SortOrder, from, to), cancellationToken);
            }
            else
            {
                await admin.CreatePositionAsync(new PositionInput(
                    Input.Key?.Trim() ?? "", Input.Title ?? "", OwnerUnit!.Key, type, Input.IsManagerial, Input.Code, Input.SortOrder, from, to), cancellationToken);
            }

            return Done(UnitUrl(OwnerUnit!.Key, IndexModel.TabPositions),
                OrgText.Format(IsEdit ? "Flash_PositionSaved" : "Flash_PositionCreated", Input.Title?.Trim()));
        }
        catch (OrgChartAdminException ex)
        {
            AddError(ex);
            return Form(FormPath);
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Chart = await reader.GetSnapshotAsync(cancellationToken);
        if (!string.IsNullOrEmpty(Key))
        {
            Existing = Chart.FindPosition(Key);
            OwnerUnit = Chart.FindUnit(Existing?.UnitKey);
        }
        else
        {
            OwnerUnit = Chart.FindUnit(Unit);
        }

        return OwnerUnit is not null;
    }
}

public sealed class PositionForm
{
    public string? Key { get; set; }
    public string? Title { get; set; }
    public string? TypeKey { get; set; }
    public bool IsManagerial { get; set; }
    public string? Code { get; set; }
    public int SortOrder { get; set; }
    public string? ValidFrom { get; set; }
    public string? ValidTo { get; set; }
}
