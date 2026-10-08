namespace OrgChart.Core.Model;

/// <summary>Persisted as int — never renumber.</summary>
public enum AssignmentKind
{
    Primary = 1,

    /// <summary>Temporary stand-in (سرپرستی موقت).</summary>
    Acting = 2,
}
