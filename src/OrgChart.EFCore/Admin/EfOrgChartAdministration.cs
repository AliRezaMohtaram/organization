using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using OrgChart.Core.Abstractions;
using OrgChart.Core.Admin;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;
using OrgChart.EFCore.Configurations;
using OrgChart.EFCore.Services;

namespace OrgChart.EFCore.Admin;

internal sealed class EfOrgChartAdministration(
    OrgChartDbContext db,
    AuditWriter audit,
    SnapshotCache cache,
    TimeProvider timeProvider,
    IEnumerable<IOrgChartChangeListener> listeners) : IOrgChartAdministration
{
    // ---------------------------------------------------------------- types

    public Task CreateTypeAsync(OrgTypeKind kind, OrgTypeInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        return RunAsync(chart: true, async change =>
        {
            string key = RequireKey(input.Key);
            string normalized = OrgKey.Normalize(key);
            if (await Types(kind).AnyAsync(t => t.NormalizedKey == normalized, cancellationToken))
            {
                throw Error(OrgChartErrors.KeyTaken, $"A {kind} type with key '{key}' already exists.");
            }

            KeyedEntity type = kind == OrgTypeKind.Unit
                ? new OrgUnitType { Level = RequireLevel(input.Level), CanBeRoot = input.CanBeRoot }
                : new PositionType();
            type.Key = key;
            type.Title = RequireTitle(input.Title);
            type.SortOrder = input.SortOrder;
            db.Add(type);

            audit.Write("TypeCreated", TypeEntity(kind), type.Key, new { after = TypeState(type) });
            change.Kind = OrgChartChangeKind.Details;
        }, cancellationToken);
    }

    public Task UpdateTypeAsync(OrgTypeKind kind, string key, OrgTypeUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        return RunAsync(chart: true, async change =>
        {
            KeyedEntity type = await FindTypeAsync(kind, key, cancellationToken);
            object before = TypeState(type);
            type.Title = RequireTitle(update.Title);
            type.SortOrder = update.SortOrder;
            if (type is OrgUnitType unitType)
            {
                unitType.Level = RequireLevel(update.Level);
                unitType.CanBeRoot = update.CanBeRoot;
                await EnsureTypeFitsExistingUnitsAsync(unitType, cancellationToken);
            }

            if (Changed(before, TypeState(type)))
            {
                audit.Write("TypeUpdated", TypeEntity(kind), type.Key, new { before, after = TypeState(type) });
                change.Kind = OrgChartChangeKind.Details;
            }
        }, cancellationToken);
    }

    public Task SetTypeActiveAsync(OrgTypeKind kind, string key, bool isActive, CancellationToken cancellationToken = default) =>
        RunAsync(chart: true, async change =>
        {
            KeyedEntity type = await FindTypeAsync(kind, key, cancellationToken);
            if (type.IsActive != isActive)
            {
                type.IsActive = isActive;
                audit.Write(isActive ? "TypeActivated" : "TypeDeactivated", TypeEntity(kind), type.Key, new { type.IsActive });
                change.Kind = OrgChartChangeKind.Details;
            }
        }, cancellationToken);

    // ---------------------------------------------------------------- units

    public Task CreateUnitAsync(UnitInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        return RunAsync(chart: true, async change =>
        {
            string key = RequireKey(input.Key);
            string normalized = OrgKey.Normalize(key);
            if (await db.OrgUnits.AnyAsync(u => u.NormalizedKey == normalized, cancellationToken))
            {
                throw Error(OrgChartErrors.KeyTaken, $"A unit with key '{key}' already exists.");
            }

            OrgUnit? parent = null;
            if (input.ParentKey is not null)
            {
                parent = await db.OrgUnits.Include(u => u.Type)
                        .SingleOrDefaultAsync(u => u.NormalizedKey == OrgKey.Normalize(input.ParentKey), cancellationToken)
                    ?? throw Error(OrgChartErrors.ParentNotFound, $"Parent unit '{input.ParentKey}' does not exist.");
                if (!parent.IsActive)
                {
                    throw Error(OrgChartErrors.ParentInactive, $"Parent unit '{parent.Key}' is inactive.");
                }
            }

            (DateTime? from, DateTime? to) = RequirePeriod(input.ValidFrom, input.ValidTo);
            OrgUnit unit = new()
            {
                Key = key,
                Title = RequireTitle(input.Title),
                Code = await UnitCodeAsync(input.Code, null, cancellationToken),
                Type = (OrgUnitType)await FindActiveTypeAsync(OrgTypeKind.Unit, input.TypeKey, cancellationToken),
                Parent = parent,
                SortOrder = input.SortOrder,
                ValidFrom = from,
                ValidTo = to,
            };
            EnsureLevelFits(unit.Type, parent);
            db.OrgUnits.Add(unit);

            audit.Write("UnitCreated", nameof(OrgUnit), unit.Key, new { after = UnitState(unit) });
            change.Kind = OrgChartChangeKind.Structure;
        }, cancellationToken);
    }

    public Task UpdateUnitAsync(string key, UnitUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        return RunAsync(chart: true, async change =>
        {
            OrgUnit unit = await FindUnitAsync(key, cancellationToken);
            object before = UnitState(unit);
            (DateTime? oldFrom, DateTime? oldTo) = (unit.ValidFrom, unit.ValidTo);

            unit.Title = RequireTitle(update.Title);
            unit.Code = await UnitCodeAsync(update.Code, unit.Id, cancellationToken);
            unit.SortOrder = update.SortOrder;
            (unit.ValidFrom, unit.ValidTo) = RequirePeriod(update.ValidFrom, update.ValidTo);
            if (!OrgKey.AreEqual(unit.Type.Key, update.TypeKey))
            {
                unit.Type = (OrgUnitType)await FindActiveTypeAsync(OrgTypeKind.Unit, update.TypeKey, cancellationToken);
                if (unit.ParentId is not null)
                {
                    await db.Entry(unit).Reference(u => u.Parent).Query().Include(u => u.Type).LoadAsync(cancellationToken);
                }

                EnsureLevelFits(unit.Type, unit.Parent);
                await EnsureChildrenFitAsync(unit.Id, unit.Type.Level, cancellationToken);
            }

            object after = UnitState(unit);
            if (Changed(before, after))
            {
                audit.Write("UnitUpdated", nameof(OrgUnit), unit.Key, new { before, after });
                change.Kind = unit.ValidFrom != oldFrom || unit.ValidTo != oldTo
                    ? OrgChartChangeKind.Structure
                    : OrgChartChangeKind.Details;
            }
        }, cancellationToken);
    }

    public Task MoveUnitAsync(string key, string? newParentKey, CancellationToken cancellationToken = default) =>
        RunAsync(chart: true, async change =>
        {
            OrgUnit unit = await FindUnitAsync(key, cancellationToken);
            OrgUnit? parent = null;
            if (newParentKey is not null)
            {
                parent = await db.OrgUnits.Include(u => u.Type)
                        .SingleOrDefaultAsync(u => u.NormalizedKey == OrgKey.Normalize(newParentKey), cancellationToken)
                    ?? throw Error(OrgChartErrors.ParentNotFound, $"Parent unit '{newParentKey}' does not exist.");
            }

            if (parent?.Id == unit.ParentId)
            {
                return;
            }

            if (parent is not null)
            {
                if (!parent.IsActive)
                {
                    throw Error(OrgChartErrors.ParentInactive, $"Parent unit '{parent.Key}' is inactive.");
                }

                if (await IsSelfOrDescendantAsync(parent.Id, unit.Id, cancellationToken))
                {
                    throw Error(OrgChartErrors.ParentCycle, $"Unit '{parent.Key}' is '{unit.Key}' or below it.");
                }
            }

            EnsureLevelFits(unit.Type, parent);
            string? from = unit.Parent?.Key;
            unit.Parent = parent;
            audit.Write("UnitMoved", nameof(OrgUnit), unit.Key, new { before = new { Parent = from }, after = new { Parent = parent?.Key } });
            change.Kind = OrgChartChangeKind.Structure;
        }, cancellationToken);

    public Task SetUnitActiveAsync(string key, bool isActive, CancellationToken cancellationToken = default) =>
        RunAsync(chart: true, async change =>
        {
            OrgUnit unit = await FindUnitAsync(key, cancellationToken);
            if (unit.IsActive == isActive)
            {
                return;
            }

            if (isActive)
            {
                if (unit.Parent is { IsActive: false })
                {
                    throw Error(OrgChartErrors.ParentInactive, $"Parent unit '{unit.Parent.Key}' is inactive.");
                }
            }
            else
            {
                if (await db.OrgUnits.AnyAsync(u => u.ParentId == unit.Id && u.IsActive, cancellationToken))
                {
                    throw Error(OrgChartErrors.UnitHasActiveChildren, $"Unit '{unit.Key}' has active sub-units.");
                }

                if (await db.Positions.AnyAsync(p => p.OrgUnitId == unit.Id && p.IsActive, cancellationToken))
                {
                    throw Error(OrgChartErrors.UnitHasActivePositions, $"Unit '{unit.Key}' has active positions.");
                }
            }

            unit.IsActive = isActive;
            audit.Write(isActive ? "UnitActivated" : "UnitDeactivated", nameof(OrgUnit), unit.Key, new { unit.IsActive });
            change.Kind = OrgChartChangeKind.Structure;
        }, cancellationToken);

    public Task SetUnitManagerAsync(string unitKey, string? positionKey, CancellationToken cancellationToken = default) =>
        RunAsync(chart: true, async change =>
        {
            OrgUnit unit = await FindUnitAsync(unitKey, cancellationToken);
            Position? manager = null;
            if (positionKey is not null)
            {
                manager = await FindPositionAsync(positionKey, cancellationToken);
                if (manager.OrgUnitId != unit.Id)
                {
                    throw Error(OrgChartErrors.ManagerPositionNotInUnit, $"Position '{manager.Key}' is not in unit '{unit.Key}'.");
                }

                if (!manager.IsActive)
                {
                    throw Error(OrgChartErrors.PositionInactive, $"Position '{manager.Key}' is inactive.");
                }

                if (manager.ParentPositionId is not null)
                {
                    throw Error(OrgChartErrors.UnitHeadHasParent, $"Position '{manager.Key}' reports to another position of the unit; a head cannot.");
                }
            }

            if (manager?.Id == unit.ManagerPositionId)
            {
                return;
            }

            string? before = unit.ManagerPosition?.Key;
            unit.ManagerPosition = manager;
            audit.Write("UnitManagerChanged", nameof(OrgUnit), unit.Key,
                new { before = new { Manager = before }, after = new { Manager = manager?.Key } });
            change.Kind = OrgChartChangeKind.Structure;
        }, cancellationToken);

    // ---------------------------------------------------------------- positions

    public Task CreatePositionAsync(PositionInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        return RunAsync(chart: true, async change =>
        {
            string key = RequireKey(input.Key);
            string normalized = OrgKey.Normalize(key);
            if (await db.Positions.AnyAsync(p => p.NormalizedKey == normalized, cancellationToken))
            {
                throw Error(OrgChartErrors.KeyTaken, $"A position with key '{key}' already exists.");
            }

            OrgUnit unit = await FindUnitAsync(input.UnitKey, cancellationToken);
            if (!unit.IsActive)
            {
                throw Error(OrgChartErrors.UnitInactive, $"Unit '{unit.Key}' is inactive.");
            }

            (DateTime? from, DateTime? to) = RequirePeriod(input.ValidFrom, input.ValidTo);
            Position position = new()
            {
                Key = key,
                Title = RequireTitle(input.Title),
                Code = await PositionCodeAsync(input.Code, null, cancellationToken),
                OrgUnit = unit,
                Type = input.TypeKey is null ? null : (PositionType)await FindActiveTypeAsync(OrgTypeKind.Position, input.TypeKey, cancellationToken),
                IsManagerial = input.IsManagerial,
                SortOrder = input.SortOrder,
                ValidFrom = from,
                ValidTo = to,
                ParentPosition = await ParentPositionAsync(input.ParentPositionKey, unit.Id, null, cancellationToken),
            };
            db.Positions.Add(position);

            audit.Write("PositionCreated", nameof(Position), position.Key, new { after = PositionState(position) });
            change.Kind = OrgChartChangeKind.Structure;
        }, cancellationToken);
    }

    public Task UpdatePositionAsync(string key, PositionUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        return RunAsync(chart: true, async change =>
        {
            Position position = await FindPositionAsync(key, cancellationToken);
            object before = PositionState(position);
            (DateTime? oldFrom, DateTime? oldTo) = (position.ValidFrom, position.ValidTo);

            position.Title = RequireTitle(update.Title);
            position.Code = await PositionCodeAsync(update.Code, position.Id, cancellationToken);
            position.IsManagerial = update.IsManagerial;
            position.SortOrder = update.SortOrder;
            (position.ValidFrom, position.ValidTo) = RequirePeriod(update.ValidFrom, update.ValidTo);
            if (!OrgKey.AreEqual(position.Type?.Key, update.TypeKey))
            {
                position.Type = update.TypeKey is null
                    ? null
                    : (PositionType)await FindActiveTypeAsync(OrgTypeKind.Position, update.TypeKey, cancellationToken);
            }

            int? oldParent = position.ParentPositionId;
            position.ParentPosition = await ParentPositionAsync(update.ParentPositionKey, position.OrgUnitId, position, cancellationToken);
            if (position.ParentPosition is not null && await HeadsUnitAsync(position.Id, cancellationToken))
            {
                throw Error(OrgChartErrors.UnitHeadHasParent, $"Position '{position.Key}' heads its unit and cannot report to a position of it.");
            }

            object after = PositionState(position);
            if (Changed(before, after))
            {
                audit.Write("PositionUpdated", nameof(Position), position.Key, new { before, after });
                change.Kind = position.ValidFrom != oldFrom || position.ValidTo != oldTo || position.ParentPosition?.Id != oldParent
                    ? OrgChartChangeKind.Structure
                    : OrgChartChangeKind.Details;
            }
        }, cancellationToken);
    }

    public Task MovePositionAsync(string key, string newUnitKey, CancellationToken cancellationToken = default) =>
        RunAsync(chart: true, async change =>
        {
            Position position = await FindPositionAsync(key, cancellationToken);
            OrgUnit unit = await FindUnitAsync(newUnitKey, cancellationToken);
            if (unit.Id == position.OrgUnitId)
            {
                return;
            }

            if (!unit.IsActive)
            {
                throw Error(OrgChartErrors.UnitInactive, $"Unit '{unit.Key}' is inactive.");
            }

            if (await HeadsUnitAsync(position.Id, cancellationToken))
            {
                throw Error(OrgChartErrors.PositionIsUnitManager, $"Position '{position.Key}' heads its unit.");
            }

            if (await db.Positions.AnyAsync(p => p.ParentPositionId == position.Id, cancellationToken))
            {
                throw Error(OrgChartErrors.PositionHasSubordinates, $"Position '{position.Key}' has positions reporting to it.");
            }

            // The parent must be in the same unit, so a moved position starts at the top of its new unit.
            string from = position.OrgUnit.Key;
            string? fromParent = position.ParentPosition?.Key;
            position.OrgUnit = unit;
            position.ParentPosition = null;
            audit.Write("PositionMoved", nameof(Position), position.Key,
                new { before = new { Unit = from, Parent = fromParent }, after = new { Unit = unit.Key, Parent = (string?)null } });
            change.Kind = OrgChartChangeKind.Structure;
        }, cancellationToken);

    public Task SetPositionActiveAsync(string key, bool isActive, CancellationToken cancellationToken = default) =>
        RunAsync(chart: true, async change =>
        {
            Position position = await FindPositionAsync(key, cancellationToken);
            if (position.IsActive == isActive)
            {
                return;
            }

            if (isActive)
            {
                if (!position.OrgUnit.IsActive)
                {
                    throw Error(OrgChartErrors.UnitInactive, $"Unit '{position.OrgUnit.Key}' is inactive.");
                }

                if (position.ParentPosition is { IsActive: false })
                {
                    throw Error(OrgChartErrors.PositionInactive, $"Parent position '{position.ParentPosition.Key}' is inactive.");
                }
            }
            else
            {
                if (await db.Positions.AnyAsync(p => p.ParentPositionId == position.Id && p.IsActive, cancellationToken))
                {
                    throw Error(OrgChartErrors.PositionHasSubordinates, $"Position '{position.Key}' has active positions reporting to it.");
                }

                if (await HeadsUnitAsync(position.Id, cancellationToken))
                {
                    throw Error(OrgChartErrors.PositionIsUnitManager, $"Position '{position.Key}' heads its unit.");
                }

                DateTime now = UtcNow();
                if (await db.Assignments.AnyAsync(a => a.PositionId == position.Id && (a.ValidTo == null || a.ValidTo > now), cancellationToken))
                {
                    throw Error(OrgChartErrors.PositionHasAssignments, $"Position '{position.Key}' has current or future assignments.");
                }
            }

            position.IsActive = isActive;
            audit.Write(isActive ? "PositionActivated" : "PositionDeactivated", nameof(Position), position.Key, new { position.IsActive });
            change.Kind = OrgChartChangeKind.Structure;
        }, cancellationToken);

    // ---------------------------------------------------------------- assignments

    public Task<int> AssignAsync(AssignmentInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        return RunAsync(chart: false, async change =>
        {
            Position position = await FindActivePositionAsync(input.PositionKey, cancellationToken);
            (DateTime? from, DateTime? to) = RequirePeriod(input.ValidFrom, input.ValidTo);
            Assignment assignment = new()
            {
                Position = position,
                UserId = RequireUserId(input.UserId),
                Kind = RequireKind(input.Kind),
                ValidFrom = from,
                ValidTo = to,
                Note = OptionalNote(input.Note),
            };
            await EnsureNoOverlapAsync(assignment, cancellationToken);
            db.Assignments.Add(assignment);
            await db.SaveChangesAsync(cancellationToken);

            audit.Write("AssignmentCreated", nameof(Assignment), assignment.Id, new { after = AssignmentState(assignment) });
            change.Assignments(assignment.UserId);
            return assignment.Id;
        }, cancellationToken);
    }

    public Task UpdateAssignmentAsync(int assignmentId, AssignmentUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        return RunAsync(chart: false, async change =>
        {
            Assignment assignment = await FindAssignmentAsync(assignmentId, cancellationToken);
            object before = AssignmentState(assignment);
            assignment.Kind = RequireKind(update.Kind);
            (assignment.ValidFrom, assignment.ValidTo) = RequirePeriod(update.ValidFrom, update.ValidTo);
            assignment.Note = OptionalNote(update.Note);

            object after = AssignmentState(assignment);
            if (Changed(before, after))
            {
                await EnsureNoOverlapAsync(assignment, cancellationToken);
                audit.Write("AssignmentUpdated", nameof(Assignment), assignment.Id, new { before, after });
                change.Assignments(assignment.UserId);
            }
        }, cancellationToken);
    }

    public Task EndAssignmentAsync(int assignmentId, DateTime endAtUtc, CancellationToken cancellationToken = default) =>
        RunAsync(chart: false, async change =>
        {
            Assignment assignment = await FindAssignmentAsync(assignmentId, cancellationToken);
            object before = AssignmentState(assignment);
            End(assignment, endAtUtc);

            audit.Write("AssignmentEnded", nameof(Assignment), assignment.Id, new { before, after = AssignmentState(assignment) });
            change.Assignments(assignment.UserId);
        }, cancellationToken);

    public Task<int> TransferAsync(int assignmentId, TransferInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        return RunAsync(chart: false, async change =>
        {
            Assignment current = await FindAssignmentAsync(assignmentId, cancellationToken);
            Position target = await FindActivePositionAsync(input.ToPositionKey, cancellationToken);
            object before = AssignmentState(current);
            DateTime at = End(current, input.EffectiveAt);
            await db.SaveChangesAsync(cancellationToken);

            Assignment next = new()
            {
                Position = target,
                UserId = current.UserId,
                Kind = RequireKind(input.Kind),
                ValidFrom = at,
                Note = OptionalNote(input.Note),
            };
            await EnsureNoOverlapAsync(next, cancellationToken);
            db.Assignments.Add(next);
            await db.SaveChangesAsync(cancellationToken);

            audit.Write("AssignmentTransferred", nameof(Assignment), current.Id,
                new { before, after = AssignmentState(current), next = new { next.Id, State = AssignmentState(next) } });
            change.Assignments(current.UserId);
            return next.Id;
        }, cancellationToken);
    }

    public Task RemoveAssignmentAsync(int assignmentId, CancellationToken cancellationToken = default) =>
        RunAsync(chart: false, async change =>
        {
            Assignment assignment = await FindAssignmentAsync(assignmentId, cancellationToken);
            audit.Write("AssignmentRemoved", nameof(Assignment), assignment.Id, new { before = AssignmentState(assignment) });
            db.Assignments.Remove(assignment);
            change.Assignments(assignment.UserId);
        }, cancellationToken);

    // ---------------------------------------------------------------- unit of work

    private sealed class Change
    {
        /// <summary>Null when the operation turned out to change nothing.</summary>
        public OrgChartChangeKind? Kind { get; set; }

        public HashSet<string> UserIds { get; } = new(StringComparer.Ordinal);

        public void Assignments(string userId)
        {
            Kind = OrgChartChangeKind.Assignments;
            UserIds.Add(userId);
        }
    }

    private async Task RunAsync(bool chart, Func<Change, Task> work, CancellationToken cancellationToken) =>
        await RunAsync<object?>(chart, async change =>
        {
            await work(change);
            return null;
        }, cancellationToken);

    /// <param name="chart">
    /// The operation changes units, positions or types: bump the chart stamp first. That invalidates cached
    /// snapshots on every server and, because the stamp row stays locked until commit, serializes structural
    /// changes so validation (e.g. the cycle check) always sees committed data.
    /// </param>
    private async Task<T> RunAsync<T>(bool chart, Func<Change, Task<T>> work, CancellationToken cancellationToken)
    {
        Change change = new();
        T result;
        try
        {
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (chart)
            {
                Guid stamp = Guid.NewGuid();
                DateTime now = UtcNow();
                await db.ChartStamps
                    .Where(s => s.Id == ChartStamp.SingletonId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Stamp, stamp).SetProperty(x => x.UpdatedAt, now), cancellationToken);
            }

            result = await work(change);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // The transaction is rolled back; drop the half-applied edits so a later save in this scope cannot write them.
            db.ChangeTracker.Clear();
            throw;
        }

        if (chart)
        {
            cache.Clear();
        }

        if (change.Kind is { } kind)
        {
            OrgChartChange notice = new(kind, change.UserIds.ToList());
            foreach (IOrgChartChangeListener listener in listeners)
            {
                await listener.OnChangedAsync(notice, cancellationToken);
            }
        }

        return result;
    }

    // ---------------------------------------------------------------- lookups

    private IQueryable<KeyedEntity> Types(OrgTypeKind kind) => kind switch
    {
        OrgTypeKind.Unit => db.OrgUnitTypes,
        OrgTypeKind.Position => db.PositionTypes,
        _ => throw Error(OrgChartErrors.InvalidKind, $"Type kind '{kind}' is not valid."),
    };

    private async Task<KeyedEntity> FindTypeAsync(OrgTypeKind kind, string? key, CancellationToken cancellationToken)
    {
        string normalized = OrgKey.Normalize(key ?? "");
        return await Types(kind).SingleOrDefaultAsync(t => t.NormalizedKey == normalized, cancellationToken)
            ?? throw Error(OrgChartErrors.TypeNotFound, $"{kind} type '{key}' does not exist.");
    }

    private async Task<KeyedEntity> FindActiveTypeAsync(OrgTypeKind kind, string? key, CancellationToken cancellationToken)
    {
        KeyedEntity type = await FindTypeAsync(kind, key, cancellationToken);
        return type.IsActive ? type : throw Error(OrgChartErrors.TypeInactive, $"{kind} type '{type.Key}' is inactive.");
    }

    private async Task<OrgUnit> FindUnitAsync(string? key, CancellationToken cancellationToken)
    {
        string normalized = OrgKey.Normalize(key ?? "");
        return await db.OrgUnits
                .Include(u => u.Type)
                .Include(u => u.Parent)
                .Include(u => u.ManagerPosition)
                .SingleOrDefaultAsync(u => u.NormalizedKey == normalized, cancellationToken)
            ?? throw Error(OrgChartErrors.UnitNotFound, $"Unit '{key}' does not exist.");
    }

    private async Task<Position> FindPositionAsync(string? key, CancellationToken cancellationToken)
    {
        string normalized = OrgKey.Normalize(key ?? "");
        return await db.Positions
                .Include(p => p.OrgUnit)
                .Include(p => p.Type)
                .Include(p => p.ParentPosition)
                .SingleOrDefaultAsync(p => p.NormalizedKey == normalized, cancellationToken)
            ?? throw Error(OrgChartErrors.PositionNotFound, $"Position '{key}' does not exist.");
    }

    private async Task<Position> FindActivePositionAsync(string? key, CancellationToken cancellationToken)
    {
        Position position = await FindPositionAsync(key, cancellationToken);
        return position.IsActive ? position : throw Error(OrgChartErrors.PositionInactive, $"Position '{position.Key}' is inactive.");
    }

    private async Task<Assignment> FindAssignmentAsync(int id, CancellationToken cancellationToken) =>
        await db.Assignments.Include(a => a.Position).SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
        ?? throw Error(OrgChartErrors.AssignmentNotFound, $"Assignment {id} does not exist.");

    /// <summary>
    /// The parent position for <paramref name="key"/>: null for none; else an active position of the same unit that is
    /// not <paramref name="self"/> or below it.
    /// </summary>
    private async Task<Position?> ParentPositionAsync(string? key, int unitId, Position? self, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        string normalized = OrgKey.Normalize(key.Trim());
        Position parent = await db.Positions.SingleOrDefaultAsync(p => p.NormalizedKey == normalized, cancellationToken)
            ?? throw Error(OrgChartErrors.PositionNotFound, $"Position '{key}' does not exist.");
        if (parent.OrgUnitId != unitId)
        {
            throw Error(OrgChartErrors.ParentPositionNotInUnit, $"Position '{parent.Key}' is not in the same unit.");
        }

        if (!parent.IsActive)
        {
            throw Error(OrgChartErrors.PositionInactive, $"Position '{parent.Key}' is inactive.");
        }

        if (self is not null)
        {
            Dictionary<int, int?> parents = await db.Positions.AsNoTracking()
                .Where(p => p.OrgUnitId == unitId)
                .ToDictionaryAsync(p => p.Id, p => p.ParentPositionId, cancellationToken);
            HashSet<int> seen = [];
            for (int? id = parent.Id; id is { } current && seen.Add(current); id = parents.GetValueOrDefault(current))
            {
                if (current == self.Id)
                {
                    throw Error(OrgChartErrors.ParentPositionCycle, $"Position '{parent.Key}' is '{self.Key}' or below it.");
                }
            }
        }

        return parent;
    }

    /// <summary>The unit type's level must be greater than its parent's; without a parent the type must allow roots.</summary>
    private static void EnsureLevelFits(OrgUnitType type, OrgUnit? parent)
    {
        if (parent is null)
        {
            if (!type.CanBeRoot)
            {
                throw Error(OrgChartErrors.TypeCannotBeRoot, $"Units of type '{type.Key}' cannot be roots.");
            }
        }
        else if (!OrgUnitType.AllowsUnder(type.Level, parent.Type.Level))
        {
            throw Error(OrgChartErrors.TypeLevelNotAllowed,
                $"A unit of type '{type.Key}' (level {type.Level}) cannot be under '{parent.Key}' of type '{parent.Type.Key}' (level {parent.Type.Level}).");
        }
    }

    private async Task EnsureChildrenFitAsync(int unitId, int? level, CancellationToken cancellationToken)
    {
        List<int?> childLevels = await db.OrgUnits.Where(u => u.ParentId == unitId).Select(u => u.Type.Level).ToListAsync(cancellationToken);
        if (childLevels.Any(child => !OrgUnitType.AllowsUnder(child, level)))
        {
            throw Error(OrgChartErrors.TypeLevelNotAllowed, $"Sub-units of unit {unitId} do not fit under level {level}.");
        }
    }

    /// <summary>A changed level or root flag must keep every existing unit valid.</summary>
    private async Task EnsureTypeFitsExistingUnitsAsync(OrgUnitType type, CancellationToken cancellationToken)
    {
        var units = await db.OrgUnits.AsNoTracking()
            .Select(u => new { u.Id, u.ParentId, u.TypeId, u.Type.Level })
            .ToListAsync(cancellationToken);
        Dictionary<int, int?> levels = units.ToDictionary(u => u.Id, u => u.TypeId == type.Id ? type.Level : u.Level);

        foreach (var unit in units)
        {
            bool broken = unit.ParentId is { } parentId
                ? (unit.TypeId == type.Id || units.Any(p => p.Id == parentId && p.TypeId == type.Id))
                    && !OrgUnitType.AllowsUnder(levels[unit.Id], levels[parentId])
                : unit.TypeId == type.Id && !type.CanBeRoot;
            if (broken)
            {
                throw Error(OrgChartErrors.TypeLevelConflict, $"Existing units would break the hierarchy rules of type '{type.Key}'.");
            }
        }
    }

    private static int? RequireLevel(int? level) =>
        level is null or >= 1 ? level : throw Error(OrgChartErrors.InvalidLevel, $"Level {level} is not valid; use 1 or more.");

    private Task<bool> HeadsUnitAsync(int positionId, CancellationToken cancellationToken) =>
        db.OrgUnits.AnyAsync(u => u.ManagerPositionId == positionId, cancellationToken);

    /// <summary>Walks up from <paramref name="unitId"/>; true when it reaches <paramref name="ancestorId"/>.</summary>
    private async Task<bool> IsSelfOrDescendantAsync(int unitId, int ancestorId, CancellationToken cancellationToken)
    {
        Dictionary<int, int?> parents = await db.OrgUnits.AsNoTracking()
            .ToDictionaryAsync(u => u.Id, u => u.ParentId, cancellationToken);

        HashSet<int> seen = [];
        for (int? id = unitId; id is { } current && seen.Add(current); id = parents.GetValueOrDefault(current))
        {
            if (current == ancestorId)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<string?> UnitCodeAsync(string? code, int? exceptId, CancellationToken cancellationToken)
    {
        string? value = OptionalCode(code);
        if (value is not null && await db.OrgUnits.AnyAsync(u => u.Code == value && u.Id != exceptId, cancellationToken))
        {
            throw Error(OrgChartErrors.CodeTaken, $"Unit code '{value}' is already used.");
        }

        return value;
    }

    private async Task<string?> PositionCodeAsync(string? code, int? exceptId, CancellationToken cancellationToken)
    {
        string? value = OptionalCode(code);
        if (value is not null && await db.Positions.AnyAsync(p => p.Code == value && p.Id != exceptId, cancellationToken))
        {
            throw Error(OrgChartErrors.CodeTaken, $"Position code '{value}' is already used.");
        }

        return value;
    }

    private async Task EnsureNoOverlapAsync(Assignment assignment, CancellationToken cancellationToken)
    {
        int positionId = assignment.Position.Id;
        (DateTime? from, DateTime? to) = (assignment.ValidFrom, assignment.ValidTo);
        bool overlaps = await db.Assignments.AnyAsync(a =>
                a.PositionId == positionId
                && a.UserId == assignment.UserId
                && a.Id != assignment.Id
                && (to == null || a.ValidFrom == null || a.ValidFrom < to)
                && (from == null || a.ValidTo == null || from < a.ValidTo),
            cancellationToken);
        if (overlaps)
        {
            throw Error(OrgChartErrors.AssignmentOverlap,
                $"User '{assignment.UserId}' already holds position '{assignment.Position.Key}' in an overlapping period.");
        }
    }

    // ---------------------------------------------------------------- validation

    private DateTime End(Assignment assignment, DateTime endAt)
    {
        DateTime end = Utc(endAt);
        if (assignment.ValidTo is { } validTo && validTo <= end)
        {
            throw Error(OrgChartErrors.AssignmentAlreadyEnded, $"Assignment {assignment.Id} already ends at {validTo:O}.");
        }

        if (assignment.ValidFrom is { } validFrom && end <= validFrom)
        {
            throw Error(OrgChartErrors.InvalidEndDate, $"Assignment {assignment.Id} starts at {validFrom:O}; it cannot end at {end:O}.");
        }

        assignment.ValidTo = end;
        return end;
    }

    private static string RequireKey(string? key)
    {
        string value = key?.Trim() ?? "";
        return OrgKey.IsValid(value)
            ? value
            : throw Error(OrgChartErrors.KeyInvalid, $"'{key}' is not a valid key (ASCII letters, digits, '.', '-', '_').");
    }

    private static string RequireTitle(string? title)
    {
        string value = title?.Trim() ?? "";
        if (value.Length == 0)
        {
            throw Error(OrgChartErrors.TitleRequired, "Title is required.");
        }

        return value.Length <= ColumnLengths.Title
            ? value
            : throw Error(OrgChartErrors.TitleTooLong, $"Title is longer than {ColumnLengths.Title} characters.");
    }

    private static string? OptionalCode(string? code)
    {
        string? value = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        return value is null || value.Length <= ColumnLengths.Code
            ? value
            : throw Error(OrgChartErrors.CodeTooLong, $"Code is longer than {ColumnLengths.Code} characters.");
    }

    private static string? OptionalNote(string? note)
    {
        string? value = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return value is null || value.Length <= ColumnLengths.Note
            ? value
            : throw Error(OrgChartErrors.NoteTooLong, $"Note is longer than {ColumnLengths.Note} characters.");
    }

    private static string RequireUserId(string? userId)
    {
        string value = userId?.Trim() ?? "";
        if (value.Length == 0)
        {
            throw Error(OrgChartErrors.UserIdRequired, "User id is required.");
        }

        return value.Length <= ColumnLengths.UserId
            ? value
            : throw Error(OrgChartErrors.UserIdTooLong, $"User id is longer than {ColumnLengths.UserId} characters.");
    }

    private static AssignmentKind RequireKind(AssignmentKind kind) =>
        Enum.IsDefined(kind) ? kind : throw Error(OrgChartErrors.InvalidKind, $"Assignment kind '{kind}' is not valid.");

    private static (DateTime? From, DateTime? To) RequirePeriod(DateTime? from, DateTime? to)
    {
        DateTime? f = from is { } a ? Utc(a) : null;
        DateTime? t = to is { } b ? Utc(b) : null;
        return Period.IsEmpty(f, t)
            ? throw Error(OrgChartErrors.InvalidPeriod, $"The period starts ({f:O}) at or after it ends ({t:O}).")
            : (f, t);
    }

    /// <summary>UTC as is; Unspecified is taken as UTC; Local is converted.</summary>
    private static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static OrgChartAdminException Error(string code, string message) => new(code, message);

    // ---------------------------------------------------------------- audit snapshots

    private static string TypeEntity(OrgTypeKind kind) => kind == OrgTypeKind.Unit ? nameof(OrgUnitType) : nameof(PositionType);

    private static bool Changed(object before, object after) => !before.Equals(after);

    private static object TypeState(KeyedEntity type) => type is OrgUnitType unit
        ? new { type.Key, type.Title, type.SortOrder, Level = unit.Level, CanBeRoot = (bool?)unit.CanBeRoot }
        : new { type.Key, type.Title, type.SortOrder, Level = (int?)null, CanBeRoot = (bool?)null };

    private static object UnitState(OrgUnit unit) => new
    {
        unit.Key,
        unit.Title,
        unit.Code,
        Type = unit.Type.Key,
        Parent = unit.Parent?.Key,
        unit.SortOrder,
        unit.ValidFrom,
        unit.ValidTo,
    };

    private static object PositionState(Position position) => new
    {
        position.Key,
        position.Title,
        position.Code,
        Unit = position.OrgUnit.Key,
        Type = position.Type?.Key,
        position.IsManagerial,
        Parent = position.ParentPosition?.Key,
        position.SortOrder,
        position.ValidFrom,
        position.ValidTo,
    };

    private static object AssignmentState(Assignment assignment) => new
    {
        assignment.UserId,
        Position = assignment.Position.Key,
        Kind = assignment.Kind.ToString(),
        assignment.ValidFrom,
        assignment.ValidTo,
        assignment.Note,
    };
}
