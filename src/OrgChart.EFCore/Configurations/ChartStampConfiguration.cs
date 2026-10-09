using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OrgChart.EFCore.Services;

namespace OrgChart.EFCore.Configurations;

internal sealed class ChartStampConfiguration : IEntityTypeConfiguration<ChartStamp>
{
    public void Configure(EntityTypeBuilder<ChartStamp> builder)
    {
        builder.ToTable("ChartStamps");
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.HasData(new ChartStamp
        {
            Id = ChartStamp.SingletonId,
            Stamp = new Guid("5d0f3c9e-2f6b-4b8e-9a51-0c7f6e1d2a10"),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}
