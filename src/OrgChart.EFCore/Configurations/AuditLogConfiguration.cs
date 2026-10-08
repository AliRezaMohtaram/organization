using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OrgChart.Core.Model;

namespace OrgChart.EFCore.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.Property(e => e.ActorUserId).HasMaxLength(ColumnLengths.UserId);
        builder.Property(e => e.Operation).HasMaxLength(64).IsRequired();
        builder.Property(e => e.EntityType).HasMaxLength(128).IsRequired();
        builder.Property(e => e.EntityId).HasMaxLength(128);

        builder.HasIndex(e => e.At);
        builder.HasIndex(e => new { e.EntityType, e.EntityId });
    }
}
