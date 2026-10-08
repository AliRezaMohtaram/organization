using System.Reflection;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Options;

using OrgChart.Core;
using OrgChart.Razor.Admin;

namespace Microsoft.Extensions.DependencyInjection;

public static class OrgChartUiExtensions
{
    /// <summary>
    /// Adds the admin pages at <c>/OrgChart</c> (Razor Pages area "OrgChart"; the host must call
    /// <c>MapRazorPages()</c>). Pages use the policies <see cref="OrgChartPolicies.View"/> and
    /// <see cref="OrgChartPolicies.Edit"/>, configured through <see cref="OrgChartUiOptions"/>.
    /// </summary>
    public static OrgChartBuilder AddAdminUi(this OrgChartBuilder builder, Action<OrgChartUiOptions>? configure = null)
    {
        Assembly assembly = typeof(OrgChartUiExtensions).Assembly;

        builder.Services.AddOptions<OrgChartUiOptions>();
        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        builder.Services.AddAuthorization();
        builder.Services.AddOptions<AuthorizationOptions>()
            .Configure<IOptions<OrgChartUiOptions>>((authorization, ui) =>
            {
                authorization.AddPolicy(OrgChartPolicies.View, ui.Value.ViewPolicy);
                authorization.AddPolicy(OrgChartPolicies.Edit, ui.Value.EditPolicy);
            });

        // Hosts usually discover this library on their own; add it only when they did not, to avoid duplicate routes.
        builder.Services.AddRazorPages().ConfigureApplicationPartManager(manager =>
        {
            if (!manager.ApplicationParts.OfType<AssemblyPart>().Any(p => p.Assembly == assembly))
            {
                foreach (ApplicationPart part in ApplicationPartFactory.GetApplicationPartFactory(assembly).GetApplicationParts(assembly))
                {
                    manager.ApplicationParts.Add(part);
                }
            }
        });

        return builder;
    }
}
