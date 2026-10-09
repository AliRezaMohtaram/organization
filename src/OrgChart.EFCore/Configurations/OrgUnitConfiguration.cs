using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OrgChart.Core.Model;

namespace OrgChart.EFCore.Configurations;

internal sealed class OrgUnitConfiguration : IEntityTypeConfiguration<OrgUnit>
{
    public void Configure(EntityTypeBuilder<OrgUnit> builder)
    {
        builder.ToTable("OrgUnits", t => t.HasCheckConstraint(
            EntityBuilderExtensions.ValidRangeCheck("OrgUnits"),
            EntityBuilderExtensions.ValidRangeSql));
        builder.ConfigureKeyed();

        builder.Property(e => e.Code).HasMaxLength(ColumnLengths.Code);

        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
        builder.HasIndex(e => e.ManagerPositionId).IsUnique().HasFilter("[ManagerPositionId] IS NOT NULL");

        // Units are soft-deleted, so nothing cascades.
        builder.HasOne(e => e.Type)
            .WithMany()
            .HasForeignKey(e => e.TypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Parent)
            .WithMany(e => e.Children)
            .HasForeignKey(e => e.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ManagerPosition)
            .WithMany()
            .HasForeignKey(e => e.ManagerPositionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
