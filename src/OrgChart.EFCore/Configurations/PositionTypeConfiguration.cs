using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OrgChart.Core.Model;

namespace OrgChart.EFCore.Configurations;

internal sealed class PositionTypeConfiguration : IEntityTypeConfiguration<PositionType>
{
    public void Configure(EntityTypeBuilder<PositionType> builder)
    {
        builder.ToTable("PositionTypes");
        builder.ConfigureKeyed();
    }
}
