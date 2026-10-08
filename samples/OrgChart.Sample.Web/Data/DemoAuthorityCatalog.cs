using OrgChart.Core.Abstractions;

namespace OrgChart.Sample.Web.Data;

/// <summary>
/// Demo authorities offered for partial delegation. In a real host the OrgChart.Acl bridge lists the Acl roles
/// bound to the position.
/// </summary>
public sealed class DemoAuthorityCatalog : IAuthorityCatalog
{
    private static readonly AuthorityInfo[] s_authorities =
    [
        new("ROLE-APPROVE", "تأیید اسناد", "تأیید اسناد و درخواست‌های واحد"),
        new("ROLE-PURCHASE", "ثبت سفارش خرید", null),
        new("ROLE-REPORTS", "مشاهدهٔ گزارش‌ها", null),
    ];

    public Task<IReadOnlyList<AuthorityInfo>> GetAuthoritiesAsync(string positionKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AuthorityInfo>>(s_authorities);
}
