using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Types;

[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class EditModel(IOrgChartReader reader, IOrgChartAdministration admin) : OrgPageModel
{
    public const string FormPath = "/Areas/OrgChart/Pages/Types/_TypeForm.cshtml";

    [BindProperty(SupportsGet = true)]
    public OrgTypeKind Kind { get; set; } = OrgTypeKind.Unit;

    /// <summary>The type to edit; empty to create one.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Key { get; set; }

    [BindProperty]
    public string? Title { get; set; }

    [BindProperty]
    public int SortOrder { get; set; }

    /// <summary>Unit types: place in the hierarchy (1 = top); empty = no rule.</summary>
    [BindProperty]
    public int? Level { get; set; }

    [BindProperty]
    public bool CanBeRoot { get; set; } = true;

    public OrgTypeNode? Existing { get; private set; }
    public bool IsEdit => Existing is not null;
    public string BackUrl => Url.Page("/Types/Index", new { area = "OrgChart" }) ?? "/OrgChart/Types";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        if (Existing is { } type)
        {
            Title = type.Title;
            SortOrder = type.SortOrder;
            Level = type.Level;
            CanBeRoot = type.CanBeRoot;
        }

        return Form(FormPath);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        try
        {
            if (Existing is { } type)
            {
                await admin.UpdateTypeAsync(Kind, type.Key, new OrgTypeUpdate(Title ?? "", SortOrder, Level, CanBeRoot), cancellationToken);
            }
            else
            {
                await admin.CreateTypeAsync(Kind, new OrgTypeInput(null, Title ?? "", SortOrder, Level, CanBeRoot), cancellationToken);
            }

            return Done(BackUrl, OrgText.Format("Flash_TypeSaved", Title?.Trim()));
        }
        catch (OrgChartAdminException ex)
        {
            AddError(ex);
            return Form(FormPath);
        }
    }

    /// <summary>Activates or deactivates a type straight from the list.</summary>
    public async Task<IActionResult> OnPostStatusAsync(bool active, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken) || Existing is null)
        {
            return NotFound();
        }

        try
        {
            await admin.SetTypeActiveAsync(Kind, Existing.Key, active, cancellationToken);
            TempData[FlashKey] = OrgText.Format(active ? "Flash_TypeActivated" : "Flash_TypeDeactivated", Existing.Title);
        }
        catch (OrgChartAdminException ex)
        {
            TempData[FlashKey] = OrgText.Error(ex.Code);
        }

        return Redirect(BackUrl);
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(Kind))
        {
            return false;
        }

        if (string.IsNullOrEmpty(Key))
        {
            return true;
        }

        OrgChartSnapshot chart = await reader.GetSnapshotAsync(cancellationToken);
        Existing = Kind == OrgTypeKind.Unit ? chart.FindUnitType(Key) : chart.FindPositionType(Key);
        return Existing is not null;
    }
}
