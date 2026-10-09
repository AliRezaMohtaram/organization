# OrgChart — reusable organizational chart module for ASP.NET Core

A standalone, reusable module that stores an organization's **structure**: org units (tree),
positions under units, and which user holds which position over time. It is consumed by other
modules (first consumer: the `Acl` access-control module) through interfaces only.

## Communication & conventions

- Talk to the user in **Persian**. Code, identifiers, comments, and commit messages in **English**.
- Work in **stages**: data model first, then the rest. Ask before changing any architectural decision below.
- **Commit only when the user asks.** (Exception: the cloud session's stop hook requires pushing finished work.)
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
    DateTime? ValidTo)                          // UTC, exclusive
{
    PositionAssignmentKind Kind { get; init; }  // Holder (default), Delegated, Deputy — Acl 0.2.0
    IReadOnlyCollection<int>? RoleIds { get; init; } // delegated/deputy: position roles passed on; null = all
}

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
  - `OrgChart.AspNetCore` — ASP.NET Core integration (current user from HttpContext, etc.)
  - `OrgChart.Razor` — admin pages: Persian, RTL, Razor Pages, plain CSS/JS (no CDN, no SPA
    framework), Jalali (Shamsi) dates, texts in `.resx`
- Single entry point `services.AddOrgChart(o => ...)` lives in `OrgChart.Core` (DI abstractions only, so it
  also works outside ASP.NET Core); stores and integrations plug in via `OrgChartBuilder`, like Acl's `AclBuilder`.
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

## Decisions (answered by the user)

1. Data source: edited only in this module; a key-based import service may come later for syncing.
2. Reporting line: positions have an `IsManagerial` flag; each unit may name one position as its manager
   (`OrgUnit.ManagerPositionId`).
3. No structure versioning. Units and positions have `ValidFrom`/`ValidTo`; history comes from
   assignments and the audit log.
4. UI: tree + detail page first (see the sample); graphical chart later.
5. Names: `OrgChart.*`.
6. Bridge `OrgChart.Acl` lives in this repo as a separate project; only it references Acl.
7. MX assets: admin pages render inside the host's layout by default (host loads MX); an optional
   standalone layout with the module's own copy of MX for hosts without it, chosen in `AddOrgChart`.
8. Unit types: admin-editable lookup table (`OrgUnitType`).
9. Companies: root units whose type is "company"; no separate entity.
10. Unit relations (reporting, collaboration, succession): not in v1.
11. Position categories: admin-editable lookup (`PositionType`) plus the `IsManagerial` flag.
12. Positions form a tree inside their unit: `Position.ParentPositionId`, **same unit only**. Reporting line
    (superior): parent position; else the unit's head (unless it is the head); else the head of the nearest
    ancestor unit that has one (e.g. head of accounting → head of the finance management above it).
13. Unit hierarchy levels: `OrgUnitType.Level` (1 = top; a unit's type needs a greater level than its parent's;
    null = no rule) and `CanBeRoot`.
14. Next features, in order: succession (deputy positions) and delegation (temporary transfer of a position's
    authority, seen by Acl as a temporary assignment); then a separate `Users` module (Identity, MX pages,
    implements `IUserDirectory`; replaceable by AD/SSO later) and one admin panel for users, org chart and Acl
    (Acl's admin UI to move to MX). Partial delegation of single permissions stays in Acl (dated `UserPermission`).
15. Testing: the user runs browser tests; Claude runs automated tests only and writes a manual checklist
    (`docs/test-checklist.md`) instead of driving a browser, unless asked (saves tokens).

## Domain model (src/OrgChart.Core/Model, namespace OrgChart.Core.Model)

- `AuditableEntity` base: CreatedAt/By, UpdatedAt/By (UTC).
- `KeyedEntity` base (for the four entities below): Id, `Key`, `NormalizedKey`, Title, SortOrder, IsActive.
  - `Key` keeps its original spelling; setting it also sets `NormalizedKey` (`OrgKey.Normalize` =
    upper-invariant), which carries the unique index → keys are unique ignoring case on every database.
  - `Key`/`NormalizedKey` have `PropertySaveBehavior.Throw` after save: EF refuses to change a saved key.
  - Key format (`OrgKey.IsValid`): ASCII letters, digits, `.`, `-`, `_`; starts with letter/digit; ≤ 256.
  - `IsActive = false` is the soft delete. Nothing cascades; FKs are all Restrict.
- `OrgUnitType`, `PositionType`: lookup tables.
- `OrgUnit`: Code? (editable, unique when set), TypeId, ParentId?, ManagerPositionId? (unique when set),
  ValidFrom?, ValidTo?.
- `Position`: Code? (editable, unique when set), OrgUnitId, ParentPositionId? (same unit; Restrict), TypeId?,
  IsManagerial, ValidFrom?, ValidTo?.
- `OrgUnitType` (besides the keyed fields): Level? (CHECK ≥ 1), CanBeRoot (default true).
- `Assignment`: Id, PositionId, UserId (string, 450), Kind (`AssignmentKind`: Primary = 1, Acting = 2 —
  persisted as int, never renumber), ValidFrom?, ValidTo?, Note?. Several concurrent assignments per user;
  future-dated rows are planned moves; ending one sets ValidTo (rows are history).
- `AuditLog`: Id (long), At, ActorUserId, Operation, EntityType, EntityId, ChangeJson.
- Validity everywhere: ValidFrom inclusive, ValidTo exclusive, UTC, null = unbounded;
  CHECK `ValidFrom <= ValidTo` on OrgUnits, Positions, Assignments.
- Not enforceable in the database (service layer, stage 2): no cycles in the unit tree; the manager
  position belongs to its unit; keys valid per `OrgKey.IsValid`.
- No materialized path: the whole tree is small (target ≤ a few thousand units) and is loaded and
  cached in memory for descendant/ancestor queries.

## Commands

- Build: `dotnet build`. Test: `dotnet test`.
- Packages: `dotnet pack OrgChart.sln -c Release` → `artifacts/packages` (git-ignored).
- `dotnet-ef` 9.x is a local tool: `dotnet tool restore` first.
- Migration: `dotnet ef migrations add <Name> -p src/OrgChart.EFCore -s src/OrgChart.EFCore`
  (design-time factory `OrgChartDbContextDesignTimeFactory`, LocalDB; never used at runtime).
- After every migration regenerate `db/orgchart-schema.sql`:
  `dotnet ef migrations script -p src/OrgChart.EFCore -s src/OrgChart.EFCore --idempotent`
  and keep the SET-options header (filtered indexes need QUOTED_IDENTIFIER ON).
- Environment note: in the cloud container only the .NET 10 SDK is available. It builds the `net9.0`
  projects fine; run tests and `dotnet ef` with `DOTNET_ROLL_FORWARD=Major` (no 9.0 runtime there).
  Projects stay on `net9.0` and 9.x packages.

## Status / next steps

1. DONE: Handoff, decisions, CLAUDE.md.
2. DONE: Stage 1 — data model.
   - `OrgChart.Core` model, `OrgChart.EFCore` (`OrgChartDbContext`, schema `org`, history table
     `org.__EFMigrationsHistory`, UTC converter), migration `InitialCreate`, `db/orgchart-schema.sql`,
     `db/orgchart-drop.sql` (drops the OrgUnits↔Positions FK first).
   - Tests: `tests/OrgChart.Tests/EFCore` (SQLite in-memory via `SqliteOrgChartDb`).
3. DONE: Stage 2 — Core abstractions and services.
   - Host setup: `services.AddOrgChart().AddSqlServerStore(cs)` (or `.AddEntityFrameworkStore(o => ...)`),
     optional `.AddCurrentUser<T>()` (default `NullCurrentUser`), `.AddUserDirectory<T>()` (default
     `NullUserDirectory`), `.AddChangeListener<T>()` (several allowed, scoped). `TimeProvider` is TryAdd'ed.
   - `OrgChart.Core.Abstractions`: `ICurrentUser`, `IUserDirectory` + `UserInfo` (same shape as Acl's),
     `IOrgChartChangeListener` with `OrgChartChange(Kind, UserIds)`; kinds: `Assignments` (only those users),
     `Structure` (tree/validity/active/manager — anyone), `Details` (titles, codes, sort order, types).
     A bridge to Acl maps Assignments → `BumpUsersAsync`, Structure → `BumpGlobalAsync`, Details → nothing.
   - `OrgChart.Core.Chart`: `OrgChartSnapshot` (pure, immutable, case-insensitive keys): Units in tree order,
     Roots, GetChildren, GetAncestorKeys (nearest first), GetDescendantKeys, GetLevel, GetPath, IsSelfOrDescendant,
     GetPositions, GetManagerChain (unit's manager, then each ancestor's; skips self, missing and inactive
     managers). Missing parent → root; a cycle is cut at its smallest key (becomes a root).
     `Period` helpers ([from, to), null = unbounded).
   - `IOrgChartReader` (EF: `EfOrgChartReader`): GetSnapshotAsync (cached), GetUserPositionsAsync (past/current/
     future; period = assignment ∩ position ∩ unit validity; inactive position/unit or empty period → left out),
     GetUnitAssignmentsAsync (optionally with sub-units), GetHoldersAsync(positionKeys, at),
     GetManagersAsync(userId, at) (nearest manager position in the chain held by someone else).
   - Snapshot cache: singleton `SnapshotCache` keyed by the `org.ChartStamps` row (one row, Id 1, seeded).
     Every read compares the stamp (one query), so other servers see changes at once. Admin operations on
     units/positions/types update the stamp first inside their transaction: invalidates caches everywhere and,
     since the row stays locked until commit, serializes structural changes (cycle checks see committed data).
     Assignment operations do not touch the stamp.
   - `IOrgChartAdministration` (EF: `EfOrgChartAdministration`), all by key: types (create/update/activate, per
     `OrgTypeKind`), units (create/update/move/activate/set manager), positions (create/update/move/activate),
     assignments (assign/update/end/transfer/remove). One transaction each, audit row, listeners notified after
     commit (a listener exception reaches the caller; the change stays saved). No-op changes write no audit
     and notify nobody. On failure the change tracker is cleared so nothing half-applied is saved later.
     Errors: `OrgChartAdminException(code)` with codes in `OrgChartErrors`.
   - Rules: keys per `OrgKey.IsValid` and unique; title required ≤ 256; code ≤ 64, unique per table; parent/type
     must be active when chosen; no cycles; deactivate unit only without active sub-units/positions; reactivate
     needs an active parent/unit; manager position must be an active position of the unit; a unit head cannot be
     moved or deactivated; deactivate position only without current/future assignments; same user cannot hold
     the same position in overlapping periods; End: after start, before the current end (shortening only);
     Transfer = end at EffectiveAt + new open-ended assignment from EffectiveAt. Unspecified DateTime = UTC.
   - Migration `ChartStamps`; schema/drop scripts updated.
   - Tests: tests/OrgChart.Tests/Chart (snapshot), tests/OrgChart.Tests/Integration (`OrgChartServices` = real DI +
     SQLite + manual clock + recording listener; `CreateServer()` simulates a second server on the same DB).
4. DONE: Stage 3 — admin UI (`OrgChart.AspNetCore`, `OrgChart.Razor`, sample host). The `OrgChart.Acl` bridge was
   postponed by the user until the package distribution of Acl is decided (NuGet feed vs. submodule).
   - `OrgChart.AspNetCore`: `AddHttpContextUser(o => o.UserIdClaimType = ...)` → `HttpContextCurrentUser` (NameIdentifier).
   - `OrgChart.Razor`: `AddAdminUi(ui => ...)` adds the RCL as application part (if not discovered) and the policies
     `OrgChart.View` / `OrgChart.Edit` from `OrgChartUiOptions.ViewPolicy/EditPolicy` (default: authenticated user).
     `ui.Layout` default `"_Layout"` (host's, must load MX); `ui.UseStandaloneLayout()` = module's own shell
     (`Shared/_OrgStandaloneLayout.cshtml`, DataMapper's layout with the module's MX copy under `wwwroot/mx`, re-copy
     when MX changes; see `wwwroot/mx/README.md`). `ui.TimeZone` (default server's) for Jalali dates; `ui.BackUrl`.
   - Pages (area "OrgChart", namespace `OrgChart.Razor.Admin.Pages`): `/OrgChart` (tree + detail, `?unit=&tab=positions|people|units&history=true`),
     `Units/{Edit,Move,Manager,Status}`, `Positions/{Edit,Move,Status}`, `Assignments/{Edit,End,Transfer,Remove}`
     (`Edit?handler=Users&q=` = JSON user search via `IUserDirectory`), `Types/{Index,Edit}` (`Edit?handler=Status`), `Audit/Index`.
     View pages: `[Authorize(OrgChart.View)]`; forms: `[Authorize(OrgChart.Edit)]` on GET and POST.
   - Modal-first like DataMapper: links with `data-modal`; `OrgPageModel.Form(partialPath)` returns the `.modal` form
     partial for `X-MX-Modal: 1`, else the full page (`.modal-page`); success → `Done(url, message)` = JSON `{redirect}`
     or a redirect, with the message in TempData (`OrgChart.Flash`), shown as an MX toast by `orgchart.js`.
     Errors: `OrgChartAdminException` → model error → `_FormErrors` callout in the same form.
   - Every page starts with `Shared/_OrgPage.cshtml`: links `css/orgchart.css`, `js/orgchart.js` (defer), the own icon
     sprite `_OrgIcons` (`oc-*` ids, so pages work in any host layout), the flash, and — inside a host layout — the
     page title and a small nav (`.fchips`). No `@section`s are used, so any host layout works.
   - `orgchart.css` uses only MX tokens/components; MX has no tree, so the tree is defined there. `orgchart.js`: tree
     (toggle, expand/collapse all, search + type filter keeping ancestors, inactive toggle remembered in
     localStorage, roving-tabindex keyboard incl. RTL arrows) and MX component `oc-user-search`.
   - Texts: `Resources/OrgText.resx` (neutral = Persian) via `OrgText.Get/Format/Error/Operation/Entity/Kind`. Numbers and
     dates are shown with Persian digits (`OrgFormat`); inputs accept Persian or Latin digits. Dates: `OrgDates`
     (Jalali, whole days; "to"/"last day" inclusive in the UI, stored exclusive). No date picker: MX's jalali-picker
     styles live in the legacy theme.css.
   - Razor gotcha: `data-*` attributes are rendered even when the value is null — use classes (e.g. `is-inactive`).
   - New-unit form suggests a type (most common among siblings, else the type after the parent's) and puts new units
     and positions last (max sort order + 1).
   - `IOrgChartReader.GetAssignmentAsync(id)`; `IOrgChartAuditReader` (EF: `EfOrgChartAuditReader`, newest first, max 200/page).
   - Sample `samples/OrgChart.Sample.Web`: SQLite file (EnsureCreated; `OrgChart:Provider=SqlServer` uses migrations),
     demo seed like the reference page, `DemoUserDirectory`, demo sign-in (every request = `Sample:UserId`),
     `OrgChart:Layout` = Host (its own MX `_Layout`) or Standalone.
   - Tests: `tests/OrgChart.Tests/Razor` — `TextResourceTests` (every error code, audit operation, entity, kind, state
     and every key used in the Razor sources exists) and `AdminPagesTests` on a real host (`AdminUiHost`: TestServer,
     SQLite, header sign-in `X-Test-User`, users "editor*" pass the edit policy): access, all pages render, modal
     protocol, form POST with antiforgery, errors in the form, invalid dates, user search.
   - Checked in Chromium (Playwright) at 1440px and 390px, dark and light, host and standalone layouts; screenshots in
     `docs/screenshots`.
5. DONE: Position tree and unit levels (decisions 12–13). Migration `PositionHierarchyAndTypeLevels`.
   - Snapshot: `PositionNode.ParentKey`; `GetPositions(unit)` in tree order; `GetChildPositions`, `GetParentPositionKey`,
     `GetPositionDepth`, `IsSelfOrSubordinate`, `GetSuperiorKey` (rule above), `GetManagerChain` (walks superiors,
     active only). A parent in another unit is ignored; position cycles are cut like unit cycles (`CutCycles`).
   - Admin rules: parent must be an active position of the same unit, no cycle (`ParentPositionNotInUnit`,
     `ParentPositionCycle`); a unit head cannot have a parent and a position with a parent cannot become head
     (`UnitHeadHasParent`); a position with subordinates cannot move (`PositionHasSubordinates`), with active ones
     cannot be deactivated; moving clears the parent; reactivating needs an active parent. Parent change = Structure.
   - Levels: create/move/change type checked (`TypeLevelNotAllowed`, `TypeCannotBeRoot`), children must fit a new type;
     changing a type's level/root flag is rejected when existing units would break (`TypeLevelConflict`); `InvalidLevel`.
   - UI: tree nests positions under their parents; positions table indented by depth with a "reports to" column and a
     "new subordinate" action (`Positions/Edit?unit=&parent=`); position form has the parent select (heads: hint
     instead); types form/list show Level and CanBeRoot; unit form offers only types allowed under the chosen parent
     (MX component `oc-unit-form`), move form only allowed parents (`OrgOptions.ParentsFor/Fits/PositionParents`).
   - Sample seed: type levels 1–6 (only companies are roots) and the oil-accounting example under DP-ACC.
     Delete `orgchart-sample.db` to reseed.
   - Fixes after the user's test: display nests every top position under the unit head
     (`GetUnitTopPositions`, `GetReportingChildren`, `GetPositionOutline` — display only, data unchanged; a moved
     position therefore appears under the head of its new unit). Ending an assignment offers "now" (default; the
     position is vacant at once) or "on a date" (last day inclusive); holders with an end date show "until …".
   - Moving a position means choosing its new superior position (user's request): `MovePositionUnderAsync(key,
     superiorKey)` moves the position **with its whole branch** into the superior's unit (parent links inside the
     branch kept); `MovePositionAsync(key, unitKey)` puts the branch at the top of a unit (for units without a head).
     Rejected: target inside the branch (`ParentPositionCycle`), a unit head anywhere in the branch
     (`PositionIsUnitManager`). Move form lists positions by unit in outline order, plus "top of unit" for units
     without an active head. Audit row lists the moved branch.
6. DONE: Succession and delegation (decision 14). Migration `Delegations`. User answers: deputy authority is always
   active within its scope; scope = Acl roles; both admin and the holder record delegations; no maximum duration.
   The delegator/holder keeps their own authority; a vacant position's authority is granted by the system admin.
   - Model: `Delegation` (Kind ToUser = person-to-person, needs From/To user and ValidTo; ToPosition = standing deputy
     position with Priority), `DelegationScope` (AuthorityKey rows; none + `IsFullScope` = everything).
     CHECK `CK_Delegations_Target`. `AssignmentKind.Delegated = 3`, `Deputy = 4` exist only in reader output.
   - `IAuthorityCatalog` (`AddAuthorityCatalog<T>()`, default `NullAuthorityCatalog` = full scope only) lists what can be
     delegated per position; the Acl bridge will list the position's roles (keys = Acl role ids).
   - Admin: `DelegateAsync`, `AddDeputyAsync`, `UpdateDelegationAsync`, `EndDelegationAsync`, `RemoveDelegationAsync`.
     Rules: delegator holds the position sometime in the period; not to self; no overlapping duplicate; scope keys
     must be in the catalog and non-empty. Changes notify `Assignments` for the recipient / deputy-position holders;
     assignment changes also notify users who receive the position's authority (`AddDependentUsersAsync`).
   - Reader: `GetUserPositionsAsync` adds Delegated entries (delegation ∩ delegator's holding) and Deputy entries
     (deputy's holding ∩ deputy period ∩ periods the position has a holder), with `AuthorityKeys` (null = full) and
     `DelegationId`. `GetDelegationsAsync(DelegationQuery)`, `GetDelegationAsync(id)`.
   - UI: unit page tab "delegations"; positions table shows deputies and current delegations with row actions;
     `Delegations/{Edit,End}` (`End?mode=remove` deletes). Self-service `/OrgChart/My` (policy `OrgChart.Self`,
     `ui.SelfPolicy`, default authenticated): my positions, given (end now; not-yet-started ones are deleted), received;
     `My/Delegate?position=` only for positions the user holds now or later (else 404). Shared `_ScopeFields` partial.
   - Sample: deputy and a 30-day delegation on POS-PUR-MGR; `DemoAuthorityCatalog` offers three demo authorities.
7. DONE: Acl 0.2.0 (pushed to Acl's `master`, user's choice) and the `OrgChart.Acl` bridge.
   - Acl: `PositionAssignment.Kind`/`RoleIds` (see contract above); a delegate/deputy gets only the position's
     PositionRoles filtered by RoleIds, in the position's unit, no OrgUnitRoles, and does not count as a member of the
     unit for user data-scope rules. `IAssignmentAdministration.GetPositionRolesAsync(positionKey)`.
   - Distribution (user's choice): local NuGet feed. `OrgChart.Acl` and `tests/OrgChart.Acl.Tests` add
     `$(AclPackages)` (default `../Acl/artifacts/packages`, overridable by property/env var) via
     `RestoreAdditionalProjectSources`; pack Acl first (`dotnet pack Acl.sln -c Release`). In the cloud container the
     clone is `/home/user/acl`: run with `AclPackages=/home/user/acl/artifacts/packages`.
   - Bridge: `OrgChartBuilder.AddAcl()` = `OrgChartOrgStructure` (replaces Acl's `NullOrgStructure`; active units and
     positions for pickers; Primary/Acting → Holder, Delegated/Deputy mapped, AuthorityKeys parsed as role ids),
     `AclStampChangeListener` (Assignments → BumpUsers, Structure → BumpGlobal, Details → nothing),
     `AclAuthorityCatalog` (authority key = Acl role id, title = role name). `AclBuilder.AddOrgChart()` for the Acl side only.
   - Tests: `tests/OrgChart.Acl.Tests` (both modules in one container, each on its own SQLite connection):
     holder/delegate/deputy access, immediate effect of ending, catalog, stamps, IOrgStructure lists.
8. Users and the host (user decisions): no separate admin panel — the host app (first: DataMapper/Mapper,
   github.com/AliRezaMohtaram/DataMapper) is the integration point. Mapper had no sign-in, so the Users module was built
   inside its solution but self-contained (`Borc.Users`, `Borc.Users.Web`; Identity, `long` user ids, schema `usr`,
   sign-in by user name or e-mail, no public registration; pushed to Mapper's `master`). OrgChart/Acl store the id as a
   string. A host `IUserDirectory` over `IUserLookup` serves both modules.
9. NEXT: Acl admin UI to MX; a shared menu contract (module pages in the host sidebar); connect OrgChart + Acl to Mapper
   (`AddOrgChart().AddAcl()`, `IUserDirectory` adapter, end assignments on user deactivation via `IUserStatusListener`).
   Later: graphical chart; NuGet packaging.
