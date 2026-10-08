using OrgChart.Core.Model;

namespace OrgChart.EFCore.Configurations;

internal static class ColumnLengths
{
    public const int Key = OrgKey.MaxLength;
    public const int Code = 64;
    public const int Title = 256;
    public const int Note = 1000;

    // Matches AspNetUsers.Id so host user ids always fit.
    public const int UserId = 450;
}
