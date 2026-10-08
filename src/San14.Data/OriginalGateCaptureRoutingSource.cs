using San14.Core;

namespace San14.Data;

public sealed record OriginalGateCaptureNearbyArmyAdmission(int OldForcePhysical, int Radius,
    int AllocatedHexCandidates, int ExaminedArmyNodes, int MatchingForceArmies, int ExcludedArmies);

/// <summary>Ordinary Gate's26DA10 routing tail andCity/Gate2851B0 mode0's
/// current nearby-Force-Army empty body. This mutates only the caller's
/// operation-local context/pool. Nonempty2EED60 is a concrete unresolved
/// branch, rather than an assumption based on Gate troops or Core Units.</summary>
public static class OriginalGateCaptureRoutingSource
{
    public static int[] ClearTargetRouting(OriginalEventInitializationContext current, OriginalBaseReference gate)
    {
        int gateLocation = CheckGate(current, gate);
        var written = new List<int>();
        foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("routing_managerD0"))
        {
            int district = (entry ?? throw Missing("routing_District_pointer")).PhysicalSlot ?? throw Missing("routing_District_null");
            byte kind = K(current.ReadDistrictRawByte(district, 0x19), "routing_District19");
            bool match = false;
            if (kind == 2) match = K(current.ReadDistrictRawByte(district, 0x18), "routing_District18") == gateLocation;
            else if (kind == 6)
            {
                byte target = K(current.ReadDistrictRawByte(district, 0x18), "routing_District18");
                match = target is >= 1 and <= 10 && target + 51 == gateLocation;
            }
            //kind5 clamps to0..51 and cannot name Gate52..61. No Force,
            //District validity, or leader predicate belongs to this traversal.
            if (!match) continue;
            Clear2EE1F0(current, district); written.Add(district);
        }
        return written.ToArray();
    }

    public static OriginalGateCaptureNearbyArmyAdmission RequireEmptyOldForceNearbyArmies(
        GameState state, OriginalEventInitializationContext current, OriginalBaseReference target)
    {
        int location = K(current.ReadBaseObjectLink(target), "mode0_Base4E");
        if (target.Kind == OriginalBaseKind.Gate) _ = CheckGate(current, target);
        else if (target.Kind != OriginalBaseKind.City || target.PhysicalSlot is not (>= 1 and <= 51) || location is < 1 or > 51)
            throw Missing("branch.ordinary_Base_receiver");
        var center = OriginalCurrentGateMapQueries.FindHex(target, current);
        //2851B0 passesA7A8 to2A7EE0 forGate, A7A8+1 forCity.
        int radius = unchecked(K(OriginalBaseCaptureResourceInputs.ReadCurrentTc(state, 0x1A4A7A8), "mode0_Base_radiusA7A8") +
            (target.Kind == OriginalBaseKind.City ? 1 : 0));
        var hexes = current.CreateNearbyTemporary();
        try
        {
            //2851B0 uses the cumulative1..radius producer, excluding center,
            //before constructing its ForceArmy iterator even when it is empty.
            int candidates = OriginalNearbyArmyTemporarySource.FillNearbyHexTemporary(current, center, radius, hexes);
            int oldForce = ForceIdentity(); //222FD0 captures this iterator key.
            _ = ForceIdentity(); //223250's fresh null end receiver.
            int examined = 0, matching = 0, excluded = 0;
            foreach (var entry in current.ReadRegistryReferences(OriginalRegistryKind.Army) ?? throw Missing("mode0_manager60"))
            {
                examined++;
                //334740 rejects key0 before any payload field read.
                if (oldForce is < 1 or > 51) continue;
                var reference = entry ?? throw Missing("mode0_Army_payload");
                if (reference.Kind != OriginalRegistryKind.Army) throw Missing("mode0_manager60_Army_pointer_kind");
                int physical = reference.PhysicalSlot ?? throw Missing("mode0_Army_null");
                var army = new OriginalArmyUnitReference(physical);
                if (current.ReadArmyUnitRawForceSlot(army) != oldForce) continue; //22CD80, no Army validity gate.
                if (K(current.ReadOwnedArmyExclusion233560(reference), "mode0_Army233560")) { excluded++; continue; }
                matching++;
                int rawHex = K(current.ReadArmyUnitRawHex2A(army), "mode0_Army2A");
                var position = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw Missing("mode0_Army_Hex_pointer");
                var node = current.ReadHexListHead(hexes) ?? throw Missing("mode0_ring_head");
                while (node.Identity.HasValue)
                {
                    if ((current.ReadHexListNodePayload(node) ?? throw Missing("mode0_ring_payload")) == position)
                        throw Missing("branch.2851B0_mode0_related_Army_2EED60");
                    node = current.ReadHexListNodeNext(node) ?? throw Missing("mode0_ring_next");
                }
                _ = ForceIdentity(); //Native current iterator end query after each accepted body.
            }
            return new(oldForce, radius, candidates, examined, matching, excluded);
        }
        finally { current.ClearNearbyTemporary(hexes, true); }

        int ForceIdentity()
        {
            int raw = K(current.ReadBaseForceSlot(target), "mode0_current_Base_B0");
            if (raw < 0) throw Missing("mode0_negative_Force_table_index");
            var force = current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("mode0_current_Force_pointer");
            return K(current.ReadForcePhysicalSlot(force), "mode0_current_Force_identity");
        }
    }

    private static void Clear2EE1F0(OriginalEventInitializationContext current, int district)
    {
        current.SetDistrictRawByte(district, 0x17, 1);
        current.SetDistrictRawByte(district, 0x19, 0xFF); current.SetDistrictRawByte(district, 0x1A, 0);
        current.SetDistrictRawByte(district, 0x1B, 0xFF);
        //The source sets bits0..30 in place; bit31 is preserved, unlike
        //the District constructor's fixed7FFFFFFF initialization.
        byte high = K(current.ReadDistrictRawByte(district, 0x1F), "routing_District1C_high_byte");
        for (int at = 0x1C; at < 0x1F; at++) current.SetDistrictRawByte(district, at, 0xFF);
        current.SetDistrictRawByte(district, 0x1F, (byte)(high | 0x7F));
        current.SetDistrictRawByte(district, 0x20, 0); current.SetDistrictRawByte(district, 0x21, 0);
        current.SetDistrictRawByte(district, 0x22, 0); current.SetDistrictRawByte(district, 0x18, 0);
    }
    private static int CheckGate(OriginalEventInitializationContext current, OriginalBaseReference gate)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(gate);
        int location = K(current.ReadBaseObjectLink(gate), "Gate4E");
        if (gate.Kind != OriginalBaseKind.Gate || gate.PhysicalSlot is not (>= 1 and <= 10) || location is < 52 or > 61)
            throw Missing("branch.ordinary_Gate_receiver");
        return location;
    }
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.Gate_capture_routing." + field);
}
