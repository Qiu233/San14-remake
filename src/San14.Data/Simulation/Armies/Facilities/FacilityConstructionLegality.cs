using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

internal static class FacilityConstructionLegality
{
    internal static bool CheckCommander(GameState state, SourceWorldContext current, int person,
        HexReference target, ObjectDefinitionReference reference)
    {
        var people = current.CreateNearbyTemporary();
        try
        {
            current.AppendConstructionPersonTemporary(people, person);
            return Check(state, current, people, target, reference);
        }
        finally { current.ClearNearbyTemporary(people, true); }
    }

    internal static bool Check(GameState state, SourceWorldContext current, HexListHandle people,
        HexReference target, ObjectDefinitionReference reference)
    {
        var entries = current.ReadCurrentListValues(people);
        if (entries.Count == 0 || target.PhysicalSlot is null) return false;
        if (SceneBlocks(current, target) || !K(current.ReadObjectDefinitionValidity(reference), "legality_definition_validity")) return false;
        var definition = Read(current, reference);
        int surface = K(current.ReadHexSurfaceCode(target), "legality_surface");
        if (surface is < 0 or >= 31 || (definition.TerrainMask & (1u << surface)) == 0) return false;
        int first = Person(entries[0]);
        var force = current.ReadPersonForce(first) ?? throw Missing("legality_first_Person_Force");
        if (!ForceSite(state, current, force, target, definition)) return false;
        bool qualified = false;
        foreach (var entry in entries)
        {
            int person = Person(entry);
            if (!K(current.ReadPersonNativeValidity(person), "legality_person_validity")) continue;
            var personForce = current.ReadPersonForce(person) ?? throw Missing("legality_Person_Force");
            if (!K(current.ReadForceNativeValidity(personForce), "legality_Person_Force_validity")) continue;
            int physical = K(personForce.PhysicalSlot, "legality_Person_Force_pointer");
            if (!state.Forces.TryGetValue(physical, out var ownedForce)) throw Missing("legality_Force_owner");
            if (definition.RequiredPolicyEffect != 0)
            {
                if (!state.PolicyEffects.ContainsKey(definition.RequiredPolicyEffect)) throw Missing("legality_policy_definition");
                int level = K(BattleRemakeCurrentForcePolicies.ReadForceLevel(state, ownedForce,
                    definition.RequiredPolicyEffect), "legality_policy_current_level");
                if (level < definition.RequiredPolicyLevel) continue;
            }
            // The accepted consumer is ordinary kind7. Its required trait is
            // exactly zero; other definitions need2307D0's actual pool collector.
            if (definition.RequiredTrait != 0) throw Missing("legality_required_trait_collector");
            qualified = true; break;
        }
        if (!qualified) return false;
        if (definition.RequiredTrait != 0)
            return entries.Any(entry => K(TacticRemakeCurrentTraits.CommanderHasEffect(current,
                Person(entry), definition.RequiredTrait), "legality_selected_trait"));
        return true;
    }

    internal static bool SceneBlocks(SourceWorldContext current, HexReference target)
    {
        int raw = K(current.ReadHexScenerySlot(target), "site_scenery_link");
        var scenery = current.ReadSceneryTableReference(raw <= 100 ? raw : 0) ?? throw Missing("site_scenery_pointer");
        return K(current.ReadSceneryNativeValidity(scenery), "site_scenery_validity") &&
            (K(current.ReadSceneryFlags47(scenery), "site_scenery_flags") & 1) != 0;
    }

    private static bool ForceSite(GameState state, SourceWorldContext current, ForceReference force,
        HexReference target, FacilityConstructionDefinition definition)
    {
        if (!K(current.ReadForceNativeValidity(force), "site_Force_validity") || target.PhysicalSlot is null) return false;
        //2795E0 repeats scenery/terrain/definition gates before object/land.
        if (SceneBlocks(current, target)) return false;
        int surface = K(current.ReadHexSurfaceCode(target), "site_surface");
        if (surface is < 0 or >= 31 || (definition.TerrainMask & (1u << surface)) == 0) return false;
        int own = K(force.PhysicalSlot, "site_Force_pointer");
        var existing = ObjectAt(current, target);
        if (K(current.ReadObjectNativeValidity(existing), "site_Object_validity"))
        {
            var existingDefinition = DefinitionOf(current, existing);
            if (K(current.ReadObjectDefinitionRaw2C(existingDefinition), "site_Object_category") != 4) return false;
            if (!Relation(state, own, K(current.ReadObjectForceSlot13(existing), "site_Object_force"))) return false;
        }
        int land = K(current.ReadHexForceSlot(target), "site_land_force");
        var landForce = current.ReadForceTableReference(land <= 51 ? land : 0) ?? throw Missing("site_land_Force_pointer");
        int landPhysical = K(landForce.PhysicalSlot, "site_land_Force_identity");
        if (landPhysical != own && !Relation(state, own, landPhysical)) return false;
        int areaRaw = K(current.ReadHexAreaSlot(target), "site_Area_link");
        var area = current.ReadAreaTableReference(areaRaw <= 500 ? areaRaw : 0) ?? throw Missing("site_Area_pointer");
        if (AreaIsFull(current, area)) return false;
        if (definition.Category == 4) throw Missing("site_trap_Area_Force");
        //Actual caller passes0: no Force-global2C4D60 count gate here.
        return RadiusAllows(current, target, definition.Category == 2);
    }

    private static bool AreaIsFull(SourceWorldContext current, AreaReference area)
    {
        if (!K(current.ReadAreaNativeValidity(area), "capacity_Area_validity")) return false;
        int physical = K(area.PhysicalSlot, "capacity_Area_identity"), count = 0;
        for (int hex = AreaHexIterator.FindNextIndex(0, physical, current); hex < 48400;
            hex = AreaHexIterator.FindNextIndex(hex + 1, physical, current))
        {
            var candidate = current.ReadHexTableReference(hex) ?? throw Missing("capacity_Hex_pointer");
            var obj = ObjectAt(current, candidate);
            if (!K(current.ReadObjectNativeValidity(obj), "capacity_Object_validity")) continue;
            var definition = DefinitionOf(current, obj);
            if (!K(current.ReadObjectDefinitionValidity(definition), "capacity_definition_validity")) continue;
            byte category = K(current.ReadObjectDefinitionRaw2C(definition), "capacity_definition_category");
            if (category is 2 or 3 or 4) count++;
        }
        return K(current.ReadAreaFacilityCapacity(area), "capacity_current_limit") <= count;
    }

    private static bool RadiusAllows(SourceWorldContext current, HexReference target, bool includeFacilities)
    {
        int center = K(target.PhysicalSlot, "radius_center");
        var ring = current.CreateNearbyTemporary();
        try
        {
            for (byte radius = 1; radius <= 4; radius++)
            {
                current.ClearNearbyTemporary(ring, false);
                //2B4B20/2B4C70 emit only this outer ring; reuse the already
                //proved geometry order without allocating the preceding rings.
                foreach (int key in TacticRemakeCurrentContext.NativeHexOrder(center, radius).Skip(1))
                    if (ArmyRouteSearch.Distance(center, key) == radius)
                    {
                        var hex = current.ReadHexTableReference(key) ?? throw Missing("radius_Hex_pointer");
                        if (hex.PhysicalSlot.HasValue) current.AppendNearbyHexTemporary(ring, hex);
                    }
                foreach (var entry in current.ReadCurrentListValues(ring))
                {
                    var hex = entry as HexReference ?? throw Missing("radius_Hex_payload");
                    var obj = ObjectAt(current, hex);
                    if (!K(current.ReadObjectNativeValidity(obj), "radius_Object_validity")) continue;
                    var reference = DefinitionOf(current, obj);
                    if (!K(current.ReadObjectDefinitionValidity(reference), "radius_definition_validity")) continue;
                    byte category = K(current.ReadObjectDefinitionRaw2C(reference), "radius_definition_category");
                    if (category is 3 or 4 || !includeFacilities && category == 2) continue;
                    if (radius <= Read(current, reference).ExclusionRadius) return false;
                }
            }
            return true;
        }
        finally { current.ClearNearbyTemporary(ring, true); }
    }

    internal static ObjectReference ObjectAt(SourceWorldContext current, HexReference target)
    {
        ushort raw = K(current.ReadHexObjectLink(target), "Hex_Object_link");
        return current.ReadObjectTableReference(raw <= 3000 ? raw : 0) ?? throw Missing("Hex_Object_pointer");
    }
    internal static ObjectDefinitionReference DefinitionOf(SourceWorldContext current, ObjectReference obj)
    {
        var record = current.ReadPhysicalObject(K(obj.PhysicalSlot, "Object_physical")) ?? throw Missing("Object_definition_fields");
        return current.ReadObjectDefinitionReference(record.RawField12 <= 30 ? record.RawField12 : 0) ?? throw Missing("Object_definition_pointer");
    }
    private static bool Relation(GameState state, int own, int other) =>
        K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, own, other), "site_Force_relation");
    private static int Person(object? entry) => entry is RegistryObjectReference { Kind: RegistryKind.Person, PhysicalSlot: int physical }
        ? physical : throw Missing("selected_Person_payload");
}

public sealed partial class SourceWorldContext
{
    internal void AppendConstructionPersonTemporary(HexListHandle list, int person)
    {
        ConditionalRegistryEnter();
        try
        {
            var node = AllocateRegistryNode(new(list.Identity)) ?? throw FacilityConstructionDefinitions.Missing("Person_node_allocator");
            if (node.Identity.HasValue) WriteRegistryNodePayload(node, new(RegistryKind.Person, person));
        }
        finally { ConditionalRegistryExit(); }
    }
}
