using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using OrgChart.Core.Abstractions;

namespace OrgChart.AspNetCore;

/// <summary>The authenticated user of the current request, by <see cref="OrgChartHttpOptions.UserIdClaimType"/>.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor, IOptions<OrgChartHttpOptions> options) : ICurrentUser
{
    public string? UserId =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
            ? user.FindFirst(options.Value.UserIdClaimType)?.Value
            : null;
}
