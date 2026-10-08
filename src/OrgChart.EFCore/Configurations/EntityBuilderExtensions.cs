using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OrgChart.Core.Model;

namespace OrgChart.EFCore.Configurations;

internal static class EntityBuilderExtensions
{
    public static EntityTypeBuilder<T> ConfigureAudit<T>(this EntityTypeBuilder<T> builder)
        where T : AuditableEntity
    {
        builder.Property(e => e.CreatedBy).HasMaxLength(ColumnLengths.UserId);
        builder.Property(e => e.UpdatedBy).HasMaxLength(ColumnLengths.UserId);
        return builder;
    }

    /// <summary>Key, normalized key (unique), title, sort order and active flag.</summary>
    public static EntityTypeBuilder<T> ConfigureKeyed<T>(this EntityTypeBuilder<T> builder)
        where T : KeyedEntity
    {
        builder.ConfigureAudit();

        // Keys are stable: EF refuses to save a changed key.
        builder.Property(e => e.Key).HasMaxLength(ColumnLengths.Key).IsRequired()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(e => e.NormalizedKey).HasMaxLength(ColumnLengths.Key).IsRequired()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(e => e.Title).HasMaxLength(ColumnLengths.Title).IsRequired();

        builder.HasIndex(e => e.NormalizedKey).IsUnique();
        return builder;
    }

    public static string ValidRangeCheck(string table) => $"CK_{table}_ValidRange";

    public const string ValidRangeSql = "[ValidFrom] IS NULL OR [ValidTo] IS NULL OR [ValidFrom] <= [ValidTo]";
}
