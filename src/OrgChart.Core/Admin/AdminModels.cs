using OrgChart.Core.Model;

namespace OrgChart.Core.Admin;

/// <summary>Which lookup table a type belongs to.</summary>
public enum OrgTypeKind
{
    Unit = 1,
    Position = 2,
}

public sealed record OrgTypeInput(string Key, string Title, int SortOrder = 0);

public sealed record OrgTypeUpdate(string Title, int SortOrder);

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

public sealed record PositionInput(
    string Key,
    string Title,
    string UnitKey,
    string? TypeKey = null,
    bool IsManagerial = false,
    string? Code = null,
    int SortOrder = 0,
    DateTime? ValidFrom = null,
    DateTime? ValidTo = null);

/// <summary>Everything about a position that can change in place. Moving is <c>MovePositionAsync</c>.</summary>
public sealed record PositionUpdate(
    string Title,
    string? TypeKey,
    bool IsManagerial,
    string? Code,
    int SortOrder,
    DateTime? ValidFrom,
    DateTime? ValidTo);

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
