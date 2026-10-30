using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>One immutable world reference and its borrowed native query view.
/// The planning operation replaces this reader after publishing a new candidate.
/// No query captures, hashes or serializes another world.</summary>
internal sealed class OriginalAiDomesticCurrent
{
    private readonly ArmyCampaignRuleAdapter adapter;
    private readonly GameState state;
    private readonly OriginalAiActor actor;
    private readonly SourceWorldContext current;
    internal SourceWorldContext Source => current;
    private readonly Dictionary<(int Area, int Force), bool> hostileAreas = [];

    internal OriginalAiDomesticCurrent(ArmyCampaignRuleAdapter adapter, GameState state, OriginalAiActor actor)
    {
        this.adapter = adapter; this.state = state; this.actor = actor;
        current = adapter.CaptureOriginalAiDomesticCurrent(state);
    }

    internal IReadOnlyList<int> FootholdAreas(int nativeBaseKey)
    {
        var at = BaseLookup.Find(nativeBaseKey, current);
        if (!K(current.ReadBaseNativeValidity(at), "foothold_validity")) return [];
        var city = CurrentGateMapQueries.FindCity(at, current);
        int selected = K(city.PhysicalSlot, "foothold_related_City");
        var result = new List<int>();
        foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw Missing("AreaB0"))
        {
            var area = entry ?? throw Missing("Area_payload");
            if (selected is < 1 or > 51 || K(current.ReadAreaCitySlot(area), "Area35") != selected) continue;
            if (K(current.ReadAreaCitySlot(area), "Area35_again") != K(CurrentGateMapQueries.FindCity(at, current).PhysicalSlot, "related_City_again")) continue;
            if (K(current.ReadAreaCurrentForceSlot(area), "Area_Force") != K(current.ReadBaseForceSlot(at), "Foothold_Force")) continue;
            result.Add(K(area.PhysicalSlot, "Area_identity"));
        }
        return result;
    }

    internal int? AreaScalar(int areaId, int field)
    {
        var area = Area(areaId);
        if (!K(current.ReadAreaNativeValidity(area), "Area_validity")) return null;
        return field switch
        {
            7 => adapter.ReadOriginalAiDomesticFoodBasis(state, K(area.PhysicalSlot, "Area_food_identity")),
            14 => Security(K(area.PhysicalSlot, "Area_security_identity")),
            _ => throw Missing("AreaInfo_" + field)
        };
    }
    internal int? PersonScalar(int personId, int field)
    {
        int person = Person(personId);
        if (!K(current.ReadPersonNativeValidity(person), "Person_validity")) return null;
        return field switch
        {
            11 => adapter.ReadOriginalAiDomesticPolitics(state, current, K(current.ReadPersonIdentity(person), "Person10")),
            // The original +8 evaluation is written by the AI scoring node.
            // The operation's explicit evaluation dictionary owns field25.
            25 => throw Missing("PersonInfo25_operation_evaluation"),
            36 => unchecked(PersonAreaAppointmentSource.AdditionalCount(state, current, person) + 1),
            37 => K(current.ReadAssignmentPersonAreaVector(person)?.ActiveCount1E8, "Person1E8"),
            38 => unchecked(PersonAreaAppointmentSource.AdditionalCount(state, current, person) + 1) <=
                K(current.ReadAssignmentPersonAreaVector(person)?.ActiveCount1E8, "Person1E8") ? 1 : 0,
            _ => throw Missing("PersonInfo_" + field)
        };
    }

    internal bool Condition(uint queryId, IReadOnlyList<OriginalAiEntity> operands, IReadOnlyList<int> literals)
    {
        return queryId switch
        {
            2 => SameActorForce(Entity(0), Literal(0)),
            11 => NativeValid(Entity(0)) && DevelopmentLevel(Entity(0).Id, Literal(0)) == Literal(1),
            12 => HasAreaOfficer(Entity(0).Id),
            22 => NativeValid(Entity(0)),
            32 => HasValidAreaAssignment(Entity(0).Id),
            33 => IsCityDuty(Entity(0).Id, true),
            34 => IsCityDuty(Entity(0).Id, false),
            112 => AreaHasHostileTerritory(Entity(0).Id, Entity(1).Id),
            113 => HasPersonality(Entity(0).Id, Literal(0), true),
            114 => HasPersonality(Entity(0).Id, Literal(0), false),
            118 => DistrictNumberEquals(Entity(0).Id, Literal(0)),
            _ => throw Missing("condition_" + queryId)
        };
        OriginalAiEntity Entity(int index) => index < operands.Count ? operands[index] : throw Missing("condition_operand");
        int Literal(int index) => index < literals.Count ? literals[index] : throw Missing("condition_literal");
    }

    private bool NativeValid(OriginalAiEntity entity) => entity.Kind switch
    {
        WorldEntityKind.Officer => K(current.ReadPersonNativeValidity(Person(entity.Id)), "valid_Person"),
        WorldEntityKind.Area => K(current.ReadAreaNativeValidity(Area(entity.Id)), "valid_Area"),
        WorldEntityKind.Force => K(current.ReadForceNativeValidity(Force(entity.Id)), "valid_Force"),
        WorldEntityKind.District => K(current.ReadDistrictNativeValidity(District(entity.Id)), "valid_District"),
        WorldEntityKind.City => K(current.ReadBaseNativeValidity(current.ReadCityTableReference(entity.Id < 52 ? entity.Id : 0)
            ?? throw Missing("valid_City_pointer")), "valid_City"),
        WorldEntityKind.Gate => K(current.ReadBaseNativeValidity(current.ReadGateTableReference(entity.Id is >= 52 and <= 61 ? entity.Id - 51 : 0)
            ?? throw Missing("valid_Gate_pointer")), "valid_Gate"),
        WorldEntityKind.Army => K(current.ReadArmyUnitNativeValidity(new(ArmyPhysical(entity.Id))), "valid_Army"),
        _ => throw Missing("valid_entity_" + entity.Kind)
    };
    private bool SameActorForce(OriginalAiEntity target, int mode)
    {
        var actorForce = adapter.ReadOriginalAiMyselfForce(current, actor)
            ?? throw Missing("actor_Force_pointer");
        var own = Force(actorForce.Id);
        if (!K(current.ReadForceNativeValidity(own), "actor_Force_validity")) return false;
        // The 84-node domestic subtree uses resolver6 (Area -> Force).
        // Other source resolver families are not interpreted as direct IDs.
        if (mode != 6 || target.Kind != WorldEntityKind.Area) throw Missing("condition2_resolver_" + mode);
        var area = Area(target.Id);
        if (!K(current.ReadAreaNativeValidity(area), "condition2_Area_validity")) return false;
        return own == Force(K(current.ReadAreaCurrentForceSlot(area), "condition2_Area_Force"));
    }
    private bool HasAreaOfficer(int id)
    {
        var area = Area(id);
        return K(current.ReadAreaNativeValidity(area), "assigned_Area_validity") &&
            K(current.ReadAssignmentArea36(K(area.PhysicalSlot, "assigned_Area_identity")), "Area36") is >= 1 and <= 6000;
    }
    private int DevelopmentLevel(int id, int field)
    {
        var area = Area(id);
        if (!K(current.ReadAreaNativeValidity(area), "development_Area_validity")) return -1;
        var development = state.Areas.GetValueOrDefault(K(area.PhysicalSlot, "development_Area_identity"))?.Development
            ?? throw Missing("Area_development");
        int? progress = field switch { -1 or 0 => development.Commerce, 1 => development.Agriculture, 2 => development.Barracks, _ => null };
        if (field is not (-1 or 0 or 1 or 2)) return 0;
        return progress is >= 0 and <= ushort.MaxValue ? CeAreaIncomeMath.Level((ushort)progress.Value) : throw Missing("Area_progress");
    }
    private bool HasValidAreaAssignment(int id)
    {
        int person = Person(id);
        if (!K(current.ReadPersonNativeValidity(person), "assignment_Person_validity")) return false;
        var vector = current.ReadAssignmentPersonAreaVector(person) ?? throw Missing("Person_Area_vector");
        for (int index = 0; index < vector.ActiveCount1E8; index++)
        {
            if (index >= vector.Values.Length) throw Missing("Person_Area_vector_length");
            if (K(current.ReadAreaNativeValidity(Area(vector.Values[index])), "assignment_Area_validity")) return true;
        }
        return false;
    }
    private bool IsCityDuty(int id, bool trainer)
    {
        int person = Person(id);
        if (!K(current.ReadPersonNativeValidity(person), "duty_Person_validity")) return false;
        var home = BaseLookup.Find(K(current.ReadPersonFirstLocation(person), "duty_PersonE2"), current);
        if (!K(current.ReadBaseNativeValidity(home), "duty_Base_validity")) return false;
        var city = CurrentGateMapQueries.FindCity(home, current);
        if (!K(current.ReadBaseNativeValidity(city), "duty_City_validity")) return false;
        int physical = K(city.PhysicalSlot, "duty_City_identity");
        return K(trainer ? current.ReadAssignmentCityCC(physical) : current.ReadAssignmentCityCA(physical), "duty_City_officer") ==
            K(current.ReadPersonIdentity(person), "duty_Person10");
    }
    private bool HasPersonality(int id, int value, bool effect)
    {
        int person = Person(id);
        if (!K(current.ReadPersonNativeValidity(person), "trait_Person_validity")) return false;
        for (int slot = 0; slot < 9; slot++)
        {
            var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, slot) ?? throw Missing("trait_pointer");
            if (!K(current.ReadSkillPersonalityValidity(trait), "trait_validity")) continue;
            if (effect ? K(current.ReadPersonalityByte(trait, 0xB6), "trait_first_effect") == value ||
                K(current.ReadPersonalityByte(trait, 0xBA), "trait_second_effect") == value :
                K(current.ReadPersonalityPhysicalSlot(trait), "trait_identity") == value) return true;
        }
        return false;
    }
    private bool DistrictNumberEquals(int id, int number)
    {
        int district = District(id);
        return K(current.ReadDistrictNativeValidity(district), "number_District_validity") &&
            K(current.ReadDistrictNumber(district), "District11") == number;
    }
    private bool AreaHasHostileTerritory(int id, int forceId)
    {
        var key = (id, forceId);
        if (hostileAreas.TryGetValue(key, out bool saved)) return saved;
        bool value = ReadAreaHasHostileTerritory(id, forceId);
        hostileAreas.Add(key, value);
        return value;
    }
    private bool ReadAreaHasHostileTerritory(int id, int forceId)
    {
        var area = Area(id); var force = Force(forceId);
        if (!K(current.ReadAreaNativeValidity(area), "frontier_Area_validity") ||
            !K(current.ReadForceNativeValidity(force), "frontier_Force_validity")) return false;
        if (K(current.ReadAreaCurrentForceSlot(area), "frontier_Area_Force") == K(current.ReadForcePhysicalSlot(force), "frontier_Force_identity") &&
            K(current.ReadAreaRawField68(area), "frontier_owned68") == K(current.ReadAreaRawField76(area), "frontier_terrain76")) return false;
        int physical = K(area.PhysicalSlot, "frontier_Area_identity");
        for (int hex = AreaHexIterator.FindNextIndex(0, physical, current); hex < 48400;
            hex = AreaHexIterator.FindNextIndex(hex + 1, physical, current))
        {
            int owner = K(current.ReadHexForceSlot(current.ReadHexTableReference(hex) ?? throw Missing("frontier_Hex")), "frontier_Hex14");
            if (K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, K(current.ReadForcePhysicalSlot(force), "frontier_Force_identity"), owner), "frontier_hostility")) return true;
        }
        return false;
    }
    private int Security(int id)
    {
        int? value = state.Areas.GetValueOrDefault(id)?.Security;
        return value is >= 0 and <= byte.MaxValue ? value.Value : throw Missing("Area_security54");
    }
    private int Person(int id) => K((id is >= 0 and <= 6000 ? current.ReadPersonLookupReference(id) :
        current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
    private AreaReference Area(int id) => current.ReadAreaTableReference(id is >= 0 and <= 500 ? id : 0) ?? throw Missing("Area_lookup");
    private ForceReference Force(int id) => current.ReadForceTableReference(id is >= 0 and <= 51 ? id : 0) ?? throw Missing("Force_lookup");
    private int District(int id) => K(current.ReadDistrictTableReference(id is >= 0 and <= 51 ? id : 0)?.PhysicalSlot, "District_lookup");
    private int ArmyPhysical(int id) => state.Units.GetValueOrDefault(id)?.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("Army_current_receiver");
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("original-ai.domestic-current." + field);
}
