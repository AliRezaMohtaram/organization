namespace OrgChart.Core.Model;

/// <summary>Persisted as int — never renumber.</summary>
public enum DelegationKind
{
    /// <summary>Temporary, from the holder to a person (تفویض اختیار). An end date is required.</summary>
    ToUser = 1,

    /// <summary>Standing, from a position to its deputy position (جانشینی): whoever holds the deputy position has it.</summary>
    ToPosition = 2,
}
