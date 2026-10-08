namespace OrgChart.Core.Model;

/// <summary>How a user holds a position. Persisted as int — never renumber.</summary>
public enum AssignmentKind
{
    Primary = 1,

    /// <summary>Temporary stand-in (سرپرستی موقت).</summary>
    Acting = 2,

    /// <summary>Authority delegated by the holder for a period (تفویض). Never stored on an <see cref="Assignment"/>.</summary>
    Delegated = 3,

    /// <summary>Authority held as the deputy position of the position (جانشینی). Never stored on an <see cref="Assignment"/>.</summary>
    Deputy = 4,
}
