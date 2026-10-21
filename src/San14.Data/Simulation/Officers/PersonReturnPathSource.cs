using San14.Core;

namespace San14.Data;

public sealed record PersonReturnPathResult(int? Days, bool? NativePathFound, int? OutputNodeCount,
    int[]? PathHexes, string? UnresolvedOwner);
public sealed record StateAdvantageSourceResult(int? Value, int[] CandidateStates,
    int[] QualifiedStates, string? UnresolvedOwner);

/// <summary>Actual231670→2AEDC0→1845B0/183C00 mode8 used by CCmdActionReturn.
/// The reverse search, six-direction order, equal-cost binary tree, endpoint
/// exception, primary list allocation and2318F0 scalar order are preserved.</summary>
public static partial class PersonReturnPathSource
{
    public const string Revision = "san14:ce-person-return-source-path-20261007";
    private const int HexCount = 48400;

    public static PersonReturnPathResult Fill(GameState state, SourceWorldContext current,
        HexReference start, HexReference end, ForceReference force, HexListHandle output)
    {
        bool? found = null; int[]? path = null;
        try {
            var head = Known(current.ReadHexListHead(output), "231670 output current head");
            if (!head.Identity.HasValue) {
                if (!start.PhysicalSlot.HasValue || !end.PhysicalSlot.HasValue) { found = false; path = []; }
                else {
                    var temporary = Known(current.CreateTemporaryHexList(), "2AEDC0 temporary descriptor allocation");
                    try {
                        current.ClearTemporaryHexList(temporary); //669E60, though this list stays empty.
                        path = Search(current, start.PhysicalSlot.Value, end.PhysicalSlot.Value);
                        found = path.Length != 0;
                        foreach (int hex in path) {
                            var pointer = Known(current.ReadHexTableReference(hex), "2AEDC0 output Hex pointer");
                            _ = current.AppendTemporaryHexReference(output, pointer); //Failed018CC0 drops only that node.
                        }
                    }
                    finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
                }
            }
            int count = Known(current.ReadTemporaryHexListCount(output), "231670 output count cell");
            int distance = Math.Max(1, unchecked(count - 1)), advantage = 0;
            bool validForce = Known(current.ReadForceNativeValidity(force), "2318F0 current Force validity");
            if (validForce) {
                var effect = ReadForceAdvantage(state, current, force, 5);
                if (effect.Value is not int value) return new(null, found, count, path, effect.UnresolvedOwner);
                advantage = value;
            }
            int coefficient = Known(PersonReturnPathParameterSeed.ReadCurrent(state, SimulationParameterKeys.PersonReturn.JourneyCoefficient), "2318F0 DAT1A4A9D0");
            //Native actorPerson is known-null0 in1F68B0: no trait12 read or multiplier.
            float bonus = validForce ? (float)advantage / (float)Known(
                PersonReturnPathParameterSeed.ReadCurrent(state, SimulationParameterKeys.PersonReturn.LandAdvantageDivisor), "2318F0 DAT1A49BAC") : 0f;
            float numerator = (float)coefficient * (float)distance;
            float days = numerator / (bonus + 10f);
            days = days / 12f;
            int rounded = NativeFloatToInt(MathF.Ceiling(days));
            return new(Math.Max(1, rounded), found, count, path, null);
        }
        catch (MissingOwner missing) { return new(null, found, null, path, missing.Message); }
    }

    /// <summary>Source2ADBC0. This is State/Advantage land advantage, not
    /// Force41D690 policy. Reads the existing saved growth State bank and the
    /// actual current City registration chain; candidates use the shared pool.</summary>
    public static StateAdvantageSourceResult ReadForceAdvantage(GameState state,
        SourceWorldContext current, ForceReference force, int requestedEffect, int defaultValue = 0)
    {
        var candidates = new List<int>(); var qualified = new List<int>();
        try {
            if (!Known(current.ReadForceNativeValidity(force), "2ADBC0 current Force validity") || requestedEffect is < 1 or > 13)
                return new(defaultValue, [], [], null);
            var temporary = Known(current.CreateTemporaryHexList(), "2ADBC0 State temporary descriptor");
            try {
                current.ClearTemporaryHexList(temporary);
                if (ReadInt(PersonGrowthCurrentQueries.VersionKey) != 1)
                    throw new MissingOwner("2ADBC0 saved State/Advantage field bank");
                int mode = ReadInt(PersonGrowthCurrentQueries.WorldMode40Key);
                if (mode is not (2 or 4)) {
                    for (int slot = 0; slot < 23; slot++) {
                        var pointer = Known(current.ReadStaticTableReference(0xDC80, slot), "2B5190 State table pointer");
                        if (pointer.PhysicalSlot is not int physical || physical == 0) continue; //State2345D0.
                        if ((uint)physical >= 23) throw new InvalidDataException("State physical pointer is outside CE table.");
                        int advantage = ReadByte(PersonGrowthCurrentQueries.StateAdvantageKey(physical));
                        var type = Known(current.ReadStaticTableReference(0x85990, advantage <= 17 ? advantage : 0), "2B5190 Advantage table pointer");
                        if (type.PhysicalSlot is not int typePhysical || typePhysical == 0) continue; //Advantage233BC0.
                        if ((uint)typePhysical >= 18) throw new InvalidDataException("Advantage pointer is outside CE table.");
                        for (int i = 0; i < 2; i++) {
                            if (ReadByte(PersonGrowthCurrentQueries.EffectKey(typePhysical, i)) != requestedEffect) continue;
                            if (current.AppendReturnStateTemporary(temporary, physical)) candidates.Add(physical);
                            break;
                        }
                    }
                }
                int sum = 0;
                foreach (int province in candidates) {
                    int raw = ReadByte(PersonGrowthCurrentQueries.StateAdvantageKey(province));
                    var advantage = Known(current.ReadStaticTableReference(0x85990, raw <= 17 ? raw : 0), "2C6820 Advantage pointer");
                    if (advantage.PhysicalSlot is not int type || type == 0) continue;
                    int required = ReadByte(PersonGrowthCurrentQueries.RequirementKey(type));
                    int forceId = Known(current.ReadForcePhysicalSlot(force), "2AAEE0 Force slot60");
                    int count = 0;
                    var cities = Known(current.ReadRegistryReferences(RegistryKind.City), "2AAEE0 registered City chain90");
                    foreach (var entry in cities) {
                        var city = Known(entry, "2AAEE0 City payload");
                        if (city.Kind != RegistryKind.City || city.PhysicalSlot is not int cityId)
                            throw new InvalidDataException("2AAEE0 current City payload has an unsafe kind/pointer.");
                        if (!Known(current.ReadRegistryObjectNativeValidity(city), "2AAEE0 current City validity")) continue;
                        if (ReadByte(PersonGrowthCurrentQueries.CityStateKey(cityId)) != province) continue;
                        // Consume this operation's current City knowledge, including unknown.
                        if (Known(current.ReadBaseForceSlot(new(BaseKind.City, cityId)), "2AAEE0 City current ForceB0") == forceId) count++;
                    }
                    if (count < required) continue;
                    qualified.Add(province);
                    for (int i = 0; i < 2; i++) {
                        if (ReadByte(PersonGrowthCurrentQueries.EffectKey(type, i)) != requestedEffect) continue;
                        sum = unchecked(sum + ReadByte(PersonGrowthCurrentQueries.MagnitudeKey(type, i))); break;
                    }
                }
                return new(qualified.Count != 0 ? sum : defaultValue, candidates.ToArray(), qualified.ToArray(), null);
            }
            finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
        }
        catch (MissingOwner missing) { return new(null, candidates.ToArray(), qualified.ToArray(), missing.Message); }
        int ReadInt(string key) => state.NativeArmySimulation?.Parameters is { } owner &&
            !owner.Float32Values.ContainsKey(key) && owner.Int32Values.TryGetValue(key, out int value)
            ? value : throw new MissingOwner("2ADBC0 current field " + key);
        int ReadByte(string key) { int value = ReadInt(key); return value is >= 0 and <= 255 ? value : throw new InvalidDataException("Current byte exceeds source width."); }
    }

    private static int[] Search(SourceWorldContext current, int start, int end)
    {
        if ((uint)start >= HexCount || (uint)end >= HexCount) throw new InvalidDataException("CHex physical endpoint is out of range.");
        var nodes = new SearchNode?[HexCount];
        var root = new SearchNode(end, 0, null); nodes[end] = root;
        while (root is not null) {
            SearchNode at = PopMinimum(ref root);
            if (at.Hex == start) {
                var result = new List<int>();
                for (SearchNode? node = at; node is not null; node = node.Parent) result.Add(node.Hex);
                return result.ToArray();
            }
            for (int direction = 1; direction <= 6; direction++) {
                int neighbor = Neighbor(at.Hex, direction);
                if (neighbor == HexCount) continue;
                var hex = Known(current.ReadHexTableReference(neighbor), "183C00 neighbor Hex table pointer");
                if (!hex.PhysicalSlot.HasValue) continue;
                if (neighbor != start) {
                    byte surface = Known(current.ReadHexSurfaceCode(hex), "2C6C20 current Hex surface10");
                    var terrain = Known(current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0), "2C6C20 current Surface pointer");
                    if (!terrain.PhysicalSlot.HasValue) continue;
                    ushort passable = Known(current.ReadSurfaceTypeRawField2A(terrain), "2C6C20 Surface signed word2A");
                    if (unchecked((short)passable) == 0) continue;
                }
                int cost = unchecked(at.Cost + 10000);
                SearchNode? existing = nodes[neighbor];
                if (existing is not null && cost >= existing.Cost) continue;
                //Mode8 constant positive costs make an improved existing open
                //node impossible; the native strict-less guard is preserved.
                if (existing is not null) throw new InvalidDataException("Mode8 search unexpectedly improved an existing node.");
                var next = new SearchNode(neighbor, cost, at); nodes[neighbor] = next; Insert(ref root, next);
            }
        }
        return [];
    }

    //2A7B60 source direction1..6; this is not2A6D30's ring traversal order.
    public static int Neighbor(int hex, int direction)
    {
        if ((uint)hex >= HexCount || direction is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(hex));
        int x = hex % 220, y = hex / 220, odd = x & 1;
        (int dx, int dy) = direction switch {
            1 => (-1, odd != 0 ? -1 : 0), 2 => (0, -1), 3 => (1, odd != 0 ? -1 : 0),
            4 => (-1, odd == 0 ? 1 : 0), 5 => (0, 1), _ => (1, odd == 0 ? 1 : 0) };
        x += dx; y += dy;
        return (uint)x < 220 && (uint)y < 220 ? y * 220 + x : HexCount;
    }
    private sealed class SearchNode(int hex, int cost, SearchNode? parent)
    {
        public readonly int Hex = hex, Cost = cost;
        public readonly SearchNode? Parent = parent;
        public SearchNode? Left, Right;
    }
    private static void Insert(ref SearchNode? root, SearchNode node)
    {
        if (root is null) { root = node; return; }
        SearchNode at = root;
        while (true) {
            if (node.Cost < at.Cost) { if (at.Left is null) { at.Left = node; return; } at = at.Left; }
            else { if (at.Right is null) { at.Right = node; return; } at = at.Right; }
        }
    }
    private static SearchNode PopMinimum(ref SearchNode? root)
    {
        SearchNode at = root!;
        if (at.Left is null) { root = at.Right; at.Right = null; return at; }
        SearchNode parent = at;
        while (at.Left is not null) { parent = at; at = at.Left; }
        parent.Left = at.Right; at.Right = null; return at;
    }
    private static T Known<T>(T? value, string owner) where T : class => value ?? throw new MissingOwner(owner);
    private static T Known<T>(T? value, string owner) where T : struct => value ?? throw new MissingOwner(owner);
    private static int NativeFloatToInt(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f
        ? int.MinValue : (int)value;
    private sealed class MissingOwner(string owner) : Exception(owner);
}

/// <summary>Only the actually reached State table payload, including a known
/// null pointer, is represented. Free primary nodes retain this pointer.</summary>
public sealed record ReturnStaticTablePayload(string TableKind, int? PhysicalSlot);
public sealed partial class SourceWorldContext
{
    internal bool AppendReturnStateTemporary(HexListHandle handle, int physical)
    {
        HexListNodeReference node = AllocateHexListNode(handle);
        if (!node.Identity.HasValue) return false;
        WriteCurrentListPayload(node.Identity.Value, new ReturnStaticTablePayload("State", physical));
        return true;
    }
}
