using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Positions;

[Authorize(Policy = OrgChartPolicies.Edit)]
public sealed class MoveModel(IOrgChartReader reader, IOrgChartAdministration admin) : OrgPageModel
{
    public const string FormPath = "/Areas/OrgChart/Pages/Positions/_MoveForm.cshtml";

    [BindProperty(SupportsGet = true)]
    public string? Key { get; set; }

    /// <summary>A position key (move under it), or <see cref="UnitPrefix"/> + unit key (top of a unit without a head).</summary>
    [BindProperty]
    public string? Target { get; set; }

    public const string UnitPrefix = "unit:";

    public OrgChartSnapshot Chart { get; private set; } = OrgChartSnapshot.Empty;
    public PositionNode Position { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        // Current place: explicit parent, else the unit's head, else the top of the unit.
        Target = Position.ParentKey
            ?? (Chart.FindUnit(Position.UnitKey)?.ManagerPositionKey is { } head && !Core.Model.OrgKey.AreEqual(head, Position.Key) ? head : null)
            ?? UnitPrefix + Position.UnitKey;
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
            string target = Target ?? "";
            string? unitKey;
            if (target.StartsWith(UnitPrefix, StringComparison.Ordinal))
            {
                unitKey = target[UnitPrefix.Length..];
                await admin.MovePositionAsync(Position.Key, unitKey, cancellationToken);
            }
            else
            {
                unitKey = Chart.FindPosition(target)?.UnitKey;
                await admin.MovePositionUnderAsync(Position.Key, target, cancellationToken);
            }

            return Done(UnitUrl(unitKey, IndexModel.TabPositions) + "#pos-" + Position.Key, OrgText.Format("Flash_PositionMoved", Position.Title));
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
        Position = Chart.FindPosition(Key)!;
        return Position is not null;
    }
}
