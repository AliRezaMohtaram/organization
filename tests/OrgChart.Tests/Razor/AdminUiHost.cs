using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OrgChart.Core.Abstractions;
using OrgChart.EFCore;
using OrgChart.Razor.Admin;

namespace OrgChart.Tests.Razor;

/// <summary>
/// A real ASP.NET Core host (TestServer) with the admin pages, SQLite in memory and header-based test sign-in:
/// <c>X-Test-User</c> names the user; users named "editor*" pass the edit policy, everyone signed in the view policy.
/// </summary>
public sealed partial class AdminUiHost : IAsyncDisposable
{
    public const string UserHeader = "X-Test-User";

    private readonly WebApplication _app;
    private readonly SqliteConnection _connection;

    private AdminUiHost(WebApplication app, SqliteConnection connection)
    {
        _app = app;
        _connection = connection;
    }

    public IServiceProvider Services => _app.Services;

    public static async Task<AdminUiHost> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
        builder.Services.AddRazorPages();
        builder.Services.AddOrgChart()
            .AddEntityFrameworkStore(options => options.UseSqlite(connection))
            .AddHttpContextUser()
            .AddUserDirectory<TestUserDirectory>()
            .AddAdminUi(ui =>
            {
                ui.UseStandaloneLayout();
                ui.EditPolicy = policy => policy.RequireAssertion(c => c.User.Identity?.Name?.StartsWith("editor") == true);
            });

        WebApplication app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRazorPages();

        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<OrgChartDbContext>().Database.EnsureCreatedAsync();
        }

        await app.StartAsync();
        return new AdminUiHost(app, connection);
    }

    public HttpClient Client(string? user = null, bool modal = false)
    {
        HttpClient client = _app.GetTestClient();
        if (user is not null)
        {
            client.DefaultRequestHeaders.Add(UserHeader, user);
        }

        if (modal)
        {
            client.DefaultRequestHeaders.Add(OrgPageModel.ModalHeader, "1");
        }

        return client;
    }

    /// <summary>GETs a form and POSTs it back with the antiforgery cookie and token plus <paramref name="fields"/>.</summary>
    public static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, string formUrl, IEnumerable<KeyValuePair<string, string>> fields, string? postUrl = null)
    {
        HttpResponseMessage get = await client.GetAsync(formUrl);
        get.EnsureSuccessStatusCode();
        string html = await get.Content.ReadAsStringAsync();
        string token = FirstGroup(TokenInput().Match(html)) ?? throw new InvalidOperationException("No antiforgery token in the form.");
        string action = postUrl ?? WebUtility.HtmlDecode(FirstGroup(FormAction().Match(html)) ?? throw new InvalidOperationException("No post form."));

        HttpRequestMessage post = new(HttpMethod.Post, action)
        {
            Content = new FormUrlEncodedContent(fields.Append(new("__RequestVerificationToken", token))),
        };
        if (get.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies))
        {
            post.Headers.Add("Cookie", string.Join("; ", cookies.Select(c => c.Split(';')[0])));
        }

        return await client.SendAsync(post);
    }

    public static async Task<string> TextAsync(HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        _connection.Dispose();
    }

    private static string? FirstGroup(Match match) =>
        match.Success ? match.Groups.Values.Skip(1).FirstOrDefault(g => g.Success)?.Value : null;

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"|value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"")]
    private static partial Regex TokenInput();

    [GeneratedRegex("<form[^>]*method=\"post\"[^>]*action=\"([^\"]+)\"|<form[^>]*action=\"([^\"]+)\"[^>]*method=\"post\"")]
    private static partial Regex FormAction();

    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var user) || string.IsNullOrEmpty(user))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, user!), new Claim(ClaimTypes.Name, user!)], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class TestUserDirectory : IUserDirectory
    {
        private static readonly UserInfo[] s_users = [new("u1", "علی احمدی", "EMP-1"), new("u2", "مریم رضایی", "EMP-2")];

        public Task<IReadOnlyList<UserInfo>> SearchAsync(string text, int maxResults, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserInfo>>(s_users.Where(u => u.DisplayName.Contains(text)).Take(maxResults).ToList());

        public Task<IReadOnlyDictionary<string, UserInfo>> GetUsersAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, UserInfo>>(s_users.Where(u => userIds.Contains(u.UserId)).ToDictionary(u => u.UserId));
    }
}
