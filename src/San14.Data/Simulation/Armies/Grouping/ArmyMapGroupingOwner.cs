namespace San14.Data;

public readonly record struct ArmyMapGroupingReceiver(int? UnitId, bool? IsValid,
    byte? Status10, ushort? Hex2A, byte? Byte13C);
public readonly record struct ArmyMapGroupingHex(bool? IsValid, int? PhysicalSlot, ushort? Raw1C);
public sealed record ArmyMapGroupingMember(int PhysicalSlot, int? UnitId);
public sealed record ArmyMapGroupingRecomputeResult(bool Applied, IReadOnlyList<int> CandidateOrder,
    string? Missing);

/// <summary>One operation-local current view. Comparator methods are lazy:
///2539D0 never queries a lower priority after an earlier difference.</summary>
public interface IArmyMapGroupingQueries
{
    IReadOnlyList<int>? ReadRegistry();
    ArmyMapGroupingReceiver ReadReceiver(int physical);
    ArmyMapGroupingHex ReadHex(int tableSlot);
    bool? IsExcluded233560(int physical);
    bool? IsAtHome2C96E0(int physical);
    byte? ReadDistrict22BC70(int physical);
    bool? ReadCommanderValidity(int physical);
    bool? ReadRuler(int physical);
    bool? ReadDistrictLeader(int physical);
    bool? ReadGovernor(int physical);
    byte? ReadPostOrder37(int physical);
    int? ReadLeadership(int physical);
    ushort? ReadPersonIdentity(int physical);
}

/// <summary>Frozen source fields, not a view of the mutable producer or its
/// next current context. The Canvas may consume this after a later recompute.</summary>
public sealed class ArmyMapGroupingFrame
{
    private readonly ushort?[,] _roots;
    private readonly ArmyMapGroupingReceiver[] _receivers;
    private readonly bool?[] _excluded;
    private readonly Dictionary<(int Root, int Index), IReadOnlyList<ArmyMapGroupingMember>?> _members;
    internal ArmyMapGroupingFrame(ushort?[,] roots, ArmyMapGroupingReceiver[] receivers,
        bool?[] excluded, Dictionary<(int, int), IReadOnlyList<ArmyMapGroupingMember>?> members)
        => (_roots, _receivers, _excluded, _members) = (roots, receivers, excluded, members);

    public ushort? RootId(int physical, int index) => _roots[physical, index];
    //22D520 includes raw0's real slot0 pointer; unknown is distinct from it.
    public int? RootPhysicalSlot(int physical, int index) => RootId(physical, index) is ushort raw
        ? raw <= 500 ? raw : 0 : null;
    public bool? IsValid(int physical) => _receivers[physical].IsValid;
    public bool? IsExcluded(int physical) => _excluded[physical];
    public int? UnitId(int physical) => _receivers[physical].UnitId;
    public IReadOnlyList<ArmyMapGroupingMember>? ReadMembers(int physical, int index)
    {
        if (RootPhysicalSlot(physical, index) is not int root) return null;
        return IsValid(root) is bool valid ? valid ? _members.GetValueOrDefault((root, index)) : [] : null;
    }
}

/// <summary>Army table's derived1E8/1EA/1EC owner. UI near-level clears do
/// not reset it. Excluded receivers retain their prior roots, including unknown
/// old fields; only a known2269E0 receiver reset initializes their three words.</summary>
public sealed class ArmyMapGroupingOwner
{
    private ushort?[,] _roots = new ushort?[501, 3];
    /// <summary>Starts a private game operation, preserving unknown and excluded roots.</summary>
    internal ArmyMapGroupingOwner Copy() => new() { _roots = (ushort?[,])_roots.Clone() };
    internal ushort?[] Export() => Enumerable.Range(0, 1503).Select(i => _roots[i / 3, i % 3]).ToArray();
    internal void Import(ushort?[] roots)
    {
        if (roots is not { Length: 1503 } || roots.Any(v => v > 500)) throw new InvalidDataException("Grouping continuation is invalid.");
        var copy = new ushort?[501, 3];
        for (int i = 0; i < roots.Length; i++) copy[i / 3, i % 3] = roots[i];
        _roots = copy;
    }
    public void InitializeKnownReset(IEnumerable<int> physicalSlots)
    { foreach (int physical in physicalSlots) ResetReceiver(physical); }
    public void ResetReceiver(int physical)
    { for (int index = 0; index < 3; index++) _roots[physical, index] = 0; }

    public ArmyMapGroupingRecomputeResult Recompute(ArmyMapGroupingProfile profile,
        IArmyMapGroupingQueries current)
    {
        var candidates = new List<int>();
        var next = (ushort?[,])_roots.Clone();
        try
        {
            var registry = current.ReadRegistry() ?? throw Missing("current.Army_registry60");
            foreach (int physical in registry)
            {
                if (Known(current.IsExcluded233560(physical), "current.Army233560") ||
                    Known(current.IsAtHome2C96E0(physical), "current.Army2C96E0")) continue;
                for (int index = 0; index < 3; index++) Write(next, physical, index, 0);
                if (Known(current.ReadReceiver(physical).Status10, "current.Army10") == 1) candidates.Add(physical);
            }
            //24BBD0 moves only on a strict positive comparison, preserving the
            //actual registry order for ties and invalid Person comparisons.
            for (int i = 1; i < candidates.Count; i++)
            {
                int value = candidates[i], at = i;
                while (at > 0 && Known(StrictlyPrecedes(value, candidates[at - 1], current), "current.Army2539D0"))
                { candidates[at] = candidates[at - 1]; at--; }
                candidates[at] = value;
            }
            foreach (int candidate in candidates)
                for (int index = 0; index < 3; index++)
                {
                    if (next[candidate, index] is >= 1 and <= 500) continue;
                    bool any = false;
                    foreach (int hex in Neighbors(profile, current, candidate, index))
                    {
                        var cell = current.ReadHex(hex);
                        if (!Known(cell.IsValid, "current.group_Hex_validity")) continue;
                        short occupant = unchecked((short)Known(cell.Raw1C, "current.group_Hex1C"));
                        if (occupant is < 1 or > 500) continue;
                        var receiver = current.ReadReceiver(occupant);
                        if (!Known(receiver.IsValid, "current.group_Army_validity")) continue;
                        if (Known(current.ReadDistrict22BC70(occupant), "current.group_DistrictE0") !=
                            Known(current.ReadDistrict22BC70(candidate), "current.group_root_DistrictE0")) continue;
                        if (!candidates.Contains(occupant) || next[occupant, index] is >= 1 and <= 500) continue;
                        if ((Known(receiver.Byte13C, "current.group_Army13C") != 0) !=
                            (Known(current.ReadReceiver(candidate).Byte13C, "current.group_root13C") != 0)) continue;
                        Write(next, occupant, index, candidate); any = true;
                    }
                    if (any) Write(next, candidate, index, candidate);
                }
            _roots = next;
            return new(true, candidates.AsReadOnly(), null);
        }
        catch (GroupingMissingException missing)
        {
            //Unknown inputs do not publish a half-executed derived writer.
            return new(false, candidates.AsReadOnly(), missing.Message);
        }
    }

    public ArmyMapGroupingFrame Capture(ArmyMapGroupingProfile profile,
        IArmyMapGroupingQueries current)
    {
        var roots = (ushort?[,])_roots.Clone();
        var receivers = Enumerable.Range(0, 501).Select(current.ReadReceiver).ToArray();
        var excluded = receivers.Select((row, physical) => row.IsValid == true
            ? current.IsExcluded233560(physical) : null).ToArray();
        var members = new Dictionary<(int, int), IReadOnlyList<ArmyMapGroupingMember>?>();
        for (int physical = 0; physical < 501; physical++)
            for (int index = 0; index < 3; index++)
            {
                if (roots[physical, index] is not ushort raw) continue;
                int root = raw <= 500 ? raw : 0;
                if (receivers[root].IsValid != true || members.ContainsKey((root, index))) continue;
                try
                {
                    var list = new List<ArmyMapGroupingMember> { new(root, receivers[root].UnitId) };
                    foreach (int hex in Neighbors(profile, current, root, index))
                    {
                        var cell = current.ReadHex(hex);
                        if (!Known(cell.IsValid, "current.members_Hex_validity")) continue;
                        short occupant = unchecked((short)Known(cell.Raw1C, "current.members_Hex1C"));
                        if (occupant is < 1 or > 500) continue;
                        if (!Known(receivers[occupant].IsValid, "current.members_Army_validity")) continue;
                        if (Known(roots[occupant, index], "current.members_root1E8") == root)
                            list.Add(new(occupant, receivers[occupant].UnitId)); //No dedup or new membership gates.
                    }
                    members[(root, index)] = list.AsReadOnly();
                }
                catch (GroupingMissingException) { members[(root, index)] = null; }
            }
        return new(roots, receivers, excluded, members);
    }

    public static bool? StrictlyPrecedes(int first, int second, IArmyMapGroupingQueries current)
    {
        bool? valid = current.ReadCommanderValidity(first);
        if (valid is null) return null;
        if (!valid.Value) return false;
        valid = current.ReadCommanderValidity(second);
        if (valid is null) return null;
        if (!valid.Value) return false;
        foreach (var read in new Func<int, bool?>[] { current.ReadRuler, current.ReadDistrictLeader, current.ReadGovernor })
        {
            bool? a = read(first), b = read(second);
            if (a is null || b is null) return null;
            if (a != b) return a.Value;
        }
        byte? postA = current.ReadPostOrder37(first), postB = current.ReadPostOrder37(second);
        if (postA is null || postB is null) return null;
        if (postA != postB) return postA != 0 && (postB == 0 || postA < postB);
        int? leadershipA = current.ReadLeadership(first), leadershipB = current.ReadLeadership(second);
        if (leadershipA is null || leadershipB is null) return null;
        if (leadershipA != leadershipB) return leadershipA > leadershipB;
        ushort? idA = current.ReadPersonIdentity(first), idB = current.ReadPersonIdentity(second);
        return idA is not null && idB is not null ? idA < idB : null;
    }

    private static IReadOnlyList<int> Neighbors(ArmyMapGroupingProfile profile,
        IArmyMapGroupingQueries current, int physical, int index)
    {
        ushort raw = Known(current.ReadReceiver(physical).Hex2A, "current.Army_Hex2A");
        var center = current.ReadHex(raw < 48400 ? raw : 0);
        return Known(center.IsValid, "current.group_center_Hex_validity")
            ? profile.EnumerateNeighbors(Known(center.PhysicalSlot, "current.group_center_Hex_physical"), index) : [];
    }
    private static void Write(ushort?[,] roots, int physical, int index, int value)
        => roots[physical, index] = (ushort)Math.Clamp(value, 0, 500);
    private static T Known<T>(T? value, string field) where T : struct
        => value ?? throw Missing(field);
    private static GroupingMissingException Missing(string field) => new(field);
    private sealed class GroupingMissingException(string field) : Exception(field);
}
