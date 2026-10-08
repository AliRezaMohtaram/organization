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

        _positionsByUnit = new Dictionary<string, List<PositionNode>>(OrgKey.Comparer);
        foreach (PositionNode position in _positions.Values)
        {
            GetOrAdd(_positionsByUnit, position.UnitKey).Add(position);
        }

        foreach (List<PositionNode> list in _positionsByUnit.Values)
        {
            list.Sort(s_positionOrder);
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

    /// <summary>Positions of the unit, in display order. Empty for an unknown unit.</summary>
    public IReadOnlyList<PositionNode> GetPositions(string unitKey) =>
        _positionsByUnit.TryGetValue(unitKey, out List<PositionNode>? positions) ? positions : [];

    /// <summary>
    /// The reporting line above a position: the manager positions of its unit and then of each ancestor,
    /// nearest first. The position itself, units without a manager and inactive manager positions are skipped,
    /// so the head of a unit reports to the manager of the parent unit.
    /// </summary>
    public IReadOnlyList<string> GetManagerChain(string positionKey)
    {
        if (FindPosition(positionKey) is not { } position || FindUnit(position.UnitKey) is not { } unit)
        {
            return [];
        }

        List<string> chain = [];
        foreach (string key in GetAncestorKeys(unit.Key).Prepend(unit.Key))
        {
            if (FindPosition(_units[key].ManagerPositionKey) is { IsActive: true } manager
                && !OrgKey.AreEqual(manager.Key, position.Key)
                && !chain.Contains(manager.Key, OrgKey.Comparer))
            {
                chain.Add(manager.Key);
            }
        }

        return chain;
    }

    /// <summary>
    /// Child key → parent key, keeping only parents that exist. Each cycle is cut at its member with the
    /// smallest key, which becomes a root, so the result is a forest.
    /// </summary>
    private Dictionary<string, string> EffectiveParents()
    {
        Dictionary<string, string> parents = new(OrgKey.Comparer);
        foreach (UnitNode unit in _units.Values)
        {
            if (FindUnit(unit.ParentKey) is { } parent)
            {
                parents[unit.Key] = parent.Key;
            }
        }

        HashSet<string> done = new(OrgKey.Comparer);
        foreach (string start in _units.Keys)
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
