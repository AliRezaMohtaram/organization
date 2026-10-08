# OrgChart

Reusable organizational chart module for ASP.NET Core (.NET 9, EF Core 9, SQL Server).

It stores only the **structure** of an organization: a tree of org units, positions in those units,
and which user holds which position over time. Other modules (for example the Acl access-control
module) read it through interfaces and refer to units and positions by **stable string keys** that
never change when the chart is restructured.

## Projects

| Project | Purpose |
|---|---|
| `OrgChart.Core` | Domain model and abstractions. No ASP.NET Core or EF Core dependency. |
| `OrgChart.EFCore` | `OrgChartDbContext` (schema `org`), migrations. |
| `OrgChart.AspNetCore` | *(planned)* DI wiring: `services.AddOrgChart(o => ...)`. |
| `OrgChart.Razor` | *(planned)* Persian/RTL admin pages on the MX design system. |
| `OrgChart.Acl` | *(planned)* Bridge implementing Acl's `IOrgStructure`. |

## Database

- `db/orgchart-schema.sql` — idempotent SQL Server DDL generated from the migrations.
- `db/orgchart-drop.sql` — removes the `org` schema from the current database.

## Build & test

```
dotnet build
dotnet test
```
