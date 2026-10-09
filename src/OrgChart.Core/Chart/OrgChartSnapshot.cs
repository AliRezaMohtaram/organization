using OrgChart.Core.Model;

namespace OrgChart.Core.Chart;

/// <summary>
/// The whole chart (units, positions and types, active or not) as an immutable in-memory tree.
/// Cheap to query and safe to share between threads. Keys are matched case-insensitively.
/// </summary>
/// <remarks>
/// Built from stored rows, so it tolerates what the database cannot rule out: a parent that does not
/// exist makes the unit a root, and a cycle in the tree is cut where it closes.
/// </remarks>
public sealed class OrgChartSnapshot
{
    private static readonly IComparer<UnitNode> s_unitOrder =
        Comparer<UnitNode>.Create((a, b) => CompareOrder(a.SortOrder, a.Title, b.SortOrder, b.Title));

    private static readonly IComparer<PositionNode> s_positionOrder =
        Comparer<PositionNode>.Create((a, b) => CompareOrder(a.SortOrder, a.Title, b.SortOrder, b.Title));

    private readonly Dictionary<string, UnitNode> _units;
    private readonly Dictionary<string, PositionNode> _positions;
    private readonly Dictionary<string, OrgTypeNode> _unitTypes;
    private readonly Dictionary<string, OrgTypeNode> _positionTypes;
    private readonly Dictionary<string, List<UnitNode>> _children;
    private readonly Dictionary<string, List<PositionNode>> _positionsByUnit;
    private readonly Dictionary<string, string> _positionParents;
    private readonly Dictionary<string, List<PositionNode>> _positionChildren;
    private readonly Dictionary<string, IReadOnlyList<string>> _ancestors;

    public OrgChartSnapshot(
        IEnumerable<UnitNode> units,
        IEnumerable<PositionNode> positions,
        IEnumerable<OrgTypeNode> unitTypes,
        IEnumerable<OrgTypeNode> positionTypes)
    {
        _units = units.ToDictionary(u => u.Key, OrgKey.Comparer);
        _positions = positions.ToDictionary(p => p.Key, OrgKey.Comparer);
        _unitTypes = unitTypes.ToDictionary(t => t.Key, OrgKey.Comparer);
        _positionTypes = positionTypes.ToDictionary(t => t.Key, OrgKey.Comparer);

        Dictionary<string, string> parents = EffectiveParents();
        _ancestors = new Dictionary<string, IReadOnlyList<string>>(OrgKey.Comparer);
        foreach (UnitNode unit in _units.Values)
        {
            List<string> ancestors = [];
            for (string key = unit.Key; parents.TryGetValue(key, out string? parentKey); key = parentKey)
            {
                ancestors.Add(parentKey);
            }

            _ancestors[unit.Key] = ancestors;
        }

        _children = new Dictionary<string, List<UnitNode>>(OrgKey.Comparer);
        List<UnitNode> roots = [];
        foreach (UnitNode unit in _units.Values)
        {
            if (parents.TryGetValue(unit.Key, out string? parentKey))
            {
                GetOrAdd(_children, parentKey).Add(unit);
            }
            else
            {
                roots.Add(unit);
            }
        }

        foreach (List<UnitNode> list in _children.Values)
        {
            list.Sort(s_unitOrder);
        }

        roots.Sort(s_unitOrder);
        Roots = roots;

        // Positions: a parent counts only when it exists in the same unit; cycles are cut like the unit tree's.
        _positionParents = CutCycles(
            _positions.Values
                .Where(p => FindPosition(p.ParentKey) is { } parent && OrgKey.AreEqual(parent.UnitKey, p.UnitKey))
                .ToDictionary(p => p.Key, p => _positions[p.ParentKey!].Key, OrgKey.Comparer),
            _positions.Keys);
        _positionChildren = new Dictionary<string, List<PositionNode>>(OrgKey.Comparer);
        Dictionary<string, List<PositionNode>> tops = new(OrgKey.Comparer);
        foreach (PositionNode position in _positions.Values)
        {
            GetOrAdd(_positionParents.TryGetValue(position.Key, out string? parentKey) ? _positionChildren : tops,
                parentKey ?? position.UnitKey).Add(position);
        }

        foreach (List<PositionNode> list in _positionChildren.Values.Concat(tops.Values))
        {
            list.Sort(s_positionOrder);
        }

        // Each unit's positions in tree order: a top position, then everything below it.
        _positionsByUnit = new Dictionary<string, List<PositionNode>>(OrgKey.Comparer);
        foreach ((string unitKey, List<PositionNode> top) in tops)
        {
            List<PositionNode> ordered = [];
            Stack<PositionNode> stack = new(Enumerable.Reverse(top));
            while (stack.TryPop(out PositionNode? position))
            {
                ordered.Add(position);
                foreach (PositionNode child in Enumerable.Reverse(GetChildPositions(position.Key)))
                {
                    stack.Push(child);
                }
            }

            _positionsByUnit[unitKey] = ordered;
        }

        Units = TreeOrder().ToList();
        Positions = Units.SelectMany(u => GetPositions(u.Key))
            .Concat(_positions.Values.Where(p => !_units.ContainsKey(p.UnitKey)).Order(s_positionOrder))
            .ToList();
        UnitTypes = _unitTypes.Values.OrderBy(t => t.SortOrder).ThenBy(t => t.Title, StringComparer.CurrentCulture).ToList();
        PositionTypes = _positionTypes.Values.OrderBy(t => t.SortOrder).ThenBy(t => t.Title, StringComparer.CurrentCulture).ToList();
    }

    public static OrgChartSnapshot Empty { get; } = new([], [], [], []);

    /// <summary>All units in tree order (depth-first; siblings by sort order, then title).</summary>
    public IReadOnlyList<UnitNode> Units { get; }

    /// <summary>All positions, grouped by unit in tree order.</summary>
    public IReadOnlyList<PositionNode> Positions { get; }

    public IReadOnlyList<UnitNode> Roots { get; }
    public IReadOnlyList<OrgTypeNode> UnitTypes { get; }
    public IReadOnlyList<OrgTypeNode> PositionTypes { get; }

    public UnitNode? FindUnit(string? key) => key is not null && _units.TryGetValue(key, out UnitNode? unit) ? unit : null;

    public PositionNode? FindPosition(string? key) =>
        key is not null && _positions.TryGetValue(key, out PositionNode? position) ? position : null;

    public OrgTypeNode? FindUnitType(string? key) =>
        key is not null && _unitTypes.TryGetValue(key, out OrgTypeNode? type) ? type : null;

    public OrgTypeNode? FindPositionType(string? key) =>
        key is not null && _positionTypes.TryGetValue(key, out OrgTypeNode? type) ? type : null;

    /// <summary>Direct sub-units, in display order. Empty for an unknown unit.</summary>
    public IReadOnlyList<UnitNode> GetChildren(string unitKey) =>
        _children.TryGetValue(unitKey, out List<UnitNode>? children) ? children : [];

    /// <summary>Keys of the unit's ancestors, nearest first. Empty for a root or an unknown unit.</summary>
    public IReadOnlyList<string> GetAncestorKeys(string unitKey) =>
        _ancestors.TryGetValue(unitKey, out IReadOnlyList<string>? ancestors) ? ancestors : [];

    /// <summary>Depth of the unit: 0 for a root.</summary>
    public int GetLevel(string unitKey) => GetAncestorKeys(unitKey).Count;

    /// <summary>The unit and its ancestors, root first (for breadcrumbs).</summary>
    public IReadOnlyList<UnitNode> GetPath(string unitKey)
    {
        if (FindUnit(unitKey) is not { } unit)
        {
            return [];
        }

        List<UnitNode> path = GetAncestorKeys(unitKey).Select(k => _units[k]).Reverse().ToList();
        path.Add(unit);
        return path;
    }

    /// <summary>Keys of all units below the unit (not the unit itself), in tree order.</summary>
    public IReadOnlyList<string> GetDescendantKeys(string unitKey)
    {
        List<string> keys = [];
        Stack<UnitNode> stack = new(GetChildren(unitKey).Reverse());
        while (stack.TryPop(out UnitNode? unit))
        {
            keys.Add(unit.Key);
            foreach (UnitNode child in GetChildren(unit.Key).Reverse())
            {
                stack.Push(child);
            }
        }

        return keys;
    }

    /// <summary>True when <paramref name="unitKey"/> is <paramref name="ancestorKey"/> or below it.</summary>
    public bool IsSelfOrDescendant(string unitKey, string ancestorKey) =>
        OrgKey.AreEqual(unitKey, ancestorKey) || GetAncestorKeys(unitKey).Contains(ancestorKey, OrgKey.Comparer);

    /// <summary>Positions of the unit in tree order (each top position followed by those below it). Empty for an unknown unit.</summary>
    public IReadOnlyList<PositionNode> GetPositions(string unitKey) =>
        _positionsByUnit.TryGetValue(unitKey, out List<PositionNode>? positions) ? positions : [];

    /// <summary>Positions directly below the position (same unit), in display order.</summary>
    public IReadOnlyList<PositionNode> GetChildPositions(string positionKey) =>
        _positionChildren.TryGetValue(positionKey, out List<PositionNode>? children) ? children : [];

    /// <summary>
    /// Top of a unit's position tree for display: its head when it has one (every other top position reports to
    /// the head), else its top positions.
    /// </summary>
    public IReadOnlyList<PositionNode> GetUnitTopPositions(string unitKey)
    {
        if (UnitHead(unitKey) is { } head)
        {
            return [head];
        }

        return GetPositions(unitKey).Where(p => !_positionParents.ContainsKey(p.Key)).ToList();
    }

    /// <summary>
    /// Positions shown directly below a position: its explicit subordinates and, for the head of a unit, the unit's
    /// other top positions (they report to the head).
    /// </summary>
    public IReadOnlyList<PositionNode> GetReportingChildren(string positionKey)
    {
        IReadOnlyList<PositionNode> children = GetChildPositions(positionKey);
        if (FindPosition(positionKey) is not { } position || UnitHead(position.UnitKey) is not { } head
            || !OrgKey.AreEqual(head.Key, position.Key))
        {
            return children;
        }

        return GetPositions(position.UnitKey)
            .Where(p => !_positionParents.ContainsKey(p.Key) && !OrgKey.AreEqual(p.Key, head.Key))
            .Concat(children)
            .ToList();
    }

    /// <summary>The unit's positions in display order (see <see cref="GetUnitTopPositions"/>) with their display depth.</summary>
    public IReadOnlyList<(PositionNode Position, int Depth)> GetPositionOutline(string unitKey)
    {
        List<(PositionNode, int)> outline = [];
        HashSet<string> seen = new(OrgKey.Comparer);
        Stack<(PositionNode Position, int Depth)> stack = new(GetUnitTopPositions(unitKey).Reverse().Select(p => (p, 0)));
        while (stack.TryPop(out var item))
        {
            if (!seen.Add(item.Position.Key))
            {
                continue;
            }

            outline.Add(item);
            foreach (PositionNode child in GetReportingChildren(item.Position.Key).Reverse())
            {
                stack.Push((child, item.Depth + 1));
            }
        }

        // Anything not reached (should not happen) is listed at the end, so nothing disappears.
        outline.AddRange(GetPositions(unitKey).Where(p => !seen.Contains(p.Key)).Select(p => (p, 0)));
        return outline;
    }

    /// <summary>The head of the unit when it is one of the unit's positions without an explicit parent.</summary>
    private PositionNode? UnitHead(string unitKey) =>
        FindUnit(unitKey) is { } unit && FindPosition(unit.ManagerPositionKey) is { } head
            && OrgKey.AreEqual(head.UnitKey, unit.Key) && !_positionParents.ContainsKey(head.Key)
            ? head
            : null;

    /// <summary>The explicit parent position (same unit), or null for a top position.</summary>
    public string? GetParentPositionKey(string positionKey) => _positionParents.GetValueOrDefault(positionKey);

    /// <summary>Depth of the position below the top positions of its unit: 0 for a top position.</summary>
    public int GetPositionDepth(string positionKey)
    {
        int depth = 0;
        for (string key = positionKey; _positionParents.TryGetValue(key, out string? parent); key = parent)
        {
            depth++;
        }

        return depth;
    }

    /// <summary>True when <paramref name="positionKey"/> is <paramref name="ancestorKey"/> or below it.</summary>
    public bool IsSelfOrSubordinate(string positionKey, string ancestorKey)
    {
        for (string? key = positionKey; key is not null; key = _positionParents.GetValueOrDefault(key))
        {
            if (OrgKey.AreEqual(key, ancestorKey))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The position this one reports to: its parent position; else the head of its unit (unless it is the head);
    /// else the head of the nearest ancestor unit that has an active head. Null at the top of the chart.
    /// </summary>
    public string? GetSuperiorKey(string positionKey)
    {
        if (FindPosition(positionKey) is not { } position)
        {
            return null;
        }

        if (_positionParents.TryGetValue(position.Key, out string? parent))
        {
            return parent;
        }

        if (FindUnit(position.UnitKey) is not { } unit)
        {
            return null;
        }

        IEnumerable<string> units = OrgKey.AreEqual(unit.ManagerPositionKey, position.Key)
            ? GetAncestorKeys(unit.Key)
            : GetAncestorKeys(unit.Key).Prepend(unit.Key);
        return units
            .Select(k => FindPosition(_units[k].ManagerPositionKey))
            .FirstOrDefault(head => head is { IsActive: true } && !OrgKey.AreEqual(head.Key, position.Key))
            ?.Key;
    }

    /// <summary>
    /// The reporting line above a position, nearest first (see <see cref="GetSuperiorKey"/>), active positions only.
    /// For example: clerk → section lead → head of department → head of the parent unit → …
    /// </summary>
    public IReadOnlyList<string> GetManagerChain(string positionKey)
    {
        List<string> chain = [];
        HashSet<string> seen = new(OrgKey.Comparer) { positionKey };
        for (string? key = GetSuperiorKey(positionKey); key is not null && seen.Add(key); key = GetSuperiorKey(key))
        {
            if (_positions[key].IsActive)
            {
                chain.Add(key);
            }
        }

        return chain;
    }

    /// <summary>
    /// Child key → parent key, keeping only parents that exist. Each cycle is cut at its member with the
    /// smallest key, which becomes a root, so the result is a forest.
    /// </summary>
    private Dictionary<string, string> EffectiveParents() =>
        CutCycles(
            _units.Values.Where(u => FindUnit(u.ParentKey) is not null).ToDictionary(u => u.Key, u => _units[u.ParentKey!].Key, OrgKey.Comparer),
            _units.Keys);

    /// <summary>Removes one link of every cycle in a child → parent map (at the member with the smallest key).</summary>
    private static Dictionary<string, string> CutCycles(Dictionary<string, string> parents, IEnumerable<string> keys)
    {
        HashSet<string> done = new(OrgKey.Comparer);
        foreach (string start in keys)
        {
            List<string> trail = [];
            HashSet<string> onTrail = new(OrgKey.Comparer);
            string? key = start;
            while (key is not null && !done.Contains(key) && onTrail.Add(key))
            {
                trail.Add(key);
                key = parents.GetValueOrDefault(key);
            }

            if (key is not null && onTrail.Contains(key))
            {
                // The trail closed on itself: the cycle is the part of the trail from "key" on.
                string cut = trail.Skip(trail.FindIndex(k => OrgKey.AreEqual(k, key)))
                    .Order(StringComparer.OrdinalIgnoreCase).First();
                parents.Remove(cut);
            }

            done.UnionWith(trail);
        }

        return parents;
    }

    private IEnumerable<UnitNode> TreeOrder()
    {
        foreach (UnitNode root in Roots)
        {
            yield return root;
            foreach (string key in GetDescendantKeys(root.Key))
            {
                yield return _units[key];
            }
        }
    }

    private static int CompareOrder(int sortA, string titleA, int sortB, string titleB)
    {
        int bySort = sortA.CompareTo(sortB);
        return bySort != 0 ? bySort : StringComparer.CurrentCulture.Compare(titleA, titleB);
    }

    private static List<T> GetOrAdd<T>(Dictionary<string, List<T>> map, string key)
    {
        if (!map.TryGetValue(key, out List<T>? list))
        {
            list = [];
            map[key] = list;
        }

        return list;
    }
}
