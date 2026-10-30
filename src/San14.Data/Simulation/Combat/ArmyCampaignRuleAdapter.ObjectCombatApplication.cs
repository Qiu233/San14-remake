using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The actual169CE0 generic-Object branch, using frozen pair
    /// descriptors and the same live current after every preceding pair.</summary>
    private GameState CompleteBorrowedObjectCombatPair(GameState state, SourceWorldContext current,
        NormalCombatMixedPair pair, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate operationDate, List<CampaignEffectRequest> effects)
    {
        if (pair.Target.Kind != 11 || pair.Actor.Kind is not (5 or 6 or 27))
            throw ObjectCombatMissing("pair_kind");
        int? actorForce = ReadCurrentForce(pair.Actor);
        if (actorForce is null) return state;
        int? targetForce = ReadCurrentForce(pair.Target);
        if (targetForce is null) return state;
        if (!WallNeed(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
            actorForce.Value, targetForce.Value), "Object_pair_relation")) return state;
        if (current.ArmyReturnTransferMode is not { Present: false })
            throw ObjectCombatMissing("challenge_pair_gate");

        var working = state;
        //174C50 repeats2CA7B0 using captured descriptor Hexes. A rejected
        //Object attack still returns8 and reaches174EC0; it is not a pair skip.
        if (WallThreatens(working, current, pair.Actor.HexId, pair.Target.HexId))
        {
            var obj = current.ReadObjectTableReference(pair.Target.PhysicalSlot < 3001
                ? pair.Target.PhysicalSlot : 0) ?? throw ObjectCombatMissing("wall_Object_pointer");
            if (WallNeed(current.ReadObjectNativeValidity(obj), "wall_Object_validity"))
            {
                //The original wall stage uses descriptor.word0 as an Army
                //lookup even for a Base actor. Do not substitute the Base's
                //governor or skip an actual valid alias Army receiver.
                var actor = current.ReadArmyUnitTableReference(pair.Actor.PhysicalSlot < 501
                    ? pair.Actor.PhysicalSlot : 0) ?? throw ObjectCombatMissing("wall_Army_pointer");
                if (WallNeed(current.ReadArmyUnitNativeValidity(actor), "wall_Army_validity"))
                {
                    var image = NotificationArmy(working, WallNeed(actor.PhysicalSlot, "wall_Army_physical"));
                    var context = Context(current, sampler);
                    var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                        SimulationRuleIdentity.ForLocale(Seed.Language));
                    var queries = new TacticRemakeCurrentBattleQueries(context, current, Revision);
                    float wall = new BattleRemakeCurrentArmyAttributes(_battleSeed, current, abilities, queries, Revision)
                        .ResolveWallOffense(working, image) ?? throw ObjectCombatMissing("wall_offense");
                    if (!float.IsFinite(wall) || wall < 0 || (double)wall >= 2147483648d)
                        throw ObjectCombatMissing("wall_Int32_conversion");
                    var applied = ApplyBorrowedBaseWallDamage(working, current, context, image, actor, obj,
                        (int)wall, wall, effects, operationDate,
                        reached => sampler.Read(reached)?.Day450 ?? throw ObjectCombatMissing("report_source_day"));
                    working = applied.State;
                }
            }
        }
        //An invalid reselected Object/alias Army makes174C50 return2. The
        //Object frame has no troop operand:16A0C0/16ABE0/16A810 produce0,
        //16FE60 compares0<descriptor.Troops0 and does not start a tactic.
        //174A30's minimum1 is phase metadata;178EC0 rejects targetKind11.
        //No ObjectHP, Army reward, defeat or report is invented for this path.
        var aftermath = NormalArmyPairAftermath.TryApplyMixed(working, pair.Actor, pair.Target,
            current, Context(current, sampler), _normalAftermathInputs);
        if (aftermath.Outcome == NormalArmyPairAftermathOutcome.CurrentInputsUnresolved)
            throw ObjectCombatMissing(aftermath.MissingOwner ?? "aftermath174EC0");
        return aftermath.State;

        int? ReadCurrentForce(NormalCombatParticipant participant)
        {
            if (participant.Kind == 27)
            {
                var army = current.ReadArmyUnitTableReference(participant.PhysicalSlot < 501
                    ? participant.PhysicalSlot : 0) ?? throw ObjectCombatMissing("pair_Army_pointer");
                if (!WallNeed(current.ReadArmyUnitNativeValidity(army), "pair_Army_validity")) return null;
                if (OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current,
                    WallNeed(army.PhysicalSlot, "pair_Army_physical")) is not bool excluded)
                    throw ObjectCombatMissing("pair_Army_excluded");
                return excluded ? null : current.ReadArmyUnitRawForceSlot(army);
            }
            int objectIndex = participant.PhysicalSlot;
            if (participant.Kind is 5 or 6)
            {
                var targetBase = participant.Kind == 5
                    ? current.ReadCityTableReference(participant.PhysicalSlot < 52 ? participant.PhysicalSlot : 0)
                    : current.ReadGateTableReference(participant.PhysicalSlot < 11 ? participant.PhysicalSlot : 0);
                if (targetBase is null) throw ObjectCombatMissing("pair_Base_pointer");
                objectIndex = participant.Kind == 5
                    ? participant.PhysicalSlot is >= 0 and <= 51 ? participant.PhysicalSlot : 0
                    : participant.PhysicalSlot is >= 1 and <= 10 ? participant.PhysicalSlot + 51 : 0;
                if (WallNeed(current.ReadBaseNativeValidity(targetBase), "pair_Base_validity"))
                {
                    if (OwnedArmyDestructionCurrent.ReadBaseExcluded2334B0(state, current, objectIndex) is not bool excluded)
                        throw ObjectCombatMissing("pair_Base_excluded");
                    if (excluded) return null;
                }
            }
            var obj = current.ReadObjectTableReference(objectIndex < 3001 ? objectIndex : 0)
                ?? throw ObjectCombatMissing("pair_Object_pointer");
            if (!WallNeed(current.ReadObjectNativeValidity(obj), "pair_Object_validity")) return null;
            var directory = current.CurrentMapObjectDirectory ?? throw ObjectCombatMissing("pair_Object_directory");
            var receiver = directory.ReceiverAtPhysical(WallNeed(obj.PhysicalSlot, "pair_Object_physical"));
            var pending = current.ReadOwnedCurrentListPool()?.PendingObjectRetirements
                ?? throw ObjectCombatMissing("pair_F0");
            foreach (var retired in pending) if (retired == receiver) return null;
            return WallNeed(current.ReadObjectForceSlot13(obj), "pair_Object13");
        }
    }

    /// <summary>29EC80's1B4AB0 action33 packet. The actual definition
    /// identity is stored before admission; no Area/Personality alias is used.</summary>
    private GameState QueueObjectDamageAdjustmentSpeechCurrent(GameState state, SourceWorldContext current,
        ArmyUnitReference actor, UnitState image, int definitionId, NativeMapSeasonDate operationDate)
    {
        if (!WallNeed(current.ReadArmyUnitNativeValidity(actor), "feedback_Army_validity")) return state;
        if (current.ReadOwnedPersonCommandManager() is null)
            PersonReturnCommands.BindCurrent(state, current);
        ushort commander = WallNeed(current.ReadArmyUnitCommanderId(actor), "feedback_Commander12");
        var person = commander <= 6000 ? current.ReadPersonLookupReference(commander)
            : current.ReadTransferPersonTableReference(0);
        int physical = person?.PhysicalSlot ?? throw ObjectCombatMissing("feedback_Person_pointer");
        int position;
        try
        {
            position = PersonReturnCommands.ResolvePersonPosition(state, current, physical, operationDate).PhysicalSlot ?? 48400;
        }
        finally
        {
            state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
                ?? throw ObjectCombatMissing("feedback_personnel_publication") };
        }
        int officer = WallNeed(current.ReadPersonIdentity(physical), "feedback_Person_identity");
        return QueueArmyMapSpeechCurrent(state, current, image.Id, officer, 33, position,
            facilityDefinitionId: definitionId);
    }

    private string ReadObjectDamageDefinitionName(SourceWorldContext current, ObjectDefinitionReference definition)
    {
        int physical = WallNeed(definition.PhysicalSlot, "speech_definition_identity");
        var rows = _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath).Catalog.Definitions;
        if ((uint)physical >= (uint)rows.Count) throw ObjectCombatMissing("speech_definition_resource");
        return rows[physical].Name;
    }

    private static InvalidDataException ObjectCombatMissing(string owner) => new("ordinary.Object_combat." + owner);
}
