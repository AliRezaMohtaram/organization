namespace OrgChart.Razor.Admin;

/// <summary>Authorization policies of the admin pages, built from <see cref="OrgChartUiOptions"/>.</summary>
public static class OrgChartPolicies
{
    public const string View = "OrgChart.View";
    public const string Edit = "OrgChart.Edit";
}
