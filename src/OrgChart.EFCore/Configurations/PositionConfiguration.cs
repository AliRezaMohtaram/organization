using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OrgChart.Core.Model;

namespace OrgChart.EFCore.Configurations;

internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Positions", t => t.HasCheckConstraint(
            EntityBuilderExtensions.ValidRangeCheck("Positions"),
            EntityBuilderExtensions.ValidRangeSql));
        builder.ConfigureKeyed();

        builder.Property(e => e.Code).HasMaxLength(ColumnLengths.Code);

        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[Code] IS NOT NULL");

        builder.HasOne(e => e.OrgUnit)
            .WithMany(e => e.Positions)
            .HasForeignKey(e => e.OrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ParentPosition)
            .WithMany()
            .HasForeignKey(e => e.ParentPositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Type)
            .WithMany()
            .HasForeignKey(e => e.TypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
