using Acl.Core;
using Acl.Core.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OrgChart.Core;

namespace OrgChart.Acl;

public static class OrgChartAclExtensions
{
    /// <summary>
    /// Connects the chart to Acl: Acl reads positions from the chart (<see cref="OrgChartOrgStructure"/>), chart changes
    /// invalidate Acl's cache (<see cref="AclStampChangeListener"/>) and delegations offer the position's Acl roles
    /// (<see cref="AclAuthorityCatalog"/>). Needs <c>AddAccessControl(...)</c> with a store; order does not matter.
    /// </summary>
    public static OrgChartBuilder AddAcl(this OrgChartBuilder builder)
    {
        builder.AddChangeListener<AclStampChangeListener>();
        builder.AddAuthorityCatalog<AclAuthorityCatalog>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IOrgStructure, OrgChartOrgStructure>());
        return builder;
    }

    /// <summary>The Acl side of <see cref="AddAcl"/>, for hosts that configure Acl separately: positions from the chart.</summary>
    public static AclBuilder AddOrgChart(this AclBuilder builder) => builder.AddOrgStructure<OrgChartOrgStructure>();
}
