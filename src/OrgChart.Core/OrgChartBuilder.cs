using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OrgChart.Core.Abstractions;

namespace OrgChart.Core;

/// <summary>Returned by <c>AddOrgChart</c>; stores and host integrations plug in through it.</summary>
public sealed class OrgChartBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    /// <summary>Adds a listener told about every committed change. Several may be added.</summary>
    public OrgChartBuilder AddChangeListener<TListener>()
        where TListener : class, IOrgChartChangeListener
    {
        Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrgChartChangeListener, TListener>());
        return this;
    }

    /// <summary>Lets the admin UI search users and show names (default: none, raw user ids).</summary>
    public OrgChartBuilder AddUserDirectory<TDirectory>()
        where TDirectory : class, IUserDirectory
    {
        Services.Replace(ServiceDescriptor.Scoped<IUserDirectory, TDirectory>());
        return this;
    }

    /// <summary>What can be delegated from a position (default: nothing, so only full delegations).</summary>
    public OrgChartBuilder AddAuthorityCatalog<TCatalog>()
        where TCatalog : class, IAuthorityCatalog
    {
        Services.Replace(ServiceDescriptor.Scoped<IAuthorityCatalog, TCatalog>());
        return this;
    }

    /// <summary>Who makes changes, for the audit log (default: nobody).</summary>
    public OrgChartBuilder AddCurrentUser<TCurrentUser>()
        where TCurrentUser : class, ICurrentUser
    {
        Services.Replace(ServiceDescriptor.Scoped<ICurrentUser, TCurrentUser>());
        return this;
    }
}
