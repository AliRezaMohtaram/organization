using OrgChart.Core.Chart;

namespace OrgChart.EFCore.Services;

/// <summary>The last loaded snapshot and the chart stamp it was loaded under. Singleton.</summary>
internal sealed class SnapshotCache
{
    private volatile Entry? _entry;

    public OrgChartSnapshot? Get(Guid stamp) => _entry is { } entry && entry.Stamp == stamp ? entry.Snapshot : null;

    public void Set(Guid stamp, OrgChartSnapshot snapshot) => _entry = new Entry(stamp, snapshot);

    public void Clear() => _entry = null;

    private sealed record Entry(Guid Stamp, OrgChartSnapshot Snapshot);
}
