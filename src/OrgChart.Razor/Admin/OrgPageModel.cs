using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using OrgChart.Core.Admin;
using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin;

/// <summary>
/// Shared helpers for the admin pages: modal-first forms (MX remote modals), flash messages, dates, errors.
/// </summary>
/// <remarks>
/// MX opens a link marked <c>data-modal</c> with the header <c>X-MX-Modal: 1</c> and expects the form partial (root
/// <c>.modal</c>); on success it expects JSON <c>{ redirect }</c>. Without JavaScript the same URLs are full pages.
/// </remarks>
public abstract class OrgPageModel : PageModel
{
    public const string ModalHeader = "X-MX-Modal";
    public const string FlashKey = "OrgChart.Flash";

    private OrgDates? _dates;
    private OrgChartUiOptions? _ui;

    public bool IsModal => Request.Headers[ModalHeader] == "1";

    public OrgChartUiOptions Ui => _ui ??= Service<IOptions<OrgChartUiOptions>>().Value;

    public OrgDates Dates => _dates ??= new OrgDates(Ui.TimeZone);

    /// <summary>The module's own shell is used, so the topbar already shows the page title.</summary>
    public bool Standalone => Ui.Layout == OrgChartUiOptions.StandaloneLayout;

    protected T Service<T>()
        where T : notnull => HttpContext.RequestServices.GetRequiredService<T>();

    protected DateTime UtcNow => Service<TimeProvider>().GetUtcNow().UtcDateTime;

    protected async Task<bool> CanEditAsync() =>
        (await Service<IAuthorizationService>().AuthorizeAsync(User, OrgChartPolicies.Edit)).Succeeded;

    /// <summary>The form alone in a modal request, else the full page around it.</summary>
    protected IActionResult Form(string partialPath) => IsModal ? Partial(partialPath, this) : Page();

    /// <summary>After a successful change: a toast on the next page, and the redirect (JSON for a modal).</summary>
    protected IActionResult Done(string url, string message)
    {
        TempData[FlashKey] = message;
        return IsModal ? new JsonResult(new { redirect = url }) : Redirect(url);
    }

    protected void AddError(OrgChartAdminException ex) => ModelState.AddModelError(string.Empty, OrgText.Error(ex.Code));

    protected void AddError(string textKey) => ModelState.AddModelError(string.Empty, OrgText.Get(textKey));

    /// <summary>Reads a Jalali "from" and inclusive "to"; adds a model error and returns false when either is invalid.</summary>
    protected bool TryReadPeriod(string? fromText, string? toText, out DateTime? from, out DateTime? to)
    {
        bool ok = Dates.TryParse(fromText, inclusiveEnd: false, out from) & Dates.TryParse(toText, inclusiveEnd: true, out to);
        if (!ok)
        {
            AddError("Error_InvalidDate");
        }

        return ok;
    }

    /// <summary>The signed-in user's id (as the chart stores it).</summary>
    protected string? CurrentUserId => Service<Core.Abstractions.ICurrentUser>().UserId;

    /// <summary>JSON user search for the forms: <c>[{ id, name, detail }]</c>, at most 10, needs 2+ characters.</summary>
    protected async Task<IActionResult> SearchUsersAsync(string? q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return new JsonResult(Array.Empty<object>());
        }

        IReadOnlyList<Core.Abstractions.UserInfo> found = await Service<Core.Abstractions.IUserDirectory>().SearchAsync(q.Trim(), 10, cancellationToken);
        return new JsonResult(found.Select(u => new { id = u.UserId, name = u.DisplayName, detail = u.Detail }));
    }

    /// <summary>Scope from a form: null = full; otherwise the checked keys.</summary>
    protected static IReadOnlyCollection<string>? ReadScope(bool full, IEnumerable<string>? keys) => full ? null : (keys ?? []).ToList();

    /// <summary>The chart page showing <paramref name="unitKey"/>, optionally on a tab.</summary>
    public string UnitUrl(string? unitKey, string? tab = null) =>
        Url.Page("/Index", new { area = "OrgChart", unit = unitKey, tab }) ?? "/OrgChart";
}
