using System.Globalization;
using System.Resources;

using OrgChart.Core.Model;

namespace OrgChart.Razor.Resources;

/// <summary>UI text of the admin pages. The neutral resources are Persian; add OrgText.{culture}.resx for other languages.</summary>
public static class OrgText
{
    private static readonly ResourceManager s_resources = new("OrgChart.Razor.Resources.OrgText", typeof(OrgText).Assembly);

    public static string Get(string name) => s_resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;

    public static string Format(string name, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Get(name), args);

    /// <summary>Message for an <see cref="Core.Admin.OrgChartAdminException"/> code.</summary>
    public static string Error(string code) =>
        s_resources.GetString("Error_" + code, CultureInfo.CurrentUICulture) ?? Format("Error_Unknown", code);

    /// <summary>Label of an audit operation; unknown operations show their key.</summary>
    public static string Operation(string operation) =>
        s_resources.GetString("Op_" + operation, CultureInfo.CurrentUICulture) ?? operation;

    public static string Entity(string entityType) =>
        s_resources.GetString("Entity_" + entityType, CultureInfo.CurrentUICulture) ?? entityType;

    public static string Kind(AssignmentKind kind) => Get("Kind_" + kind);

    /// <summary>Resource name check for tests: true when <paramref name="name"/> exists in the neutral resources.</summary>
    internal static bool Has(string name) => s_resources.GetString(name, CultureInfo.InvariantCulture) is not null;
}
