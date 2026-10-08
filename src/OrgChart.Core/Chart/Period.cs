namespace OrgChart.Core.Chart;

/// <summary>Helpers for [from, to) periods in UTC where null means unbounded.</summary>
public static class Period
{
    public static bool Contains(DateTime? from, DateTime? to, DateTime atUtc) =>
        (from is null || from <= atUtc) && (to is null || atUtc < to);

    /// <summary>True when the periods share at least one instant.</summary>
    public static bool Overlaps(DateTime? fromA, DateTime? toA, DateTime? fromB, DateTime? toB) =>
        (toB is null || fromA is null || fromA < toB) && (toA is null || fromB is null || fromB < toA);

    /// <summary>The latest start: null only when both are unbounded.</summary>
    public static DateTime? MaxFrom(DateTime? a, DateTime? b) => a is null ? b : b is null ? a : a > b ? a : b;

    /// <summary>The earliest end: null only when both are unbounded.</summary>
    public static DateTime? MinTo(DateTime? a, DateTime? b) => a is null ? b : b is null ? a : a < b ? a : b;

    public static bool IsEmpty(DateTime? from, DateTime? to) => from is not null && to is not null && from >= to;
}
