using Microsoft.Extensions.DependencyInjection.Extensions;

using OrgChart.Core;
using OrgChart.Core.Abstractions;

namespace Microsoft.Extensions.DependencyInjection;

public static class OrgChartServiceCollectionExtensions
{
    /// <summary>
    /// Entry point: <c>services.AddOrgChart().AddSqlServerStore(connectionString)</c>, then optionally
    /// <c>.AddCurrentUser&lt;T&gt;()</c>, <c>.AddUserDirectory&lt;T&gt;()</c>, <c>.AddChangeListener&lt;T&gt;()</c>.
    /// </summary>
    public static OrgChartBuilder AddOrgChart(this IServiceCollection services, Action<OrgChartOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<OrgChartOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ICurrentUser, NullCurrentUser>();
        services.TryAddScoped<IUserDirectory, NullUserDirectory>();
        services.TryAddScoped<IAuthorityCatalog, NullAuthorityCatalog>();
        return new OrgChartBuilder(services);
    }
}
