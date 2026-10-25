using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // 2D3410 keeps the successfully allocated candidate nodes alive while the
    // special Person is made. Only then does it filter the actual Area pointer,
    // shuffle payloads, and order them by radius-one/radius-two crowding.
    private (int? Spawn, int? Target) SelectStrategicIncitementPlacementCurrent(
        GameState state, SourceWorldContext current, int area, HexListHandle candidates)
    {
        var filtered = current.CreateTemporaryHexList() ?? throw StrategyMissing("incitement_filtered_descriptor");
        try
        {
            var nodes = new List<(int Node, int Hex)>();
            var at = current.ReadHexListHead(candidates) ?? throw StrategyMissing("incitement_candidates_head");
            while (at.Identity is not null)
            {
                var hex = current.ReadHexListNodePayload(at) ?? throw StrategyMissing("incitement_candidate_payload");
                if (StrategyNeed(current.ReadHexAreaSlot(hex), "incitement_candidate_Area12") == area)
                {
                    var node = current.AllocateHexListNode(filtered);
                    if (node.Identity is int identity)
                    {
                        int physical = StrategyNeed(hex.PhysicalSlot, "incitement_candidate_physical");
                        current.WriteCurrentListPayload(identity, physical);
                        nodes.Add((identity, physical));
                    }
                }
                at = current.ReadHexListNodeNext(at) ?? throw StrategyMissing("incitement_candidates_next");
            }
            if (nodes.Count == 0) return (null, null);
            if (nodes.Count > 1)
            {
                var sampler = ReadArmyDestructionSampler(state, current) ?? throw StrategyMissing("incitement_shuffle_sampler");
                int salt = 0;
                for (int rounds = nodes.Count / 2 * nodes.Count; rounds > 0; rounds--)
                {
                    int a = TacticEffectSourceSamplerMath.Sample(nodes.Count, 1, salt++, 0, 0, 0, sampler);
                    int b = nodes.Count - 1 - TacticEffectSourceSamplerMath.Sample(nodes.Count, 1, salt++, 0, 0, 0, sampler);
                    if (a == b) continue;
                    var left = nodes[a]; var right = nodes[b];
                    current.WriteCurrentListPayload(left.Node, right.Hex);
                    current.WriteCurrentListPayload(right.Node, left.Hex);
                    nodes[a] = (left.Node, right.Hex); nodes[b] = (right.Node, left.Hex);
                }
            }
            // The third key is the shuffled traversal ordinal, not Hex ID.
            var ordered = nodes.Select((node, index) => new
            {
                node.Hex, Index = index,
                Near = ReadIncitementCrowdingCurrent(current, node.Hex, 1),
                Far = ReadIncitementCrowdingCurrent(current, node.Hex, 2)
            }).OrderBy(row => row.Near).ThenBy(row => row.Far).ThenBy(row => row.Index).ToArray();
            int spawn = ordered[0].Hex;
            return (spawn, ReadIncitementTargetHexCurrent(current, spawn));
        }
        finally { current.ClearTemporaryHexList(filtered); current.ReleaseTemporaryHexList(filtered); }
    }

    private static int ReadIncitementCrowdingCurrent(SourceWorldContext current, int center, int radius)
    {
        var hexes = current.CreateNearbyTemporary();
        var objects = current.CreateNearbyTemporary();
        try
        {
            NearbyArmyTemporarySource.FillNearbyHexTemporary(current, new(center), radius, hexes);
            int count = 0;
            var seen = new HashSet<ObjectReference>();
            var node = current.ReadHexListHead(hexes) ?? throw StrategyMissing("incitement_crowding_head");
            while (node.Identity is not null)
            {
                var hex = current.ReadHexListNodePayload(node) ?? throw StrategyMissing("incitement_crowding_Hex");
                ushort link = StrategyNeed(current.ReadHexObjectLink(hex), "incitement_crowding_Hex1A");
                bool duplicate = false;
                if (link is >= 1 and <= 3000)
                {
                    var obj = current.ReadObjectTableReference(link) ?? throw StrategyMissing("incitement_crowding_Object");
                    _ = StrategyNeed(current.ReadObjectNativeValidity(obj), "incitement_crowding_Object_validity");
                    // 2AE7C0 does not test Object native validity. A real failed
                    // append still increases the count, but cannot deduplicate
                    // the next occurrence of that pointer.
                    duplicate = seen.Contains(obj);
                    if (!duplicate)
                    {
                        var added = current.AllocateRegistryNode(new(objects.Identity))
                            ?? throw StrategyMissing("incitement_crowding_Object_node");
                        if (added.Identity is not null)
                        {
                            current.WriteRegistryNodePayload(added, new(RegistryKind.Object, obj.PhysicalSlot));
                            seen.Add(obj);
                        }
                        count++;
                    }
                }
                if (!duplicate && unchecked((short)StrategyNeed(current.ReadHexRaw1C(hex), "incitement_crowding_Hex1C")) is >= 1 and <= 500)
                    count++;
                node = current.ReadHexListNodeNext(node) ?? throw StrategyMissing("incitement_crowding_next");
            }
            return count;
        }
        finally { current.ClearNearbyTemporary(objects, true); current.ClearNearbyTemporary(hexes, true); }
    }

    // 28AF70 first visits registered Objects in this same Area. Only when no
    // such target is found does it visit the registered Areas across the map.
    private static int? ReadIncitementTargetHexCurrent(SourceWorldContext current, int spawn)
    {
        int area = StrategyNeed(current.ReadHexAreaSlot(new(spawn)), "incitement_target_spawn_Area");
        int? selected = null; int distance = int.MaxValue;
        foreach (var entry in ReadIncitementRegistry(current, RegistryKind.Object))
        {
            int physical = StrategyNeed(entry.PhysicalSlot, "incitement_target_Object_physical");
            var obj = new ObjectReference(physical);
            bool admitted;
            if (physical is >= 1 and <= 51)
            {
                var city = current.ReadCityTableReference(physical) ?? throw StrategyMissing("incitement_target_City");
                int force = StrategyNeed(current.ReadBaseForceSlot(city), "incitement_target_City_Force");
                if (force == 0) admitted = false;
                else
                {
                    var owner = current.ReadForceTableReference(force < 52 ? force : 0)
                        ?? throw StrategyMissing("incitement_target_City_Force_pointer");
                    admitted = StrategyNeed(current.ReadForcePhysicalSlot(owner), "incitement_target_City_Force_physical")
                        is not (>= 46 and <= 50);
                }
            }
            else if (physical is >= 52 and <= 61)
            {
                var gate = current.ReadGateTableReference(physical - 51) ?? throw StrategyMissing("incitement_target_Gate");
                admitted = StrategyNeed(current.ReadBaseForceSlot(gate), "incitement_target_Gate_Force") != 0;
            }
            else
            {
                int rawForce = StrategyNeed(current.ReadObjectForceSlot13(obj), "incitement_target_Object13");
                var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0) ?? throw StrategyMissing("incitement_target_Object_Force");
                admitted = StrategyNeed(current.ReadForceNativeValidity(force), "incitement_target_Object_Force_validity");
            }
            if (!admitted) continue;
            int hex = StrategyNeed(current.ReadObjectHexIndex(obj), "incitement_target_Object10");
            var pointer = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw StrategyMissing("incitement_target_Hex");
            if (StrategyNeed(current.ReadHexAreaSlot(pointer), "incitement_target_Hex12") != area) continue;
            Keep(StrategyNeed(pointer.PhysicalSlot, "incitement_target_Hex_physical"));
        }
        if (selected is not null) return selected;
        foreach (var entry in ReadIncitementRegistry(current, RegistryKind.Area))
        {
            var region = new AreaReference(StrategyNeed(entry.PhysicalSlot, "incitement_fallback_Area"));
            int raw = StrategyNeed(current.ReadAreaCurrentForceSlot(region), "incitement_fallback_Area_Force");
            var force = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw StrategyMissing("incitement_fallback_Force");
            if (!StrategyNeed(current.ReadForceNativeValidity(force), "incitement_fallback_Force_validity") ||
                StrategyNeed(current.ReadForcePhysicalSlot(force), "incitement_fallback_Force_physical") is >= 46 and <= 50) continue;
            int rawHex = StrategyNeed(current.ReadAreaHexIndex(region), "incitement_fallback_Area3A");
            var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw StrategyMissing("incitement_fallback_Hex");
            Keep(StrategyNeed(hex.PhysicalSlot, "incitement_fallback_Hex_physical"));
        }
        return selected;
        void Keep(int hex)
        {
            int value = ArmyRouteSearch.Distance(spawn, hex);
            if (value < distance) { distance = value; selected = hex; }
        }
    }

    private static IEnumerable<RegistryObjectReference> ReadIncitementRegistry(SourceWorldContext current, RegistryKind kind)
    {
        var descriptor = current.ReadRegistryDescriptor(kind) ?? throw StrategyMissing("incitement_registry_" + kind);
        var node = current.ReadRegistryHead(descriptor) ?? throw StrategyMissing("incitement_registry_head");
        while (node.Identity is not null)
        {
            yield return current.ReadRegistryNodePayload(node) ?? throw StrategyMissing("incitement_registry_payload");
            node = current.ReadRegistryNodeNext(node) ?? throw StrategyMissing("incitement_registry_next");
        }
    }
}
