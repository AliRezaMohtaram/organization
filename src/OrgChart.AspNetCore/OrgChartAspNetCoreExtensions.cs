using Microsoft.Extensions.DependencyInjection.Extensions;

using OrgChart.AspNetCore;
using OrgChart.Core;
using OrgChart.Core.Abstractions;

namespace Microsoft.Extensions.DependencyInjection;

public static class OrgChartAspNetCoreExtensions
{
    /// <summary>Records the signed-in user of each request as the author of chart changes.</summary>
    public static OrgChartBuilder AddHttpContextUser(this OrgChartBuilder builder, Action<OrgChartHttpOptions>? configure = null)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddOptions<OrgChartHttpOptions>();
        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        builder.Services.Replace(ServiceDescriptor.Scoped<ICurrentUser, HttpContextCurrentUser>());
        return builder;
    }
}
