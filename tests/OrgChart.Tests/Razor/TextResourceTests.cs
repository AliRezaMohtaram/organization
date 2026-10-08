using System.Reflection;
using System.Text.RegularExpressions;

using OrgChart.Core.Admin;
using OrgChart.Core.Model;
using OrgChart.Razor.Resources;

namespace OrgChart.Tests.Razor;

public sealed partial class TextResourceTests
{
    [Fact]
    public void Every_error_code_has_a_message()
    {
        List<string> missing = typeof(OrgChartErrors).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (string)f.GetValue(null)!)
            .Where(code => !OrgText.Has("Error_" + code))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Every_audit_operation_and_entity_has_a_label()
    {
        string admin = File.ReadAllText(Path.Combine(RepoRoot(), "src", "OrgChart.EFCore", "Admin", "EfOrgChartAdministration.cs"));
        List<string> operations = AuditWrite().Matches(admin)
            .SelectMany(m => new[] { m.Groups[1].Value, m.Groups[2].Value })
            .Where(op => op.Length > 0)
            .Distinct()
            .ToList();

        Assert.True(operations.Count > 10, $"Only {operations.Count} operations found; the scan is broken.");
        Assert.DoesNotContain(operations, op => !OrgText.Has("Op_" + op));
        Assert.DoesNotContain(
            new[] { nameof(OrgUnit), nameof(Position), nameof(Assignment), nameof(OrgUnitType), nameof(PositionType) },
            e => !OrgText.Has("Entity_" + e));
    }

    [Fact]
    public void Every_kind_and_state_has_a_label()
    {
        Assert.All(Enum.GetNames<AssignmentKind>(), k => Assert.True(OrgText.Has("Kind_" + k), k));
        Assert.All(new[] { "Current", "Upcoming", "Ended" }, s => Assert.True(OrgText.Has("State_" + s), s));
    }

    /// <summary>Every key passed to OrgText.Get/Format in the Razor project (ternaries included) exists.</summary>
    [Fact]
    public void Every_text_key_used_in_the_ui_exists()
    {
        string root = Path.Combine(RepoRoot(), "src", "OrgChart.Razor");
        HashSet<string> keys = [];
        foreach (string file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".cshtml") || f.EndsWith(".cs"))
                         && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            foreach (Match call in TextCall().Matches(File.ReadAllText(file)))
            {
                foreach (Match literal in Literal().Matches(call.Groups[1].Value))
                {
                    // "State_" + state and similar prefixes are covered by the tests above.
                    if (!literal.Groups[1].Value.EndsWith('_'))
                    {
                        keys.Add(literal.Groups[1].Value);
                    }
                }
            }
        }

        Assert.True(keys.Count > 100, $"Only {keys.Count} keys found; the scan is broken.");
        Assert.Empty(keys.Where(k => !OrgText.Has(k)).Order());
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OrgChart.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    // The first argument of OrgText.Get/Format: up to the first ',' or ')' (one nested pair of parentheses allowed).
    [GeneratedRegex("""OrgText\.(?:Get|Format)\(((?:[^(),]|\([^()]*\))*)""")]
    private static partial Regex TextCall();

    [GeneratedRegex("\"([A-Za-z][A-Za-z0-9_]*)\"")]
    private static partial Regex Literal();

    [GeneratedRegex("""audit\.Write\(\s*(?:isActive \? )?"([A-Za-z]+)"(?: : "([A-Za-z]+)")?""")]
    private static partial Regex AuditWrite();
}
