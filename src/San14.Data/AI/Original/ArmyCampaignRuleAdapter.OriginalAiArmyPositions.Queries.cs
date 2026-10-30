using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private int? FindOriginalAiPreferredTerrainCurrent(GameState state, SourceWorldContext current,
        UnitState army, int origin, int force, IReadOnlyCollection<int> preferred)
    {
        int? selected = null;
        int shortest = int.MaxValue;
        foreach (int hex in AiPositionRing(current, origin, 3))
        {
            if (!preferred.Contains(AiPositionNeed(current.ReadHexSurfaceCode(new(hex)), "preferred_surface")) ||
                !AiPositionEligible(state, current, army, hex, force)) continue;
            //0BD640 keeps MAXINT on the first accepted pointer; distance is
            //first computed when a later candidate competes with that pointer.
            int distance = selected.HasValue ? ArmyRouteSearch.Distance(origin, hex) : shortest;
            if (selected.HasValue && shortest <= distance) continue;
            selected = hex; shortest = distance;
        }
        return selected;
    }

    private bool AiPositionEligible(GameState state, SourceWorldContext current, UnitState army, int hex, int force)
        => OccupationCenterEligible(state, current, army,
            current.ReadHexTableReference(hex) ?? throw AiPositionMissing("candidate_Hex_pointer"), force);

    private static bool AiPositionReachable(GameState state, SourceWorldContext current, UnitState army, int origin, int goal)
        //The actual source descriptor has flags8, workspace2, actual Army,
        //kind5 and the Object center Hex as its destination operand. flags8
        //uses the mature uniform-cost/passability branch, not a player order.
        => origin == goal || ArmyRouteSearch.Search(state, current, army, origin, goal, 5, checked((ushort)goal),
            army.NativeArmyOrigin?.NativeSlotIndex, out _, searchFlags: 8).Length != 0;

    private bool AiPositionInHostileObjectRange(GameState state, SourceWorldContext current, int hex, int ownForce)
    {
        foreach (var obj in AiPositionNearbyObjects(current, hex, 3))
        {
            int raw = AiPositionNeed(current.ReadObjectForceSlot13(obj), "danger_Object13");
            var owner = current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw AiPositionMissing("danger_Force_pointer");
            //0C0E70's real condition is native INVALID Force, followed by
            //2CA650. Do not silently reverse that gate to a plausible rule.
            if (AiPositionNeed(current.ReadForceNativeValidity(owner), "danger_Force_validity") ||
                !AiPositionNeed(ArmyLogisticsPreparation.ReadNativeRelationGate(state, raw, ownForce), "danger_relation")) continue;
            var definition = AiPositionDefinition(current, obj);
            if (AiPositionNeed(current.ReadObjectDefinitionRaw2C(definition), "danger_definition_category") != 2) continue;
            var effects = current.ReadFacilityEffectDefinition(definition) ?? throw AiPositionMissing("danger_effects");
            if (effects.First.Effect != 1 && effects.Second.Effect != 1) continue;
            if (AiPositionObjectRange(state, current, obj).Contains(hex)) return true;
        }
        return false;
    }

    private static int AiPositionDirectionToward(int center, int origin)
    {
        int x = center % 220, y = center / 220, ax = origin % 220, ay = origin / 220;
        return x == ax ? y > ay ? 2 : 5 : x < ax ? y > ay ? 3 : 6 : ay < y ? 1 : 4;
    }

    private static IReadOnlyList<int> AiPositionObjectRange(GameState state, SourceWorldContext current, ObjectReference obj)
    {
        //2AB840: exact ring-only definition minimum/maximum. The source
        //definition getter owns encoded data and advanced setting25 behavior.
        int center = AiPositionObjectHex(current, obj);
        var definition = AiPositionDefinition(current, obj);
        if (!AiPositionNeed(current.ReadObjectDefinitionValidity(definition), "range_definition_validity")) return [];
        int kind = AiPositionNeed(definition.PhysicalSlot, "range_definition_identity");
        var fields = current.ReadFacilityEffectDefinition(definition) ?? throw AiPositionMissing("range_definition_fields");
        return AiPositionRing(current, center, fields.MaximumFor(state, kind))
            .Where(hex => fields.Minimum < 1 || ArmyRouteSearch.Distance(center, hex) >= fields.Minimum).ToArray();
    }

    private static IReadOnlyList<ObjectReference> AiPositionNearbyObjects(SourceWorldContext current, int center, int radius)
    {
        //2AEFA0 visits ring Hexes and appends the first occurrence of each
        //actual Object pointer. It adds no Object-validity/definition filter.
        var result = new List<ObjectReference>();
        var output = current.CreateNearbyTemporary();
        try
        {
            foreach (int hex in AiPositionRing(current, center, radius))
            {
                int raw = AiPositionNeed(current.ReadHexObjectLink(new(hex)), "nearby_Hex1A");
                if (raw is < 1 or > 3000) continue;
                var obj = current.ReadObjectTableReference(raw) ?? throw AiPositionMissing("nearby_Object_pointer");
                bool present = false;
                var node = current.ReadRegistryHead(new(output.Identity)) ?? throw AiPositionMissing("nearby_Object_head");
                while (node.Identity.HasValue)
                {
                    var payload = current.ReadRegistryNodePayload(node) ?? throw AiPositionMissing("nearby_Object_payload");
                    if (payload.Kind != RegistryKind.Object) throw AiPositionMissing("nearby_Object_kind");
                    if (payload.PhysicalSlot == obj.PhysicalSlot) { present = true; break; }
                    node = current.ReadRegistryNodeNext(node) ?? throw AiPositionMissing("nearby_Object_next");
                }
                if (!present) current.AppendFacilityObjectTemporary(output, obj);
            }
            var cursor = current.ReadRegistryHead(new(output.Identity)) ?? throw AiPositionMissing("nearby_Object_head");
            while (cursor.Identity.HasValue)
            {
                var payload = current.ReadRegistryNodePayload(cursor) ?? throw AiPositionMissing("nearby_Object_payload");
                if (payload.Kind != RegistryKind.Object || payload.PhysicalSlot is not int physical) throw AiPositionMissing("nearby_Object_identity");
                result.Add(new(physical));
                cursor = current.ReadRegistryNodeNext(cursor) ?? throw AiPositionMissing("nearby_Object_next");
            }
            return result;
        }
        finally { current.ClearNearbyTemporary(output, true); }
    }

    private static IReadOnlyList<int> AiPositionRing(SourceWorldContext current, int center, int radius)
    {
        var list = current.CreateNearbyTemporary();
        try
        {
            NearbyArmyTemporarySource.FillNearbyHexTemporary(current,
                current.ReadHexTableReference(center) ?? throw AiPositionMissing("ring_center"), radius, list);
            return FacilityEffectTargets.ReadHexes(current, list)
                .Select(hex => AiPositionNeed(hex.PhysicalSlot, "ring_Hex_identity")).ToArray();
        }
        finally { current.ClearNearbyTemporary(list, true); }
    }

    private static IReadOnlyList<ArmyUnitReference> AiPositionNearbyArmies(SourceWorldContext current, ArmyUnitReference army, int radius)
    {
        var list = current.CreateNearbyTemporary();
        var result = new List<ArmyUnitReference>();
        try
        {
            NearbyArmyTemporarySource.FillNearbyArmyTemporary(current, army, radius, list);
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw AiPositionMissing("nearby_Army_head");
            while (node.Identity.HasValue)
            {
                var payload = current.ReadRegistryNodePayload(node) ?? throw AiPositionMissing("nearby_Army_payload");
                if (payload.Kind != RegistryKind.Army || payload.PhysicalSlot is not int physical) throw AiPositionMissing("nearby_Army_identity");
                result.Add(new(physical));
                node = current.ReadRegistryNodeNext(node) ?? throw AiPositionMissing("nearby_Army_next");
            }
            return result;
        }
        finally { current.ClearNearbyTemporary(list, true); }
    }

    private static ObjectDefinitionReference AiPositionDefinition(SourceWorldContext current, ObjectReference obj)
    {
        var fields = current.ReadPhysicalObject(AiPositionNeed(obj.PhysicalSlot, "Object_receiver")) ?? throw AiPositionMissing("Object_fields");
        return current.ReadObjectDefinitionReference(fields.RawField12 <= 30 ? fields.RawField12 : 0)
            ?? throw AiPositionMissing("Object_definition_pointer");
    }
    private static int AiPositionObjectHex(SourceWorldContext current, ObjectReference obj) =>
        AiPositionHex(current, AiPositionNeed(current.ReadObjectHexIndex(obj), "Object_Hex10"));
    private static int AiPositionHex(SourceWorldContext current, int raw) =>
        AiPositionNeed(current.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0)?.PhysicalSlot, "Hex_identity");
    private static int AiPositionForce(SourceWorldContext current, ArmyUnitReference army) =>
        AiPositionNeed(current.ReadArmyUnitForceReference(army).PhysicalSlot, "Army_Force");
    private static ArmyUnitReference AiPositionArmyAt(SourceWorldContext current, int hex)
    {
        int raw = unchecked((short)AiPositionNeed(current.ReadHexRaw1C(new(hex)), "neighbor_Hex1C"));
        if (raw < 0) throw AiPositionMissing("neighbor_negative_Army_index");
        return current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0) ?? throw AiPositionMissing("neighbor_Army_pointer");
    }
    private static int AiPositionCommander(SourceWorldContext current, UnitState army)
    {
        int raw = AiPositionNeed(current.ReadArmyUnitCommanderId(new(AiPositionNeed(army.NativeArmyOrigin?.NativeSlotIndex, "Army_receiver"))), "Commander12");
        return AiPositionNeed(current.ReadPersonLookupReference(raw <= 6000 ? raw : 0)?.PhysicalSlot, "Commander_pointer");
    }
    private static bool AiPositionHasPersonality(SourceWorldContext current, int person, int key)
    {
        if (!AiPositionNeed(current.ReadPersonNativeValidity(person), "Commander_validity")) return false;
        for (int index = 0; index < 9; index++)
            if (AiPositionNeed(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, person, index), "Commander_personality") == key) return true;
        return false;
    }
    private static int AiPositionFormation(GameState state, SourceWorldContext current, UnitState army)
    {
        int physical = AiPositionNeed(army.NativeArmyOrigin?.NativeSlotIndex, "Army_receiver");
        int at = AiPositionHex(current, AiPositionNeed(current.ReadArmyUnitRawHex2A(new(physical)), "formation_Army2A"));
        int surface = AiPositionNeed(current.ReadHexSurfaceCode(new(at)), "formation_Hex10");
        var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0) ?? throw AiPositionMissing("formation_Terrain");
        bool naval = terrain.PhysicalSlot != 0 &&
            (AiPositionNeed(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, AiPositionNeed(terrain.PhysicalSlot, "formation_Terrain_identity")), "formation_Terrain2C") & 2) != 0;
        int code = AiPositionNeed(naval ? ArmyConfigurationSourceAdapter.ReadNaval(state, army) : ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army), "formation_code");
        return AiPositionNeed(current.ReadStaticTableReference(0x77428, code <= 20 ? code : 0)?.PhysicalSlot, "Formation_pointer");
    }
    private static T AiPositionNeed<T>(T? value, string field) where T : struct => value ?? throw AiPositionMissing(field);
    private static InvalidDataException AiPositionMissing(string field) => new("original_ai.Army_position." + field);
}
