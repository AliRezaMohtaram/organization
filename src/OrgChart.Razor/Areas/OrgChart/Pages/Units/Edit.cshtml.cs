using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Units;

[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class EditModel(IOrgChartReader reader, IOrgChartAdministration admin) : OrgPageModel
{
    public const string FormPath = "/Areas/OrgChart/Pages/Units/_UnitForm.cshtml";

    /// <summary>The unit to edit; empty to create one.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Key { get; set; }

    /// <summary>Parent proposed for a new unit.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Parent { get; set; }

    [BindProperty]
    public UnitForm Input { get; set; } = new();

    public OrgChartSnapshot Chart { get; private set; } = OrgChartSnapshot.Empty;
    public UnitNode? Existing { get; private set; }
    public bool IsEdit => Existing is not null;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        if (Existing is { } unit)
        {
            Input = new UnitForm
            {
                Key = unit.Key,
                Title = unit.Title,
                TypeKey = unit.TypeKey,
                ParentKey = unit.ParentKey,
                Code = unit.Code,
                SortOrder = unit.SortOrder,
                ValidFrom = Dates.Input(unit.ValidFrom),
                ValidTo = Dates.Input(unit.ValidTo, isEnd: true),
            };
        }
        else
        {
            UnitNode? parent = Chart.FindUnit(Parent);
            IReadOnlyList<UnitNode> siblings = parent is null ? Chart.Roots : Chart.GetChildren(parent.Key);
            Input.ParentKey = parent?.Key;
            Input.TypeKey = SuggestType(parent, siblings);
            Input.SortOrder = siblings.Count == 0 ? 0 : siblings.Max(u => u.SortOrder) + 1;
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
            string key;
            if (Existing is { } unit)
            {
                key = unit.Key;
                await admin.UpdateUnitAsync(key, new UnitUpdate(Input.Title ?? "", Input.TypeKey ?? "", Input.Code, Input.SortOrder, from, to), cancellationToken);
            }
            else
            {
                key = Input.Key?.Trim() ?? "";
                await admin.CreateUnitAsync(new UnitInput(key, Input.Title ?? "", Input.TypeKey ?? "", NullIfEmpty(Input.ParentKey), Input.Code, Input.SortOrder, from, to), cancellationToken);
            }

            return Done(UnitUrl(key), OrgText.Format(IsEdit ? "Flash_UnitSaved" : "Flash_UnitCreated", Input.Title?.Trim()));
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
        if (string.IsNullOrEmpty(Key))
        {
            return true;
        }

        Existing = Chart.FindUnit(Key);
        return Existing is not null;
    }

    /// <summary>
    /// A sensible type for a new unit: the most common type among its future siblings, else the active type that
    /// follows the parent's type, else the first active type.
    /// </summary>
    private string? SuggestType(UnitNode? parent, IReadOnlyList<UnitNode> siblings)
    {
        List<OrgTypeNode> active = Chart.UnitTypes.Where(t => t.IsActive).ToList();
        string? common = siblings
            .Where(u => active.Any(t => Core.Model.OrgKey.AreEqual(t.Key, u.TypeKey)))
            .GroupBy(u => u.TypeKey, Core.Model.OrgKey.Comparer)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
        if (common is not null)
        {
            return common;
        }

        int index = parent is null ? -1 : active.FindIndex(t => Core.Model.OrgKey.AreEqual(t.Key, parent.TypeKey));
        return index >= 0 && index + 1 < active.Count ? active[index + 1].Key : active.FirstOrDefault()?.Key;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed class UnitForm
{
    public string? Key { get; set; }
    public string? Title { get; set; }
    public string? TypeKey { get; set; }
    public string? ParentKey { get; set; }
    public string? Code { get; set; }
    public int SortOrder { get; set; }
    public string? ValidFrom { get; set; }
    public string? ValidTo { get; set; }
}
