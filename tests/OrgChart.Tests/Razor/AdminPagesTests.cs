using System.Net;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;
using OrgChart.EFCore;

namespace OrgChart.Tests.Razor;

/// <summary>The admin pages in a real host: access, rendering, modal protocol and form posts.</summary>
public sealed class AdminPagesTests : IAsyncLifetime
{
    private AdminUiHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AdminUiHost.StartAsync();
        await AdminAsync(async admin =>
        {
            await admin.CreateTypeAsync(OrgTypeKind.Unit, new OrgTypeInput("COMPANY", "شرکت"));
            await admin.CreateTypeAsync(OrgTypeKind.Unit, new OrgTypeInput("DEPARTMENT", "اداره"));
            await admin.CreateUnitAsync(new UnitInput("C", "شرکت نمونه", "COMPANY"));
            await admin.CreateUnitAsync(new UnitInput("FIN", "اداره مالی", "DEPARTMENT", "C"));
            await admin.CreatePositionAsync(new PositionInput("POS-ACC", "حسابدار", "FIN"));
            await admin.AssignAsync(new AssignmentInput("POS-ACC", "u1"));
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task AdminAsync(Func<IOrgChartAdministration, Task> action)
    {
        await using AsyncServiceScope scope = _host.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<IOrgChartAdministration>());
    }

    private async Task<T> ScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = _host.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    [Fact]
    public async Task Anonymous_users_are_rejected()
    {
        HttpResponseMessage response = await _host.Client().GetAsync("/OrgChart");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_sees_the_chart_without_edit_actions()
    {
        HttpClient client = _host.Client("viewer");

        HttpResponseMessage response = await client.GetAsync("/OrgChart?unit=fin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = await AdminUiHost.TextAsync(response);
        Assert.Contains("اداره مالی", html);
        Assert.Contains("حسابدار", html);
        Assert.Contains("علی احمدی", html);
        Assert.Contains("data-key=\"C\"", html);
        Assert.DoesNotContain("/OrgChart/Units/Edit", html);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/OrgChart/Units/Edit")).StatusCode);
    }

    [Fact]
    public async Task Editor_sees_edit_actions_and_every_page_renders()
    {
        HttpClient client = _host.Client("editor");
        int assignmentId = (await ScopeAsync(sp => sp.GetRequiredService<IOrgChartReader>().GetUnitAssignmentsAsync("FIN")))[0].Id;

        string index = await AdminUiHost.TextAsync(await client.GetAsync("/OrgChart?unit=FIN"));
        Assert.Contains("/OrgChart/Units/Edit?key=FIN", index);

        string[] pages =
        [
            "/OrgChart/Types", "/OrgChart/Audit", "/OrgChart/Units/Edit", "/OrgChart/Units/Edit?key=FIN", "/OrgChart/Units/Edit?parent=C",
            "/OrgChart/Units/Move?key=FIN", "/OrgChart/Units/Manager?key=FIN", "/OrgChart/Units/Status?key=FIN&active=false",
            "/OrgChart/Positions/Edit?unit=FIN", "/OrgChart/Positions/Edit?key=POS-ACC", "/OrgChart/Positions/Move?key=POS-ACC",
            "/OrgChart/Positions/Status?key=POS-ACC&active=false", "/OrgChart/Assignments/Edit?unit=FIN",
            "/OrgChart/Assignments/Edit?position=POS-ACC", $"/OrgChart/Assignments/Edit?id={assignmentId}",
            $"/OrgChart/Assignments/End?id={assignmentId}", $"/OrgChart/Assignments/Transfer?id={assignmentId}",
            $"/OrgChart/Assignments/Remove?id={assignmentId}", "/OrgChart/Types/Edit?kind=Unit", "/OrgChart/Types/Edit?kind=Position&key=X",
            "/OrgChart/Delegations/Edit?position=POS-ACC&kind=ToUser", "/OrgChart/Delegations/Edit?position=POS-ACC&kind=ToPosition",
            "/OrgChart?unit=FIN&tab=delegations", "/OrgChart/My",
        ];
        foreach (string page in pages)
        {
            HttpResponseMessage response = await client.GetAsync(page);
            Assert.True(
                response.StatusCode == HttpStatusCode.OK || (page.EndsWith("key=X") && response.StatusCode == HttpStatusCode.NotFound),
                $"{page}: {response.StatusCode}");
        }

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/OrgChart/Units/Edit?key=NOPE")).StatusCode);
    }

    [Fact]
    public async Task Modal_request_gets_only_the_form()
    {
        HttpResponseMessage response = await _host.Client("editor", modal: true).GetAsync("/OrgChart/Units/Edit?parent=C");

        string html = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("<form class=\"modal\"", html.TrimStart());
        Assert.DoesNotContain("<html", html);
        Assert.Contains("data-mx-ajax", html);
    }

    [Fact]
    public async Task Modal_post_creates_the_unit_and_answers_with_a_redirect()
    {
        HttpClient client = _host.Client("editor", modal: true);

        HttpResponseMessage response = await AdminUiHost.SubmitAsync(client, "/OrgChart/Units/Edit?parent=C",
        [
            new("Input.Key", "HR"), new("Input.Title", "منابع انسانی"), new("Input.TypeKey", "DEPARTMENT"),
            new("Input.ParentKey", "C"), new("Input.ValidFrom", "۱۴۰۵/۰۱/۰۱"), new("Input.SortOrder", "2"),
        ]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("unit=HR", json.RootElement.GetProperty("redirect").GetString());

        OrgChartSnapshot chart = await ScopeAsync(sp => sp.GetRequiredService<IOrgChartReader>().GetSnapshotAsync());
        UnitNode hr = chart.FindUnit("HR")!;
        Assert.Equal(("منابع انسانی", "C", 2), (hr.Title, hr.ParentKey, hr.SortOrder));
        Assert.NotNull(hr.ValidFrom);

        AuditLog log = await ScopeAsync(sp => sp.GetRequiredService<OrgChartDbContext>().AuditLogs.OrderBy(l => l.Id).LastAsync());
        Assert.Equal(("UnitCreated", "editor"), (log.Operation, log.ActorUserId));
    }

    [Fact]
    public async Task Rejected_post_shows_the_error_in_the_form()
    {
        HttpClient client = _host.Client("editor", modal: true);

        HttpResponseMessage response = await AdminUiHost.SubmitAsync(client, "/OrgChart/Units/Edit?parent=C",
            [new("Input.Key", "fin"), new("Input.Title", "تکراری"), new("Input.TypeKey", "DEPARTMENT"), new("Input.ParentKey", "C")]);

        string html = await AdminUiHost.TextAsync(response);
        Assert.StartsWith("<form", html.TrimStart());
        Assert.Contains("callout tone-danger", html);
        Assert.Contains("این کلید قبلاً استفاده شده است", html);
        Assert.Contains("value=\"تکراری\"", html);
    }

    [Fact]
    public async Task Invalid_date_is_reported_and_nothing_is_saved()
    {
        HttpClient client = _host.Client("editor", modal: true);

        HttpResponseMessage response = await AdminUiHost.SubmitAsync(client, "/OrgChart/Units/Edit?parent=C",
            [new("Input.Key", "OPS"), new("Input.Title", "عملیات"), new("Input.TypeKey", "DEPARTMENT"), new("Input.ValidFrom", "1405/13/01")]);

        Assert.Contains("تاریخ معتبر نیست", await AdminUiHost.TextAsync(response));
        Assert.Null((await ScopeAsync(sp => sp.GetRequiredService<IOrgChartReader>().GetSnapshotAsync())).FindUnit("OPS"));
    }

    [Fact]
    public async Task Full_page_post_redirects_and_shows_the_flash()
    {
        HttpClient client = _host.Client("editor");
        int id = (await ScopeAsync(sp => sp.GetRequiredService<IOrgChartReader>().GetUnitAssignmentsAsync("FIN")))[0].Id;

        HttpResponseMessage response = await AdminUiHost.SubmitAsync(client, $"/OrgChart/Assignments/End?id={id}", [new("When", "date"), new("LastDay", "1499/12/29")]);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("tab=people", response.Headers.Location!.ToString());
        AssignmentInfo ended = (await ScopeAsync(sp => sp.GetRequiredService<IOrgChartReader>().GetAssignmentAsync(id)))!;
        Assert.NotNull(ended.ValidTo);
    }

    [Fact]
    public async Task Ending_now_makes_the_position_vacant_and_a_date_keeps_the_holder_until_then()
    {
        HttpClient client = _host.Client("editor");
        int id = (await ScopeAsync(sp => sp.GetRequiredService<IOrgChartReader>().GetUnitAssignmentsAsync("FIN")))[0].Id;

        HttpResponseMessage response = await AdminUiHost.SubmitAsync(client, $"/OrgChart/Assignments/End?id={id}", [new("When", "now")]);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        IReadOnlyDictionary<string, IReadOnlyList<AssignmentInfo>> holders =
            await ScopeAsync(sp => sp.GetRequiredService<IOrgChartReader>().GetHoldersAsync(["POS-ACC"], DateTime.UtcNow));
        Assert.Empty(holders);

        await AdminAsync(a => a.AssignAsync(new AssignmentInput("POS-ACC", "u2", ValidFrom: DateTime.UtcNow.AddDays(-1))));
        int second = (await ScopeAsync(sp => sp.GetRequiredService<IOrgChartReader>().GetUnitAssignmentsAsync("FIN"))).Single(a => a.UserId == "u2").Id;
        await AdminUiHost.SubmitAsync(client, $"/OrgChart/Assignments/End?id={second}", [new("When", "date"), new("LastDay", "1499/12/29")]);

        Assert.Single((await ScopeAsync(sp => sp.GetRequiredService<IOrgChartReader>().GetHoldersAsync(["POS-ACC"], DateTime.UtcNow)))["POS-ACC"]);
    }

    [Fact]
    public async Task User_search_returns_json()
    {
        HttpResponseMessage response = await _host.Client("editor").GetAsync("/OrgChart/Assignments/Edit?handler=Users&q=" + Uri.EscapeDataString("مریم"));

        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement user = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(("u2", "مریم رضایی"), (user.GetProperty("id").GetString(), user.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Viewer_cannot_post()
    {
        HttpClient editor = _host.Client("editor");
        HttpResponseMessage form = await editor.GetAsync("/OrgChart/Units/Edit?parent=C");
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);

        HttpResponseMessage response = await _host.Client("viewer").PostAsync("/OrgChart/Units/Edit",
            new FormUrlEncodedContent([new("Input.Key", "X"), new("Input.Title", "X"), new("Input.TypeKey", "DEPARTMENT")]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delegation_pages_render_and_end_and_remove_work()
    {
        await AdminAsync(async a =>
        {
            await a.CreatePositionAsync(new PositionInput("POS-DEP", "معاون", "FIN"));
            await a.DelegateAsync(new DelegationInput("POS-ACC", "u1", "u2", null, DateTime.UtcNow.AddDays(10)));
            await a.AddDeputyAsync(new DeputyInput("POS-ACC", "POS-DEP"));
        });
        IReadOnlyList<DelegationInfo> all = await ScopeAsync(sp => sp.GetRequiredService<IOrgChartReader>().GetDelegationsAsync(new DelegationQuery()));
        DelegationInfo toUser = all.Single(d => d.Kind == DelegationKind.ToUser);
        DelegationInfo deputy = all.Single(d => d.Kind == DelegationKind.ToPosition);
        HttpClient client = _host.Client("editor");

        string tab = await AdminUiHost.TextAsync(await client.GetAsync("/OrgChart?unit=FIN&tab=delegations"));
        Assert.Contains("مریم رضایی", tab);
        Assert.Contains("معاون", tab);
        foreach (string page in new[] { $"/OrgChart/Delegations/Edit?id={toUser.Id}", $"/OrgChart/Delegations/Edit?id={deputy.Id}", $"/OrgChart/Delegations/End?id={toUser.Id}", $"/OrgChart/Delegations/End?id={deputy.Id}&mode=remove" })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(page)).StatusCode);
        }

        HttpResponseMessage removed = await AdminUiHost.SubmitAsync(client, $"/OrgChart/Delegations/End?id={deputy.Id}&mode=remove", []);
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.Null(await ScopeAsync(sp => sp.GetRequiredService<IOrgChartReader>().GetDelegationAsync(deputy.Id)));
    }

    [Fact]
    public async Task Holder_delegates_and_ends_from_the_self_service_page()
    {
        HttpClient u1 = _host.Client("u1");

        string my = await AdminUiHost.TextAsync(await u1.GetAsync("/OrgChart/My"));
        Assert.Contains("حسابدار", my);
        Assert.Contains("/OrgChart/My/Delegate?position=POS-ACC", my);
        Assert.Equal(HttpStatusCode.NotFound, (await _host.Client("u2").GetAsync("/OrgChart/My/Delegate?position=POS-ACC")).StatusCode);

        HttpResponseMessage saved = await AdminUiHost.SubmitAsync(u1, "/OrgChart/My/Delegate?position=POS-ACC",
            [new("ToUserId", "u2"), new("ValidTo", "1499/12/29"), new("FullScope", "true")]);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        DelegationInfo given = Assert.Single(await ScopeAsync(sp => sp.GetRequiredService<IOrgChartReader>().GetDelegationsAsync(new DelegationQuery(FromUserId: "u1"))));

        string received = await AdminUiHost.TextAsync(await _host.Client("u2").GetAsync("/OrgChart/My"));
        Assert.Contains("علی احمدی", received);
        Assert.Equal(HttpStatusCode.NotFound, (await _host.Client("u2").GetAsync($"/OrgChart/My/End?id={given.Id}")).StatusCode);

        HttpResponseMessage ended = await AdminUiHost.SubmitAsync(u1, $"/OrgChart/My/End?id={given.Id}", []);
        Assert.Equal(HttpStatusCode.Redirect, ended.StatusCode);
        DelegationInfo? after = await ScopeAsync(sp => sp.GetRequiredService<IOrgChartReader>().GetDelegationAsync(given.Id));
        Assert.True(after is null || after.ValidTo <= DateTime.UtcNow);
    }
}
