using System.Text.RegularExpressions;

namespace OrgChart.Core.Model;

/// <summary>
/// Rules for the stable keys other modules use to refer to the chart (units, positions, types).
/// Keys never change once saved and are compared case-insensitively, so uniqueness is enforced
/// on <see cref="Normalize"/>d keys.
/// </summary>
public static partial class OrgKey
{
    public const int MaxLength = 256;

    /// <summary>ASCII letters, digits, '.', '-' and '_'; must start with a letter or digit.</summary>
    public static bool IsValid(string? key) =>
        !string.IsNullOrEmpty(key) && key.Length <= MaxLength && KeyPattern().IsMatch(key);

    public static string Normalize(string key) => key.ToUpperInvariant();

    public static bool AreEqual(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex KeyPattern();
}
