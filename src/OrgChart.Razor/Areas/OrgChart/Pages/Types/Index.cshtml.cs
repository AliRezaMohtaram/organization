using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Chart;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Types;

[Authorize(Policy = OrgChartPolicies.View)]
public sealed class IndexModel(IOrgChartReader reader) : OrgPageModel
{
    public OrgChartSnapshot Chart { get; private set; } = OrgChartSnapshot.Empty;
    public bool CanEdit { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Chart = await reader.GetSnapshotAsync(cancellationToken);
        CanEdit = await CanEditAsync();
        ViewData["Title"] = OrgText.Get("Nav_Types");
        ViewData["Subtitle"] = OrgText.Get("Types_Subtitle");
        return Page();
    }

    public int UnitsUsing(string typeKey) => Chart.Units.Count(u => OrgChart.Core.Model.OrgKey.AreEqual(u.TypeKey, typeKey));

    public int PositionsUsing(string typeKey) => Chart.Positions.Count(p => OrgChart.Core.Model.OrgKey.AreEqual(p.TypeKey, typeKey));
}
