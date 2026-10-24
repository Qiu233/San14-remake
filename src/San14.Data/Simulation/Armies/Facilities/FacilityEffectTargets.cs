using San14.Core;
using static San14.Data.FacilityEffectCandidates;

namespace San14.Data;

internal static class FacilityEffectTargets
{
    internal static void Fill(GameState state, SourceWorldContext current, ObjectReference facility,
        FacilityEffectDefinition definition, int kind, HexListHandle[] channels, byte category = 2)
    {
        int rawForce = K(current.ReadObjectForceSlot13(facility), "facility_Force13");
        var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0) ?? throw Missing("facility_Force_pointer");
        if (!K(current.ReadForceNativeValidity(force), "facility_Force_validity")) return;
        int identity = K(current.ReadForcePhysicalSlot(force), "facility_Force_identity");
        if (identity is < 1 or > 51) return;
        ushort rawHex = K(current.ReadObjectHexIndex(facility), "facility_Hex");
        var center = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw Missing("facility_Hex_pointer");
        if (center.PhysicalSlot is not int at) return;
        //The existing kind7 caller already supplies freshly created channels.
        //Class4 retains2ABA00's explicit clear after the same valid prefix.
        if (category == 4)
            foreach (var channel in channels) current.ClearNearbyTemporary(channel, false);
        var range = current.CreateNearbyTemporary();
        try
        {
            int minimum = definition.Minimum;
            int maximum = definition.MaximumFor(state, kind);
            NearbyArmyTemporarySource.FillNearbyHexTemporary(current, center, maximum, range);
            current.AppendNearbyHexTemporary(range, center);
            if (category == 4)
            {
                bool water = TrapCandidates.WaterAtHex(state, current, center);
                var candidate = current.ReadHexListHead(range) ?? throw Missing("trap_water_range_head");
                while (candidate.Identity.HasValue)
                {
                    var next = current.ReadHexListNodeNext(candidate) ?? throw Missing("trap_water_range_next");
                    var hex = current.ReadHexListNodePayload(candidate) ?? throw Missing("trap_water_range_payload");
                    if (TrapCandidates.WaterAtHex(state, current, hex) != water)
                        current.RemoveFacilityHexTemporary(range, candidate);
                    candidate = next;
                }
            }
            var node = current.ReadHexListHead(range) ?? throw Missing("range_head");
            while (node.Identity.HasValue)
            {
                var next = current.ReadHexListNodeNext(node) ?? throw Missing("range_next");
                var hex = current.ReadHexListNodePayload(node) ?? throw Missing("range_payload");
                int key = K(hex.PhysicalSlot, "range_Hex_pointer");
                if (minimum > 0 && ArmyRouteSearch.Distance(at, key) < minimum)
                    current.RemoveFacilityHexTemporary(range, node);
                node = next;
            }
            if (kind is 15 or 17)
            {
                node = current.ReadHexListHead(range) ?? throw Missing("special_range_head");
                while (node.Identity.HasValue)
                {
                    var next = current.ReadHexListNodeNext(node) ?? throw Missing("special_range_next");
                    var hex = current.ReadHexListNodePayload(node) ?? throw Missing("special_range_payload");
                    if (K(current.ReadHexSurfaceCode(hex), "special_range_surface10") == 10)
                        current.RemoveFacilityHexTemporary(range, node);
                    node = next;
                }
            }
            for (int channel = 0; channel < 2; channel++)
            {
                var effect = definition.Channel(channel);
                if (effect.Effect == 0) continue;
                if (effect.Effect is not (1 or 2 or 3 or 15 or 17 or 18)) throw Missing("effect_" + effect.Effect);
                // The native collector allocates this Base de-dup descriptor
                // even though type15 never admits a Base to its Hex output.
                var seenBases = current.CreateNearbyTemporary();
                try
                {
                    foreach (var hex in ReadHexes(current, range))
                    {
                        int key = K(hex.PhysicalSlot, "channel_Hex_pointer");
                        //The existing16EB20 adapter takes an index and looks it
                        //up again. New trap paths reject a non-idempotent alias
                        //rather than selecting another pointer; kind7 is kept
                        //on its existing read sequence.
                        if (category == 4 && current.ReadHexTableReference(key) != hex)
                            throw Missing("trap_target_Hex_alias_16EB20");
                        var selected = FieldHexOccupantSource.ReadCurrent(state, current, key);
                        var target = selected.Receipt ?? throw Missing(selected.UnresolvedOwner ?? "16EB20");
                        if (target.NativeKind is 5 or 6)
                        {
                            if (target.CapturedObject is not { } obj) throw Missing("Base_Object");
                            if (!K(current.ReadObjectNativeValidity(obj), "Base_Object_validity")) continue;
                            if (current.ReadCurrentListValues(seenBases).Contains(new RegistryObjectReference(RegistryKind.Object, obj.PhysicalSlot))) continue;
                            var baseRef = target.NativeKind == 5 ? current.ReadCityTableReference(target.NativePhysicalSlot)
                                : current.ReadGateTableReference(target.NativePhysicalSlot);
                            if (baseRef is null) throw Missing("Base_pointer");
                            if (K(current.ReadBaseNativeValidity(baseRef), "Base_validity") &&
                                !K(OwnedArmyDestructionCurrent.ReadBaseExcluded2334B0(state, current,
                                    target.BaseLocationId ?? throw Missing("Base_location")), "Base_2334B0"))
                            {
                                current.AppendFacilityObjectTemporary(seenBases, obj);
                                _ = K(current.ReadObjectForceSlot13(obj), "Base_Object_force");
                                if (effect.Effect == 17 && AdmitFireHex(state, current, hex, identity))
                                    current.AppendNearbyHexTemporary(channels[channel], hex);
                            }
                            continue;
                        }
                        if (target.NativeKind != 27)
                        {
                            if (effect.Effect == 17 && AdmitFireHex(state, current, hex, identity))
                                current.AppendNearbyHexTemporary(channels[channel], hex);
                            continue;
                        }
                        var army = current.ReadArmyUnitTableReference(target.NativePhysicalSlot <= 500 ? target.NativePhysicalSlot : 0)
                            ?? throw Missing("target_Army_pointer");
                        if (!K(current.ReadArmyUnitNativeValidity(army), "target_Army_validity")) continue;
                        if (K(current.ReadArmyExclusionForQuerySnapshot(state, target.NativePhysicalSlot), "target_Army233560")) continue;
                        int other = current.ReadArmyUnitRawForceSlot(army);
                        if (effect.Effect == 17)
                        {
                            if (AdmitFireHex(state, current, hex, identity)) current.AppendNearbyHexTemporary(channels[channel], hex);
                            continue;
                        }
                        if (effect.Effect is 1 or 2 or 3)
                        {
                            if (K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, identity, other), "trap_target_directional_relation"))
                                current.AppendNearbyHexTemporary(channels[channel], hex);
                            continue;
                        }
                        if (other != identity)
                        {
                            if (other is < 0 or > 51) throw Missing("Army_raw_Force_EA_index");
                            var row = state.NativeArmySimulation?.Forces.GetValueOrDefault(identity)?.RelationsEA;
                            if (row is not { Length: 52 }) throw Missing("facility_directional_EA");
                            if ((row[other] & 127) == 0) continue;
                        }
                        current.AppendNearbyHexTemporary(channels[channel], hex);
                    }
                }
                finally { current.ClearNearbyTemporary(seenBases, true); }
            }
        }
        finally { current.ClearNearbyTemporary(range, true); }
    }

    /// <summary>Effect17's independent signed-occupancy and Object gates.
    /// A friendly Object rejects only this Hex, never the remaining channel.</summary>
    private static bool AdmitFireHex(GameState state, SourceWorldContext current,
        HexReference hex, int sourceForce)
    {
        bool permitted = true;
        var indexed = IndexedHex();
        if (indexed.PhysicalSlot.HasValue)
        {
            int first = unchecked((short)K(current.ReadHexRaw1C(indexed), "fire_target_Hex_signed1C"));
            if (first is >= 1 and <= 500)
            {
                int raw = unchecked((short)K(current.ReadHexRaw1C(hex), "fire_target_Hex_signed1C_again"));
                if (raw < 0) throw Missing("fire_target_negative_Army_index");
                var army = current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0) ?? throw Missing("fire_target_Army_pointer");
                if (K(current.ReadArmyUnitNativeValidity(army), "fire_target_Army_validity"))
                    permitted = K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, sourceForce,
                        current.ReadArmyUnitRawForceSlot(army)), "fire_target_Army_relation");
            }
        }
        indexed = IndexedHex();
        if (indexed.PhysicalSlot.HasValue && K(current.ReadHexObjectLink(indexed), "fire_target_indexed_Object_link") != 0)
        {
            ushort link = K(current.ReadHexObjectLink(hex), "fire_target_Object_link");
            var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw Missing("fire_target_Object_pointer");
            if (K(current.ReadObjectNativeValidity(obj), "fire_target_Object_validity") &&
                !K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, sourceForce,
                    K(current.ReadObjectForceSlot13(obj), "fire_target_Object_Force13")), "fire_target_Object_relation"))
                return false;
        }
        return permitted;

        HexReference IndexedHex()
        {
            int identity = K(current.ReadHexPhysicalSlot(hex), "fire_target_Hex_identity");
            return current.ReadHexTableReference(identity < 48400 ? identity : 0) ?? throw Missing("fire_target_indexed_Hex_pointer");
        }
    }

    internal static IEnumerable<HexReference> ReadHexes(SourceWorldContext current, HexListHandle list)
    {
        var node = current.ReadHexListHead(list) ?? throw Missing("Hex_head");
        while (node.Identity.HasValue)
        {
            yield return current.ReadHexListNodePayload(node) ?? throw Missing("Hex_payload");
            node = current.ReadHexListNodeNext(node) ?? throw Missing("Hex_next");
        }
    }
}
