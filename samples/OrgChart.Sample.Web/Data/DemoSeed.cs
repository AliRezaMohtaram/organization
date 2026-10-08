using Microsoft.EntityFrameworkCore;

using OrgChart.Core.Admin;
using OrgChart.Core.Model;
using OrgChart.EFCore;

namespace OrgChart.Sample.Web.Data;

/// <summary>Creates the database and, when it is empty, a small chart like the reference sample.</summary>
public static class DemoSeed
{
    public static async Task RunAsync(IServiceProvider services, string provider)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        OrgChartDbContext db = scope.ServiceProvider.GetRequiredService<OrgChartDbContext>();
        if (provider == "SqlServer")
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            // Migrations are written for SQL Server; SQLite gets the schema straight from the model.
            await db.Database.EnsureCreatedAsync();
        }

        if (await db.OrgUnitTypes.AnyAsync())
        {
            return;
        }

        IOrgChartAdministration admin = scope.ServiceProvider.GetRequiredService<IOrgChartAdministration>();
        (string Key, string Title)[] unitTypes = [("COMPANY", "شرکت"), ("BRANCH", "شعبه"), ("DIVISION", "معاونت"), ("DEPARTMENT", "اداره"), ("SECTION", "بخش"), ("TEAM", "تیم")];
        for (int i = 0; i < unitTypes.Length; i++)
        {
            // Level = place in the hierarchy; only companies may be roots.
            await admin.CreateTypeAsync(OrgTypeKind.Unit, new OrgTypeInput(unitTypes[i].Key, unitTypes[i].Title, i + 1, Level: i + 1, CanBeRoot: i == 0));
        }

        (string Key, string Title)[] positionTypes = [("MANAGERIAL", "مدیریتی"), ("SUPERVISORY", "سرپرستی"), ("EXPERT", "کارشناسی"), ("ADMIN", "اداری")];
        for (int i = 0; i < positionTypes.Length; i++)
        {
            await admin.CreateTypeAsync(OrgTypeKind.Position, new OrgTypeInput(positionTypes[i].Key, positionTypes[i].Title, i + 1));
        }

        await admin.CreateUnitAsync(new UnitInput("C-001", "شرکت الف", "COMPANY", Code: "C-001"));
        await admin.CreateUnitAsync(new UnitInput("BR-THR", "شعبه تهران", "BRANCH", "C-001", "BR-001", 1));
        await admin.CreateUnitAsync(new UnitInput("BR-ISF", "شعبه اصفهان", "BRANCH", "C-001", "BR-002", 2));
        await admin.CreateUnitAsync(new UnitInput("DV-FIN", "معاونت مالی", "DIVISION", "BR-THR", "DV-FIN", 1));
        await admin.CreateUnitAsync(new UnitInput("DP-ACC", "اداره حسابداری", "DEPARTMENT", "DV-FIN", "DP-ACC", 1));
        await admin.CreateUnitAsync(new UnitInput("DP-TRE", "اداره خزانه", "DEPARTMENT", "DV-FIN", "DP-TRE", 2));
        await admin.CreateUnitAsync(new UnitInput("DV-PUR", "معاونت بازرگانی", "DIVISION", "BR-THR", "DV-PUR", 2));
        await admin.CreateUnitAsync(new UnitInput("DP-PUR", "اداره خرید", "DEPARTMENT", "DV-PUR", "DP-PUR", 1));
        await admin.CreateUnitAsync(new UnitInput("SC-LOC", "بخش خرید داخلی", "SECTION", "DP-PUR", "SC-LOC", 1));
        await admin.CreateUnitAsync(new UnitInput("SC-FRG", "بخش خرید خارجی", "SECTION", "DP-PUR", "SC-FRG", 2));
        await admin.CreateUnitAsync(new UnitInput("DP-PRD2", "اداره تولید اصفهان", "DEPARTMENT", "BR-ISF", "DP-PRD2", 1));

        await admin.CreatePositionAsync(new PositionInput("POS-CEO", "مدیرعامل", "C-001", "MANAGERIAL", true));
        await admin.CreatePositionAsync(new PositionInput("POS-FIN-VP", "معاون مالی", "DV-FIN", "MANAGERIAL", true));
        await admin.CreatePositionAsync(new PositionInput("POS-ACC-MGR", "رئیس حسابداری", "DP-ACC", "MANAGERIAL", true));
        await admin.CreatePositionAsync(new PositionInput("POS-ACC-EXP", "کارشناس حسابداری", "DP-ACC", "EXPERT"));
        await admin.CreatePositionAsync(new PositionInput("POS-ACC-OIL", "مسئول حسابداری نفت", "DP-ACC", "SUPERVISORY", ParentPositionKey: "POS-ACC-MGR"));
        await admin.CreatePositionAsync(new PositionInput("POS-ACC-OIL-EXP", "کارشناس حسابداری نفت", "DP-ACC", "EXPERT", ParentPositionKey: "POS-ACC-OIL"));
        await admin.CreatePositionAsync(new PositionInput("POS-PUR-MGR", "مدیر خرید", "DP-PUR", "MANAGERIAL", true, SortOrder: 1));
        await admin.CreatePositionAsync(new PositionInput("POS-PUR-EXP1", "کارشناس خرید ارشد", "DP-PUR", "EXPERT", SortOrder: 2));
        await admin.CreatePositionAsync(new PositionInput("POS-PUR-EXP2", "کارشناس خرید", "DP-PUR", "EXPERT", SortOrder: 3));
        await admin.CreatePositionAsync(new PositionInput("POS-PUR-ADM", "کارشناس اداری", "DP-PUR", "ADMIN", SortOrder: 4));
        await admin.CreatePositionAsync(new PositionInput("POS-LOC-SUP", "سرپرست خرید داخلی", "SC-LOC", "SUPERVISORY"));
        await admin.CreatePositionAsync(new PositionInput("POS-FRG-SUP", "سرپرست خرید خارجی", "SC-FRG", "SUPERVISORY"));

        await admin.SetUnitManagerAsync("C-001", "POS-CEO");
        await admin.SetUnitManagerAsync("DV-FIN", "POS-FIN-VP");
        await admin.SetUnitManagerAsync("DP-ACC", "POS-ACC-MGR");
        await admin.SetUnitManagerAsync("DP-PUR", "POS-PUR-MGR");

        DateTime year = new(2025, 3, 21, 0, 0, 0, DateTimeKind.Utc);
        await admin.AssignAsync(new AssignmentInput("POS-CEO", "u-007", ValidFrom: year.AddYears(-2)));
        await admin.AssignAsync(new AssignmentInput("POS-PUR-MGR", "u-001", ValidFrom: year));
        await admin.AssignAsync(new AssignmentInput("POS-PUR-EXP1", "u-002", ValidFrom: year.AddMonths(5)));
        await admin.AssignAsync(new AssignmentInput("POS-PUR-ADM", "u-003", ValidFrom: year.AddMonths(13)));
        await admin.AssignAsync(new AssignmentInput("POS-LOC-SUP", "u-004", ValidFrom: year.AddMonths(-6)));
        await admin.AssignAsync(new AssignmentInput("POS-FRG-SUP", "u-005", ValidFrom: year.AddMonths(-20)));
        await admin.AssignAsync(new AssignmentInput("POS-FIN-VP", "u-001", AssignmentKind.Acting, year.AddMonths(16), Note: "تا تعیین معاون جدید"));
        await admin.AssignAsync(new AssignmentInput("POS-ACC-EXP", "u-006", ValidFrom: year.AddMonths(-30), ValidTo: year.AddMonths(2)));
        await admin.AssignAsync(new AssignmentInput("POS-ACC-MGR", "u-006", ValidFrom: year.AddMonths(2)));
        await admin.AssignAsync(new AssignmentInput("POS-ACC-OIL", "u-008", ValidFrom: year.AddMonths(4)));
    }
}
