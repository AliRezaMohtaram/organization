using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OrgChart.Core.Model;

namespace OrgChart.EFCore.Configurations;

internal sealed class OrgUnitTypeConfiguration : IEntityTypeConfiguration<OrgUnitType>
{
    public void Configure(EntityTypeBuilder<OrgUnitType> builder)
    {
        builder.ToTable("OrgUnitTypes", t => t.HasCheckConstraint("CK_OrgUnitTypes_Level", "[Level] IS NULL OR [Level] >= 1"));
        builder.ConfigureKeyed();
        builder.Property(e => e.CanBeRoot).HasDefaultValue(true).HasSentinel(true);
    }
}
