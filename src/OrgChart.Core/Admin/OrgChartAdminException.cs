namespace OrgChart.Core.Admin;

/// <summary>
/// A rejected admin operation. <see cref="Code"/> (see <see cref="OrgChartErrors"/>) is stable and is what UIs
/// translate; the message is for logs.
/// </summary>
public sealed class OrgChartAdminException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class OrgChartErrors
{
    public const string KeyInvalid = "KeyInvalid";
    public const string KeyTaken = "KeyTaken";
    public const string TitleRequired = "TitleRequired";
    public const string TitleTooLong = "TitleTooLong";
    public const string CodeTooLong = "CodeTooLong";
    public const string CodeTaken = "CodeTaken";
    public const string NoteTooLong = "NoteTooLong";
    public const string InvalidPeriod = "InvalidPeriod";
    public const string TypeNotFound = "TypeNotFound";
    public const string TypeInactive = "TypeInactive";
    public const string UnitNotFound = "UnitNotFound";
    public const string UnitInactive = "UnitInactive";
    public const string ParentNotFound = "ParentNotFound";
    public const string ParentInactive = "ParentInactive";
    public const string ParentCycle = "ParentCycle";
    public const string UnitHasActiveChildren = "UnitHasActiveChildren";
    public const string UnitHasActivePositions = "UnitHasActivePositions";
    public const string PositionNotFound = "PositionNotFound";
    public const string PositionInactive = "PositionInactive";
    public const string PositionHasAssignments = "PositionHasAssignments";
    public const string PositionIsUnitManager = "PositionIsUnitManager";
    public const string ManagerPositionNotInUnit = "ManagerPositionNotInUnit";
    public const string UserIdRequired = "UserIdRequired";
    public const string UserIdTooLong = "UserIdTooLong";
    public const string InvalidKind = "InvalidKind";
    public const string AssignmentNotFound = "AssignmentNotFound";
    public const string AssignmentOverlap = "AssignmentOverlap";
    public const string AssignmentAlreadyEnded = "AssignmentAlreadyEnded";
    public const string InvalidEndDate = "InvalidEndDate";
    public const string ParentPositionNotInUnit = "ParentPositionNotInUnit";
    public const string ParentPositionCycle = "ParentPositionCycle";
    public const string PositionHasSubordinates = "PositionHasSubordinates";
    public const string UnitHeadHasParent = "UnitHeadHasParent";
    public const string InvalidLevel = "InvalidLevel";
    public const string TypeLevelNotAllowed = "TypeLevelNotAllowed";
    public const string TypeCannotBeRoot = "TypeCannotBeRoot";
    public const string TypeLevelConflict = "TypeLevelConflict";
}
