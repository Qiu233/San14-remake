using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal UnitState? ReadOriginalAiInstructionArmyCurrent(GameState state, SourceWorldContext current, int unitId)
    {
        if (!state.Units.TryGetValue(unitId, out var unit)) return null;
        int physical = unit.NativeArmyOrigin?.NativeSlotIndex ?? throw AiArmyMissing("Army_receiver");
        return AiArmyKnown(current.ReadArmyUnitNativeValidity(new(physical)), "Army_validity") ? unit : null;
    }

    internal TroopsInformationValue ReadOriginalAiArmyInformationCurrent(GameState state, SourceWorldContext current,
        OriginalAiActor actor, UnitState unit, int mode, Func<UnitSourceAxes> axes)
    {
        int physical = unit.NativeArmyOrigin?.NativeSlotIndex ?? throw AiArmyMissing("Army_receiver");
        var army = new ArmyUnitReference(physical);
        ArmyReturnResourceFields Resource() => current.ReadArmyReturnResourceFields(army) ?? throw AiArmyMissing("Army_resources");
        int Status() => AiArmyKnown(current.ReadArmyReturnUnitStatus10(army), "Army_status");
        int Person() => TroopsInfoPerson(current, AiArmyKnown(current.ReadArmyUnitCommanderId(army), "Commander12"));
        int Home()
        {
            int person = Person();
            return AiArmyKnown(current.ReadPersonNativeValidity(person), "Commander_validity")
                ? AiArmyKnown(current.ReadPersonFirstLocation(person), "Commander_home") : 0;
        }
        OriginalAiHex Hex() => TroopsInfoHex(current, AiArmyKnown(current.ReadArmyUnitRawHex2A(army), "Army2A"));
        TroopsInformationValue Entity(WorldEntityKind kind, int id) => new TroopsInformationEntity(new(kind, id));
        TroopsInformationValue Number(int value) => new TroopsInformationInteger(value);
        var target = mode is 8 or 19 ? current.ReadArmyLifecycleTargetFields(physical)
            ?? throw AiArmyMissing("Army_target") : null;
        switch (mode)
        {
            case 0: return Number(physical);
            case 1:
                return Entity(WorldEntityKind.Officer, AiArmyKnown(current.ReadPersonIdentity(Person()), "Commander_identity"));
            case 2: return Number(Status());
            case 3: return new ArmyInformationTroops(current.ReadOriginalAiArmyTroopsCurrent(army));
            case 4:
            {
                var home = BaseLookup.Find(Home(), current);
                return Entity(home.Kind == BaseKind.City ? WorldEntityKind.City : WorldEntityKind.Gate,
                    AiArmyKnown(current.ReadBaseObjectLink(home), "Home_identity"));
            }
            case 5: return Entity(WorldEntityKind.Force, AiArmyKnown(current.ReadArmyUnitForceReference(army)?.PhysicalSlot, "Force_pointer"));
            case 6: return new TroopsInformationHex(Hex());
            case 7: return Number(AiArmyKnown(ArmyConfigurationSourceAdapter.ReadAction(state, unit), "Action"));
            case 8: return ReadTarget(target!);
            case 9:
            {
                var value = ArmyOverrideTargetSourceAdapter.Read(state, unit);
                return Number(AiArmyKnown(value.Kind3C, "override_kind"));
            }
            case 10:
            {
                var value = ArmyOverrideTargetSourceAdapter.Read(state, unit);
                return Number(AiArmyKnown(value.Identity3E, "override_identity"));
            }
            case 11:
                _baseCapacityInputs.BindCurrent(state, current);
                return Number(Math.Min(AiArmyKnown(Resource().Morale1A, "Morale"), BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current)));
            case 12:
            {
                int selected = AiArmyKnown(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit), "formation_Army1B");
                var formation = current.ReadStaticTableReference(0x77428, selected <= 20 ? selected : 0)
                    ?? throw AiArmyMissing("formation_pointer");
                return new ArmyInformationSkill(new(OriginalAiPersonSkillKind.Formation,
                    AiArmyKnown(formation.PhysicalSlot, "formation_receiver")));
            }
            case 13:
            case 14:
            case 15:
            {
                int original = AiArmyKnown(mode == 13 ? Resource().Gold5C : mode == 14 ? Resource().Supplies60 : Resource().Ready16, "resource");
                int personality = TroopsInfoActorWord(state, current, actor, mode == 13 ? 0x170 : mode == 14 ? 0x172 : 0x16A);
                //This Army getter uses the same gold coefficient for both stores;
                //the group getter has a separate supplies coefficient.
                int coefficient = P(mode is 13 or 14 ? "original-ai.troops-gold-preservation-percent"
                    : "original-ai.troops-ready-personality-percent");
                int adjustment = unchecked(unchecked(original * personality) * coefficient) / 10000;
                return Number(mode == 15 ? unchecked(original + adjustment) : unchecked(original - adjustment));
            }
            case 16:
            {
                int raw = AiArmyKnown(current.ReadHexAreaSlot(current.ReadHexTableReference(Hex().Id)!), "Hex_Area");
                return Entity(WorldEntityKind.Area, AiArmyKnown(current.ReadAreaTableReference(raw <= 500 ? raw : 0)?.PhysicalSlot, "Area_pointer"));
            }
            case 18:
            {
                var points = Status() == 2 ? TransportRouteSourceAdapter.ReadWaypoints(state, unit)
                    : ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(unit.March);
                if (points is not { Length: 5 }) throw AiArmyMissing("five_relay_points");
                return new TroopsInformationHexes(points.Select(id => TroopsInfoHex(current, id)).ToArray());
            }
            case 19:
                return AiArmyKnown(target!.Kind38, "target_kind") switch
                {
                    2 => Number(5), 5 => Number(8), 6 => Number(9), 7 => Number(0), 8 => Number(1),
                    9 => Number(7), 10 => Number(6), 11 => Number(10), 14 => Number(4), 27 => Number(3), 29 => Number(2),
                    _ => new TroopsInformationUnchanged()
                };
            case 20: return Number(AiArmyKnown(Resource().Gold5C, "Gold"));
            case 21: return Number(AiArmyKnown(Resource().Supplies60, "Supplies"));
            case 22: return Number(AiArmyKnown(Resource().Ready16, "Troops"));
            case 23:
                return Number(BattleUnitSourceMath.TruncateSingleToInt32((float)AiArmyKnown(Resource().Ready16, "Troops") *
                    ReadOriginalAiArmyCommonCurrent(state, current, army) / (float)P(OriginalAiCrisisSource.StrengthDivisorKey)));
            case 24:
            {
                BaseReference? selected = null;
                int distance = int.MaxValue, origin = Hex().Id;
                int force = AiArmyKnown(current.ReadArmyUnitForceReference(army)?.PhysicalSlot, "Force_pointer");
                foreach (var entry in current.ReadRegisteredBases() ?? throw AiArmyMissing("Base_registry"))
                {
                    var candidate = entry ?? throw AiArmyMissing("Base_pointer");
                    int other = AiArmyKnown(current.ReadBaseForceSlot(candidate), "Base_Force");
                    if (other == 0 || !AiArmyKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force, other), "Base_hostility")) continue;
                    int range = FireCommandDistance(origin, current.ReadBaseCurrentHexIndex(candidate));
                    if (range >= distance) continue;
                    distance = range; selected = candidate;
                }
                return selected is not null && AiArmyKnown(current.ReadBaseNativeValidity(selected), "selected_Base_validity")
                    ? Entity(selected.Kind == BaseKind.City ? WorldEntityKind.City : WorldEntityKind.Gate,
                        AiArmyKnown(current.ReadBaseObjectLink(selected), "selected_Base_identity")) : new TroopsInformationUnchanged();
            }
            case >= 25 and <= 29:
            {
                var result = axes();
                var values = result.WithConditionalModifiers;
                (int? value, string key) = mode switch
                {
                    25 => (values.Mobility, "mobility"), 26 => (values.FieldAttack, "field"),
                    27 => (values.SiegeTroopAttack, "siege"), 28 => (values.WallAttack, "wall"),
                    _ => (values.Defense, "defense")
                };
                return value is int known ? Number(known) : throw new InvalidDataException(result.Missing.GetValueOrDefault("with." + key)
                    ?? result.Missing.GetValueOrDefault("with.common") ?? AiArmyMissing("axis_" + key).Message);
            }
            default: throw AiArmyMissing("information_selector_" + mode);
        }

        int P(string key) => state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(key, out int value) == true
            ? value : throw AiArmyMissing("parameter." + key);
        TroopsInformationValue ReadTarget(ArmyTargetSourceFields value)
        {
            int id = AiArmyKnown(value.Identity3A, "target_identity");
            return AiArmyKnown(value.Kind38, "target_kind") switch
            {
                5 => Entity(WorldEntityKind.City, AiArmyKnown(current.ReadCityTableReference(id <= 51 ? id : 0)?.PhysicalSlot, "target_City")),
                6 => Entity(WorldEntityKind.Gate, AiArmyKnown(current.ReadGateTableReference(id <= 10 ? id : 0)?.PhysicalSlot, "target_Gate") is int gate && gate != 0 ? gate + 51 : 0),
                9 => new TroopsInformationHex(TroopsInfoHex(current, id)),
                11 => (current.CurrentMapObjectDirectory ?? throw AiArmyMissing("target_Object_directory")).Lookup(id) is { } receiver
                    ? Entity(WorldEntityKind.MapObject, receiver.Value) : new ArmyInformationNull(),
                27 => new TroopsInformationEntity(TroopsInfoArmyEntity(state,
                    current.ReadArmyUnitTableReference(id <= 500 ? id : 0) ?? throw AiArmyMissing("target_Army"))),
                _ => new ArmyInformationNull()
            };
        }
    }

    internal UnitSourceAxes ReadOriginalAiArmyAxesCurrent(GameState state, SourceWorldContext current, UnitState unit)
    {
        _baseCapacityInputs.BindCurrent(state, current);
        _personRelationInputs.BindCurrent(state, current);
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        var queries = new TacticRemakeCurrentContext(current, _catalog, _map, _terrain, _usage, Revision, abilities, new AiCrisisTacticQueries(this));
        return ReadUnitSourceAxesCurrent(state, unit.Id, null, current, queries, abilities, null,
            listPreview: false, conditionalModifiers: true, commonModifiers: false);
    }

    private static T AiArmyKnown<T>(T? value, string field) where T : struct => value ?? throw AiArmyMissing(field);
    private static InvalidDataException AiArmyMissing(string field) => new("original_ai.Army." + field);
}
