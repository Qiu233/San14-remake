using San14.Core;
using static San14.Data.FacilityEffectCandidates;

namespace San14.Data;

internal sealed record TrapTriggerInputs(ObjectReference Facility, HexReference Center,
    ForceReference Force, bool Water);

/// <summary>Class4's current E0 collector, ring-only Army candidates and
/// trigger predicate. The enclosing trap operation owns the Army descriptor
/// through effects, demolition and the caller report; this helper never
/// caches exclusions across changing candidate worlds.</summary>
internal static class TrapCandidates
{
    internal static void Fill(GameState state, SourceWorldContext current, HexListHandle output)
    {
        current.ClearNearbyTemporary(output, false);
        var directory = current.CurrentMapObjectDirectory ?? throw Missing("trap_object_directory");
        var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("trap_pool");
        var pending = pool.PendingObjectRetirements ?? throw Missing("trap_retirement_F0");
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Object) ?? throw Missing("trap_registry_E0"))
        {
            if (entry is not { Kind: RegistryKind.Object, PhysicalSlot: int physical }) throw Missing("trap_registry_Object_pointer");
            var obj = new ObjectReference(physical);
            if (!K(current.ReadObjectNativeValidity(obj), "trap_Object_validity")) continue;
            var receiver = directory.ReceiverAtPhysical(physical);
            bool retired = false;
            foreach (var id in pending)
            {
                if (id is null) throw Missing("trap_retirement_pointer");
                if (id == receiver) { retired = true; break; }
            }
            if (retired) continue;
            var definition = Definition(current, obj);
            if (!K(current.ReadObjectDefinitionValidity(definition), "trap_definition_validity") ||
                K(current.ReadObjectDefinitionRaw2C(definition), "trap_definition_category") != 4) continue;
            if (!K(FacilityActivationCurrent.ReadEffectsEnabled(current, obj), "trap_activation16")) continue;
            current.AppendFacilityObjectTemporary(output, obj);
        }
    }

    /// <summary>The manager's pre-allocation reads: definition, Hex, Force,
    /// then center terrain. Allocate the Army descriptor only after this
    /// returns a value.190260's independent range read belongs after it.</summary>
    internal static TrapTriggerInputs? Prepare(GameState state, SourceWorldContext current,
        ObjectReference facility)
    {
        var definition = Definition(current, facility);
        if (!K(current.ReadObjectDefinitionValidity(definition), "trap_current_definition_validity")) return null;
        var center = Position(current, facility);
        if (!center.PhysicalSlot.HasValue) return null;
        int rawForce = K(current.ReadObjectForceSlot13(facility), "trap_Force13");
        var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0) ?? throw Missing("trap_Force_pointer");
        if (!K(current.ReadForceNativeValidity(force), "trap_Force_validity")) return null;
        return new(facility, center, force, WaterAtHex(state, current, center));
    }

    internal static bool IsTriggered(GameState state, SourceWorldContext current,
        TrapTriggerInputs prepared, FacilityEffectDefinition definition, HexListHandle nearby)
    {
        //18E9C0 repeats the Object-to-Hex lookup after allocating the Army
        //descriptor, before reading190260; the earlier Hex only supplied
        //the validity and saved water prefix.
        var center = Position(current, prepared.Facility);
        int range = K(definition.TriggerRange, "trap_TriggerRange");
        FillNearbyArmies(state, current, center, range, nearby);
        var node = current.ReadRegistryHead(new(nearby.Identity)) ?? throw Missing("trap_nearby_head");
        while (node.Identity.HasValue)
        {
            var entry = current.ReadRegistryNodePayload(node) ?? throw Missing("trap_nearby_payload");
            if (entry.Kind != RegistryKind.Army) throw Missing("trap_nearby_Army_kind");
            var army = new ArmyUnitReference(entry.PhysicalSlot);
            if (ArmyWater(state, current, army) == prepared.Water)
            {
                //The source reads the Army's raw Force before the saved
                //facility Force identity and then performs the directed gate.
                int other = current.ReadArmyUnitRawForceSlot(army);
                int own = K(current.ReadForcePhysicalSlot(prepared.Force), "trap_Force_identity");
                if (K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, own, other), "trap_directional_relation")) return true;
            }
            node = current.ReadRegistryNodeNext(node) ?? throw Missing("trap_nearby_next");
        }
        //18ECF6 passes channel1. This is independent of channel0's effect17.
        if (definition.Second.Effect != 17) return false;
        var hexes = current.CreateNearbyTemporary();
        try
        {
            NearbyArmyTemporarySource.FillNearbyHexTemporary(current, Position(current, prepared.Facility),
                K(definition.TriggerRange, "trap_fire_TriggerRange"), hexes);
            current.AppendNearbyHexTemporary(hexes, Position(current, prepared.Facility));
            foreach (var hex in FacilityEffectTargets.ReadHexes(current, hexes))
                if (WaterAtHex(state, current, hex) == prepared.Water &&
                    K(current.ReadCaptureHexWord16(K(hex.PhysicalSlot, "trap_fire_Hex_pointer")), "trap_current_fire16") != 0)
                    return true;
            return false;
        }
        //On a fire match the source releases this Hex chain BEFORE effects;
        //the manager-owned nearby Army chain is still alive at that point.
        finally { current.ClearNearbyTemporary(hexes, true); }
    }

    /// <summary>2AE570: ring-only current Hex1C, signed1..500, validity,
    /// exclusion and current2C96E0. No center, sort, dedup or16EB20.</summary>
    internal static int FillNearbyArmies(GameState state, SourceWorldContext current,
        HexReference center, int radius, HexListHandle output)
    {
        if (current.ReadOwnedCurrentListPool() is null) throw Missing("trap_nearby_primary_pool");
        current.ClearNearbyTemporary(output, false);
        var hexes = current.CreateNearbyTemporary();
        try
        {
            NearbyArmyTemporarySource.FillNearbyHexTemporary(current, center, Math.Max(radius, 0), hexes);
            foreach (var hex in FacilityEffectTargets.ReadHexes(current, hexes))
            {
                int first = unchecked((short)K(current.ReadHexRaw1C(hex), "trap_nearby_Hex_signed1C"));
                if (first is < 1 or > 500) continue;
                int raw = unchecked((short)K(current.ReadHexRaw1C(hex), "trap_nearby_Hex_signed1C_again"));
                if (raw < 0) throw Missing("trap_nearby_changed_negative_Army_index");
                var army = current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0) ?? throw Missing("trap_nearby_Army_pointer");
                if (!K(current.ReadArmyUnitNativeValidity(army), "trap_nearby_Army_validity")) continue;
                int physical = K(army.PhysicalSlot, "trap_nearby_Army_receiver");
                if (K(current.ReadArmyExclusionForQuerySnapshot(state, physical), "trap_nearby_Army233560")) continue;
                //This source query repeats233560 internally and uses current
                //Person/District/Home fields; it has no cross-trap cache.
                if (current.IsArmyUnitMapWriteSkipped(army)) continue;
                current.AppendNearbyArmyTemporary(output, army);
            }
            return K(current.ReadTemporaryHexListCount(output), "trap_nearby_Army_count");
        }
        finally { current.ClearNearbyTemporary(hexes, true); }
    }

    internal static bool WaterAtHex(GameState state, SourceWorldContext current, HexReference hex)
    {
        byte raw = K(current.ReadHexSurfaceCode(hex), "trap_Hex_surface");
        return WaterAtTerrain(state, current, raw <= 30 ? raw : 0);
    }

    private static bool ArmyWater(GameState state, SourceWorldContext current, ArmyUnitReference army)
    {
        ushort raw = K(current.ReadArmyUnitRawHex2A(army), "trap_Army_Hex2A");
        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("trap_Army_Hex_pointer");
        //22B8C0 has a Hex validity gate, unlike231A10's direct Hex10 read.
        if (!hex.PhysicalSlot.HasValue) return WaterAtTerrain(state, current, 0);
        return WaterAtHex(state, current, hex);
    }

    private static bool WaterAtTerrain(GameState state, SourceWorldContext current, int slot)
    {
        var terrain = current.ReadSurfaceTypeTableReference(slot) ?? throw Missing("trap_terrain_pointer");
        int physical = K(terrain.PhysicalSlot, "trap_terrain_receiver");
        return (K(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, physical), "trap_terrain_water_flags") & 2) != 0;
    }

    private static ObjectDefinitionReference Definition(SourceWorldContext current, ObjectReference facility)
    {
        var record = current.ReadPhysicalObject(K(facility.PhysicalSlot, "trap_Object_pointer")) ?? throw Missing("trap_Object_kind");
        return current.ReadObjectDefinitionReference(record.RawField12 <= 30 ? record.RawField12 : 0)
            ?? throw Missing("trap_definition_pointer");
    }

    private static HexReference Position(SourceWorldContext current, ObjectReference facility)
    {
        ushort raw = K(current.ReadObjectHexIndex(facility), "trap_Object_Hex");
        return current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("trap_Hex_pointer");
    }
}
