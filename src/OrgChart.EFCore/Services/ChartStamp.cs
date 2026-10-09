namespace OrgChart.EFCore.Services;

/// <summary>
/// A single row whose <see cref="Stamp"/> changes with every change to units, positions or types. Readers on any
/// server compare it with the stamp of their cached snapshot; admin operations update it first, which also
/// serializes concurrent structural changes (the row stays locked until commit).
/// </summary>
internal sealed class ChartStamp
{
    public const int SingletonId = 1;

    public int Id { get; set; }
    public Guid Stamp { get; set; }
    public DateTime UpdatedAt { get; set; }
}
