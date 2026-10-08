namespace OrgChart.Core.Chart;

/// <param name="ValidFrom">Inclusive start (UTC), or null for unbounded.</param>
/// <param name="ValidTo">Exclusive end (UTC), or null for unbounded.</param>
public sealed record UnitNode(
    string Key,
    string Title,
    string? Code,
    string TypeKey,
    string? ParentKey,
    string? ManagerPositionKey,
    int SortOrder,
    bool IsActive,
    DateTime? ValidFrom,
    DateTime? ValidTo);

/// <param name="ValidFrom">Inclusive start (UTC), or null for unbounded.</param>
/// <param name="ValidTo">Exclusive end (UTC), or null for unbounded.</param>
public sealed record PositionNode(
    string Key,
    string Title,
    string? Code,
    string UnitKey,
    string? TypeKey,
    bool IsManagerial,
    int SortOrder,
    bool IsActive,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    string? ParentKey = null);

/// <param name="Level">Unit types only: place in the hierarchy (1 = top); null = no rule.</param>
/// <param name="CanBeRoot">Unit types only: units of this type may be roots.</param>
public sealed record OrgTypeNode(string Key, string Title, int SortOrder, bool IsActive, int? Level = null, bool CanBeRoot = true);
