using San14.Core;
using static San14.Data.TrapParameters;

namespace San14.Data;

internal sealed partial class TrapEffectApplication
{
    //The ordinary facility caller already owns the two collected channels.
    //Borrow those descriptors; no mid-operation capture or export is legal.
    internal void ApplyOrdinaryFacility(GameState latest, ObjectReference facility, int kind,
        HexListHandle[] channels)
    {
        State = latest;
        var reference = Definition(facility);
        if (!Need(Current.ReadObjectDefinitionValidity(reference), "ordinary_definition_validity")) return;
        if (Need(Current.ReadObjectDefinitionRaw2C(reference), "ordinary_definition_category") != 2 || kind is not (3 or 4 or 5 or 6 or 8 or 15))
            throw Missing("ordinary_definition_category_kind");
        if(kind is 5 or 6 or 15)PrepareFacilityProjectiles(facility, channels);
        else if(kind is 3 or 4)PrepareFacilitySupportAuras(facility,kind,channels);
        victimForces = Current.CreateNearbyTemporary();
        try
        {
            if (Need(Current.ReadObjectNativeValidity(facility), "ordinary_ForceList_Object_validity"))
                Current.ClearNearbyTemporary(victimForces, false);
            for (int channel = 0; channel < 2; channel++)
            {
                if (Need(Current.ReadTemporaryHexListCount(channels[channel]), "ordinary_dispatch_count") == 0) continue;
                var copy = Current.CreateNearbyTemporary();
                try
                {
                    foreach (var hex in FacilityEffectTargets.ReadHexes(Current, channels[channel]))
                        Current.AppendNearbyHexTemporary(copy, hex);
                    if (!Need(Current.ReadObjectNativeValidity(facility), "ordinary_dispatch_Object_validity")) continue;
                    var dispatchReference = Definition(facility);
                    if (!Need(Current.ReadObjectDefinitionValidity(dispatchReference), "ordinary_dispatch_definition_validity")) continue;
                    var definition = Current.ReadFacilityEffectDefinition(dispatchReference)
                        ?? throw Missing("ordinary_dispatch_definition_fields");
                    int dispatchKind=Need(dispatchReference.PhysicalSlot,"ordinary_dispatch_definition_identity");
                    switch(definition.Channel(channel).Effect)
                    {
                        case 1: ApplyDamage(facility,dispatchKind,channel,copy,category:2);break;
                        case 2: ApplyAbnormal(facility,dispatchKind,channel,copy,0);break;
                        case 8: ApplyFacilityStatistics(facility,channel,copy,3,true);break;
                        case 16: ApplyFacilityNegativeMorale(facility,copy,definition.Channel(channel).Amount);break;
                        //192920 has no case18. The collected descriptor still
                        //has its real copy/dispatch/destructor lifecycle.
                        case 18: break;
                        default:throw Missing("ordinary_effect_"+definition.Channel(channel).Effect);
                    }
                }
                finally { Current.ClearNearbyTemporary(copy, true); }
            }
            ApplyRelations(facility, category: 2);
        }
        finally { Current.ClearNearbyTemporary(victimForces, true); victimForces = null; }
    }

    private void PrepareFacilityProjectiles(ObjectReference facility, HexListHandle[] channels)
        => FacilityProjectilePresentationSource.AppendCurrent(State, Current, facility, channels, Feedback.Requests);

    internal GameState CompleteOrdinaryFacilityEffects(GameState latest)
    {
        State = latest;
        //18E800 calls18FAC0 after all190F40 candidates, before their destructor.
        Aggregate();
        return State;
    }

    internal GameState FinishOrdinaryFacilities(GameState latest)
    {
        State = latest;
        State = ArmyReturnImagePublication.Publish(State, Current, []);
        PublishPeople();
        State = Feedback.PublishCurrentState(State);
        State = OwnedArmyLifecycleTargets.PublishCurrent(State.WithArmyLifecycle(
            OwnedArmyDestructorFlags.PublishCurrent(State.ReadArmyLifecycle()!, Current)), Current);
        return State with { ArmyMapOccupancy = Current.ExportArmyMapOccupancy(),
            PendingCombatSettlements = Current.OwnedDestructionQueue ?? throw Missing("ordinary_manager140_publication") };
    }
}
