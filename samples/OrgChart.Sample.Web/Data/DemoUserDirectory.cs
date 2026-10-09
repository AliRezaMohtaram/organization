using OrgChart.Core.Abstractions;

namespace OrgChart.Sample.Web.Data;

/// <summary>A fixed list of people instead of the host's user store.</summary>
public sealed class DemoUserDirectory : IUserDirectory
{
    public static readonly IReadOnlyList<UserInfo> People =
    [
        new("u-admin", "مدیر سامانه", "admin@example.com"),
        new("u-001", "علی احمدی", "EMP-00125"),
        new("u-002", "مریم رضایی", "EMP-00142"),
        new("u-003", "سارا نوری", "EMP-00158"),
        new("u-004", "حسن کریمی", "EMP-00131"),
        new("u-005", "رضا محمدی", "EMP-00117"),
        new("u-006", "زهرا حسینی", "EMP-00163"),
        new("u-007", "محمد کاظمی", "EMP-00102"),
        new("u-008", "نرگس صادقی", "EMP-00171"),
    ];

    public Task<IReadOnlyList<UserInfo>> SearchAsync(string text, int maxResults, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserInfo>>(People
            .Where(p => p.DisplayName.Contains(text, StringComparison.CurrentCultureIgnoreCase)
                || (p.Detail?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                || p.UserId.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToList());

    public Task<IReadOnlyDictionary<string, UserInfo>> GetUsersAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, UserInfo>>(People.Where(p => userIds.Contains(p.UserId)).ToDictionary(p => p.UserId));
}
