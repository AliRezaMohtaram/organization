using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OrgChart.Core.Model;

namespace OrgChart.EFCore.Configurations;

internal sealed class DelegationConfiguration : IEntityTypeConfiguration<Delegation>
{
    public void Configure(EntityTypeBuilder<Delegation> builder)
    {
        builder.ToTable("Delegations", t =>
        {
            t.HasCheckConstraint(EntityBuilderExtensions.ValidRangeCheck("Delegations"), EntityBuilderExtensions.ValidRangeSql);
            // To a person: from/to users and an end date; to a position: a deputy position, no users.
            t.HasCheckConstraint("CK_Delegations_Target",
                "([Kind] = 1 AND [FromUserId] IS NOT NULL AND [ToUserId] IS NOT NULL AND [ToPositionId] IS NULL AND [ValidTo] IS NOT NULL)"
                + " OR ([Kind] = 2 AND [FromUserId] IS NULL AND [ToUserId] IS NULL AND [ToPositionId] IS NOT NULL)");
        });
        builder.ConfigureAudit();

        builder.Property(e => e.FromUserId).HasMaxLength(ColumnLengths.UserId);
        builder.Property(e => e.ToUserId).HasMaxLength(ColumnLengths.UserId);
        builder.Property(e => e.Note).HasMaxLength(ColumnLengths.Note);

        builder.HasIndex(e => e.PositionId);
        builder.HasIndex(e => e.ToUserId);
        builder.HasIndex(e => e.ToPositionId);

        builder.HasOne(e => e.Position)
            .WithMany()
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ToPosition)
            .WithMany()
            .HasForeignKey(e => e.ToPositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Scopes)
            .WithOne()
            .HasForeignKey(s => s.DelegationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DelegationScopeConfiguration : IEntityTypeConfiguration<DelegationScope>
{
    public void Configure(EntityTypeBuilder<DelegationScope> builder)
    {
        builder.ToTable("DelegationScopes");
        builder.HasKey(e => new { e.DelegationId, e.AuthorityKey });
        builder.Property(e => e.AuthorityKey).HasMaxLength(ColumnLengths.Key);
    }
}
