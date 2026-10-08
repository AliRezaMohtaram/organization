# OrgChart — reusable organizational chart module for ASP.NET Core

A standalone, reusable module that stores an organization's **structure**: org units (tree),
positions under units, and which user holds which position over time. It is consumed by other
modules (first consumer: the `Acl` access-control module) through interfaces only.

## Communication & conventions

- Talk to the user in **Persian**. Code, identifiers, comments, and commit messages in **English**.
- Work in **stages**: data model first, then the rest. Ask before changing any architectural decision below.
- **Commit only when the user asks.**
- Keep the "Status / next steps" section at the bottom of this file updated after every stage.

## Architectural decisions (final — do not change without asking)

- **No dependency on Acl, and Acl has no dependency on OrgChart.** They connect through
  `IOrgStructure` (defined in `Acl.Core.Abstractions`), implemented either in the host app or in a
  small bridge package (proposed: `OrgChart.Acl`) that references both.
- **Stable string keys** (`OrgUnitKey`, `PositionKey`) are the only cross-module references — never
  numeric Ids. Restructuring (move, rename, re-parent) must **never** change a key.
- Keys are compared **case-insensitively** by consumers: two keys differing only by case must be
  impossible (enforce with a unique index on a normalized/case-insensitive key).
- **Soft delete only**: deleting a unit or position deactivates it; old data in host apps still
  references its key.
- **No HR/personnel data.** Only structure: units, positions, assignments.
- Users are referenced only by a **string `UserId`**; no dependency on the host's user table or
  ASP.NET Identity. Display names come from an `IUserDirectory` interface (same pattern as Acl).
- The module exposes a **change hook** (e.g. `IOrgChartChangeListener`) so a bridge can invalidate
  consumer caches without OrgChart knowing about them.

## Required features

- **Unit tree**: stable key, title, parent, active flag, display order.
- **Positions** under units: stable key, title, active flag.
- **Assignments** (user → position): `UserId` string, `ValidFrom`/`ValidTo` (UTC, from inclusive,
  to exclusive), assignment type (primary / acting), multiple concurrent positions per user,
  future-dated moves ("from next month in the new position").
- **Tree queries**: descendants and ancestors of a unit.
- **Audit log** of changes (who, when, what) — like Acl's `AuditLog`.
- **Change notifications** for other modules (see contract below).

## Contract expected by Acl (`Acl.Core/Abstractions`)

```csharp
public interface IOrgStructure
{
    // User's positions, including expired and future ones (acting assignments too)
    Task<IReadOnlyList<PositionAssignment>> GetUserPositionsAsync(string userId, CancellationToken ct = default);

    // For each unit: keys of all descendants (excluding itself). Units without descendants may be omitted.
    Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetDescendantUnitKeysAsync(
        IReadOnlyCollection<string> orgUnitKeys, CancellationToken ct = default);

    // All current units and positions, for pickers in admin pages
    Task<IReadOnlyList<OrgUnitInfo>> GetUnitsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PositionInfo>> GetPositionsAsync(CancellationToken ct = default);
}

public sealed record PositionAssignment(
    string PositionKey,
    string OrgUnitKey,                          // the position's unit
    IReadOnlyList<string> OrgUnitAncestorKeys,  // ancestors of that unit, nearest first
    DateTime? ValidFrom,                        // UTC, inclusive
    DateTime? ValidTo);                         // UTC, exclusive

public sealed record OrgUnitInfo(string Key, string Title, string? ParentKey);
public sealed record PositionInfo(string Key, string Title, string OrgUnitKey);
```

- `GetUserPositionsAsync` returns past and future assignments too; Acl checks dates itself and
  derives cache expiry from them.
- Acl caches results, but calls must still be fast. Target scale: 200–2000 users, 10–30 concurrent.
- Cache invalidation via Acl's `IAccessStampStore` (called by the bridge, from OrgChart's change hook):
  - assignment changes for specific users → `BumpUsersAsync(userIds)`
  - structural changes (move unit, re-parent, deactivate unit/position) → `BumpGlobalAsync()`

Acl source: https://github.com/AliRezaMohtaram/Acl. Hosts register the adapter with
`AclBuilder.AddOrgStructure<T>()`; Acl ships `NullOrgStructure` as the no-chart default.
Reference fake implementation: `FakeOrgStructure` in Acl's `tests/Acl.Tests/Integration/AclServices.cs`.
OrgChart defines its **own** `IUserDirectory` (same shape as Acl's: `SearchAsync`, `GetUsersAsync`,
`UserInfo(UserId, DisplayName, Detail)`) so a host can implement both with one class or adapt one to the other.

## Stack & layout (consistent with Acl)

- **.NET 9**; Microsoft packages stay on 9.x (moving to .NET 10 is deferred to the user).
- **EF Core 9 + SQL Server**, separate schema `org`. Idempotent DDL scripts in `db/`.
  Tests use **SQLite** (not InMemory).
- Projects:
  - `OrgChart.Core` — domain, abstractions, services; no ASP.NET or EF dependency
  - `OrgChart.EFCore` — persistence
  - `OrgChart.AspNetCore` — DI wiring, single entry point `services.AddOrgChart(o => ...)`
  - `OrgChart.Razor` — admin pages: Persian, RTL, Razor Pages, plain CSS/JS (no CDN, no SPA
    framework), Jalali (Shamsi) dates, texts in `.resx`
- NuGet packaging like Acl: `Directory.Build.props`, output in `artifacts/packages`.

## UI design system (mandatory for any front-end)

All UI must follow the **MX design system** from https://github.com/AliRezaMohtaram/DataMapper
(`Mapper/wwwroot/`). Do not invent new visual styles; stay within its bounds.

- `css/mx.css` — the design system (tokens + components). `css/theme.css` and `css/mx-legacy.css`
  are legacy/bridge files: do **not** build on them.
- Theme axes are attributes on `<html>` (managed by `js/theme.js`): `data-theme` (dark|light|system),
  `data-preset`, `data-accent`, `data-size`, `data-radius`. Use the CSS tokens, never hard-coded colors.
- Font: Vazir (`font/Vazir.woff2`). Icons: SVG sprite (`Views/Shared/_Icons.cshtml`, `<use href="#i-...">`).
- Components to reuse: `.app/.sidebar/.topbar/.main/.page` shell, `.card*`, `.btn*`, `.tbl*`,
  `.field/.label/.input/.select/.hint`, `.badge` + `.st-*`, `.modal*` (modal-first workflows),
  `.mx-drawer`, `.toast`, `.toolbar`, `.empty`, `.tl*` (timeline), `.tone-*`.
- Behaviours from `js/mx.js` via declarative hooks (`data-open`, `data-close`, `data-dd`, `data-tabs`, …);
  Jalali dates via `js/jalali-picker.js`.
- Sample org-chart page: `docs/reference/org-chart-sample.html` — a **pattern only** (layout and
  features), not a style source. It has its own tokens, BEM classes and a CDN font; none of that is
  used. Re-express everything with MX tokens/classes; where it deviates from MX, MX wins.
  What to take from it:
  - Page: header with actions → toolbar (search, filters, "show inactive" toggle) → two columns:
    tree panel (count, expand/collapse all, type badge per node, keyboard selectable, search expands
    ancestors) and detail panel (unit header with icon/type/path, info grid, tabs: positions,
    people, sub-units, relations).
  - Data ideas: unit type (company, branch, division, department, section, team), unit code,
    level and path, unit validity dates, position type and "managerial" flag, current holder,
    assignment type (primary/acting) with start date.
  - Not taken: personnel number and other HR data (out of scope; `IUserDirectory.Detail` can show it).

## Open questions (to be answered by the user)

1. Data source: edited only in this module, or sometimes synced from another system (HR / existing DB)?
2. Reporting line: is a "manager" of a unit/position needed (workflows, approvals)?
3. Structure history: needed ("what did the chart look like on date X"), or is assignment history enough?
4. Graphical chart view needed, or is a tree list enough? (UI must use MX — see above.)
7. MX assets: does `OrgChart.Razor` ship its own copy of `mx.css`/`mx.js`/icons, or rely on the host layout already loading them?
8. Unit types: fixed enum, or an admin-editable lookup table (better for reuse across projects)?
9. Multiple companies: just root units of type "company" (proposed), or a separate entity?
10. Unit relations (reporting, collaboration, succession — "روابط" tab in the sample): needed in v1?
11. Position categories (managerial, supervisory, expert, administrative): lookup table + `IsManagerial` flag?
5. Project/package names: `OrgChart.*` or something else?
6. Bridge package `OrgChart.Acl` (implements `IOrgStructure`): built in this repo, or written per host app?

## Status / next steps

- [x] Handoff received; CLAUDE.md written.
- [ ] Answer open questions above.
- [ ] Stage 1: data model (`OrgChart.Core` entities + `OrgChart.EFCore` mapping + DDL in `db/`).
