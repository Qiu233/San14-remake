using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

internal sealed record OriginalAiDeparturePreparation(DeploymentSource Source, DeploymentUnitRequest Unit,
    ArmyCreationConfiguration Configuration, ArmyTargetSourceFields TargetFields, int GoalHex);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>034E60's actual inputs and admission before1EC980. The caller
    /// owns the formal command, source bit0 and receiver creation transaction.
    /// No UI deployment defaults, world capture or provisional Army is used.</summary>
    internal OriginalAiDeparturePreparation? PrepareOriginalAiDepartureCurrent(GameState state,
        SourceWorldContext current, OriginalAiActor actor, int officerId, int requestedTroops,
        int formationId, IReadOnlyList<int> tactics, int action, OriginalAiEntity? targetEntity,
        OriginalAiHex? targetHex, int category, int retreat, int pursuit, int choice,
        OriginalAiDefinitionReference? facility, OriginalAiHex? continuation)
    {
        int person = ReadOriginalAiPersonInformationReceiver(current, officerId);
        if (!DepartureNeed(current.ReadPersonNativeValidity(person), "Person_validity")) return null;
        int districtRaw = DepartureNeed(current.ReadPersonDistrictSlot(person), "PersonE0");
        int district = DepartureNeed(current.ReadDistrictTableReference(districtRaw < 52 ? districtRaw : 0)?.PhysicalSlot,
            "District_pointer");
        if (DepartureNeed(current.ReadDistrictRawByte(district, 0x14), "District_books14") == 0) return null;
        //22CF60 returns the valid District's raw Force10 byte;0B5E30
        //returns an actor Force pointer whose virtual60 is physical identity.
        int personForceCode = DepartureNeed(current.ReadPersonOldForceSlot(person), "Person_Force_code");
        var actorForce = ReadOriginalAiMyselfForce(current, actor) ?? throw OriginalAiDepartureMissing("actor_Force_pointer");
        if (personForceCode != actorForce.Id) return null;
        if (formationId is < 0 or > 20) throw OriginalAiDepartureMissing("Formation_identity");
        // CFormationData native validity is physical identity !=0. There is
        // no UI formation-mask, naval-kind or command-capacity gate here.
        if (formationId == 0) return null;
        var forceRef = current.ReadPersonForce(person) ?? throw OriginalAiDepartureMissing("Person_Force_pointer");
        if (!DepartureNeed(current.ReadForceNativeValidity(forceRef), "Person_Force_validity")) return null;
        int force = DepartureNeed(forceRef.PhysicalSlot, "Person_Force_identity");
        int naval = PersonNavalFormationSelection.Resolve(state, current, person);
        var selectedTactics = new List<int>(3);
        foreach (int physical in tactics)
        {
            if (physical is < 0 or > 200) throw OriginalAiDepartureMissing("Tactic_identity");
            uint mask = 0;
            for (int part = 0; part < 4; part++) mask |= (uint)DepartureNeed(
                current.ReadSkillTacticRawByte(new(physical), 0x6C + part), "Tactic_formation_mask") << (part * 8);
            if (((mask >> formationId) & 1) == 0) continue;
            selectedTactics.Add(physical);
            if (selectedTactics.Count == 3) break;
        }
        while (selectedTactics.Count < 3) selectedTactics.Add(0);

        byte targetKind = 255;
        int targetIdentity = 0;
        FacilityConstructionIntent? construction = null;
        int goal = -1;
        switch (action)
        {
            case 1:
                if (targetHex is { } cell) { targetKind = 9; targetIdentity = cell.Id; }
                else if (targetEntity is { Kind: WorldEntityKind.Area } area)
                {
                    var at = current.ReadAreaTableReference(area.Id is >= 0 and < 501 ? area.Id : 0)
                        ?? throw OriginalAiDepartureMissing("target_Area_pointer");
                    targetKind = 9; targetIdentity = DepartureNeed(current.ReadAreaHexIndex(at), "target_Area3A");
                }
                else if (targetEntity is { Kind: WorldEntityKind.City } city)
                {
                    targetKind = 5; targetIdentity = DepartureNeed(current.ReadCityTableReference(city.Id is >= 0 and < 52 ? city.Id : 0)?.PhysicalSlot,
                        "target_City_pointer");
                }
                else if (targetEntity is null) throw OriginalAiDepartureMissing("move_target_object");
                // Other native object classes deliberately retain255/0.
                break;
            case 2 or 4:
            {
                var at = DepartureBase(current, targetEntity);
                int location = DepartureNeed(current.ReadBaseObjectLink(at), "target_Base4E");
                targetKind = 5; targetIdentity = location < 52 ? location : 0;
                break;
            }
            case 3 or 5:
            {
                var at = DepartureBase(current, targetEntity);
                int location = DepartureNeed(current.ReadBaseObjectLink(at), "target_Base4E");
                targetKind = 6; targetIdentity = location is >= 52 and <= 61 ? location - 51 : 0;
                break;
            }
            case 6 or 10:
                if (targetEntity is not { Kind: WorldEntityKind.MapObject } obj) throw OriginalAiDepartureMissing("target_Object");
                targetKind = 11;
                targetIdentity = obj.Id == 0 ? 0 : (current.CurrentMapObjectDirectory ?? throw OriginalAiDepartureMissing("Object_directory")).PhysicalOf(new(obj.Id));
                break;
            case 7:
                if (targetEntity is not { Kind: WorldEntityKind.Army } army) throw OriginalAiDepartureMissing("target_Army");
                targetIdentity = army.Id == 0 ? 0 : WorldIndexContext.FindArmyPhysical(state, army.Id) ?? throw OriginalAiDepartureMissing("Army_receiver");
                if (!DepartureNeed(current.ReadArmyUnitNativeValidity(new(targetIdentity)), "target_Army_validity")) return null;
                targetKind = 27;
                break;
            case 8:
            {
                if (targetHex is not { } constructionCell) throw OriginalAiDepartureMissing("construction_Hex");
                if (facility is not { Kind: OriginalAiDefinitionKind.Facility } selected) throw OriginalAiDepartureMissing("construction_Facility");
                bool permitted = !DepartureNeed(current.ReadDistrictNativeValidity(district), "construction_District_validity");
                if (!permitted)
                {
                    var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw OriginalAiDepartureMissing("construction_player_pointer");
                    bool isPlayer = DepartureNeed(current.ReadForceNativeValidity(player), "construction_player_validity") &&
                        DepartureNeed(current.ReadDistrictForceId(district), "construction_District10") == DepartureNeed(player.PhysicalSlot, "construction_player_identity");
                    permitted = !isPlayer;
                    if (isPlayer)
                    {
                        uint allowed = 0;
                        for (int i = 0; i < 4; i++) allowed |= (uint)DepartureNeed(
                            current.ReadDistrictRawByte(district, 0x1C + i), "construction_District1C") << (i * 8);
                        permitted = selected.PhysicalSlot is >= 0 and < 31 && ((allowed >> selected.PhysicalSlot) & 1) != 0;
                    }
                }
                if (permitted)
                {
                    targetKind = 9; targetIdentity = constructionCell.Id;
                    int last = 48400;
                    byte kind = DepartureNeed(current.ReadObjectDefinitionRaw2C(new(selected.PhysicalSlot)), "construction_Facility2C");
                    if (kind == 3) last = continuation?.Id ?? constructionCell.Id; // Hex native slot3 always true for a known pointer.
                    construction = new(MapObjectSourceDirectory.DefinitionKey(selected.PhysicalSlot), Site(constructionCell.Id), Site(last));
                }
                // A denied player bitmap retains the actual fresh255/0 and
                // no construction selection; it does not change action8.
                break;
            }
            case 14:
                targetKind = 9; targetIdentity = targetHex?.Id ?? throw OriginalAiDepartureMissing("occupation_Hex");
                break;
        }

        ushort homeRaw = DepartureNeed(current.ReadPersonFirstLocation(person), "PersonE2");
        var home = BaseLookup.Find(homeRaw, current);
        int formationGold = DepartureNeed(TacticRemakeCurrentFormations.ReadRawWord(state, formationId, 0x9A), "Formation9A");
        if (formationGold > DepartureNeed(current.ReadBaseResource(home, BaseResourceKind.Gold), "Home34")) return null;
        //0A19B0 evaluates all three current contribution sums in order and
        //overwrites only the four controls. It never supplies missing troops.
        int first = DepartureNeed(TacticRemakeCurrentTraits.CommanderEffectSum(current, person, 1), "effect1");
        int second = DepartureNeed(TacticRemakeCurrentTraits.CommanderEffectSum(current, person, 2), "effect2");
        int third = DepartureNeed(TacticRemakeCurrentTraits.CommanderEffectSum(current, person, 3), "effect3");
        int encodedCategory = category, encodedRetreat = retreat != 0 ? 1 : 0, encodedPursuit = pursuit != 0 ? 1 : 0, encodedChoice = choice;
        if (first != 0) { encodedCategory = 2; encodedRetreat = 0; }
        if (second != 0) { encodedCategory = 1; encodedRetreat = 1; encodedPursuit = 0; }
        if (third != 0) { encodedPursuit = 1; encodedChoice = 2; }
        var behavior = DeploymentDraftDefaults.Decode(Math.Clamp(encodedCategory, 0, 2), encodedRetreat, encodedPursuit, Math.Clamp(encodedChoice, 0, 2));
        if (encodedRetreat == 1)
        {
            var parameters = state.NativeArmySimulation?.Parameters ?? throw OriginalAiDepartureMissing("retreat_parameter_owner");
            const string key = "original-ai.departure-retreat-morale-minimum";
            if (!parameters.Int32Values.TryGetValue(key, out int minimum)) throw OriginalAiDepartureMissing("retreat_morale_parameter");
            if (DepartureNeed(current.ReadBaseMorale44(home), "Home44") < minimum) return null;
        }
        if (!BaseInfoAvailablePeople(state, current, home).Contains(person)) return null;

        var fields = new ArmyTargetSourceFields(targetKind, unchecked((ushort)targetIdentity));
        goal = DepartureGoalHex(current, fields);
        int locationId = DepartureNeed(current.ReadBaseObjectLink(home), "Home4E");
        var source = new DeploymentSource(locationId, force, district, current.ReadBaseCurrentHexIndex(home),
            DepartureNeed(current.ReadBaseResource(home, BaseResourceKind.Troops), "Home3C"));
        int? occupationArea = null;
        if (action == 14 && goal >= 0)
        {
            var target = current.ReadHexTableReference(goal) ?? throw OriginalAiDepartureMissing("occupation_Hex_pointer");
            int rawArea = DepartureNeed(current.ReadHexAreaSlot(target), "occupation_Hex12");
            occupationArea = DepartureNeed(current.ReadAreaTableReference(rawArea < 501 ? rawArea : 0)?.PhysicalSlot, "occupation_Area_pointer");
        }
        var unit = new DeploymentUnitRequest(officerId, formationId, requestedTroops, selectedTactics.ToArray(),
            goal < 0 ? [] : [DepartureDomainAction(action, targetEntity, goal, construction, occupationArea)])
        { NavalFormationId = naval, Behavior = behavior, ConstructionSelection = construction };
        var configuration = new ArmyCreationConfiguration(naval, action, encodedCategory, encodedRetreat, encodedPursuit)
        { Choice50Input = encodedChoice, RouteHold40Input = 0, TargetKind38 = targetKind, SourceDeparture = true };
        return new(source, unit, configuration, fields, goal);
    }

    private static BaseReference DepartureBase(SourceWorldContext current, OriginalAiEntity? entity) =>
        entity is { Kind: WorldEntityKind.City or WorldEntityKind.Gate } value
            ? BaseLookup.Find(value.Id, current) : throw OriginalAiDepartureMissing("target_Base");
    private static ArmyMarchDestination Site(int value) => value == 48400 ? ArmyMarchDestination.None : ArmyMarchDestination.AtHex(value);
    private static DeploymentAction DepartureDomainAction(int action, OriginalAiEntity? entity, int goal, FacilityConstructionIntent? construction, int? occupationArea)
        => action switch
        {
            1 => new("move", new(DeploymentTargetKind.Hex, goal)),
            2 or 3 => new("enter", new(DeploymentTargetKind.Location, entity?.Id ?? 0)),
            4 or 5 => new("attack", new(DeploymentTargetKind.Location, entity?.Id ?? 0)),
            6 => new("attack", new(DeploymentTargetKind.Structure, entity?.Id ?? 0)),
            7 => new("attack", new(DeploymentTargetKind.Unit, entity?.Id ?? 0)),
            8 => new("build", new(DeploymentTargetKind.Hex, goal), construction?.DefinitionKey),
            10 => new("demolish", new(DeploymentTargetKind.Structure, entity?.Id ?? 0)),
            14 => new("occupy", new(DeploymentTargetKind.Area, occupationArea ?? throw OriginalAiDepartureMissing("occupation_projection_Area"))),
            _ => new("source-action", new(DeploymentTargetKind.Hex, goal))
        };
    private static int DepartureGoalHex(SourceWorldContext current, ArmyTargetSourceFields fields)
    {
        int raw = fields.Kind38 switch
        {
            9 => fields.Identity3A!.Value,
            27 => ArmyHex(),
            11 => ObjectHex(),
            5 or 6 => BaseHex(),
            _ => 48400
        };
        // The raw target fields retain native sentinel/invalid knowledge;
        // the domain projection never treats48400 as an actual map cell.
        return raw is >= 0 and < 48400 ? raw : -1;
        int ArmyHex()
        {
            int ordinal = fields.Identity3A!.Value;
            var army = current.ReadArmyUnitTableReference(ordinal < 501 ? ordinal : 0) ?? throw OriginalAiDepartureMissing("goal_Army_pointer");
            return DepartureNeed(current.ReadArmyUnitNativeValidity(army), "goal_Army_validity")
                ? DepartureNeed(current.ReadArmyUnitRawHex2A(army), "goal_Army2A") : 48400;
        }
        int ObjectHex()
        {
            int ordinal = fields.Identity3A!.Value;
            var obj = current.ReadObjectTableReference(ordinal < 3001 ? ordinal : 0) ?? throw OriginalAiDepartureMissing("goal_Object_pointer");
            return DepartureNeed(current.ReadObjectNativeValidity(obj), "goal_Object_validity") ? DepartureNeed(current.ReadObjectHexIndex(obj), "goal_Object10") : 48400;
        }
        int BaseHex()
        {
            int ordinal = fields.Identity3A!.Value;
            var at = fields.Kind38 == 5 ? current.ReadCityTableReference(ordinal < 52 ? ordinal : 0)
                : current.ReadGateTableReference(ordinal < 11 ? ordinal : 0);
            if (at is null) throw OriginalAiDepartureMissing("goal_Base_pointer");
            if (!DepartureNeed(current.ReadBaseNativeValidity(at), "goal_Base_validity")) return 48400;
            int rawObject = DepartureNeed(current.ReadBaseObjectLink(at), "goal_Base4E");
            var obj = current.ReadObjectTableReference(rawObject < 3001 ? rawObject : 0) ?? throw OriginalAiDepartureMissing("goal_Base_Object_pointer");
            return DepartureNeed(current.ReadObjectNativeValidity(obj), "goal_Base_Object_validity") ? DepartureNeed(current.ReadObjectHexIndex(obj), "goal_Base_Object10") : 48400;
        }
    }
    private static T DepartureNeed<T>(T? value, string field) where T : struct => value ?? throw OriginalAiDepartureMissing(field);
    private static InvalidDataException OriginalAiDepartureMissing(string field) => new("original_ai.departure." + field);
}
