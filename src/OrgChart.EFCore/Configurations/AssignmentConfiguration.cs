using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OrgChart.Core.Model;

namespace OrgChart.EFCore.Configurations;

internal sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("Assignments", t => t.HasCheckConstraint(
            EntityBuilderExtensions.ValidRangeCheck("Assignments"),
            EntityBuilderExtensions.ValidRangeSql));
        builder.ConfigureAudit();

        builder.Property(e => e.UserId).HasMaxLength(ColumnLengths.UserId).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(ColumnLengths.Note);

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.PositionId);

        // Positions are soft-deleted; assignments are history and stay with them.
        builder.HasOne(e => e.Position)
            .WithMany(e => e.Assignments)
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
