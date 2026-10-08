using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin.Pages.Audit;

[Authorize(Policy = OrgChartPolicies.View)]
public sealed class IndexModel(IOrgChartAuditReader audit, IUserDirectory users) : OrgPageModel
{
    public const int PageSize = 50;

    private static readonly JsonSerializerOptions s_pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [BindProperty(SupportsGet = true)]
    public string? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Actor { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Operation { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Entity { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public AuditPage Result { get; private set; } = new([], 0, 1, PageSize);
    public IReadOnlyList<string> Operations { get; private set; } = [];
    public IReadOnlyDictionary<string, UserInfo> Users { get; private set; } = new Dictionary<string, UserInfo>();
    public bool Filtering => !string.IsNullOrWhiteSpace(From) || !string.IsNullOrWhiteSpace(To) || !string.IsNullOrWhiteSpace(Actor)
        || !string.IsNullOrWhiteSpace(Operation) || !string.IsNullOrWhiteSpace(Entity);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        ViewData["Title"] = OrgText.Get("Nav_Audit");
        ViewData["Subtitle"] = OrgText.Get("Audit_Subtitle");
        Operations = await audit.GetOperationsAsync(cancellationToken);

        if (!Dates.TryParse(From, inclusiveEnd: false, out DateTime? from) | !Dates.TryParse(To, inclusiveEnd: true, out DateTime? to))
        {
            AddError("Error_InvalidDate");
            return Page();
        }

        Result = await audit.ReadAsync(new AuditQuery(from, to, Actor, Operation, null, Entity, PageNumber, PageSize), cancellationToken);
        HashSet<string> actors = Result.Items.Where(i => i.ActorUserId is not null).Select(i => i.ActorUserId!).ToHashSet();
        Users = actors.Count == 0 ? Users : await users.GetUsersAsync(actors, cancellationToken);
        return Page();
    }

    public string ActorName(string? userId) =>
        userId is null ? OrgText.Get("Audit_System") : Users.TryGetValue(userId, out UserInfo? user) ? user.DisplayName : userId;

    /// <summary>The stored JSON, indented, Persian kept readable.</summary>
    public static string Pretty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "";
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, s_pretty);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    public string PageUrl(int page) => Url.Page("/Audit/Index", new { area = "OrgChart", From, To, Actor, Operation, Entity, p = page }) ?? "";
}
