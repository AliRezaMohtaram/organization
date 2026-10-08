using OrgChart.Core.Model;

namespace OrgChart.Core.Admin;

/// <summary>Which lookup table a type belongs to.</summary>
public enum OrgTypeKind
{
    Unit = 1,
    Position = 2,
}

/// <param name="Level">Unit types only: place in the hierarchy (1 = top); a unit's type needs a greater level than its parent's. Null = no rule.</param>
/// <param name="CanBeRoot">Unit types only: units of this type may be roots of the tree.</param>
public sealed record OrgTypeInput(string Key, string Title, int SortOrder = 0, int? Level = null, bool CanBeRoot = true);

/// <param name="Level">Unit types only; see <see cref="OrgTypeInput"/>. Rejected when existing units would break the rule.</param>
public sealed record OrgTypeUpdate(string Title, int SortOrder, int? Level = null, bool CanBeRoot = true);

/// <param name="ParentKey">Null for a root unit (e.g. a company).</param>
/// <param name="ValidFrom">Inclusive start (UTC), or null for unbounded.</param>
/// <param name="ValidTo">Exclusive end (UTC), or null for unbounded.</param>
public sealed record UnitInput(
    string Key,
    string Title,
    string TypeKey,
    string? ParentKey = null,
    string? Code = null,
    int SortOrder = 0,
    DateTime? ValidFrom = null,
    DateTime? ValidTo = null);

/// <summary>Everything about a unit that can change in place. Moving is <c>MoveUnitAsync</c>.</summary>
public sealed record UnitUpdate(
    string Title,
    string TypeKey,
    string? Code,
    int SortOrder,
    DateTime? ValidFrom,
    DateTime? ValidTo);

/// <param name="ParentPositionKey">The position (in the same unit) this one reports to; null for a top position.</param>
public sealed record PositionInput(
    string Key,
    string Title,
    string UnitKey,
    string? TypeKey = null,
    bool IsManagerial = false,
    string? Code = null,
    int SortOrder = 0,
    DateTime? ValidFrom = null,
    DateTime? ValidTo = null,
    string? ParentPositionKey = null);

/// <summary>Everything about a position that can change in place. Moving is <c>MovePositionAsync</c>.</summary>
/// <param name="ParentPositionKey">The position (in the same unit) this one reports to; null makes it a top position.</param>
public sealed record PositionUpdate(
    string Title,
    string? TypeKey,
    bool IsManagerial,
    string? Code,
    int SortOrder,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    string? ParentPositionKey = null);

public sealed record AssignmentInput(
    string PositionKey,
    string UserId,
    AssignmentKind Kind = AssignmentKind.Primary,
    DateTime? ValidFrom = null,
    DateTime? ValidTo = null,
    string? Note = null);

public sealed record AssignmentUpdate(AssignmentKind Kind, DateTime? ValidFrom, DateTime? ValidTo, string? Note);

/// <param name="EffectiveAt">When the move happens (UTC): the old assignment ends and the new one starts.</param>
public sealed record TransferInput(
    string ToPositionKey,
    DateTime EffectiveAt,
    AssignmentKind Kind = AssignmentKind.Primary,
    string? Note = null);
