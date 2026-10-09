using System.Security.Claims;

namespace OrgChart.AspNetCore;

public sealed class OrgChartHttpOptions
{
    /// <summary>Claim holding the user id OrgChart stores (default: NameIdentifier, as ASP.NET Core Identity sets it).</summary>
    public string UserIdClaimType { get; set; } = ClaimTypes.NameIdentifier;
}
