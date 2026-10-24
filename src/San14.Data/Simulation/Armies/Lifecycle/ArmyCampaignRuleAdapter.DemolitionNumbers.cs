using San14.Core;

namespace San14.Data;

internal sealed record ArmyDemolitionNumberResult(GameState State, MapObjectReceiverId Receiver,
    int Physical, int HexId, ushort AreaId, int DefinitionSlot, byte Category);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The numerical prefix of the actual valid-object remover.
    /// The caller must also complete its ordered effect/report suffix before
    /// publishing this returned world.</summary>
    private ArmyDemolitionNumberResult? ApplyDemolitionNumbers(GameState state,
        SourceWorldContext current, ObjectReference obj, ArmyUnitReference army,
        Action<GameState>? publishReached = null)
    {
        if (!DemolitionKnown(current.ReadObjectNativeValidity(obj), "remove_object_validity")) return null;
        int physical = obj.PhysicalSlot ?? throw DemolitionMissing("remove_object_pointer");
        var record = current.ReadPhysicalObject(physical) ?? throw DemolitionMissing("remove_object_fields");
        int definitionId = record.RawField12 <= 30 ? record.RawField12 : 0;
        var definition = current.ReadObjectDefinitionReference(definitionId) ?? throw DemolitionMissing("remove_definition_pointer");
        if (!DemolitionKnown(current.ReadObjectDefinitionValidity(definition), "remove_definition_validity")) return null;
        byte category = DemolitionKnown(current.ReadObjectDefinitionRaw2C(definition), "remove_definition_category");
        var hex = current.ReadHexTableReference(record.RawField10 < 48400 ? record.RawField10 : 0)
            ?? throw DemolitionMissing("remove_hex_pointer");
        int at = hex.PhysicalSlot ?? throw DemolitionMissing("remove_hex_identity");
        ushort area = DemolitionKnown(current.ReadHexAreaSlot(hex), "remove_area_before_clear");
        var directory = current.CurrentMapObjectDirectory ?? throw DemolitionMissing("remove_source_directory");
        var receiver = directory.ReceiverAtPhysical(physical);
        //241900 clears the selected Hex1A even when its current link differs.
        state = OwnedMapObjectLifecycle.WriteHexBinding(state, current, at, 0);
        publishReached?.Invoke(state);
        state = MapObjectLifecycleRules.EnqueueRetirement(state, receiver);
        publishReached?.Invoke(state);
        var pool = current.ReadOwnedCurrentListPool() ?? throw DemolitionMissing("remove_pool");
        pool.PendingObjectRetirements = new(state.ReadArmyLifecycle()!.Indexes.PendingObjectRetirements!);
        if (DemolitionKnown(current.ReadArmyUnitNativeValidity(army), "reward_army_validity"))
        {
            var force = current.ReadArmyUnitForceReference(army);
            var home = current.FindArmyUnitCurrentBase(army);
            if (DemolitionKnown(current.ReadBaseNativeValidity(home), "reward_home_validity") &&
                DemolitionKnown(current.ReadForceNativeValidity(force), "reward_force_validity"))
            {
                int faction = DemolitionKnown(current.ReadForcePhysicalSlot(force), "reward_force_identity");
                if (faction != 51 && unchecked((uint)(faction - 46)) > 4 && category == 2)
                {
                    byte oldFaction = DemolitionKnown(current.ReadObjectForceSlot13(obj), "reward_object_faction");
                    byte actorFaction = current.ReadArmyUnitRawForceSlot(army);
                    if (oldFaction != actorFaction && oldFaction != 0 && actorFaction != 0)
                    {
                        ushort commander = DemolitionKnown(current.ReadArmyUnitCommanderId(army), "reward_commander");
                        var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
                            ?? throw DemolitionMissing("reward_person_pointer");
                        int personPhysical = person.PhysicalSlot ?? throw DemolitionMissing("reward_person_identity");
                        if (DemolitionKnown(TacticRemakeCurrentTraits.CommanderHasEffect(current, personPhysical, 124), "reward_trait_presence"))
                        {
                            int amount = DemolitionKnown(TacticRemakeCurrentTraits.CommanderEffectSum(current, personPhysical, 124), "reward_trait_sum");
                            if (amount > 0)
                            {
                                _baseCapacityInputs.BindCurrent(state, current);
                                state = BaseCurrencyAdditionSource.Add(state, current, home, amount,
                                    target => BasePostLoad.ReadResourceCapacities(target, current).Gold).State;
                                publishReached?.Invoke(state);
                            }
                        }
                    }
                }
            }
        }
        return new(state, receiver, physical, at, area, definitionId, category);
    }
}
