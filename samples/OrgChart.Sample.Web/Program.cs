using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

using OrgChart.Razor.Admin;
using OrgChart.Sample.Web.Data;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Demo sign-in: every request is the user configured in Sample:UserId. Replace with real authentication (e.g. Identity).
builder.Services.AddAuthentication(DemoAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(DemoAuthenticationHandler.SchemeName, null);
builder.Services.AddRazorPages();

string provider = builder.Configuration["OrgChart:Provider"] ?? "Sqlite";
string connectionString = builder.Configuration.GetConnectionString("OrgChart")
    ?? throw new InvalidOperationException("Connection string 'OrgChart' is missing.");

OrgChart.Core.OrgChartBuilder orgChart = builder.Services.AddOrgChart();
if (provider == "SqlServer")
{
    orgChart.AddSqlServerStore(connectionString);
}
else
{
    orgChart.AddEntityFrameworkStore(options => options.UseSqlite(connectionString));
}

orgChart
    .AddHttpContextUser()
    .AddUserDirectory<DemoUserDirectory>()
    .AddAdminUi(ui =>
    {
        // "Host" renders the pages in this app's Pages/Shared/_Layout (which loads MX); "Standalone" uses the module's shell.
        if (builder.Configuration["OrgChart:Layout"] == "Standalone")
        {
            ui.UseStandaloneLayout();
        }
    });

WebApplication app = builder.Build();

await DemoSeed.RunAsync(app.Services, provider);

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => Results.Redirect("/OrgChart"));
app.MapRazorPages();
app.Run();
