using Microsoft.AspNetCore.Authorization;

namespace OrgChart.Razor.Admin;

public sealed class OrgChartUiOptions
{
    /// <summary>Layout of the module's own shell (sidebar, topbar, its copy of MX).</summary>
    public const string StandaloneLayout = "/Areas/OrgChart/Pages/Shared/_OrgStandaloneLayout.cshtml";

    /// <summary>
    /// Layout the pages render in. Default: the host's "_Layout", which must load the MX design system
    /// (mx.css, mx.js). Use <see cref="StandaloneLayout"/> (or <c>UseStandaloneLayout()</c>) for hosts without MX.
    /// </summary>
    public string Layout { get; set; } = "_Layout";

    /// <summary>Who may open the chart pages. Default: any signed-in user.</summary>
    public Action<AuthorizationPolicyBuilder> ViewPolicy { get; set; } = policy => policy.RequireAuthenticatedUser();

    /// <summary>Who may change the chart. Default: any signed-in user — narrow this in production.</summary>
    public Action<AuthorizationPolicyBuilder> EditPolicy { get; set; } = policy => policy.RequireAuthenticatedUser();

    /// <summary>Who may open "my delegations" (delegate their own positions). Default: any signed-in user.</summary>
    public Action<AuthorizationPolicyBuilder> SelfPolicy { get; set; } = policy => policy.RequireAuthenticatedUser();

    /// <summary>"Back to the application" link in the standalone layout; null hides it.</summary>
    public string? BackUrl { get; set; } = "~/";

    /// <summary>Time zone for showing and entering dates (Jalali, whole days). Default: the server's.</summary>
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Local;

    public OrgChartUiOptions UseStandaloneLayout()
    {
        Layout = StandaloneLayout;
        return this;
    }
}
